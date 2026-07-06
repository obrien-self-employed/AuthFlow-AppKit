; ============================================================================
; AuthFlowApp1 - Windows Service installer (Inno Setup)
; ============================================================================
; Adapted from the FneManuelInvoiceApp pilot installer (see
; Brackford-0brien/FneManuelInvoiceApp/installer/setup.iss and INSTALLER.md
; for the full rationale). Installs the self-contained, single-file,
; published Kestrel build of this app as a genuine Windows Service.
;
; IMPORTANT: the AppId GUID below is regenerated automatically by
; `dotnet new` when this template is instantiated (see .template.config/template.json,
; symbol "installerGuid") - do NOT reuse the same GUID across different apps,
; and do NOT change it again once an app has shipped a first release (Inno
; Setup uses it to detect "this is an upgrade", not a new product).
; ----------------------------------------------------------------------------

#ifndef PublishDir
  #define PublishDir "..\bin\Release\net10.0\win-x64\publish"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppExeName "AuthFlowApp1.exe"
#define DefaultServiceName "AuthFlowApp1"
#define DefaultPort "5200"

[Setup]
AppId={{00000000-0000-0000-0000-000000000000}
AppName=AuthFlowApp1
AppVersion={#AppVersion}
AppPublisher=Brackford-0brien
DefaultDirName={autopf}\AuthFlowApp1
DefaultGroupName=AuthFlowApp1
DisableProgramGroupPage=yes
OutputDir=.\dist
OutputBaseFilename=AuthFlowApp1-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
DisableWelcomePage=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\AuthFlowApp1"; Filename: "http://localhost:{code:GetPort}/"
Name: "{group}\Désinstaller AuthFlowApp1"; Filename: "{uninstallexe}"

[Registry]
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\{code:GetServiceName}"; ValueType: multisz; ValueName: "Environment"; ValueData: "ASPNETCORE_ENVIRONMENT=Production"; Flags: uninsdeletevalue

[Code]
var
  InstancePage: TInputQueryWizardPage;
  ServiceName, Port, DbConnString: string;

procedure InitializeWizard;
begin
  InstancePage := CreateInputQueryPage(wpSelectDir,
    'Configuration de l''instance', 'Une machine peut héberger plusieurs instances de AuthFlowApp1',
    'Chaque instance a son propre nom de service Windows, son propre port d''écoute, ' +
    'et sa propre licence AuthFlow (importée après l''installation via /license-activation). ' +
    'Utilisez un nom différent par client/site si vous installez plusieurs instances sur cette machine.');
  InstancePage.Add('Nom du site / de l''instance (utilisé pour le nom du service Windows) :', False);
  InstancePage.Add('Port d''écoute HTTP :', False);
  InstancePage.Add('Chaîne de connexion SQL Server (optionnel, laissez vide si non utilisé) :', False);

  InstancePage.Values[0] := ExpandConstant('{#DefaultServiceName}');
  InstancePage.Values[1] := ExpandConstant('{#DefaultPort}');
  InstancePage.Values[2] := '';

  if ExpandConstant('{param:INSTANCENAME|}') <> '' then
    InstancePage.Values[0] := ExpandConstant('{param:INSTANCENAME}');
  if ExpandConstant('{param:PORT|}') <> '' then
    InstancePage.Values[1] := ExpandConstant('{param:PORT}');
  if ExpandConstant('{param:DBCONNSTRING|}') <> '' then
    InstancePage.Values[2] := ExpandConstant('{param:DBCONNSTRING}');
end;

procedure CurPageChanged(CurPageID: Integer);
var
  ExistingName: AnsiString;
begin
  if (CurPageID = InstancePage.ID) and FileExists(ExpandConstant('{app}\.service-name')) then
  begin
    if LoadStringFromFile(ExpandConstant('{app}\.service-name'), ExistingName) then
      InstancePage.Values[0] := Trim(String(ExistingName));
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = InstancePage.ID then
  begin
    if Trim(InstancePage.Values[0]) = '' then
    begin
      MsgBox('Merci de saisir un nom d''instance.', mbError, MB_OK);
      Result := False;
      exit;
    end;
    if (Trim(InstancePage.Values[1]) = '') or (StrToIntDef(InstancePage.Values[1], -1) <= 0) then
    begin
      MsgBox('Merci de saisir un port valide.', mbError, MB_OK);
      Result := False;
      exit;
    end;
    ServiceName := Trim(InstancePage.Values[0]);
    Port := Trim(InstancePage.Values[1]);
    DbConnString := Trim(InstancePage.Values[2]);
  end;
end;

function GetPort(Param: string): string;
begin
  Result := Port;
end;

function GetServiceName(Param: string): string;
begin
  Result := ServiceName;
end;

function RunSc(const Params: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    and (ResultCode = 0);
end;

function ServiceExists(const Name: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), 'query "' + Name + '"', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure WriteInstanceAppSettings;
var
  Json: string;
  DbLine: string;
begin
  if DbConnString <> '' then
    DbLine := '"ConnectionStrings": { "SqlServer": "' + DbConnString + '" },' + #13#10
  else
    DbLine := '';

  Json := '{' + #13#10 +
    '  ' + DbLine +
    '  "Kestrel": { "Endpoints": { "Http": { "Url": "http://localhost:' + Port + '" } } },' + #13#10 +
    '  "WindowsService": { "ServiceName": "' + ServiceName + '" }' + #13#10 +
    '}' + #13#10;

  SaveStringToFile(ExpandConstant('{app}\appsettings.Production.json'), Json, False);
  SaveStringToFile(ExpandConstant('{app}\.service-name'), ServiceName, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ExePath, BinPath: string;
begin
  ServiceName := Trim(InstancePage.Values[0]);
  Port := Trim(InstancePage.Values[1]);
  DbConnString := Trim(InstancePage.Values[2]);
  if ServiceName = '' then
    ServiceName := ExpandConstant('{#DefaultServiceName}');
  if Port = '' then
    Port := ExpandConstant('{#DefaultPort}');

  if CurStep = ssInstall then
  begin
    if ServiceExists(ServiceName) then
    begin
      RunSc('stop "' + ServiceName + '"');
      Sleep(2000);
    end;
  end;

  if CurStep = ssPostInstall then
  begin
    ExePath := ExpandConstant('{app}\{#AppExeName}');
    WriteInstanceAppSettings;

    if ServiceExists(ServiceName) then
    begin
      RunSc('config "' + ServiceName + '" binPath= "' + ExePath + '"');
    end
    else
    begin
      BinPath := 'binPath= "' + ExePath + '" start= auto';
      RunSc('create "' + ServiceName + '" ' + BinPath);
      RunSc('description "' + ServiceName + '" "AuthFlowApp1 (voir README.md / INSTALLER.md)"');
    end;

    RunSc('start "' + ServiceName + '"');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RawName: AnsiString;
  Name: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    if not LoadStringFromFile(ExpandConstant('{app}\.service-name'), RawName) or (Trim(String(RawName)) = '') then
      Name := ExpandConstant('{#DefaultServiceName}')
    else
      Name := Trim(String(RawName));

    if ServiceExists(Name) then
    begin
      RunSc('stop "' + Name + '"');
      Sleep(2000);
      RunSc('delete "' + Name + '"');
    end;
  end;
end;
