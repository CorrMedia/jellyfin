#Requires -Version 5.1
<#
.SYNOPSIS
    Builds CorrMedia (with patched core ref when available) and copies it into a Jellyfin plugins folder.
.DESCRIPTION
    Builds CorrMedia from this repo against Jellyfin 10.11.11 (net9.0). When jellyfin-source exists, the plugin is built against the patched core.
    Deploys CorrMedia only.
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

$pluginProject = Join-Path $RepoRoot 'Jellyfin.Plugin.CorrMedia\Jellyfin.Plugin.CorrMedia.csproj'
$pluginOut = Join-Path $RepoRoot 'Jellyfin.Plugin.CorrMedia\bin\Debug'
$tfm = 'net9.0'
$pluginDll = Join-Path $pluginOut "$tfm\Jellyfin.Plugin.CorrMedia.dll"

Write-Host "Building CorrMedia ($tfm)..."
dotnet build "$pluginProject" -c Debug
if ($LASTEXITCODE -ne 0) { throw "CorrMedia build failed." }

$pluginTargetDir = Join-Path $TargetPluginsPath 'Jellyfin.Plugin.CorrMedia'
New-Item -ItemType Directory -Force -Path $pluginTargetDir | Out-Null
Copy-Item -Force $pluginDll $pluginTargetDir
Copy-Item -Force (Join-Path $RepoRoot 'Jellyfin.Plugin.CorrMedia\manifest.json') $pluginTargetDir
Copy-Item -Force (Join-Path $RepoRoot 'Jellyfin.Plugin.CorrMedia\manifest.json') (Join-Path $pluginTargetDir 'meta.json')

Write-Host "CorrMedia deployed to $TargetPluginsPath."
