#Requires -Version 5.1
<#
.SYNOPSIS
    Builds CorrMedia (with patched core ref when available) and copies it into a Jellyfin plugins folder.
.DESCRIPTION
    Builds CorrMedia from this repo. When jellyfin-source exists, the plugin is built against the patched core (net9.0).
    AudioControl is deprecated for mute (folded into CorrMedia); it is not deployed by this script.
.PARAMETER TargetPluginsPath
    Directory where Jellyfin looks for plugins.
.PARAMETER RepoRoot
    Repo root. Defaults to parent of the scripts folder.
.EXAMPLE
    .\deploy-patched-plugins.ps1 -TargetPluginsPath C:\jellyfin\plugins
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $TargetPluginsPath,

    [string] $RepoRoot
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
}

$skipProject = Join-Path $RepoRoot 'Jellyfin.Plugin.CorrMedia\Jellyfin.Plugin.CorrMedia.csproj'
$skipOut = Join-Path $RepoRoot 'Jellyfin.Plugin.CorrMedia\bin\Debug'
$tfm = if (Test-Path (Join-Path $RepoRoot 'jellyfin-source\MediaBrowser.Controller\MediaBrowser.Controller.csproj')) { 'net9.0' } else { 'net8.0' }
$skipDll = Join-Path $skipOut "$tfm\Jellyfin.Plugin.CorrMedia.dll"

Write-Host "Building CorrMedia ($tfm)..."
dotnet build "$skipProject" -c Debug
if ($LASTEXITCODE -ne 0) { throw "CorrMedia build failed." }

$skipTargetDir = Join-Path $TargetPluginsPath 'Jellyfin.Plugin.CorrMedia'
New-Item -ItemType Directory -Force -Path $skipTargetDir | Out-Null
Copy-Item -Force $skipDll $skipTargetDir
Copy-Item -Force (Join-Path $RepoRoot 'Jellyfin.Plugin.CorrMedia\manifest.json') $skipTargetDir

Write-Host "CorrMedia deployed to $TargetPluginsPath (AudioControl not required for Phase 1 mute)."
