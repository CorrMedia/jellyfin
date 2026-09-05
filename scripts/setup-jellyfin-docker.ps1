param(
    [switch]$BuildPlugins = $true,
    [switch]$StartStack = $true
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$composeFile = Join-Path $repoRoot 'docker-compose.jellyfin.yml'
$pluginRoot = Join-Path $repoRoot 'docker\jellyfin\plugins'
$dockerClientConfig = Join-Path $repoRoot '.docker-client'

$audioProject = Join-Path $repoRoot 'Jellyfin.Plugin.AudioControl\Jellyfin.Plugin.AudioControl.csproj'
$skipProject = Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\Jellyfin.Plugin.CorrMedia.csproj'

# When jellyfin-source exists, AudioControl builds for net9.0 (patched core)
$audioOutDir = if (Test-Path (Join-Path $repoRoot 'jellyfin-source\MediaBrowser.Controller\MediaBrowser.Controller.csproj')) { 'net9.0' } else { 'net8.0' }
$audioDll = Join-Path $repoRoot "Jellyfin.Plugin.AudioControl\bin\Debug\$audioOutDir\Jellyfin.Plugin.AudioControl.dll"
$skipDll = Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\bin\Debug\net8.0\Jellyfin.Plugin.CorrMedia.dll'

$audioManifest = Join-Path $repoRoot 'Jellyfin.Plugin.AudioControl\manifest.json'
$skipManifest = Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\manifest.json'

$audioTargetDir = Join-Path $pluginRoot 'Jellyfin.Plugin.AudioControl'
$skipTargetDir = Join-Path $pluginRoot 'Jellyfin.Plugin.CorrMedia'

if ($BuildPlugins) {
    Write-Host 'Building AudioControl plugin...'
    dotnet build "$audioProject"
    if ($LASTEXITCODE -ne 0) {
        throw 'AudioControl build failed.'
    }

    Write-Host 'Building CorrMedia plugin...'
    dotnet build "$skipProject"
    if ($LASTEXITCODE -ne 0) {
        throw 'CorrMedia build failed.'
    }
}

New-Item -ItemType Directory -Force -Path $audioTargetDir | Out-Null
New-Item -ItemType Directory -Force -Path $skipTargetDir | Out-Null

Copy-Item -Force $audioDll (Join-Path $audioTargetDir 'Jellyfin.Plugin.AudioControl.dll')
Copy-Item -Force $audioManifest (Join-Path $audioTargetDir 'manifest.json')

Copy-Item -Force $skipDll (Join-Path $skipTargetDir 'Jellyfin.Plugin.CorrMedia.dll')
Copy-Item -Force $skipManifest (Join-Path $skipTargetDir 'manifest.json')

Write-Host 'Plugins staged into docker/jellyfin/plugins.'

if ($StartStack) {
    Write-Host 'Starting Jellyfin docker stack...'

    New-Item -ItemType Directory -Force -Path $dockerClientConfig | Out-Null
    '{}' | Set-Content -Path (Join-Path $dockerClientConfig 'config.json')

    docker --config "$dockerClientConfig" compose -f "$composeFile" up -d
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to start Jellyfin docker stack.'
    }

    Write-Host 'Jellyfin is starting at http://localhost:18096'
}

