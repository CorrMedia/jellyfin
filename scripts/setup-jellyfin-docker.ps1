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

$pluginProject = Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\Jellyfin.Plugin.CorrMedia.csproj'
$pluginOutDir = 'net9.0'
$pluginDll = Join-Path $repoRoot "Jellyfin.Plugin.CorrMedia\bin\Debug\$pluginOutDir\Jellyfin.Plugin.CorrMedia.dll"
$pluginManifest = Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\manifest.json'
$pluginTargetDir = Join-Path $pluginRoot 'Jellyfin.Plugin.CorrMedia'

if ($BuildPlugins) {
    Write-Host 'Building CorrMedia plugin...'
    dotnet build $pluginProject
    if ($LASTEXITCODE -ne 0) {
        throw 'CorrMedia build failed.'
    }
}

New-Item -ItemType Directory -Force -Path $pluginTargetDir | Out-Null
Copy-Item -Force $pluginDll (Join-Path $pluginTargetDir 'Jellyfin.Plugin.CorrMedia.dll')
Copy-Item -Force $pluginManifest (Join-Path $pluginTargetDir 'manifest.json')
Copy-Item -Force $pluginManifest (Join-Path $pluginTargetDir 'meta.json')

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
