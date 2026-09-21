; Inno Setup Skript fuer Voice Push-to-Talk
; Erzeugt einen vollstaendig offline arbeitenden Installer:
; die .NET-Laufzeit ist im Programmordner enthalten, es wird nichts nachgeladen.

#define MyAppName "Voice Push-to-Talk"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "lokal"
#define MyAppExeName "VoicePTT.exe"

[Setup]
AppId={{7C3F1E52-9D4B-4B57-9E1E-3B7A9D2C4F10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\VoicePTT
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
OutputDir=..\dist
OutputBaseFilename=VoicePTT-Setup
SetupIconFile=..\src\VoicePTT\voiceptt.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
VersionInfoVersion=2.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
CloseApplications=no

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
de.AutostartTask=Automatisch mit Windows starten (empfohlen)
de.AdditionalTasks=Zusaetzliche Aufgaben:
de.RemoveUserData=Einstellungen und Protokoll ebenfalls entfernen?
de.OldVersionFound=Es wurden Reste einer aelteren Version von Voice Push-to-Talk (Python) gefunden.%n%nAutostart-Eintrag und Registrierung der alten Version jetzt entfernen? Die Einstellungen werden vorher uebernommen; ein eigener Programmordner der alten Version bleibt erhalten und kann spaeter von Hand geloescht werden.
en.AutostartTask=Start automatically with Windows (recommended)
en.AdditionalTasks=Additional tasks:
en.RemoveUserData=Remove settings and log file as well?
en.OldVersionFound=Leftovers of an older (Python based) Voice Push-to-Talk version were found.%n%nRemove the old autostart entry and registration now? Settings are migrated first; an old program folder is left in place and can be deleted manually.

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:AdditionalTasks}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalTasks}"; Flags: unchecked

[Files]
Source: "..\build\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "ANLEITUNG.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName} - Anleitung"; Filename: "{app}\ANLEITUNG.txt"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VoicePTT"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "VoicePTT"; Flags: deletevalue uninsdeletevalue; Tasks: not autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--quit"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "QuitVoicePTT"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
{ Laufende Instanz beenden, damit die Dateien ersetzt werden koennen. }
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  if FileExists(ExpandConstant('{app}\{#MyAppExeName}')) then
    Exec(ExpandConstant('{app}\{#MyAppExeName}'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

{ config.json der Python-Version suchen: zuerst ueber den Pfad im alten
  Autostart-Skript, danach an den ueblichen Ablageorten. }
function FindOldConfig(): String;
var
  Vbs, S, Dir: String;
  Raw: AnsiString;
  Candidates: array[0..3] of String;
  P, I: Integer;
begin
  Result := '';

  Vbs := ExpandConstant('{userstartup}\voice-ptt.vbs');
  if FileExists(Vbs) and LoadStringFromFile(Vbs, Raw) then
  begin
    S := String(Raw);
    P := Pos('client.py', S);
    if P > 0 then
    begin
      S := Copy(S, 1, P - 1);
      Dir := '';
      for I := Length(S) downto 1 do
        if S[I] = '"' then
        begin
          Dir := Copy(S, I + 1, Length(S) - I);
          Break;
        end;
      if (Dir <> '') and FileExists(Dir + 'config.json') then
      begin
        Result := Dir + 'config.json';
        Exit;
      end;
    end;
  end;

  Candidates[0] := ExpandConstant('{localappdata}\VoicePTT\config.json');
  Candidates[1] := ExpandConstant('{userprofile}\voice-ptt\config.json');
  Candidates[2] := 'C:\voice-ptt\config.json';
  Candidates[3] := ExpandConstant('{userdocs}\voice-ptt\config.json');
  for I := 0 to 3 do
    if FileExists(Candidates[I]) then
    begin
      Result := Candidates[I];
      Exit;
    end;
end;

{ Konfiguration der Python-Version uebernehmen. }
procedure MigrateOldConfig();
var
  OldCfg, NewDir, NewCfg: String;
begin
  NewDir := ExpandConstant('{userappdata}\VoicePTT');
  NewCfg := NewDir + '\config.json';
  if FileExists(NewCfg) then
    Exit;

  OldCfg := FindOldConfig();
  if OldCfg = '' then
    Exit;

  ForceDirectories(NewDir);
  if FileCopy(OldCfg, NewCfg, True) then
    Log('Alte Konfiguration uebernommen: ' + OldCfg);
end;

{ Reste der Python-Version entfernen - nur nach Rueckfrage und nur bekannte Dateien. }
procedure RemoveOldPythonInstall();
var
  OldDir, Vbs, Lower: String;
  Content: AnsiString;
  HasVenv, HasVbs: Boolean;
  I: Integer;
  Names: array[0..7] of String;
begin
  OldDir := ExpandConstant('{localappdata}\VoicePTT');
  Vbs := ExpandConstant('{userstartup}\voice-ptt.vbs');

  HasVenv := DirExists(OldDir + '\venv');
  HasVbs := False;
  if FileExists(Vbs) then
    if LoadStringFromFile(Vbs, Content) then
    begin
      Lower := Lowercase(String(Content));
      HasVbs := (Pos('voice-ptt', Lower) > 0) or (Pos('voiceptt', Lower) > 0) or (Pos('client.py', Lower) > 0);
    end;

  if not (HasVenv or HasVbs) then
    Exit;

  if not WizardSilent() then
    if MsgBox(ExpandConstant('{cm:OldVersionFound}'), mbConfirmation, MB_YESNO) <> IDYES then
      Exit;

  if HasVbs then
    DeleteFile(Vbs);

  RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\VoicePTT');

  if HasVenv then
    DelTree(OldDir + '\venv', True, True, True);

  Names[0] := 'client.py';
  Names[1] := 'list_mics.py';
  Names[2] := 'requirements.txt';
  Names[3] := 'run.bat';
  Names[4] := 'mikrofone.bat';
  Names[5] := 'start-hidden.vbs';
  Names[6] := 'uninstall.bat';
  Names[7] := 'ANLEITUNG.txt';
  for I := 0 to 7 do
    if FileExists(OldDir + '\' + Names[I]) then
      DeleteFile(OldDir + '\' + Names[I]);

  { config.json bleibt liegen, falls die Uebernahme spaeter noch gebraucht wird. }
  Log('Python-Version entfernt: ' + OldDir);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopRunningApp();
  if CurStep = ssPostInstall then
  begin
    MigrateOldConfig();
    RemoveOldPythonInstall();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if not UninstallSilent() then
      if MsgBox(ExpandConstant('{cm:RemoveUserData}'), mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(ExpandConstant('{userappdata}\VoicePTT'), True, True, True);
        DelTree(ExpandConstant('{localappdata}\VoicePTT'), True, True, True);
      end;
  end;
end;
