[Setup]
AppName=VolumeKeeper
AppVersion=0.1
AppVerName=VolumeKeeper v0.1
AppPublisher=SecretX
DefaultDirName={autopf}\VolumeKeeper
DefaultGroupName=VolumeKeeper
OutputDir=..\VolumeKeeper\bin\Release\installer
OutputBaseFilename=VolumeKeeper-Setup-x64-dev
Compression=none
SolidCompression=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\docs\icons\app_icon.ico
UninstallDisplayIcon={app}\VolumeKeeper.exe
UninstallDisplayName=VolumeKeeper
LicenseFile=..\LICENSE

[Files]
Source: "..\VolumeKeeper\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs replacesameversion

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
