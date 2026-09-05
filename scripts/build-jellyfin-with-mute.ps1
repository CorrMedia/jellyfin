#Requires -Version 5.1
<#
.SYNOPSIS
    Builds Jellyfin from a patched source tree (after applying the core overlay).
.DESCRIPTION
    Restores and builds the Jellyfin solution. Use after apply-core-overlay.ps1.
    Prints success/failure and last lines of build output.
.PARAMETER JellyfinSourcePath
    Path to the root of the Jellyfin source (with overlay already applied).
.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Debug.
.EXAMPLE
    .\build-jellyfin-with-mute.ps1 -JellyfinSourcePath C:\jellyfin
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $JellyfinSourcePath,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$sln = Join-Path $JellyfinSourcePath 'Jellyfin.sln'
if (-not (Test-Path $sln)) {
    Write-Error "Jellyfin.sln not found at $sln"
}

Push-Location $JellyfinSourcePath
try {
    Write-Host "Restoring and building Jellyfin ($Configuration)..."
    dotnet restore $sln
    if ($LASTEXITCODE -ne 0) { throw "Restore failed." }
    dotnet build $sln -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
    Write-Host "Jellyfin build succeeded."
} finally {
    Pop-Location
}
