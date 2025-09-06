# Inno Setup Configuration Guide

This guide covers essential Inno Setup patterns and configuration for creating Windows installers.

## Core Sections

### [Setup] Section - Required Directives

Every Inno Setup script must have these:

```iss
[Setup]
AppName=YourAppName              ; Required: Application name
AppVersion=1.0.0                  ; Required: Version number
DefaultDirName={autopf}\YourApp  ; Installation directory ({autopf} = Program Files)
```

### Common Setup Directives

```iss
DefaultGroupName=YourApp                 ; Start Menu folder name
OutputDir=.\installer                    ; Where installer is created
OutputBaseFilename=Setup                 ; Installer filename (without .exe)
Compression=lzma                         ; lzma, lzma2, zip, bzip, or none
SolidCompression=yes                     ; Better compression for multiple files
PrivilegesRequired=admin                 ; admin, lowest, or none
UninstallDisplayIcon={app}\YourApp.exe   ; Icon in Add/Remove Programs
LicenseFile=LICENSE.txt                  ; Path to license file
```

### Architecture Configuration

For 64-bit applications:
```iss
ArchitecturesAllowed=x64compatible       ; Only runs on x64 systems
ArchitecturesInstallIn64BitMode=x64      ; Installs in 64-bit mode
```

For 32-bit applications (default):
```iss
ArchitecturesAllowed=x86compatible       ; Runs on x86 and x64
; ArchitecturesInstallIn64BitMode not set
```

For both architectures with separate installers:
```iss
; x86 installer - prevent x64 users from running wrong installer
ArchitecturesAllowed=x86compatible and not x64compatible

; x64 installer - require x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64
```

## File Installation

### [Files] Section

```iss
[Files]
; Basic file copy
Source: "app.exe"; DestDir: "{app}"; Flags: ignoreversion

; Recursive directory copy
Source: "data\*"; DestDir: "{app}\data"; Flags: ignoreversion recursesubdirs

; Conditional installation
Source: "x64\*.dll"; DestDir: "{app}"; Check: Is64BitInstallMode

; Register DLL/OCX
Source: "mylib.dll"; DestDir: "{sys}"; Flags: regserver
```

Common Flags:
- `ignoreversion` - Always overwrite (recommended for app files)
- `recursesubdirs` - Include subdirectories
- `createallsubdirs` - Create empty subdirectories too
- `onlyifdoesntexist` - Don't overwrite existing files
- `regserver` - Register DLL/OCX
- `sharedfile` - Reference counted shared file

## Shortcuts and Icons

### [Icons] Section

```iss
[Icons]
; Start Menu shortcuts
Name: "{group}\YourApp"; Filename: "{app}\YourApp.exe"
Name: "{group}\Uninstall"; Filename: "{uninstallexe}"

; Desktop shortcut (conditional on task)
Name: "{commondesktop}\YourApp"; Filename: "{app}\YourApp.exe"; Tasks: desktopicon

; Startup shortcut
Name: "{commonstartup}\YourApp"; Filename: "{app}\YourApp.exe"; Tasks: startupicon
```

### [Tasks] Section

```iss
[Tasks]
Name: "desktopicon"; Description: "Create desktop icon"; GroupDescription: "Additional icons:"
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Startup:"
```

## Run After Install

### [Run] Section

```iss
[Run]
; Launch application after install
Filename: "{app}\YourApp.exe"; Description: "Launch YourApp"; Flags: nowait postinstall skipifsilent

; Run with parameters
Filename: "{app}\setup.exe"; Parameters: "/configure"; StatusMsg: "Configuring..."

; Run only on first install
Filename: "{app}\init.exe"; Flags: runonce
```

## Uninstaller Configuration

### Delete User Data Option

```iss
[Code]
var
  DeleteUserDataCheckBox: TNewCheckBox;

procedure InitializeUninstallProgressForm();
begin
  DeleteUserDataCheckBox := TNewCheckBox.Create(UninstallProgressForm);
  DeleteUserDataCheckBox.Parent := UninstallProgressForm;
  DeleteUserDataCheckBox.Left := UninstallProgressForm.StatusLabel.Left;
  DeleteUserDataCheckBox.Top := UninstallProgressForm.StatusLabel.Top +
                               UninstallProgressForm.StatusLabel.Height + 16;
  DeleteUserDataCheckBox.Width := UninstallProgressForm.StatusLabel.Width;
  DeleteUserDataCheckBox.Caption := 'Delete user settings and data';
  DeleteUserDataCheckBox.Checked := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserDataPath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if (DeleteUserDataCheckBox <> nil) and DeleteUserDataCheckBox.Checked then
    begin
      UserDataPath := ExpandConstant('{userappdata}\YourApp');
      if DirExists(UserDataPath) then
        DelTree(UserDataPath, True, True, True);
    end;
  end;
end;
```

### [UninstallDelete] Section

