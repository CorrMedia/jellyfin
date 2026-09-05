# Generates a 90s test clip with on-screen original-second counter + tone audio.
# corr.json verification:
#   - mute 0-10 / 30-35 / 60-65: silence while ORIG Ns still advances
#   - skip 15-20: after ORIG 14s, next frame should show ORIG 20s (cut omitted)
# Usage: .\scripts\generate-test-movie.ps1
param(
    [string]$OutputPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'test-movie.mkv'),
    [string]$Container = 'jellyfin-patched-test',
    [int]$DurationSeconds = 90
)

$ErrorActionPreference = 'Stop'
$tmpInContainer = '/media/test-movie.mkv'

Write-Host "Generating ${DurationSeconds}s testsrc2 + ORIG second overlay via $Container..."

$vf = @"
[0:v]drawtext=fontfile=/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf:fontsize=140:fontcolor=white:borderw=4:bordercolor=black:x=(w-tw)/2:y=(h-th)/2-40:text='ORIG %{eif\:t\:d}s',drawtext=fontfile=/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf:fontsize=28:fontcolor=yellow:borderw=2:bordercolor=black:x=40:y=40:text='mute 0-10 / skip 15-20 / mute 30-35 / mute 60-65',drawtext=fontfile=/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf:fontsize=24:fontcolor=white:borderw=2:bordercolor=black:x=40:y=h-60:text='After cut\: at playhead ~15s you should see ORIG 20s'[v];[1:a]volume=0.4[a]
"@.Trim()

docker exec $Container /usr/lib/jellyfin-ffmpeg/ffmpeg -y `
  -f lavfi -i "testsrc2=size=1280x720:rate=24:duration=$DurationSeconds" `
  -f lavfi -i "sine=frequency=880:sample_rate=44100:duration=$DurationSeconds" `
  -filter_complex $vf `
  -map "[v]" -map "[a]" -c:v libx264 -pix_fmt yuv420p -preset veryfast -crf 23 -c:a aac -b:a 128k `
  $tmpInContainer

if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed ($LASTEXITCODE)" }

docker cp "${Container}:${tmpInContainer}" $OutputPath
$mediaDir = Join-Path (Split-Path $PSScriptRoot -Parent) 'docker\jellyfin\media'
New-Item -ItemType Directory -Force -Path $mediaDir | Out-Null
Copy-Item -Force $OutputPath (Join-Path $mediaDir 'test-movie.mkv')
$corr = Join-Path (Split-Path $PSScriptRoot -Parent) 'test-movie.corr.json'
if (Test-Path $corr) {
    Copy-Item -Force $corr (Join-Path $mediaDir 'test-movie.corr.json')
}

Write-Host "Wrote $OutputPath and staged under docker\jellyfin\media."
Write-Host "Rescan the library in Jellyfin if the item still shows the old blue clip."
