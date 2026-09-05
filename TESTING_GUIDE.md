# 🧪 Audio Control Plugin Testing Guide

## Overview
This guide provides comprehensive testing instructions for the Audio Control plugin, including deployment, verification, and functional testing.

## 📋 Prerequisites

### Required Software
- **Jellyfin Server** (version 10.8.0 or later)
- **PowerShell** (Windows) or **Bash** (Linux/macOS)
- **curl** (for manual API testing)
- **Media files** for testing playback

### Required Knowledge
- Basic Jellyfin administration
- Understanding of REST APIs
- Familiarity with command line tools

## 🚀 Step 1: Deploy the Plugin

### Windows Deployment
```powershell
# Copy plugin files to Jellyfin plugins directory
$jellyfinPluginsPath = "C:\ProgramData\Jellyfin\Server\plugins"
Copy-Item "bin\Release\publish\Jellyfin.Plugin.AudioControl.dll" $jellyfinPluginsPath
Copy-Item "manifest.json" $jellyfinPluginsPath

# Restart Jellyfin service
Restart-Service JellyfinServer
```

### Linux Deployment
```bash
# Copy plugin files
sudo cp bin/Release/publish/Jellyfin.Plugin.AudioControl.dll /var/lib/jellyfin/plugins/
sudo cp manifest.json /var/lib/jellyfin/plugins/

# Restart Jellyfin
sudo systemctl restart jellyfin
```

### Docker Deployment
```bash
# Copy plugin files to Docker container
docker cp bin/Release/publish/Jellyfin.Plugin.AudioControl.dll jellyfin:/config/plugins/
docker cp manifest.json jellyfin:/config/plugins/

# Restart container
docker restart jellyfin
```

## 🔍 Step 2: Verify Plugin Installation

### Check Jellyfin Dashboard
1. Open Jellyfin web interface: `http://your-server:8096`
2. Navigate to **Dashboard** → **Plugins** → **General**
3. Look for "Audio Control" plugin
4. Verify status shows as **Enabled**

### Check Logs
Look for these messages in Jellyfin logs:

**Windows:**
```powershell
Get-Content "C:\ProgramData\Jellyfin\Server\logs\jellyfin.log" -Tail 50 | Select-String "Audio Control"
```

**Linux:**
```bash
tail -50 /var/log/jellyfin/jellyfin.log | grep "Audio Control"
```

**Expected Log Messages:**
```
[INF] Audio Control plugin registered successfully
[INF] FFmpeg integration service initialized
[INF] Audio control service initialized
[INF] Plugin service registrator completed
```

## 🧪 Step 3: Automated Testing

### Using PowerShell Test Script
```powershell
# Run the test script
.\test-audio-control.ps1 -JellyfinUrl "http://localhost:8096" -Verbose

# With API key (if authentication is required)
.\test-audio-control.ps1 -JellyfinUrl "http://localhost:8096" -ApiKey "your-api-key" -Verbose
```

### Using Bash Test Script
```bash
# Make script executable
chmod +x test-audio-control.sh

# Run the test script
./test-audio-control.sh "http://localhost:8096"

# With API key
./test-audio-control.sh "http://localhost:8096" "your-api-key"
```

## 🔧 Step 4: Manual API Testing

### Test Plugin Registration
```bash
curl -X GET "http://localhost:8096/System/Plugins" \
  -H "Content-Type: application/json"
```

### Test Session Discovery
```bash
curl -X GET "http://localhost:8096/Sessions" \
  -H "Content-Type: application/json"
```

### Test Audio Control Endpoints

#### Get All Audio States
```bash
curl -X GET "http://localhost:8096/AudioControl/State" \
  -H "Content-Type: application/json"
```

#### Mute a Session
```bash
curl -X POST "http://localhost:8096/AudioControl/Mute/session-id-here?muted=true" \
  -H "Content-Type: application/json"
```

