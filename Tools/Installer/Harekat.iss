; HAREKÂT Windows kurulum betiği (Inno Setup 6+)
; Kaynak: Builds/Windows (Unity Player build çıktısı)
; Derleme: ISCC.exe Harekat.iss  veya  .\build.ps1

#define MyAppName "HAREKÂT"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "HAREKÂT Studios"
#define MyAppURL "https://harekat.example"
#define MyAppExeName "HAREKAT.exe"
#define MyLauncherExeName "Harekat.Launcher.exe"
#ifndef SourceDir
  #define SourceDir "..\..\Builds\Windows"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\Builds\Installer"
#endif

[Setup]
AppId={{A7C3E91B-4F2D-4B8A-9E6C-1D0F8A5B3C27}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=
OutputDir={#OutputDir}
OutputBaseFilename=HarekatSetup-{#MyAppVersion}
SetupIconFile=
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no
; Kod imzalama: SignTool satırını CodeSigning.md içindeki örnekle açın.
; SignTool=signtool

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Unity Windows player build (Builds/Windows)
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Launcher (Tools/Launcher publish çıktısı — yoksa atlanır)
Source: "{#SourceDir}\Launcher\{#MyLauncherExeName}"; DestDir: "{app}\Launcher"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SourceDir}\Launcher\*"; DestDir: "{app}\Launcher"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\Launcher\{#MyLauncherExeName}"; WorkingDir: "{app}"; Check: LauncherExists
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Check: not LauncherExists
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Launcher\{#MyLauncherExeName}"; WorkingDir: "{app}"; Tasks: desktopicon; Check: LauncherExists
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon; Check: not LauncherExists

[Run]
Filename: "{app}\Launcher\{#MyLauncherExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent; Check: LauncherExists
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent; Check: not LauncherExists

[Code]
const
  VC_REDIST_KEY_X64 = 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64';

function LauncherExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\Launcher\{#MyLauncherExeName}'));
end;

function IsVCRedistInstalled: Boolean;
var
  Installed: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, VC_REDIST_KEY_X64, 'Installed', Installed) then
    Result := Installed = 1;
  if not Result then
  begin
    if RegQueryDWordValue(HKLM64, VC_REDIST_KEY_X64, 'Installed', Installed) then
      Result := Installed = 1;
  end;
end;

function InitializeSetup: Boolean;
var
  MsgTr, MsgEn: string;
begin
  Result := True;
  if not IsVCRedistInstalled then
  begin
    MsgTr := 'Microsoft Visual C++ 2015-2022 x64 Yeniden Dağıtılabilir paketi bulunamadı.' + #13#10 +
             'Kuruluma devam etmeden önce https://aka.ms/vs/17/release/vc_redist.x64.exe adresinden yükleyin.';
    MsgEn := 'Microsoft Visual C++ 2015-2022 x64 Redistributable was not found.' + #13#10 +
             'Install it from https://aka.ms/vs/17/release/vc_redist.x64.exe before continuing.';
    if ActiveLanguage = 'turkish' then
      MsgBox(MsgTr, mbError, MB_OK)
    else
      MsgBox(MsgEn, mbError, MB_OK);
    Result := False;
  end;
end;
