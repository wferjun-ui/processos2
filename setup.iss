#define MyAppName "Sistema Juridico Pro"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Setor Juridico"
#define MyAppExeName "SistemaJuridico.exe"

[Setup]
; AppId único para identificar a instalação
AppId={{C8686620-1383-4318-97C3-890251148899}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Instala na AppData do usuário (Não pede Admin)
DefaultDirName={userappdata}\{#MyAppName}
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
; Pasta onde o instalador final será salvo
OutputDir=Output
OutputBaseFilename=Instalador_SistemaJuridico_v2
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Pega o EXE gerado pelo dotnet publish na pasta ReleaseOutput
Source: "ReleaseOutput\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Copia quaisquer DLLs ou configs extras
Source: "ReleaseOutput\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
