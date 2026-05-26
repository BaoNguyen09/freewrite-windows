param(
    [Parameter(Mandatory = $false)]
    [string]$Version = "0.1.0-beta.1"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $repoRoot "dist\win-x64"
$releaseDir = Join-Path $repoRoot "release"
$zipName = "FreewriteWindows-v$Version-win-x64.zip"
$zipPath = Join-Path $releaseDir $zipName
$checksumPath = "$zipPath.sha256"

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

dotnet publish `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:Version=$Version `
    -o $distDir

if (Test-Path $zipPath) {
    Remove-Item $zipPath
}

Compress-Archive -Path (Join-Path $distDir "*") -DestinationPath $zipPath

$hash = Get-FileHash -Algorithm SHA256 -Path $zipPath
"$($hash.Hash)  $zipName" | Set-Content -Path $checksumPath -NoNewline

Write-Host "Created $zipPath"
Write-Host "Created $checksumPath"
