# VolumeKeeper

VolumeKeeper is a Windows desktop application that automatically remembers and restores volume levels for your applications. Never lose your carefully adjusted volume settings again.

## What VolumeKeeper Does

VolumeKeeper monitors volume changes in Windows and automatically saves them. When you launch an application later, VolumeKeeper restores its volume to exactly where you left it.

**Key Features:**
- Automatically saves volume levels when you adjust them
- Restores saved volumes when applications launch
- Clean, modern interface showing all your applications and their saved volumes
- Real-time activity log showing what VolumeKeeper is doing
- System tray integration for easy access
- Lightweight and efficient background operation

## How It Works

1. **Monitor**: VolumeKeeper watches for volume changes in Windows Volume Mixer
2. **Save**: When you adjust an application's volume, VolumeKeeper saves that setting
3. **Restore**: When the application launches again, VolumeKeeper automatically applies your saved volume level

## Getting Started

### System Requirements

- Windows 10 or Windows 11
- .NET 9.0 Runtime

### Installation

1. Download the latest release from the releases page
2. Extract the files to your desired location
3. Run `VolumeKeeper.exe`
4. The application will start and appear in your system tray

### First Use

1. Launch VolumeKeeper
2. Open some applications that produce audio (music player, browser, games, etc.)
3. Adjust their volumes using Windows Volume Mixer or VolumeKeeper's interface
4. VolumeKeeper will automatically save these settings
5. Next time you open those applications, their volumes will be restored automatically

## User Interface

### Home Tab
- View all detected applications with their current and saved volume levels
- Manually adjust volumes using the sliders
- Save or revert volume changes
- Toggle automatic volume restoration on/off

### Logs Tab  
- Real-time activity feed showing volume changes, application launches, and restorations
- Clear logs when needed
- Toggle auto-scroll to see the latest activity

### System Tray
- Right-click the tray icon to open VolumeKeeper or exit
- Left-click to quickly open the main window

## Volume Management

### Saving Volumes
- Adjust any application's volume using the slider in VolumeKeeper
- Click the save button (disk icon) to permanently save that volume level
- Or use Windows Volume Mixer - VolumeKeeper will detect and save changes automatically

### Reverting Changes
- If you change a volume but haven't saved it yet, click the revert button (undo icon)
- This restores the volume to its last saved state

### Muting Applications
- Click the volume icon next to any slider to mute/unmute
- VolumeKeeper remembers the volume level before muting for easy restoration

## Settings

### Auto-restore Volumes
Toggle this setting in the Home tab to enable or disable automatic volume restoration when applications launch.

### Auto-scroll Logs
Toggle this setting in the Logs tab to automatically scroll to the latest log entries.

## Troubleshooting

### VolumeKeeper doesn't detect my application
- Make sure the application is actually producing audio
- Try refreshing the application list using the Refresh button
- Some applications may take a moment to appear after launching

### Volumes aren't being restored
- Check that "Auto-restore volumes" is enabled in the Home tab
- Verify that you've actually saved volume settings for the application
- Check the Logs tab for any error messages

### System tray icon is missing
- VolumeKeeper runs without elevated privileges and may have limited system tray access
- The application will still function normally even without the tray icon
- Try running as administrator if tray icon functionality is important

## Data Storage

VolumeKeeper stores its settings in:
`%APPDATA%\VolumeKeeper\configs\`

This includes:
- `volume_settings.json` - Your saved volume levels and application settings
- `window_settings.json` - Window position and size preferences

## Privacy

VolumeKeeper operates entirely on your local machine. No data is sent to external servers or services. All volume settings and preferences are stored locally on your computer.

## License

VolumeKeeper is released under the MIT License. See the LICENSE file for details.

## Support

If you encounter issues or have suggestions:
- Check the Logs tab for error messages
- Restart VolumeKeeper to resolve temporary issues
- Ensure you have the latest .NET 9.0 Runtime installed

VolumeKeeper is designed to run quietly in the background, making your Windows audio experience more consistent and convenient.