```iss
[UninstallDelete]
; Delete specific files created by app
Type: files; Name: "{app}\*.log"
Type: files; Name: "{userappdata}\YourApp\cache\*"

; Delete directory if empty
Type: dirifempty; Name: "{app}\data"

; Delete files and directories
Type: filesandordirs; Name: "{app}\temp"
```

## Constants Reference

Common path constants:
- `{app}` - Application directory
- `{win}` - Windows directory
- `{sys}` - System32 directory
- `{autopf}` - Auto-selects Program Files (32-bit on x64)
- `{commondesktop}` - All Users desktop
- `{userdesktop}` - Current user desktop
- `{commonstartup}` - All Users startup
- `{group}` - Start Menu program group
- `{userappdata}` - User's AppData\Roaming
- `{localappdata}` - User's AppData\Local
- `{tmp}` - Temp directory for setup

## Common Patterns

### Version Detection

```iss
[Setup]
MinVersion=10.0.17763  ; Windows 10 1809 minimum
OnlyBelowVersion=0     ; No maximum version
```

### Mutex for Single Instance

```iss
[Setup]
AppMutex=YourAppMutex
SetupMutex=YourAppSetupMutex  ; Prevent multiple installers
```

### Custom Messages

```iss
[CustomMessages]
InstallingLabel=Installing YourApp...
LaunchProgram=Launch %1 after installation

[Messages]
SetupWindowTitle=Setup - YourApp
```

### Conditional Logic

```iss
[Code]
function Is64BitWindows: Boolean;
begin
  Result := Is64BitInstallMode or IsWin64;
end;

function ShouldInstallFeature: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\feature.dat'));
end;
```

## Common Pitfalls

### 1. Missing Equal Sign
❌ Wrong: `ArchitecturesAllowedx64compatible`
✅ Correct: `ArchitecturesAllowed=x64compatible`

### 2. Wildcards in {app}
**Never** use wildcards to delete all files in {app} during uninstall:
```iss
; DANGEROUS - DON'T DO THIS
Type: files; Name: "{app}\*"
```
Users may have important data there, or worse, selected wrong install directory.

### 3. Path Placeholders
Use constants instead of hardcoded paths:
❌ Wrong: `DefaultDirName=C:\Program Files\YourApp`
✅ Correct: `DefaultDirName={autopf}\YourApp`

### 4. Registry Cleanup
Be careful with registry deletion - only remove keys you created:
```iss
[Registry]
Root: HKCU; Subkey: "Software\YourCompany\YourApp"; Flags: uninsdeletekey
```

### 5. File Associations
Remember to notify Windows when changing associations:
```iss
[Setup]
ChangesAssociations=yes  ; Required when modifying file associations
```

### 6. Compression Settings
For large installers with many similar files:
```iss
Compression=lzma2/ultra64    ; Maximum compression
SolidCompression=yes          ; Better for similar files
LZMANumBlockThreads=4         ; Multi-threaded compression
```

## Template Placeholders

For CI/CD pipelines, use placeholders:
```iss
[Setup]
AppVersion={{APP_VERSION}}
OutputBaseFilename={{OUTPUT_NAME}}-{{ARCH}}
ArchitecturesAllowed={{ARCH_ALLOWED}}
ArchitecturesInstallIn64BitMode={{ARCH_64BIT_MODE}}

[Files]
Source: ".\publish\{{ARCH}}\*.exe"; DestDir: "{app}"
```

Replace in build script:
```powershell
$content = Get-Content "template.iss" -Raw
$content = $content -replace '{{APP_VERSION}}', '1.0.0'
$content = $content -replace '{{ARCH}}', 'x64'
```

## Testing

### Command Line Compilation
```bash
ISCC.exe script.iss
ISCC.exe /Q script.iss              # Quiet mode
ISCC.exe /O"C:\Output" script.iss   # Output directory
```

### Silent Installation
```bash
Setup.exe /SILENT                    # No wizard, shows progress
Setup.exe /VERYSILENT                # No UI at all
Setup.exe /VERYSILENT /NORESTART    # Don't restart
Setup.exe /DIR="C:\CustomPath"      # Custom install path
```

## Best Practices

1. **Always test uninstaller** - Verify it removes only what it should
2. **Use compression** - LZMA provides excellent compression
3. **Sign your installer** - Use SignTool directive for code signing
4. **Version everything** - Include version in installer filename
5. **Provide both architectures** - Create separate x86 and x64 installers
6. **Check prerequisites** - Verify .NET, VC++ runtime, etc.
7. **Use tasks for options** - Let users choose desktop icons, auto-start
8. **Keep user data** - Don't delete user settings without permission
9. **Test upgrades** - Ensure installer handles existing installations
10. **Use constants** - Never hardcode paths, use Inno constants

## Additional Resources

- Use `{#emit ...}` for preprocessor variables
- Check `[Code]` section for Pascal scripting capabilities
- Use `Check:` parameter for conditional installation
- Implement custom wizard pages for complex setups
- Use `BeforeInstall`/`AfterInstall` for custom actions
