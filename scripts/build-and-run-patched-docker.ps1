#Requires -Version 5.1
<#
.SYNOPSIS
    Builds patched Jellyfin from jellyfin-source, builds plugins, and runs the stack in Docker.
.DESCRIPTION
    1. Builds the Docker image jellyfin-patched:local from jellyfin-source (patched server).
    2. Builds CorrMedia (patched, net9.0) and copies it to docker/jellyfin/plugins.
    2b. Copies test-movie.edl and test-movie.mkv from repo root to docker/jellyfin/media (if present) for EDL testing.
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
    $skipProject = Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\Jellyfin.Plugin.CorrMedia.csproj'
    $skipOutDir = if (Test-Path (Join-Path $repoRoot 'jellyfin-source\MediaBrowser.Controller')) { 'net9.0' } else { 'net8.0' }
    $skipDll = Join-Path $repoRoot "Jellyfin.Plugin.CorrMedia\bin\Debug\$skipOutDir\Jellyfin.Plugin.CorrMedia.dll"

    Write-Host 'Building CorrMedia plugin...'
    dotnet build $skipProject -c Debug
    if ($LASTEXITCODE -ne 0) { throw 'CorrMedia build failed.' }

    $skipTargetDir = Join-Path $pluginRoot 'Jellyfin.Plugin.CorrMedia'
    New-Item -ItemType Directory -Force -Path $skipTargetDir | Out-Null
    Copy-Item -Force $skipDll $skipTargetDir
    Copy-Item -Force (Join-Path $repoRoot 'Jellyfin.Plugin.CorrMedia\manifest.json') $skipTargetDir

    Write-Host 'CorrMedia staged to docker\jellyfin\plugins.'
} else {
    Write-Host 'Skipping plugin build and stage.'
}

# 2b. Ensure test media/EDL are in docker/jellyfin/media (for EDL mute testing)
$mediaDir = Join-Path $repoRoot 'docker\jellyfin\media'
New-Item -ItemType Directory -Force -Path $mediaDir | Out-Null
$rootEdl = Join-Path $repoRoot 'test-movie.edl'
$rootMkv = Join-Path $repoRoot 'test-movie.mkv'
if (Test-Path $rootEdl) {
    Copy-Item -Force $rootEdl (Join-Path $mediaDir 'test-movie.edl')
    Write-Host 'Copied test-movie.edl to docker\jellyfin\media.'
}
if (Test-Path $rootMkv) {
    Copy-Item -Force $rootMkv (Join-Path $mediaDir 'test-movie.mkv')
    Write-Host 'Copied test-movie.mkv to docker\jellyfin\media.'
}

# 3. Start stack (force-recreate so new image/plugins are used)
if ($StartStack) {
    Write-Host 'Starting patched Jellyfin stack (recreating container)...'
    New-Item -ItemType Directory -Force -Path $dockerClientConfig | Out-Null
    '{}' | Set-Content -Path (Join-Path $dockerClientConfig 'config.json')

    docker --config $dockerClientConfig compose -f $composeFile up -d --force-recreate
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to start stack.'
    }
    Write-Host 'Patched Jellyfin is running at http://localhost:18096'
} else {
    Write-Host 'Skipping stack start. Run: docker compose -f docker-compose.patched.yml up -d'
}
