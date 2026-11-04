; Installer of GazeBar (Setup.exe), made with Inno Setup 6 (https://jrsoftware.org/isinfo.php).
;
; Installs the "full installation" build (with UI access, see Platform/UiAccess.cs) in
; C:\Program Files\GazeBar, signs GazeBar.exe with a certificate made on the user's computer,
; adds GazeBar to the Start menu and to "Settings > Apps" of Windows (to uninstall it).
; The settings (%APPDATA%\GazeBar) and the log (%LOCALAPPDATA%\GazeBar) are never touched.
;
; Compiled by .github/scripts/make-setup.ps1:
;   ISCC.exe /DAppVersion=0.1.0 /DProgramFolder=<UI access build> /DOutputFolder=<folder> GazeBar.iss
;
; Saved in UTF-8 WITH a BOM: Inno Setup then reads the accented French letters correctly.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
; The folder of the build made with "dotnet build -p:UiAccess=true".
#ifndef ProgramFolder
  #define ProgramFolder "..\bin\uiaccess"
#endif
#ifndef OutputFolder
  #define OutputFolder "..\bin\setup"
#endif

[Setup]
; Identifies GazeBar in Windows: never change it, or an update would be a second program.
AppId={{34565D2D-8A8C-48F9-870B-3E9EDE72F182}
AppName=GazeBar
AppVersion={#AppVersion}
AppVerName=GazeBar {#AppVersion}
AppPublisher=GazeBar
AppPublisherURL=https://gazebar.kopol.xyz
AppSupportURL=https://github.com/Kopolitoo/GazeBar/issues
VersionInfoVersion={#AppVersion}
; Administrator: Program Files is a protected folder, and Windows gives UI access only to a
; program installed there. 64-bit Windows: "C:\Program Files", the folder of the old Install.cmd
; script, so that the installer also updates a copy installed by it.
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
DefaultDirName={autopf}\GazeBar
; As few pages as possible (each click is an effort by gaze): no folder and no Start menu
; folder to choose; the language is the one of Windows (French or English).
DisableDirPage=yes
DisableProgramGroupPage=yes
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage
; GazeBar is closed by the [Code] below, without asking.
CloseApplications=no
WizardStyle=modern
SetupIconFile=..\Resources\GazeBar.ico
UninstallDisplayIcon={app}\GazeBar.exe
UninstallDisplayName=GazeBar
OutputDir={#OutputFolder}
OutputBaseFilename=GazeBar-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
en.SignFailed=GazeBar could not be signed, so Windows will not start it. Please run this installation again.%n%nReason: %1
fr.SignFailed=GazeBar n'a pas pu être signé : Windows ne le démarrera pas. Merci de relancer cette installation.%n%nRaison : %1
en.RemoveFailed=The GazeBar certificate could not be removed.%n%nReason: %1
fr.RemoveFailed=Le certificat de GazeBar n'a pas pu être retiré.%n%nRaison : %1
en.UnknownReason=unknown (exit code %1)
fr.UnknownReason=inconnue (code de sortie %1)

[InstallDelete]
; Data folders of the program: emptied before the copy, so that no old file stays (the
; personal dictionary is in %APPDATA%, not here).
Type: filesandordirs; Name: "{app}\Languages"
Type: filesandordirs; Name: "{app}\Keyboards"
Type: filesandordirs; Name: "{app}\Dictionaries"
; Left by older versions of this installer (its content is now in setup-certificate.ps1).
Type: files; Name: "{app}\Setup\common.ps1"

[Files]
; The certificate script first: it must be there when the signature step runs.
Source: "setup-certificate.ps1"; DestDir: "{app}\Setup"; Flags: ignoreversion
; The program and its data files. Never the debug files, the eyetracker DLLs (they belong to
; Tobii AB) or a local "Eyetracker" folder.
Source: "{#ProgramFolder}\*"; DestDir: "{app}"; Excludes: "*.pdb,Tobii*.dll,\Eyetracker"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.fr.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.fr.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; For all the users of the computer (same shortcut as the old Install.cmd script).
Name: "{autoprograms}\GazeBar"; Filename: "{app}\GazeBar.exe"; WorkingDir: "{app}"

[Run]
; Last page: "Launch GazeBar" (ticked). GazeBar starts as the normal user, not as administrator.
; "shellexec": started like a double-click (ShellExecute). A program with UI access cannot be
; started by CreateProcess: Windows answers "error 740, the operation requires elevation".
Filename: "{app}\GazeBar.exe"; Description: "{cm:LaunchProgram,GazeBar}"; Flags: nowait postinstall runasoriginaluser shellexec skipifsilent

[UninstallDelete]
; Everything left in the folder (for example an "Eyetracker" folder of DLLs
; copied by the user). The folder is always Program Files\GazeBar (no folder page).
Type: filesandordirs; Name: "{app}"

[Code]
// Closes every running GazeBar: the files of a running program cannot be replaced.
procedure CloseGazeBar;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM GazeBar.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // taskkill returns 0 when GazeBar was running and is now closed.
  if ResultCode = 0 then
    Sleep(2000);
end;

// Runs setup-certificate.ps1 (Step "Sign" or "Remove"). Returns True when it worked;
// otherwise Reason says why.
function RunCertificateStep(const Step: String; var Reason: String): Boolean;
var
  ErrorFile, Parameters, PowerShell: String;
  Lines: TArrayOfString;
  ResultCode, I: Integer;
begin
  ErrorFile := AddBackslash(GetTempDir) + 'GazeBar-setup-error.txt';
  DeleteFile(ErrorFile);
  Parameters := '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\Setup\setup-certificate.ps1') + '"'
    + ' -Step ' + Step
    + ' -Folder "' + ExpandConstant('{app}') + '"'
    + ' -ErrorFile "' + ErrorFile + '"';
  // The 64-bit Windows PowerShell on 64-bit Windows ({sysnative}: the installer is 32-bit).
  if IsWin64 then
    PowerShell := ExpandConstant('{sysnative}\WindowsPowerShell\v1.0\powershell.exe')
  else
    PowerShell := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Result := Exec(PowerShell, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
  if not Result then
  begin
    Reason := '';
    if LoadStringsFromFile(ErrorFile, Lines) then
      for I := 0 to GetArrayLength(Lines) - 1 do
        Reason := Reason + Lines[I] + ' ';
    if Trim(Reason) = '' then
      Reason := FmtMessage(CustomMessage('UnknownReason'), [IntToStr(ResultCode)]);
  end;
  DeleteFile(ErrorFile);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseGazeBar;
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Reason: String;
begin
  // After the copy: sign GazeBar.exe (before it is started from the last page).
  if CurStep = ssPostInstall then
    if not RunCertificateStep('Sign', Reason) then
      SuppressibleMsgBox(FmtMessage(CustomMessage('SignFailed'), [Reason]), mbError, MB_OK, IDOK);
end;

function InitializeUninstall: Boolean;
begin
  CloseGazeBar;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Reason: String;
begin
  // Before the files are removed (the scripts are still there).
  if CurUninstallStep = usUninstall then
    if not RunCertificateStep('Remove', Reason) then
      SuppressibleMsgBox(FmtMessage(CustomMessage('RemoveFailed'), [Reason]), mbError, MB_OK, IDOK);
end;
