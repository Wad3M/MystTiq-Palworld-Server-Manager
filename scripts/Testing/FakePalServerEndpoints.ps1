[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Rcon', 'Rest')][string]$Mode,
    [Parameter(Mandatory)][int]$Port,
    [string]$LogPath,
    [string]$PlayersJsonPath
)
# v0.7.113.0: a stand-in for PalServer's two admin endpoints, for route smokes that must see what MystTiq
# actually sends. Rcon: Source RCON, accepts any password, appends every command body to $LogPath (one per
# line) and answers "ok" (or a position for getpos). Rest: answers every request with the players JSON, which is
# what MystTiq reads from /v1/api/players (read from a file: JSON does not survive a command line). Runs until killed. Test use only.
$ErrorActionPreference = 'Stop'
$PlayersJson = if ($PlayersJsonPath) { Get-Content -LiteralPath $PlayersJsonPath -Raw } else { '{"players":[]}' }
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
$listener.Start()

function Read-Exactly([System.IO.Stream]$stream, [int]$count) {
    $buffer = [byte[]]::new($count); $offset = 0
    while ($offset -lt $count) { $read = $stream.Read($buffer, $offset, $count - $offset); if ($read -le 0) { throw 'closed' }; $offset += $read }
    , $buffer
}
function Read-Packet([System.IO.Stream]$stream) {
    $size = [BitConverter]::ToInt32((Read-Exactly $stream 4), 0)
    $payload = Read-Exactly $stream $size
    @{ Id = [BitConverter]::ToInt32($payload, 0); Type = [BitConverter]::ToInt32($payload, 4); Body = [Text.Encoding]::UTF8.GetString($payload, 8, [Math]::Max(0, $size - 10)) }
}
function Write-Packet([System.IO.Stream]$stream, [int]$id, [int]$type, [string]$body) {
    $bodyBytes = [Text.Encoding]::UTF8.GetBytes($body)
    $packet = [byte[]]::new($bodyBytes.Length + 14)
    [BitConverter]::GetBytes($bodyBytes.Length + 10).CopyTo($packet, 0)
    [BitConverter]::GetBytes($id).CopyTo($packet, 4)
    [BitConverter]::GetBytes($type).CopyTo($packet, 8)
    $bodyBytes.CopyTo($packet, 12)
    $stream.Write($packet, 0, $packet.Length); $stream.Flush()
}

while ($true) {
    $client = $listener.AcceptTcpClient()
    try {
        $client.ReceiveTimeout = 5000
        $stream = $client.GetStream()
        if ($Mode -eq 'Rcon') {
            $auth = Read-Packet $stream
            Write-Packet $stream $auth.Id 2 ''
            $command = Read-Packet $stream
            if ($LogPath) { Add-Content -LiteralPath $LogPath -Value $command.Body }
            $reply = if ($command.Body -like 'getpos *') { 'X=-358.2 Y=270.5 Z=1200' } else { 'ok' }
            Write-Packet $stream $command.Id 0 $reply
        }
        else {
            $reader = [System.IO.StreamReader]::new($stream)
            while (($line = $reader.ReadLine()) -and $line.Length -gt 0) { }
            $bytes = [Text.Encoding]::UTF8.GetBytes($PlayersJson)
            $head = [Text.Encoding]::ASCII.GetBytes("HTTP/1.1 200 OK`r`nContent-Type: application/json`r`nContent-Length: $($bytes.Length)`r`nConnection: close`r`n`r`n")
            $stream.Write($head, 0, $head.Length); $stream.Write($bytes, 0, $bytes.Length); $stream.Flush()
        }
    }
    catch { }
    finally { $client.Close() }
}
