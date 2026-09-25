[CmdletBinding()]
param([string]$ProjectRoot = '.', [switch]$ShowUnresolved)
# v0.8.23.0: which role each Desktop command needs, derived from the code rather than kept by hand. Read-only.
#   1. The service's routes and the role each needs (LocalManagementApiHost.cs): a declared RequireRole, otherwise, in a
#      server's route group, Viewer to read (GET) and Admin to change (anything else), as RbacEndpointExtensions does.
#   2. The API client's methods and the route each calls (MystTiqApiClient.cs).
#   3. Each command in the ViewModel (new AsyncCommand/RelayCommand), followed through the methods it calls (in the
#      ViewModel files) to the API client methods it reaches. Its role is the highest any of those routes needs.
# Emits one object per command: Command, Role ('' = no route needing more than Viewer), Via (the client calls behind it).
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
$rank = @{ '' = -1; 'Viewer' = 0; 'Operator' = 1; 'Admin' = 2; 'Owner' = 3 }

function Get-Block([string]$Text, [int]$OpenIndex) {
    # The text from the brace or parenthesis at $OpenIndex to its match (strings and comments are not special-cased;
    # good enough for this codebase's method bodies).
    $open = $Text[$OpenIndex]; $close = if ($open -eq '{') { '}' } else { ')' }
    $depth = 0
    for ($i = $OpenIndex; $i -lt $Text.Length; $i++) {
        if ($Text[$i] -eq $open) { $depth++ } elseif ($Text[$i] -eq $close) { $depth--; if ($depth -eq 0) { return $Text.Substring($OpenIndex, $i - $OpenIndex + 1) } }
    }
    return $Text.Substring($OpenIndex)
}
function ConvertTo-RoutePattern([string]$Path) {
    $p = ($Path -split '\?')[0].TrimEnd('/')
    $parts = $p.Trim('/') -split '/' | ForEach-Object { if ($_ -match '\{') { '[^/]+' } else { [regex]::Escape($_) } }
    '^/' + ($parts -join '/') + '$'
}

