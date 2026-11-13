// Universal Media Import (UMI) - Inno Setup [Code] module: running-UMI guard (I-024)
//
// Included from umi-setup.iss INSIDE its [Code] section (#include "umi-process-guard.iss").
// Setup (PrepareToInstall) and Uninstall (InitializeUninstall) both call the ONE entry
// point EnsureUmiStopped below - there is no second copy of this logic.
//
// Question this module answers: "Does a UMI program from THIS installation folder still
// run, and if so, may it be ended?"
//
// Rules (see docs/issues.md I-024):
//  1. Only umi.exe / umi-gui.exe whose FULL path lies directly in {app} count. The same
//     file names started from src\...\bin\ (dev builds) are never touched. Detection is
//     WMI Win32_Process.ExecutablePath, compared against {app} - never by file name alone.
//  2. First wait (the updater starts Setup and ends the GUI right afterwards), then ask.
//  3. Gentle first (taskkill without /F = WM_CLOSE), hard (taskkill /F /PID) only after a
//     second explicit confirmation, always by PID, never by image name.
//  4. Silent runs send NOTHING (no WM_CLOSE, no /F): they only wait (rule 2) and, if UMI still
//     runs, abort - an unattended run must not close a window with an import behind it.
//
// Note on "gentle": umi-gui.exe up to 2.2.5 has no close confirmation (no Closing handler); WM_CLOSE
// ends the window, App.OnExit stops the GPU queue (max. 5 s) and the process is gone - a
// running import is aborted WITHOUT a question from the GUI. A newer GUI may ask itself
// ("import running, really quit?", I-028); its question is answered in the UMI window while
// the wait below runs. The dialog texts warn on the first question and point to that.

const
  // Updater: Process.Start returns, then the GUI closes its main window (up to 2.2.5: Shutdown()); App.OnExit may wait up to
  // 5 s for the GPU queue (App.xaml.cs) before the process is gone.
  UmiProcessExitWaitMs = 10000;
  // Gentle close (WM_CLOSE) -> MainWindow closes -> OnExit (same 5 s GPU-queue stop). Too short
  // for a human to answer UMI's own "import running, really quit?" (I-028); that is by design:
  // afterwards the force question comes with default No, and No there returns to the first question.
  UmiProcessCloseWaitMs = 10000;
  UmiProcessKillWaitMs = 5000;
  UmiProcessPollMs = 250;
  UmiWmiQuery = 'SELECT ProcessId, Name, ExecutablePath FROM Win32_Process ' +
                'WHERE Name = ''umi.exe'' OR Name = ''umi-gui.exe''';

// Pascal Script has no tick counter of its own; milliseconds since boot, wraps after ~49 days
// (unsigned subtraction below stays correct across a wrap).
function GetTickCount: Cardinal;
  external 'GetTickCount@kernel32.dll stdcall';

// Lists the UMI programs of installation folder AppDir that run right now.
// Result: number found; -1 = the WMI query itself failed ("unknown", not "none").
function QueryUmiProcesses(const AppDir: string; var Pids: TArrayOfInteger;
  var Names: TArrayOfString): Integer;
var
  Locator, Services, ObjectSet, Item, PathValue: Variant;
  I, Found, Pid: Integer;
  ExePath, AppDirNoSlash: string;
begin
  Result := 0;
  Found := 0;
  AppDirNoSlash := RemoveBackslash(AppDir);
  SetArrayLength(Pids, 0);
  SetArrayLength(Names, 0);
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Services := Locator.ConnectServer('.', 'root\CIMV2');
    ObjectSet := Services.ExecQuery(UmiWmiQuery);
    for I := 0 to ObjectSet.Count - 1 do
    begin
      Item := ObjectSet.ItemIndex(I);
      Pid := Item.ProcessId;
      PathValue := Item.ExecutablePath;
      if VarIsNull(PathValue) or VarIsEmpty(PathValue) then
        // Path not readable (e.g. protected process): cannot belong to {app} by proof.
        Log(Format('UMI guard: path of PID %d unreadable, ignored', [Pid]))
      else
      begin
        ExePath := PathValue;
        if CompareText(ExtractFileDir(ExePath), AppDirNoSlash) = 0 then
        begin
          SetArrayLength(Pids, Found + 1);
          SetArrayLength(Names, Found + 1);
          Pids[Found] := Pid;
          Names[Found] := ExtractFileName(ExePath);
          Found := Found + 1;
        end;
      end;
    end;
    Result := Found;
  except
    Log('UMI guard: WMI query failed: ' + GetExceptionMessage);
    Result := -1;
  end;
end;

// Polls until no UMI program of AppDir runs or TimeoutMs elapsed (state polling, no blind delay).
// Result as QueryUmiProcesses; the arrays hold the last observation.
function WaitForUmiExit(const AppDir: string; TimeoutMs: Cardinal; var Pids: TArrayOfInteger;
  var Names: TArrayOfString): Integer;
var
  StartTick: Cardinal;
begin
  StartTick := GetTickCount;
  Result := QueryUmiProcesses(AppDir, Pids, Names);
  while (Result > 0) and ((GetTickCount - StartTick) < TimeoutMs) do
  begin
    Sleep(UmiProcessPollMs);
    Result := QueryUmiProcesses(AppDir, Pids, Names);
  end;
