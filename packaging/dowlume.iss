#ifndef PublishDirectory
  #error PublishDirectory must point to the self-contained win-x64 publish output.
#endif
#ifndef OutputDirectory
  #define OutputDirectory "..\artifacts"
#endif

[Setup]
AppId={{A65DE461-2F0E-47BC-8352-3F980674F3DB}
AppName=Dowlume
AppVersion=0.1.0
AppPublisher=Dowlume contributors
AppPublisherURL=https://github.com/marcosgonzalezhidalgo/dowlume
DefaultDirName={localappdata}\Programs\Dowlume
DefaultGroupName=Dowlume
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDirectory}
OutputBaseFilename=Dowlume-0.1.0-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Dowlume.App.exe
CloseApplications=yes
RestartApplications=no
LicenseFile=..\LICENSE
SetupIconFile=..\Dowlume\Dowlume.App\Assets\dowlume.ico

[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Dowlume"; Filename: "{app}\Dowlume.App.exe"

[Run]
Filename: "{app}\Dowlume.App.exe"; Description: "Open Dowlume"; Flags: nowait postinstall skipifsilent unchecked

; Deliberately retain the local database, diagnostics, and all user files on uninstall.
