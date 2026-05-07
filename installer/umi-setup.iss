; Universal Media Import (UMI) - Inno Setup Script
; Version: 2.1.0
; Requires: Inno Setup 6

#define MyAppName "Universal Media Import"
; MyAppVersion wird von build.ps1 via /DMyAppVersion=X.Y.Z übergeben.
; Fallback für direkten Doppelklick auf die .iss-Datei in der Inno-Setup-IDE:
#ifndef MyAppVersion
#define MyAppVersion "2.1.0"
#endif
#define MyAppPublisher "Dirk Schelhasse"
#define MyAppURL "https://github.com/dm7ds/Universal-Media-Import-v2"
#define MyAppExeName "umi.exe"

[Setup]
AppId={{A3E2F5C8-1B47-4D9E-8F3A-2C6D0E7B4A91}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\UMI
DefaultGroupName=UMI
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=UMI_Setup_{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
ShowLanguageDialog=yes
SetupIconFile=assets\umi-icon.ico
; Wizard bitmaps regenerated from umi-icon.ico via PowerShell + System.Drawing
; (white background, centered logo). Native Inno-Setup-Slot sizes 164x314 / 55x58.
; Re-run the regeneration from scripts/regenerate-wizard-bitmaps.ps1 if the
; icon changes.
WizardImageFile=assets\umi-wizard.bmp
WizardSmallImageFile=assets\umi-wizard-small.bmp
; Icon for the Apps & Features uninstall entry + readable display name
UninstallDisplayIcon={app}\umi-icon.ico
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german";  MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
; Component descriptions
english.CompCli=UMI Command-Line Interface (umi.exe)
german.CompCli=UMI Kommandozeile (umi.exe)
english.CompGui=UMI GUI (umi-gui.exe)
german.CompGui=UMI Benutzeroberfläche (umi-gui.exe)

; Installation type descriptions
english.TypeFull=Full installation
german.TypeFull=Vollständige Installation
english.TypeCustom=Custom installation
german.TypeCustom=Benutzerdefinierte Installation

; Desktop shortcut task
english.CreateDesktopIcon=Create a &desktop shortcut
german.CreateDesktopIcon=&Desktop-Verknüpfung erstellen
english.AdditionalIcons=Additional icons:
german.AdditionalIcons=Zusätzliche Symbole:

; "Launch UMI" checkbox shown on the Finished page (postinstall Run entry)
english.LaunchUMI=Launch &UMI
german.LaunchUMI=&UMI jetzt starten

; Uninstall: ask whether to keep or wipe the user's configuration
english.UninstallConfigPromptTitle=Remove configuration?
german.UninstallConfigPromptTitle=Konfiguration entfernen?
english.UninstallConfigPromptMsg=Do you also want to remove your UMI configuration?%n(Cameras, SD cards, profiles, tool paths)%n%nIf you choose No, your settings stay and will be picked up by a future install.
german.UninstallConfigPromptMsg=Möchtest du auch deine UMI-Konfiguration entfernen?%n(Kameras, SD-Karten, Profile, Tool-Pfade)%n%nWenn nein, bleiben deine Einstellungen erhalten und werden bei einer erneuten Installation wiederverwendet.

; .NET 8 Runtime silent-install messages
english.DotNet8Downloading=Downloading .NET 8 Runtime, please wait...
german.DotNet8Downloading=Lade .NET 8 Runtime herunter, bitte warten...
english.DotNet8Installing=Installing .NET 8 Runtime silently...
german.DotNet8Installing=Installiere .NET 8 Runtime...
english.DotNet8DownloadFailed=Failed to download .NET 8 Runtime (exit code: %1).%n%nPlease install it manually:%nhttps://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe
german.DotNet8DownloadFailed=Download der .NET 8 Runtime fehlgeschlagen (Exit-Code: %1).%n%nBitte manuell installieren:%nhttps://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe
english.DotNet8InstallFailed=.NET 8 Runtime installation failed (exit code: %1).%n%nPlease install it manually:%nhttps://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe
german.DotNet8InstallFailed=Installation der .NET 8 Runtime fehlgeschlagen (Exit-Code: %1).%n%nBitte manuell installieren:%nhttps://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe

[Types]
Name: "full";   Description: "{cm:TypeFull}"
Name: "custom"; Description: "{cm:TypeCustom}"; Flags: iscustom

[Components]
Name: "cli"; Description: "{cm:CompCli}"; Types: full custom
Name: "gui"; Description: "{cm:CompGui}"; Types: full custom

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; Components: gui

[Files]
Source: "..\publish\umi.exe";                    DestDir: "{app}";                  Flags: ignoreversion;                           Excludes: "*.pdb"; Components: cli
Source: "..\publish\umi-gui.exe";                DestDir: "{app}";                  Flags: ignoreversion;                           Excludes: "*.pdb"; Components: gui
; Templates-only config — staged by build.ps1 from `git ls-files config/`,
; so user files (config.json, *.bak, custom gyroflow presets) never ship.
Source: "..\publish\config-clean\*";             DestDir: "{app}\config";           Flags: recursesubdirs createallsubdirs;          Components: cli gui
Source: "..\publish\tools\exiftool\*";           DestDir: "{app}\tools\exiftool";   Flags: recursesubdirs createallsubdirs;          Components: cli gui
Source: "..\publish\tools\gyroflow\*";           DestDir: "{app}\tools\gyroflow";   Flags: recursesubdirs createallsubdirs skipifsourcedoesntexist; Components: gui
Source: "..\publish\README.md";                  DestDir: "{app}";                                                                   Components: cli gui
Source: "..\publish\LICENSE";                    DestDir: "{app}";                                                                   Components: cli gui
Source: "..\publish\THIRD_PARTY_LICENSES.txt";   DestDir: "{app}";                                                                   Components: cli gui
; Icon for the Apps & Features uninstall entry (referenced via UninstallDisplayIcon)
Source: "assets\umi-icon.ico";                   DestDir: "{app}";                  Flags: ignoreversion;                           Components: cli gui

[Icons]
Name: "{group}\UMI";                Filename: "{app}\umi-gui.exe";  Components: gui
Name: "{commondesktop}\UMI";        Filename: "{app}\umi-gui.exe";  Components: gui; Tasks: desktopicon
Name: "{group}\UMI Dokumentation";  Filename: "{app}\README.md"
; "Uninstall" entry intentionally omitted — Apps & Features is the standard
; Windows path. The legacy start-menu shortcut surfaced "unins000.exe" via
; the UAC prompt, which looked unprofessional.

[Run]
; Optional checkbox on the Finished page — pre-ticked, user can untick.
; runascurrentuser: launch as the logged-in user, NOT elevated. Setup itself
;   runs as admin (PrivilegesRequired=admin); without this flag the GUI would
;   inherit those credentials, which breaks WPF drag-drop and confuses
;   per-user paths the app uses.
; nowait: don't block the installer.
; postinstall: render the entry as a checkbox on the Finished page.
; skipifsilent: do not auto-launch during /SILENT installs.
Filename: "{app}\umi-gui.exe"; Description: "{cm:LaunchUMI}"; Flags: nowait postinstall skipifsilent runascurrentuser; Components: gui

[Registry]
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Check: NeedsAddPath(ExpandConstant('{app}'))

[Code]

function HasDotNet8At(RootKey: Integer; SubKey: string): Boolean;
var
  Names: TArrayOfString;
  i: Integer;
begin
  Result := False;
  if RegGetValueNames(RootKey, SubKey, Names) then
  begin
    for i := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[i], 1, 2) = '8.' then
      begin
        Result := True;
        Exit;
      end;
  end;
end;

// .NET writes its sharedfx subkeys into the 64-bit native view on some installs
// and into WOW6432Node on others — depends on which dotnet-runtime msi was used.
// The previous check read sharedhost.Version, which only reports the NEWEST host
// (e.g. "10.0.7" when .NET 10 SDK is also installed) and never matched 8.x.
// We now iterate Microsoft.WindowsDesktop.App (required for the GUI) and
// Microsoft.NETCore.App (required for the CLI) in both views.
function IsDotNet8Installed(): Boolean;
var
  KeyDesktop, KeyNetCore: string;
begin
  KeyDesktop := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  KeyNetCore := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App';
  Result := HasDotNet8At(HKLM64, KeyDesktop) or
            HasDotNet8At(HKLM32, KeyDesktop) or
            HasDotNet8At(HKLM64, KeyNetCore) or
            HasDotNet8At(HKLM32, KeyNetCore);
end;

function InstallDotNet8(): Boolean;
var
  DotNetExe: string;
  ResultCode: Integer;
  DownloadArgs: string;
  InstallArgs: string;
begin
  Result := False;
  DotNetExe := ExpandConstant('{tmp}\dotnet-runtime-8-win-x64.exe');

  // Step 1: Download via PowerShell (no external plugin dependency)
  WizardForm.StatusLabel.Caption := CustomMessage('DotNet8Downloading');
  DownloadArgs := '-NoProfile -NonInteractive -Command ' +
    '"Invoke-WebRequest -Uri https://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe' +
    ' -OutFile ''' + DotNetExe + ''' -UseBasicParsing"';
  if not Exec('powershell.exe', DownloadArgs, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    ResultCode := -1;

  if ResultCode <> 0 then
  begin
    MsgBox(FmtMessage(CustomMessage('DotNet8DownloadFailed'), [IntToStr(ResultCode)]),
           mbError, MB_OK);
    Exit;
  end;

  // Step 2: Silent install
  WizardForm.StatusLabel.Caption := CustomMessage('DotNet8Installing');
  InstallArgs := '/install /quiet /norestart';
  if not Exec(DotNetExe, InstallArgs, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    ResultCode := -1;

  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    // Exit 3010 = success, reboot required (acceptable)
    MsgBox(FmtMessage(CustomMessage('DotNet8InstallFailed'), [IntToStr(ResultCode)]),
           mbError, MB_OK);
    Exit;
  end;

  // Step 3: Verify installation succeeded
  Result := IsDotNet8Installed();
end;

// Flag set by InitializeSetup, consumed by PrepareToInstall
var
  NeedsDotNet8Install: Boolean;

function InitializeSetup(): Boolean;
begin
  Result := True;
  NeedsDotNet8Install := not IsDotNet8Installed();
  // Note: InstallDotNet8 is NOT called here — WizardForm is nil at this stage.
  // The actual install happens in PrepareToInstall where WizardForm is available.
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if NeedsDotNet8Install then
  begin
    if not InstallDotNet8() then
      Result := FmtMessage(CustomMessage('DotNet8InstallFailed'), ['?']);
  end;
end;

// Hand the chosen setup language over to UMI by writing a tiny hint file
// into {app}\config\. UMI reads it on first run (before config.json exists)
// to pick the GUI language and then persists it; the file is deleted by the
// app after the wizard completes. See InstallLanguageHint.cs in UMI.Core.
procedure WriteInstallLanguageHint();
var
  LangCode: string;
  ConfigDir: string;
begin
  if ActiveLanguage() = 'german' then
    LangCode := 'de'
  else
    LangCode := 'en';

  ConfigDir := ExpandConstant('{app}\config');
  if not DirExists(ConfigDir) then
    ForceDirectories(ConfigDir);

  SaveStringToFile(ConfigDir + '\install-language.txt', LangCode, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteInstallLanguageHint();
end;

function NeedsAddPath(Param: string): Boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKCU, 'Environment', 'Path', OrigPath) then
  begin
    Result := True;
    Exit;
  end;
  // Check case-insensitive whether Param is already in PATH
  Result := Pos(';' + Lowercase(Param) + ';', ';' + Lowercase(OrigPath) + ';') = 0;
end;

// Uninstall flag: True when the user clicked "Yes" on the config-removal prompt.
// Read in usPostUninstall to decide whether to wipe {app}\config\.
var
  DeleteUserConfigOnUninstall: Boolean;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  // Default = No (keep config). MB_DEFBUTTON2 makes "No" the highlighted button.
  DeleteUserConfigOnUninstall :=
    MsgBox(
      CustomMessage('UninstallConfigPromptMsg'),
      mbConfirmation,
      MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  InstallPath: string;
  CurrentPath: string;
  NewPath: string;
  SearchStr: string;
  Pos1: Integer;
  ConfigDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Wipe user config on request. Inno Setup itself only deletes files it
    // installed; the user's config.json + Profile-Subordner are written by
    // UMI at runtime, so we have to remove them manually.
    if DeleteUserConfigOnUninstall then
    begin
      ConfigDir := ExpandConstant('{app}\config');
      if DirExists(ConfigDir) then
        DelTree(ConfigDir, True, True, True);
    end;

    InstallPath := ExpandConstant('{app}');
    if RegQueryStringValue(HKCU, 'Environment', 'Path', CurrentPath) then
    begin
      SearchStr := ';' + InstallPath;
      // Case-insensitive search and remove via Pos on lowercased strings
      Pos1 := Pos(Lowercase(SearchStr), Lowercase(CurrentPath));
      if Pos1 > 0 then
        NewPath := Copy(CurrentPath, 1, Pos1 - 1) + Copy(CurrentPath, Pos1 + Length(SearchStr), Length(CurrentPath))
      else
        NewPath := CurrentPath;
      // Handle the case where {app} is at the start of PATH (without leading semicolon)
      if Lowercase(Copy(NewPath, 1, Length(InstallPath))) = Lowercase(InstallPath) then
        NewPath := Copy(NewPath, Length(InstallPath) + 1, Length(NewPath));
      // Remove leading semicolon if any
      if (Length(NewPath) > 0) and (NewPath[1] = ';') then
        NewPath := Copy(NewPath, 2, Length(NewPath));
      RegWriteExpandStringValue(HKCU, 'Environment', 'Path', NewPath);
    end;
  end;
end;
