#Requires -Version 5.1
<#
.SYNOPSIS
    Builds patched Jellyfin from jellyfin-source, builds plugins, and runs the stack in Docker.
.DESCRIPTION
    0. Copies distribution/jellyfin-core-overlay into jellyfin-source (overlay is the source of truth).
    1. Builds the Docker image jellyfin-patched:local from jellyfin-source (patched server).
    2. Builds CorrMedia (patched, net9.0) and copies it to docker/jellyfin/plugins.
    2b. Bind-mounts repo-root test-movie.corr.json, test-movie.mkv, and test-movie.vtt over /media (see docker-compose.patched.yml).
    3. Starts the container with docker-compose.patched.yml.
.PARAMETER SkipImageBuild
    If set, skip building the Docker image (use existing jellyfin-patched:local).
.PARAMETER SkipPlugins
    If set, skip building and copying plugins.
.PARAMETER StartStack
    If set (default), start the stack after building. If -StartStack:$false, only build.
.EXAMPLE
    .\scripts\build-and-run-patched-docker.ps1
#>
param(
    [switch] $SkipImageBuild = $false,
    [switch] $SkipPlugins = $false,
    [switch] $StartStack = $true
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$jellyfinSource = Join-Path $repoRoot 'jellyfin-source'
$dockerfile = Join-Path $repoRoot 'docker\Dockerfile.patched-jellyfin'
$composeFile = Join-Path $repoRoot 'docker-compose.patched.yml'
$pluginRoot = Join-Path $repoRoot 'docker\jellyfin\plugins'
$dockerClientConfig = Join-Path $repoRoot '.docker-client'

if (-not (Test-Path $jellyfinSource)) {
    Write-Error "jellyfin-source not found at $jellyfinSource"
}
if (-not (Test-Path $dockerfile)) {
    Write-Error "Dockerfile not found at $dockerfile"
}

# 0. Overlay is the committed patch set; apply it before any image build so
#    jellyfin-source cannot drift from distribution/jellyfin-core-overlay.
Write-Host 'Applying core overlay to jellyfin-source...'
& (Join-Path $PSScriptRoot 'apply-core-overlay.ps1') -JellyfinSourcePath $jellyfinSource
if (-not $?) {
    throw 'Failed to apply core overlay.'
}

# 1. Build Docker image (patched Jellyfin)
if (-not $SkipImageBuild) {
    Write-Host 'Building patched Jellyfin Docker image (jellyfin-patched:local)...'
    docker build -f $dockerfile -t jellyfin-patched:local $repoRoot
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker build failed.'
    }
    Write-Host 'Image jellyfin-patched:local built.'
} else {
    Write-Host 'Skipping image build (using existing jellyfin-patched:local).'
}

# 2. Build and stage CorrMedia
if (-not $SkipPlugins) {
    $pluginProject = Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\Jellyfin.Plugin.CorrMedia.csproj'
    $pluginOutDir = 'net9.0'
    $pluginDll = Join-Path $repoRoot "Jellyfin.Plugin.CorrMedia\bin\Debug\$pluginOutDir\Jellyfin.Plugin.CorrMedia.dll"

    Write-Host 'Building CorrMedia plugin...'
    dotnet build $pluginProject -c Debug
    if ($LASTEXITCODE -ne 0) { throw 'CorrMedia build failed.' }

    $pluginTargetDir = Join-Path $pluginRoot 'Jellyfin.Plugin.CorrMedia'
    New-Item -ItemType Directory -Force -Path $pluginTargetDir | Out-Null
    Copy-Item -Force $pluginDll $pluginTargetDir
    Copy-Item -Force (Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\manifest.json') $pluginTargetDir
    Copy-Item -Force (Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\manifest.json') (Join-Path $pluginTargetDir 'meta.json')

    Write-Host 'CorrMedia staged to docker\jellyfin\plugins.'

    foreach ($legacyName in @('Jellyfin.Plugin.AudioControl')) {
        $legacyDir = Join-Path $pluginRoot $legacyName
        if (Test-Path $legacyDir) {
            Remove-Item -Recurse -Force $legacyDir
            Write-Host "Removed leftover $legacyName plugin folder."
        }
    }
} else {
    Write-Host 'Skipping plugin build and stage.'
}

# 2b. Test media: compose bind-mounts repo-root test-movie / test2 files over /media.
# Copies below keep docker/jellyfin/media in sync for inspection; playback uses the bind mounts.
$mediaDir = Join-Path $repoRoot 'docker\jellyfin\media'
New-Item -ItemType Directory -Force -Path $mediaDir | Out-Null
foreach ($name in @('test-movie.corr.json', 'test-movie.mkv', 'test-movie.vtt', 'test2.corr.json')) {
    $src = Join-Path $repoRoot $name
    if (Test-Path $src) {
        Copy-Item -Force $src (Join-Path $mediaDir $name)
        Write-Host "Copied $name to docker\jellyfin\media."
    }
}
# test2.mkv is multi-GB — rely on compose bind-mount; do not copy into media/.
if (Test-Path (Join-Path $repoRoot 'test2.mkv')) {
    Write-Host 'test2.mkv will be bind-mounted from repo root (not copied).'
}

# 3. Start stack (force-recreate so new image/plugins are used)
if ($StartStack) {
    Write-Host 'Starting patched Jellyfin stack (recreating container)...'
    New-Item -ItemType Directory -Force -Path $dockerClientConfig | Out-Null
    '{}' | Set-Content -Path (Join-Path $dockerClientConfig 'config.json')

    $composeArgs = @('--config', $dockerClientConfig, 'compose', '-f', $composeFile)
    $nvidiaCompose = Join-Path $repoRoot 'docker-compose.patched.nvidia.yml'
    if ((Test-Path $nvidiaCompose) -and (Get-Command nvidia-smi -ErrorAction SilentlyContinue)) {
        Write-Host 'NVIDIA GPU detected; passing it into the container for NVENC.'
        $composeArgs += @('-f', $nvidiaCompose)
    }

    docker @composeArgs up -d --force-recreate
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to start stack.'
    }
    Write-Host 'Patched Jellyfin is running at http://localhost:18096'
} else {
    Write-Host 'Skipping stack start. Run: docker compose -f docker-compose.patched.yml up -d'
}