#### Unmute a Session
```bash
curl -X POST "http://localhost:8096/AudioControl/Mute/session-id-here?muted=false" \
  -H "Content-Type: application/json"
```

#### Set Volume Level
```bash
curl -X POST "http://localhost:8096/AudioControl/Volume/session-id-here?volumeLevel=50" \
  -H "Content-Type: application/json"
```

#### Get Session Audio State
```bash
curl -X GET "http://localhost:8096/AudioControl/State/session-id-here" \
  -H "Content-Type: application/json"
```

## 🎯 Step 5: Functional Testing

### Test Scenario 1: Basic Mute/Unmute
1. **Start playback** of any media file in Jellyfin
2. **Note the session ID** from the API response
3. **Send mute command:**
   ```bash
   curl -X POST "http://localhost:8096/AudioControl/Mute/SESSION_ID?muted=true"
   ```
4. **Verify audio is muted** in the client
5. **Send unmute command:**
   ```bash
   curl -X POST "http://localhost:8096/AudioControl/Mute/SESSION_ID?muted=false"
   ```
6. **Verify audio is restored**

### Test Scenario 2: Volume Control
1. **Start playback** of any media file
2. **Set volume to 50%:**
   ```bash
   curl -X POST "http://localhost:8096/AudioControl/Volume/SESSION_ID?volumeLevel=50"
   ```
3. **Verify volume is reduced** in the client
4. **Set volume to 100%:**
   ```bash
   curl -X POST "http://localhost:8096/AudioControl/Volume/SESSION_ID?volumeLevel=100"
   ```
5. **Verify volume is restored**

### Test Scenario 3: Multiple Sessions
1. **Start playback** on multiple clients (web, mobile, TV)
2. **Test mute on each session** individually
3. **Verify each session** responds independently
4. **Test volume control** on each session

### Test Scenario 4: Error Handling
1. **Test invalid session ID:**
   ```bash
   curl -X GET "http://localhost:8096/AudioControl/State/invalid-id"
   ```
2. **Test invalid volume level:**
   ```bash
   curl -X POST "http://localhost:8096/AudioControl/Volume/SESSION_ID?volumeLevel=150"
   ```
3. **Verify proper error responses**

## 📊 Step 6: Monitoring and Logs

### Monitor Plugin Activity
Watch for these log patterns:

**Successful Operations:**
```
[INF] Applying FFmpeg mute filter to session {SessionId}: True
[INF] Successfully applied audio filters to session {SessionId}
[INF] Successfully muted session {SessionId} using FFmpeg approach
```

**Error Conditions:**
```
[ERR] Failed to apply audio filters to session {SessionId}
[WRN] No active transcoding process found for session {SessionId}
[ERR] FFmpeg mute failed for session {SessionId}, falling back to client-side
```

### Performance Monitoring
- **Response times** for API calls
- **Memory usage** of the plugin
- **CPU usage** during audio operations
- **Network activity** for client-side fallback

## 🐛 Step 7: Troubleshooting

### Common Issues

#### Plugin Not Loading
**Symptoms:** Plugin doesn't appear in dashboard
**Solutions:**
- Check file permissions
- Verify DLL is compatible with Jellyfin version
- Check for missing dependencies
- Review Jellyfin logs for errors

#### API Endpoints Not Responding
**Symptoms:** 404 errors on API calls
**Solutions:**
- Verify plugin is loaded
- Check API endpoint URLs
- Ensure proper authentication
- Review plugin registration logs

#### Audio Control Not Working
**Symptoms:** Commands sent but no audio change
**Solutions:**
- Check if session is transcoding
- Verify FFmpeg integration
- Test client-side fallback
- Review session state logs

#### Performance Issues
**Symptoms:** Slow response times
**Solutions:**
- Monitor system resources
- Check for memory leaks
- Optimize FFmpeg operations
- Review concurrent session handling

### Debug Mode
Enable verbose logging by adding to Jellyfin configuration:
```json
{
  "LogLevel": "Debug",
  "EnableDebugLogging": true
}
```