# 1. Routes.
$hostText = [IO.File]::ReadAllText((Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'))
$maps = [regex]::Matches($hostText, '(routes|app)\.Map(Get|Post|Put|Delete)\("([^"]+)"')
$routes = for ($i = 0; $i -lt $maps.Count; $i++) {
    $end = if ($i + 1 -lt $maps.Count) { $maps[$i + 1].Index } else { $hostText.Length }
    $chunk = $hostText.Substring($maps[$i].Index, $end - $maps[$i].Index)
    $declared = [regex]::Match($chunk, 'RequireRole\(MystTiqRole\.(\w+)')
    $method = $maps[$i].Groups[2].Value.ToUpper()
    $role = if ($declared.Success) { $declared.Groups[1].Value } elseif ($maps[$i].Groups[1].Value -eq 'routes') { if ($method -eq 'GET') { 'Viewer' } else { 'Admin' } } else { '' }
    $path = $maps[$i].Groups[3].Value
    if ($maps[$i].Groups[1].Value -eq 'routes') { $path = '/api/v1/' + $path.TrimStart('/') }
    [pscustomobject]@{ Method = $method; Pattern = (ConvertTo-RoutePattern $path); Path = $path; Role = $role }
}

# 2. API client methods -> routes.
$clientText = [IO.File]::ReadAllText((Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs'))
$clientRole = @{}
$unresolved = [System.Collections.Generic.List[string]]::new()
foreach ($m in [regex]::Matches($clientText, 'public (?:async )?[\w<>\[\]?, ]+ (\w+Async)\(')) {
    $brace = $clientText.IndexOf('{', $m.Index)
    $body = Get-Block $clientText $brace
    $best = ''
    # A path built first in a local ("var path = $"...""; client.PostAsync(path, ...)) is read through the local.
    foreach ($local in [regex]::Matches($body, 'var (\w+) = \$?"([^"]+)"')) { $body = $body -replace ('client\.(\w+(?:<[^>]*(?:<[^>]*>)?>)?)\(\s*' + $local.Groups[1].Value + '\b'), ('client.$1("' + $local.Groups[2].Value.Replace('$', '$$') + '"') }
    foreach ($call in [regex]::Matches($body, 'client\.(GetFromJsonAsync<[^>]*(?:<[^>]*>)?>|GetAsync|GetStringAsync|GetStreamAsync|PostAsync|PostAsJsonAsync|PutAsJsonAsync|PutAsync|DeleteAsync)\(\s*\$?"([^"]+)"')) {
        $verb = switch -Regex ($call.Groups[1].Value) { '^Get' { 'GET' } '^Post' { 'POST' } '^Put' { 'PUT' } '^Delete' { 'DELETE' } }
        $path = $call.Groups[2].Value -replace '\{query\}', ''
        $route = @($routes | Where-Object { $_.Method -eq $verb -and ($path -split '\?')[0] -match $_.Pattern }) | Select-Object -First 1
        if ($route -and $rank[$route.Role] -gt $rank[$best]) { $best = $route.Role }
        if (-not $route) { $unresolved.Add("$($m.Groups[1].Value): $verb $path") }
    }
    # A change (POST/PUT/DELETE) whose path is not a literal cannot be matched to a route: reported, never guessed.
    if (-not $best -and $body -match 'client\.(Post|Put|Delete|Send)\w*\(\s*[^"$\s]') { $unresolved.Add("$($m.Groups[1].Value): path is not a literal") }
    $clientRole[$m.Groups[1].Value] = $best
}
if ($ShowUnresolved) { return $unresolved }

# 3. ViewModel methods and commands.
$vmText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop\ViewModels') -Filter 'MainWindowViewModel*.cs' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$methodBodies = @{}
foreach ($m in [regex]::Matches($vmText, '(?m)^\s+(?:(?:private|public|internal|protected)\s+)(?:static\s+)?(?:async\s+)?[\w<>\[\]?,\. ]+?\s(\w+)\([^;{]*\)\s*(?:=>|\{)')) {
    $name = $m.Groups[1].Value
    $afterSig = $m.Index + $m.Length - 1
    $body = if ($vmText[$afterSig] -eq '{') { Get-Block $vmText $afterSig } else { $semi = $vmText.IndexOf(';', $afterSig); $vmText.Substring($afterSig, $semi - $afterSig) }
    $methodBodies[$name] = ($methodBodies[$name] + "`n" + $body)
}
$memo = @{}
# A command's own action is always followed. Below it, a Refresh/Reload/Load method is the best-effort reload after the
# action (its failure for a lower role is expected and handled), and the Ribbon builders and navigation only reference
# commands; none of them decides the role the button needs.
$notFollowedBelowAction = '^(Refresh|Reload|Load|Rebuild|Build|PerformNavigation)'
function Get-Reach([string]$Text, [hashtable]$Seen, [int]$Depth = 0) {
    $calls = [System.Collections.Generic.HashSet[string]]::new()
    # A call, or a method group handed on (RunLifecycleAsync("Stopping…", _api.StopServerAsync)).
    foreach ($c in [regex]::Matches($Text, '_api\.(\w+Async)\b')) { [void]$calls.Add($c.Groups[1].Value) }
    foreach ($c in [regex]::Matches($Text, '\b(\w+)\(')) {
        $name = $c.Groups[1].Value
        if ($Seen.ContainsKey($name) -or -not $methodBodies.ContainsKey($name)) { continue }
        if ($Depth -ge 1 -and $name -match $notFollowedBelowAction) { continue }
        $Seen[$name] = $true
        foreach ($x in (Get-Reach $methodBodies[$name] $Seen ($Depth + 1))) { [void]$calls.Add($x) }
    }
    return $calls
}
function Resolve-Role([string]$Name, [string]$Kind, $Calls) {
    $best = ''; $via = @()
    foreach ($c in $Calls) { $r = $clientRole[$c]; if ($null -eq $r) { continue }; if ($rank[$r] -gt $rank['Viewer']) { $via += "$c=$r" }; if ($rank[$r] -gt $rank[$best]) { $best = $r } }
    if ($best -eq 'Viewer') { $best = '' }
    [pscustomobject]@{ Command = $Name; Kind = $Kind; Role = $best; Via = ($via | Sort-Object -Unique) -join ' ' }
}
foreach ($m in [regex]::Matches($vmText, '(\w+Command) = new (AsyncCommand|RelayCommand(?:<[^>]+>)?)\(')) {
    $args = Get-Block $vmText ($m.Index + $m.Length - 1)
    # Only the action (the first argument), not the can-execute condition.
    $depth = 0; $cut = $args.Length - 1
    for ($i = 1; $i -lt $args.Length - 1; $i++) { $ch = $args[$i]; if ('({[' -contains $ch) { $depth++ } elseif (')}]' -contains $ch) { $depth-- } elseif ($ch -eq ',' -and $depth -eq 0) { $cut = $i; break } }
    $action = $args.Substring(1, $cut - 1)
    if ($action -match '^\s*(\w+)\s*$' -and $methodBodies.ContainsKey($Matches[1])) { $action = "$($Matches[1])()" }
    Resolve-Role $m.Groups[1].Value 'Command' (Get-Reach $action @{})
}

# 4. The window's code-behind Click handlers that change something through the ViewModel (world edits, mod ZIP install).
$codeBehind = [IO.File]::ReadAllText((Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml.cs'))
# Handlers may be one-liners handing over to a shared code-behind helper (the guild operations do), so the code-behind's
# own methods are followed too, collecting the ViewModel methods they call.
$cbBodies = @{}
foreach ($m in [regex]::Matches($codeBehind, '(?m)^\s+(?:(?:private|public|internal|protected)\s+)(?:static\s+)?(?:async\s+)?[\w<>\[\]?,\. ]+?\s(\w+)\([^;{]*\)\s*(?:=>|\{)')) {
    $a = $m.Index + $m.Length - 1
    $cbBodies[$m.Groups[1].Value] = if ($codeBehind[$a] -eq '{') { Get-Block $codeBehind $a } else { $codeBehind.Substring($a, $codeBehind.IndexOf(';', $a) - $a) }
}
function Get-VmCalls([string]$Text, [hashtable]$Seen) {
    $found = @([regex]::Matches($Text, '\bvm\.(\w+)\(') | ForEach-Object { "$($_.Groups[1].Value)()" })
    foreach ($c in [regex]::Matches($Text, '\b(\w+)\(')) {
        $n = $c.Groups[1].Value
        if ($Seen.ContainsKey($n) -or -not $cbBodies.ContainsKey($n)) { continue }
        $Seen[$n] = $true; $found += Get-VmCalls $cbBodies[$n] $Seen
    }
    return $found
}
foreach ($name in @($cbBodies.Keys | Where-Object { $_ -match '_(On)?Click$' } | Sort-Object)) {
    Resolve-Role $name 'Click' (Get-Reach ((Get-VmCalls $cbBodies[$name] @{ $name = $true }) -join "`n") @{})
}
