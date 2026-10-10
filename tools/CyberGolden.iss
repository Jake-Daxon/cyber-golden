#define AppVersion "0.4.1-preview"
#define AppName "赛博金毛"
[Setup]
AppId={{86774E89-51C7-4E0B-B1CE-60297E5E4403}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=CyberGolden contributors
DefaultDirName={localappdata}\Programs\CyberGolden
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=CyberGolden-Setup-{#AppVersion}-x64
SetupIconFile=..\assets\pet.ico
UninstallDisplayIcon={app}\CyberGolden.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
LicenseFile=..\docs\LICENSE-VPet.txt
CloseApplications=yes
RestartApplications=no
[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
[Files]
Source: "..\dist\CyberGolden-0.4.1-preview-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\CyberGolden.exe"
Name: "{group}\卸载{#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\CyberGolden.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\CyberGolden.exe"; Description: "启动{#AppName}"; Flags: nowait postinstall skipifsilent
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "CyberGolden"; Flags: uninsdeletevalue
[Code]
var ClearData: Boolean;
procedure InitializeUninstallProgressForm();
begin
  ClearData := False;
  // Silent uninstall and default UI choice retain all data.
  if not UninstallSilent then
    ClearData := MsgBox('是否同时清除赛博金毛的名称、设置和养成存档？' + #13#10 + '选择“否”将保留存档（默认推荐）。', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and ClearData then
    DelTree(ExpandConstant('{localappdata}\CyberGolden'), True, True, True);
end;
