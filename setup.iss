; Este valor será sobrescrito automaticamente pelo GitHub Actions
#define MyAppVersion "1.0.0" 
#define MyAppName "Sistema Juridico Pro"
#define MyAppPublisher "Setor Juridico"
#define MyAppExeName "SistemaJuridico.exe"

[Setup]
AppId={{C8686620-1383-4318-97C3-890251148899}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Instala na pasta do usuário (Não pede senha de Admin)
DefaultDirName={userappdata}\{#MyAppName}
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=Instalador_SistemaJuridico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Pega o executável gerado
Source: "ReleaseOutput\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Pega DLLs e arquivos de configuração (JSON, etc)
Source: "ReleaseOutput\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
