; ============================================================================
;  ShareDeal Attend - Inno Setup installer script
;  Build with: "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
;  (Download Inno Setup 6 from https://jrsoftware.org/isdl.php)
;
;  Before compiling, publish the app first (see README.md):
;     dotnet publish ...  ->  produces ShareDealAttend.exe
;  The single published EXE is expected at:
;     ..\ShareDealAttend\bin\Release\net8.0-windows\win-x64\publish\ShareDealAttend.exe
; ============================================================================

#define MyAppName        "ShareDeal Attend"
#define MyAppVersion     "1.0.0"
#define MyAppPublisher   "ShareDeal"
#define MyAppExeName     "ShareDealAttend.exe"
#define MyAppId          "{{8F3C2A14-9B6D-4E15-AA72-2C9E7F4B10D9}"
#define PublishDir       "..\ShareDealAttend\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=Output
OutputBaseFilename=ShareDealAttend-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Install into Program Files -> requires admin once at install time.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; Single self-contained EXE (no .NET runtime needed on the target machine).
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; appsettings.json is embedded next to the exe so the URL stays configurable.
Source: "{#PublishDir}\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Register the app to start with Windows for ALL users (machine-wide).
; The app ALSO self-registers under HKCU on first run, so per-user startup
; works even if this machine-wide key is later cleared.
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "ShareDeal Attend"; \
    ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue

[Run]
; Launch immediately after installation (silently, no extra prompt).
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up per-user runtime data created by the app at uninstall time.
Type: filesandordirs; Name: "{localappdata}\ShareDeal Attend"

[UninstallRun]
; Remove the HKCU startup value the app may have written for the current user.
Filename: "{cmd}"; Parameters: "/c reg delete ""HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v ""ShareDeal Attend"" /f"; \
    Flags: runhidden; RunOnceId: "DelHkcuRun"
