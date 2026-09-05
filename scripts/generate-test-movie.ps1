# Generates a 90s test clip with on-screen original-second counter + 5.1 tone audio.
# Distinct sine per channel so named-channel mute is audible (even after stereo downmix):
#   FL 220 Hz, FR 330 Hz, FC 880 Hz (loud), LFE 60 Hz, BL 440 Hz, BR 550 Hz
# corr.json verification (times are original timeline):
#   - mute 0-10 all channels: full silence while ORIG Ns still advances
#   - skip 15-20: after ORIG 14s, next frame should show ORIG 20s (cut omitted)
#   - mute 30-35 channels [FC]: center tone drops; fronts/surrounds remain
#   - zoom 40-50: punch-in that pans left→right (edited playhead ~35-45s)
#   - mute 60-65 channels [FL, FR]: front tones drop; FC/surrounds remain
#   - blur 70-80: box blur that pans left→right (edited playhead ~65-75s)
# Usage: .\scripts\generate-test-movie.ps1
param(
    [string]$OutputPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'test-movie.mkv'),
    [string]$Container = 'jellyfin-patched-test',
    [int]$DurationSeconds = 90
)

$ErrorActionPreference = 'Stop'
$tmpInContainer = '/media/test-movie.mkv'

Write-Host "Generating ${DurationSeconds}s testsrc2 + 5.1 tones + ORIG overlay via $Container..."

# Video overlays + join six mono sines into 5.1 (FFmpeg order: FL FR FC LFE BL BR).
$filterComplex = @"
[0:v]drawtext=fontfile=/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf:fontsize=140:fontcolor=white:borderw=4:bordercolor=black:x=(w-tw)/2:y=(h-th)/2-40:text='ORIG %{eif\:t\:d}s',drawtext=fontfile=/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf:fontsize=22:fontcolor=yellow:borderw=2:bordercolor=black:x=40:y=40:text='mute-all 0-10 / skip 15-20 / mute-FC 30-35',drawtext=fontfile=/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf:fontsize=22:fontcolor=yellow:borderw=2:bordercolor=black:x=40:y=72:text='zoom 40-50 / mute-FL+FR 60-65 / blur 70-80',drawtext=fontfile=/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf:fontsize=20:fontcolor=white:borderw=2:bordercolor=black:x=40:y=h-60:text='5.1\: FL220 FR330 FC880 LFE60 BL440 BR550'[v];[1:a]volume=0.35[fl];[2:a]volume=0.35[fr];[3:a]volume=0.55[fc];[4:a]volume=0.22[lfe];[5:a]volume=0.3[bl];[6:a]volume=0.3[br];[fl][fr][fc][lfe][bl][br]join=inputs=6:channel_layout=5.1[a]
"@.Trim()

docker exec $Container /usr/lib/jellyfin-ffmpeg/ffmpeg -y `
  -f lavfi -i "testsrc2=size=1280x720:rate=24:duration=$DurationSeconds" `
  -f lavfi -i "sine=frequency=220:sample_rate=48000:duration=$DurationSeconds" `
  -f lavfi -i "sine=frequency=330:sample_rate=48000:duration=$DurationSeconds" `
  -f lavfi -i "sine=frequency=880:sample_rate=48000:duration=$DurationSeconds" `
  -f lavfi -i "sine=frequency=60:sample_rate=48000:duration=$DurationSeconds" `
  -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=$DurationSeconds" `
  -f lavfi -i "sine=frequency=550:sample_rate=48000:duration=$DurationSeconds" `
  -filter_complex $filterComplex `
  -map "[v]" -map "[a]" `
  -c:v libx264 -pix_fmt yuv420p -preset veryfast -crf 23 `
  -c:a aac -b:a 448k -ac 6 `
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
ffprobe -v error -select_streams a:0 -show_entries stream=channels,channel_layout -of default=nw=1 $OutputPath
Write-Host "Rescan the library in Jellyfin if the item still shows the old clip."