end;

// taskkill per PID. Force=False: WM_CLOSE to the windows (a console umi.exe has none and
// refuses - then only the force path remains). Force=True: /F, terminates immediately.
procedure SignalUmiProcesses(const Pids: TArrayOfInteger; Force: Boolean);
var
  I, ResultCode: Integer;
  Args: string;
begin
  for I := 0 to GetArrayLength(Pids) - 1 do
  begin
    Args := '/PID ' + IntToStr(Pids[I]);
    if Force then
      Args := Args + ' /F';
    if not Exec(ExpandConstant('{sys}\taskkill.exe'), Args, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      ResultCode := -1;
    Log(Format('UMI guard: taskkill %s -> %d', [Args, ResultCode]));
  end;
end;

// One line per distinct program name for the dialog text.
function DescribeUmiProcesses(const Names: TArrayOfString): string;
var
  I: Integer;
begin
  Result := '';
  for I := 0 to GetArrayLength(Names) - 1 do
    if Pos(Names[I] + #13#10, Result + #13#10) = 0 then
      Result := Result + Names[I] + #13#10;
end;

// Hard end after the second confirmation. PIDs are queried afresh: the user may have
// waited minutes in the dialog, the earlier PIDs could be stale or reused.
function ForceCloseUmi(const AppDir: string): Boolean;
var
  Pids: TArrayOfInteger;
  Names: TArrayOfString;
  Count: Integer;
begin
  Count := QueryUmiProcesses(AppDir, Pids, Names);
  if Count > 0 then
  begin
    SignalUmiProcesses(Pids, True);
    Count := WaitForUmiExit(AppDir, UmiProcessKillWaitMs, Pids, Names);
  end;
  Result := Count = 0;
end;

// THE entry point for Setup and Uninstall. True = Setup/Uninstall may continue.
// AppDir empty or missing (first installation) = nothing to check.
function EnsureUmiStopped(const AppDir: string; Silent: Boolean): Boolean;
var
  Pids: TArrayOfInteger;
  Names: TArrayOfString;
  Labels: TArrayOfString;
  Count: Integer;
  ProcessList: string;
begin
  Result := True;
  if (AppDir = '') or (not DirExists(AppDir)) then
    Exit;

  SetArrayLength(Labels, 3);
  Labels[0] := CustomMessage('UmiBtnClose');
  Labels[1] := CustomMessage('UmiBtnRetry');
  Labels[2] := CustomMessage('UmiBtnCancel');

  Count := WaitForUmiExit(AppDir, UmiProcessExitWaitMs, Pids, Names);
  if Count < 0 then
  begin
    // Fallback: the WMI query is broken, "running" is unknown. Do not block Setup on that;
    // a file that is really locked then surfaces as Inno's own "file in use" error
    // (CloseApplications=no, so Restart Manager never ends anything behind our back).
    Log('UMI guard: process state unknown, continuing');
    Exit;
  end;

  if Silent and (Count > 0) then
  begin
    // Rule 4: nothing is sent. The wait above already gave UMI the chance to end by itself.
    Log('UMI guard: silent run and UMI still running (' + Trim(DescribeUmiProcesses(Names)) +
        ') - aborting, nothing was sent to it');
    Result := False;
    Exit;
  end;

  while Count > 0 do
  begin
    ProcessList := DescribeUmiProcesses(Names);
    // Yes = close, No = check again, Cancel = abort (labels set above).
    case TaskDialogMsgBox(CustomMessage('UmiRunningTitle'),
           FmtMessage(CustomMessage('UmiRunningMsg'), [ProcessList]),
           mbConfirmation, MB_YESNOCANCEL, Labels, 0) of
      IDYES:
        begin
          // The PIDs from before the (blocking) dialog may be stale: UMI could have ended by
          // itself meanwhile and the PID been given to another program. Ask afresh, like ForceCloseUmi.
          Count := QueryUmiProcesses(AppDir, Pids, Names);
          if Count > 0 then
          begin
            SignalUmiProcesses(Pids, False);
            Count := WaitForUmiExit(AppDir, UmiProcessCloseWaitMs, Pids, Names);
          end;
          if Count > 0 then
          begin
            ProcessList := DescribeUmiProcesses(Names);
            if MsgBox(FmtMessage(CustomMessage('UmiForceMsg'), [ProcessList]),
                      mbError, MB_YESNO or MB_DEFBUTTON2) = IDYES then
            begin
              if ForceCloseUmi(AppDir) then
                Count := 0
              else
              begin
                MsgBox(CustomMessage('UmiKillFailed'), mbError, MB_OK);
                Result := False;
                Exit;
              end;
            end
            else
              // "No" on the force question: back to the first question (retry/cancel possible),
              // with a fresh observation - UMI may have ended meanwhile (e.g. answered "Yes" in its own
              // import question while this box was open) and must not be asked about again.
              Count := QueryUmiProcesses(AppDir, Pids, Names);
          end;
        end;
      IDNO:
        Count := QueryUmiProcesses(AppDir, Pids, Names);
    else
      begin
        Result := False;
        Exit;
      end;
    end;
  end;
end;
