param(
    [switch]$BuildPlugins = $true,
    [switch]$StartStack = $true
)

# Legacy helper for the stock (unpatched) docker-compose.jellyfin.yml stack.
# For mute-then-cut, prefer: .\scripts\build-and-run-patched-docker.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$composeFile = Join-Path $repoRoot 'docker-compose.jellyfin.yml'
$pluginRoot = Join-Path $repoRoot 'docker\jellyfin\plugins'
$dockerClientConfig = Join-Path $repoRoot '.docker-client'

$skipProject = Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\Jellyfin.Plugin.CorrMedia.csproj'
$skipOutDir = if (Test-Path (Join-Path $repoRoot 'jellyfin-source\MediaBrowser.Controller\MediaBrowser.Controller.csproj')) { 'net9.0' } else { 'net8.0' }
$skipDll = Join-Path $repoRoot "Jellyfin.Plugin.CorrMedia\bin\Debug\$skipOutDir\Jellyfin.Plugin.CorrMedia.dll"
$skipManifest = Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\manifest.json'
$skipTargetDir = Join-Path $pluginRoot 'Jellyfin.Plugin.CorrMedia'

if ($BuildPlugins) {
    Write-Host 'Building CorrMedia plugin...'
    dotnet build $skipProject
    if ($LASTEXITCODE -ne 0) {
        throw 'CorrMedia build failed.'
    }
}

New-Item -ItemType Directory -Force -Path $skipTargetDir | Out-Null
Copy-Item -Force $skipDll (Join-Path $skipTargetDir 'Jellyfin.Plugin.CorrMedia.dll')
Copy-Item -Force $skipManifest (Join-Path $skipTargetDir 'manifest.json')

Write-Host 'CorrMedia staged into docker/jellyfin/plugins.'

if ($StartStack) {
    Write-Host 'Starting Jellyfin docker stack...'

    New-Item -ItemType Directory -Force -Path $dockerClientConfig | Out-Null
    '{}' | Set-Content -Path (Join-Path $dockerClientConfig 'config.json')

    docker --config "$dockerClientConfig" compose -f "$composeFile" up -d
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to start Jellyfin docker stack.'
    }

    Write-Host 'Jellyfin is starting at http://localhost:18096'
    Write-Host 'Note: server-side mute/cut requires the patched image (build-and-run-patched-docker.ps1).'
}
