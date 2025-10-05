[Setup]
AppName=VolumeKeeper
AppVersion={{APP_VERSION}}
AppVerName=VolumeKeeper v{{APP_VERSION}}
AppPublisher=SecretX
DefaultDirName={autopf}\VolumeKeeper
DefaultGroupName=VolumeKeeper
OutputDir={{OUTPUT_DIR}}
OutputBaseFilename={{INSTALLER_BASE_FILENAME}}
Compression={{COMPRESSION_MODE}}
SolidCompression={{ENABLE_SOLID_COMPRESSION}}
PrivilegesRequired=admin
ArchitecturesAllowed={{ARCHITECTURES_ALLOWED}}
ArchitecturesInstallIn64BitMode={{ARCHITECTURES_INSTALL_IN_64BIT_MODE}}
SetupIconFile=.\docs\icons\app_icon.ico
UninstallDisplayIcon={app}\VolumeKeeper.exe
UninstallDisplayName=VolumeKeeper
LicenseFile=.\LICENSE

[Files]
Source: "{{SOURCE_DIR}}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\VolumeKeeper"; Filename: "{app}\VolumeKeeper.exe"
Name: "{commondesktop}\VolumeKeeper"; Filename: "{app}\VolumeKeeper.exe"; Tasks: desktopicon
Name: "{commonstartup}\VolumeKeeper"; Filename: "{app}\VolumeKeeper.exe"; Parameters: "--minimized"; Tasks: startupicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"
Name: "startupicon"; Description: "Start automatically with Windows"; GroupDescription: "Startup:"

[Run]
Filename: "{app}\VolumeKeeper.exe"; Description: "Launch VolumeKeeper"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Delete user configuration and settings?', mbConfirmation, MB_YESNO) = IDYES then
      DelTree(ExpandConstant('{userappdata}\VolumeKeeper'), True, True, True);
  end;
end;
