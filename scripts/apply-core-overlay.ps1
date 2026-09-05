#Requires -Version 5.1
<#
.SYNOPSIS
    Applies the jellyfin-core-overlay into a local Jellyfin source checkout.
.DESCRIPTION
    Copies patched files from distribution/jellyfin-core-overlay into the
    given Jellyfin source path. Idempotent; prints what was copied.
.PARAMETER JellyfinSourcePath
    Path to the root of a Jellyfin source tree (e.g. where Jellyfin.sln lives).
.PARAMETER OverlayPath
    Path to the overlay folder. Defaults to repo distribution/jellyfin-core-overlay.
.EXAMPLE
    .\apply-core-overlay.ps1 -JellyfinSourcePath C:\jellyfin
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $JellyfinSourcePath,

    [string] $OverlayPath
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
if (-not $OverlayPath) {
    $OverlayPath = Join-Path $repoRoot 'distribution\jellyfin-core-overlay'
}

if (-not (Test-Path $OverlayPath)) {
    Write-Error "Overlay path not found: $OverlayPath"
}

$jellyfinRoot = $JellyfinSourcePath
if (-not (Test-Path $jellyfinRoot)) {
    Write-Error "Jellyfin source path not found: $jellyfinRoot"
}

$items = Get-ChildItem -Path $OverlayPath -Recurse -File
foreach ($f in $items) {
    $relative = $f.FullName.Substring($OverlayPath.Length).TrimStart('\', '/')
    $dest = Join-Path $jellyfinRoot $relative
    $destDir = Split-Path -Parent $dest
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }
    Copy-Item -Force -LiteralPath $f.FullName -Destination $dest
    Write-Host "Copied: $relative -> $dest"
}

Write-Host "Core overlay applied to $jellyfinRoot"
