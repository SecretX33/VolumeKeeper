# Technical Guide

This guide contains technical details and troubleshooting information for VolumeKeeper.

# Data Storage

VolumeKeeper stores its settings in your Windows user profile:

**Location:** `%APPDATA%\VolumeKeeper\configs\`

This directory contains:

## Volume Settings
Stores your pinned volume levels and application settings.

`volume_settings.json`
```json
{
  "ApplicationVolumes": [
    {
      "Id": "C:\\Program Files\\Mozilla Firefox\\firefox.exe",
      "Volume": 10
    },
    {
      "Id": "C:\\Program Files\\Slack\\app-4.46.101\\slack.exe",
      "Volume": 20
    }
  ],
  "AutoRestoreEnabled": true,
  "AutoScrollLogsEnabled": false,
  "LastUpdated": "2025-10-19T23:05:33.703829-03:00"
}
```

The file structure includes:
- **ApplicationVolumes**: Your pinned volume levels for each application (identified by full executable path, case-insensitive)
- **AutoRestoreEnabled**: Whether automatic volume restoration is enabled
- **AutoScrollLogsEnabled**: Whether the activity log auto-scrolls to show latest entries
- **LastUpdated**: Timestamp of the last settings update

## Window Settings
Stores window position and size preferences so VolumeKeeper opens where you left it.

`window_settings.json`
```json
{
  "Main": {
    "Width": 619,
    "Height": 595,
    "X": 1013,
    "Y": 394,
    "IsMaximized": false
  }
}
```

# Troubleshooting

## VolumeKeeper doesn't detect my application

**Possible causes:**
- The application isn't producing any audio
- The application hasn't appeared in Windows Volume Mixer yet
- There's a delay between launching and audio initialization

**Solutions:**
- Make sure the application is playing audio (music, video, sound effects, etc.)
- Click the **Refresh** button in the Home tab to update the application list
- Wait a few seconds after launching the application - some apps take time to initialize audio

## Volumes aren't being restored automatically

**Possible causes:**
- Auto-restore is disabled
- No volume has been pinned for that application
- The application is being detected under a different name

**Solutions:**
- Check that "Auto-restore volumes" toggle is enabled in the Home tab
- Verify you've clicked the Pin button (pin icon) to save the volume for that application (once pinned, the slider will automatically update the saved volume)
- Look in the activity log (Logs tab) for any error messages
- Check the pinned volume display under the application name - it should show "Pinned: XX%"

## System tray icon is missing

**Possible causes:**
- Windows may hide the tray icon based on system settings
- Insufficient permissions to create tray icon
- System tray overflow (too many icons)

**Solutions:**
- VolumeKeeper will continue to work normally even without the tray icon
- Check Windows Settings > Personalization > Taskbar to enable the icon
- Try running VolumeKeeper as administrator if tray icon functionality is important
- Look for the icon in the hidden icons overflow area (up arrow in system tray)

### Pin/Unpin button behavior

The pin button and slider work together:
- **First click**: Pins the current volume level
- **Once pinned**: Moving the VolumeKeeper slider automatically updates the saved volume (no need to click pin again)
- **When volume matches pinned**: Clicking the pin button unpins the volume (removes the saved setting)
- **When volume differs from pinned**: Clicking the pin button re-pins at the new volume (though moving the slider also updates it automatically)
- **Revert button**: Only appears when current volume differs from pinned volume (typically from external Windows Volume Mixer changes)

**Important distinction**: Only slider changes in VolumeKeeper automatically update pinned volumes. External changes made through Windows Volume Mixer will trigger auto-restore (reverting to the pinned volume) when auto-restore is enabled.

## Application appears multiple times

**Why this happens:**
- Multiple windows or instances of the same application
- Each audio session from the application appears separately

**This is normal behavior** - Windows treats each audio session independently. VolumeKeeper will restore the pinned volume to all instances when they launch.

## Volumes keep reverting unexpectedly

**Possible causes:**
- Another application or Windows itself is changing volumes (only occurs when auto-restore is disabled)

**Check the activity log** (Logs tab) to see exactly what's happening and when volumes are being changed.

# Advanced Information

## How Application Matching Works

VolumeKeeper identifies applications by their **full executable path** with case-insensitive matching (e.g., `C:\Program Files\Mozilla Firefox\firefox.exe`).

This means:
- ✅ Case-insensitive matching (`C:\Firefox\firefox.exe` = `c:\firefox\FIREFOX.EXE`)
- ✅ Different installations of the same executable can have different pinned volumes (e.g., `C:\Firefox\firefox.exe` vs `D:\Apps\firefox.exe`)
- ✅ All audio sessions from the same program share the same pinned volume
- ❌ Application icon is **not** considered
- ❌ Window title is **not** considered
- ❌ Command line arguments are **not** considered

## Volume Restoration Behavior

When an application launches:
1. VolumeKeeper detects the new audio session
2. Checks if a pinned volume exists for that executable path
3. If found, automatically applies the pinned volume
4. Logs the restoration in the activity log

### Performance Notes

- VolumeKeeper uses minimal system resources
- Settings are saved with a 2-second delay to reduce disk writes
- Only the active audio sessions are monitored
- Background monitoring is lightweight and efficient

## Privacy & Security

- **No network activity**: VolumeKeeper operates entirely offline
- **No telemetry**: No data is collected or sent anywhere
- **Local storage only**: All settings stay on your computer
- **No elevated privileges required**: Runs with normal user permissions

## File Locations Summary

| File | Location | Purpose |
|------|----------|---------|
| VolumeKeeper.exe | Installation directory | Main application |
| volume_settings.json | `%APPDATA%\VolumeKeeper\configs\` | Pinned volumes and settings |
| window_settings.json | `%APPDATA%\VolumeKeeper\configs\` | Window position/size |

## Support

If you encounter issues:
1. Check this troubleshooting guide
2. Review the activity log (Logs tab) for error messages
3. Try restarting VolumeKeeper
4. Check that your audio applications are working correctly in Windows Volume Mixer
5. Report issues on the GitHub repository with log details
