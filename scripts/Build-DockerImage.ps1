# MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
# v1.1.1.0 (owner 2026-10-14: "our package should include a linux build and a docker build"): builds the MystTiq headless
# Docker image (deploy/docker/Dockerfile) from this source and, with -Save, writes it as an image archive for the release:
#   artifacts/MystTiqPalworldServer_v<version>_Docker-linux-x64.tar.gz   (load it with: docker load -i <file>)
# The image is tagged mysttiq-headless:<version>. The release workflow runs this on a Linux runner; it also runs here with
# Docker Desktop's Linux engine.
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputDirectory,
    [switch]$Save
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts' }
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.VersionPrefix | Where-Object { $_ } | Select-Object -First 1
$image = "mysttiq-headless:$version"
$context = Join-Path ([IO.Path]::GetTempPath()) ("mysttiq-docker-" + [guid]::NewGuid().ToString('N'))
New-Item $context -ItemType Directory -Force | Out-Null
try {
    & dotnet publish (Join-Path $root 'src/MystTiq.HeadlessHost/MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true `
        -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $context 'app') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish of the service failed' }
    # v1.1.1.1: .NET's crash-dump helper is left out of every publish (Directory.Build.targets); the image never holds it.
    if (Test-Path (Join-Path $context 'app/createdump')) { throw "The image must not contain .NET's createdump helper." }
    # The image's scripts must keep Unix line endings whatever the checkout did.
    foreach ($f in 'Dockerfile', 'entrypoint.sh') {
        [IO.File]::WriteAllText((Join-Path $context $f), [IO.File]::ReadAllText((Join-Path $root "deploy/docker/$f")).Replace("`r`n", "`n"))
    }
    & docker build -q -t $image --build-arg "VERSION=$version" $context | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'docker build failed' }
    "Image: $image"
    if ($Save) {
        New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
        $tar = Join-Path $OutputDirectory "MystTiqPalworldServer_v${version}_Docker-linux-x64.tar"
        & docker save -o $tar $image
        if ($LASTEXITCODE -ne 0) { throw 'docker save failed' }
        $gz = "$tar.gz"
        $in = [IO.File]::OpenRead($tar)
        try {
            $out = [IO.File]::Create($gz)
            try { $zip = [IO.Compression.GZipStream]::new($out, [IO.Compression.CompressionLevel]::Optimal); try { $in.CopyTo($zip) } finally { $zip.Dispose() } }
            finally { $out.Dispose() }
        }
        finally { $in.Dispose() }
        Remove-Item $tar
        "Image archive: $gz ($([math]::Round((Get-Item $gz).Length / 1MB, 1)) MB)"
    }
}
finally {
    Remove-Item $context -Recurse -Force -ErrorAction SilentlyContinue
}