## 📈 Step 8: Performance Testing

### Load Testing
```bash
# Test multiple concurrent sessions
for i in {1..10}; do
  curl -X POST "http://localhost:8096/AudioControl/Mute/SESSION_ID?muted=true" &
done
wait
```

### Stress Testing
```bash
# Rapid mute/unmute cycles
for i in {1..100}; do
  curl -X POST "http://localhost:8096/AudioControl/Mute/SESSION_ID?muted=true"
  sleep 0.1
  curl -X POST "http://localhost:8096/AudioControl/Mute/SESSION_ID?muted=false"
  sleep 0.1
done
```

## ✅ Step 9: Success Criteria

### Plugin Installation
- [ ] Plugin appears in Jellyfin dashboard
- [ ] Plugin status shows as "Enabled"
- [ ] No errors in Jellyfin logs

### API Functionality
- [ ] All API endpoints respond correctly
- [ ] Proper error handling for invalid inputs
- [ ] Session state tracking works correctly

### Audio Control
- [ ] Mute/unmute functionality works
- [ ] Volume control works
- [ ] Multiple sessions handled independently
- [ ] FFmpeg integration functions properly

### Performance
- [ ] API response times < 100ms
- [ ] No memory leaks during extended use
- [ ] Concurrent sessions handled efficiently

## 📝 Step 10: Reporting

### Test Results Template
```
Test Date: [DATE]
Jellyfin Version: [VERSION]
Plugin Version: [VERSION]
Test Environment: [OS/BROWSER]

✅ Passed Tests:
- Plugin installation
- API endpoints
- Mute functionality
- Volume control
- Error handling

❌ Failed Tests:
- [List any failures]

⚠️ Issues Found:
- [List any issues]

Performance Metrics:
- Average API response time: [TIME]
- Memory usage: [USAGE]
- CPU usage: [USAGE]

Recommendations:
- [Any recommendations]
```

## Post-implementation validation checklist

After finishing the AudioControl plugin implementation, run through this manual matrix:

| Scenario | Steps | Expected |
|----------|--------|----------|
| **Direct play + mute API** | Start playback (direct play), call `POST /AudioControl/MuteSession?sessionId=...&muted=true` | State tracks muted; no crash. Client may not mute if FFmpeg path not used. |
| **Transcoding + mute API** | Force transcoding (e.g. unsupported codec), call MuteSession | State tracks muted; filter requested via extension service. |
| **EDL mute ranges** | Install CorrMedia + AudioControl; play item with `.edl` containing type-1 (mute) ranges | CorrMedia sends SetMuteRanges to AudioControl; hook service logs filter requests. |
| **Session cleanup** | End playback or close client | Session removed from mute state; no duplicate timers or leaks. |

Verify in logs:

- `AudioControlController initialized with shared DI services`
- `JellyfinTranscodingHookService started - monitoring for mute ranges`
- For EDL mute: `Set mute ranges and requested audio filter for session ...`

### Patched Jellyfin (robust server-side mute)

When using the **patched Jellyfin core** (see [distribution/APPLY_GUIDE.md](../distribution/APPLY_GUIDE.md)):

| Scenario | Steps | Expected |
|----------|--------|----------|
| **Transcoding + EDL mute** | Force transcoding; play item with `.edl` containing type-1 (mute) ranges | Audio is actually muted during those ranges. |
| **Log check** | After starting transcoded playback with mute ranges | Log line: `SessionAudioFilterProvider returning mute filter for session ...` and FFmpeg args include `volume='if(between(t,...'`:eval=frame. |
| **Session cleanup** | Stop playback | Mute ranges cleared for session; no errors. |

Rollback: restore original Jellyfin source and rebuild; replace plugins with unpatched builds if desired.

## 🎉 Conclusion

This testing guide provides a comprehensive approach to validating the Audio Control plugin. Follow each step systematically to ensure the plugin works correctly in your environment.

For additional support or to report issues, refer to the plugin documentation or contact the development team.
