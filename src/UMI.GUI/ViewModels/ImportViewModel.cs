// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This file is part of UMI - Universal Media Import.
//
//     UMI - Universal Media Import is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     UMI - Universal Media Import is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with UMI - Universal Media Import.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Resources;
using UMI.GUI.Services;
using UMI.GUI.Views;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the Import tab.
/// Manages Watch mode, Quick Import, per-camera import tasks, and global Cancel.
/// DriveWatcher runs for the entire app lifetime — card status is always visible.
/// Watch mode only controls whether imports trigger automatically on card insertion.
/// </summary>
public class ImportViewModel : ViewModelBase, IDisposable
{
    private readonly IImportOrchestrationService _orchestration;
    private readonly IConfigWriterService _configWriter;
    private readonly IDriveWatcherService? _driveWatcher;
    private readonly ISdCardRegistryService _sdCardRegistry;
    private readonly ISdFingerprintService _fingerprintService;
    private readonly IMtpDeviceDetectionService? _mtpDetection;
    private readonly IMtpService? _mtpService;
    private readonly IMtpImportService? _mtpImportService;
    private readonly IImportHistoryService? _historyService;
    private readonly ILogger<ImportViewModel>? _logger;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _cardDialogSemaphore = new(1, 1);

    private CancellationTokenSource? _mtpPollingCts;

    /// <summary>
    /// Counter of active MTP imports. When > 0, MTP polling is suspended to avoid
    /// concurrent WPD COM API connections which break the active import connection.
    /// Use Interlocked for thread-safe increment/decrement from background threads.
    /// </summary>
    private int _activeMtpImports;

    /// <summary>
    /// Snapshot of device keys seen in the last MTP poll cycle, mapped to their resolved cameraId.
    /// Storing the cameraId here ensures disconnect can reset the correct CameraViewModel even for
    /// ModelMatch devices that have no entry in config.MtpDevices.
    /// Only mutated from the single MTP polling task — no locking needed.
    /// </summary>
    private readonly Dictionary<string, string?> _connectedMtpDeviceKeys = new(StringComparer.OrdinalIgnoreCase);

    private ObservableCollection<CameraViewModel>? _cameras;

    /// <summary>
    /// Camera collection shared with MainViewModel.
    /// Set by MainViewModel after LoadAsync() so ImportViewModel can find
    /// the CameraViewModel matching an incoming drive event.
    /// Subscribes to each camera's PropertyChanged to keep IsAnyPaused in sync.
    /// </summary>
    public ObservableCollection<CameraViewModel>? Cameras
    {
        get => _cameras;
        set
        {
            if (_cameras is not null)
            {
                foreach (var cam in _cameras)
                    cam.PropertyChanged -= OnCameraPropertyChanged;
            }
            _cameras = value;
            if (_cameras is not null)
            {
                foreach (var cam in _cameras)
                    cam.PropertyChanged += OnCameraPropertyChanged;
            }
        }
    }

    private void OnCameraPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CameraViewModel.IsPaused))
            _dispatcher.InvokeAsync(UpdateIsAnyPaused);
    }

    /// <summary>
    /// DevicesTab ViewModel for updating card connection status on device entries.
    /// Set by MainViewModel after LoadAsync().
    /// </summary>
    public DevicesTabViewModel? DevicesTab { get; set; }

    private bool _isWatching;
    /// <summary>True while file-system watch mode is active.</summary>
    public bool IsWatching
    {
        get => _isWatching;
        private set => SetProperty(ref _isWatching, value);
    }

    private bool _hasActiveImports;
    /// <summary>True while at least one camera import is running. Controls "Cancel All" visibility.</summary>
    public bool HasActiveImports
    {
        get => _hasActiveImports;
        private set => SetProperty(ref _hasActiveImports, value);
    }

    private bool _isAnyPaused;
    /// <summary>True while at least one active import is paused. Controls Pause/Resume All label.</summary>
    public bool IsAnyPaused
    {
        get => _isAnyPaused;
        private set
        {
            if (SetProperty(ref _isAnyPaused, value))
                OnPropertyChanged(nameof(PauseResumeAllLabel));
        }
    }

    /// <summary>Label for the global pause/resume button.</summary>
    public string PauseResumeAllLabel => IsAnyPaused ? Strings.ImportTab_ResumeAll : Strings.ImportTab_PauseAll;

    private readonly Dictionary<string, CancellationTokenSource> _activeCts = new();

    /// <summary>Toggles file-system watch mode.</summary>
    public ICommand WatchCommand { get; }

    /// <summary>Triggers a one-shot quick import of all currently connected drives.</summary>
    public ICommand QuickImportCommand { get; }

    /// <summary>Cancels all active per-camera imports.</summary>
    public ICommand CancelAllCommand { get; }

    /// <summary>Pauses or resumes all active per-camera imports.</summary>
    public ICommand PauseResumeAllCommand { get; }

    /// <summary>Shared date range filter logic. Single source of truth.</summary>
    public DateFilterViewModel DateFilter { get; } = new();

    /// <summary>True while the date filter popup is open (bound to ToggleButton.IsChecked).</summary>
    public bool IsDateFilterPopupOpen
    {
        get => DateFilter.IsPopupOpen;
        set => DateFilter.IsPopupOpen = value;
    }

    /// <summary>Lower bound of the import date range filter (session-only, not persisted).</summary>
    public DateTime? DateFrom
    {
        get => DateFilter.DateFrom;
        set => DateFilter.DateFrom = value;
    }

    /// <summary>Upper bound of the import date range filter (session-only, not persisted).</summary>
    public DateTime? DateTo
    {
        get => DateFilter.DateTo;
        set => DateFilter.DateTo = value;
    }

    /// <summary>True when at least one date boundary is set.</summary>
    public bool HasDateFilter => DateFilter.HasDateFilter;

    /// <summary>Human-readable summary of the active date filter for the pill label.</summary>
    public string DateFilterText => DateFilter.DateFilterText;

    /// <summary>
    /// Time portion of DateFrom as HH:mm string.
    /// Setting this merges the time into the existing or today's date.
    /// </summary>
    public string DateFromTime
    {
        get => DateFilter.DateFromTime;
        set => DateFilter.DateFromTime = value;
    }

    /// <summary>
    /// Time portion of DateTo as HH:mm string.
    /// Setting this merges the time into the existing or today's date.
    /// </summary>
    public string DateToTime
    {
        get => DateFilter.DateToTime;
        set => DateFilter.DateToTime = value;
    }

    /// <summary>Clears both DateFrom and DateTo, removing the active date filter.</summary>
    public ICommand ClearDateFilterCommand => DateFilter.ClearDateFilterCommand;

    /// <summary>Sets the date filter to today (00:00:00 – 23:59:59).</summary>
    public ICommand SetTodayFilterCommand => DateFilter.SetTodayFilterCommand;

    /// <summary>Sets the date filter to yesterday (00:00:00 – 23:59:59).</summary>
    public ICommand SetYesterdayFilterCommand => DateFilter.SetYesterdayFilterCommand;

    public ImportViewModel(
        IImportOrchestrationService orchestration,
        IConfigWriterService configWriter,
        IDriveWatcherService? driveWatcher,
        ISdCardRegistryService sdCardRegistry,
        ISdFingerprintService fingerprintService,
        IMtpDeviceDetectionService? mtpDetection = null,
        IMtpService? mtpService = null,
        IMtpImportService? mtpImportService = null,
        IImportHistoryService? historyService = null,
        ILogger<ImportViewModel>? logger = null)
    {
        _orchestration = orchestration;
        _configWriter = configWriter;
        _driveWatcher = driveWatcher;
        _sdCardRegistry = sdCardRegistry;
        _fingerprintService = fingerprintService;
        _mtpDetection = mtpDetection;
        _mtpService = mtpService;
        _mtpImportService = mtpImportService;
        _historyService = historyService;
        _logger = logger;
        _dispatcher = Dispatcher.CurrentDispatcher;

        WatchCommand            = new RelayCommand(ExecuteWatch);
        QuickImportCommand      = new RelayCommand(ExecuteQuickImport);
        CancelAllCommand        = new RelayCommand(ExecuteCancelAll);
        PauseResumeAllCommand   = new RelayCommand(ExecutePauseResumeAll);

        DateFilter.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
    }

    /// <summary>
    /// Starts the DriveWatcher and performs initial card status scan.
    /// Also starts MTP polling for live MTP device connection status.
    /// Called once after cameras are loaded. DriveWatcher runs for the
    /// entire app lifetime (card status is always visible).
    /// </summary>
    public void InitializeCardStatus()
    {
        if (_driveWatcher is not null)
        {
            _driveWatcher.DriveArrived += OnDriveStatusChanged;
            _driveWatcher.DriveRemoved += OnDriveRemoved;
            _driveWatcher.StartWatching();

            var currentDrives = _driveWatcher.GetCurrentDrives();
            foreach (var drive in currentDrives)
                _ = UpdateCardStatusAsync(drive.RootPath, drive.DriveLetter);
        }

        if (_mtpDetection is not null)
        {
            _mtpPollingCts = new CancellationTokenSource();
            _ = StartMtpPollingAsync(_mtpPollingCts.Token);
        }
    }

    /// <summary>
    /// Re-scans all currently connected SD drives and MTP devices and updates card status.
    /// Called after the Add Device dialog closes so newly registered devices get a green dot immediately.
    /// </summary>
    public void RescanConnectedDevices()
    {
        if (_driveWatcher is not null)
        {
            var currentDrives = _driveWatcher.GetCurrentDrives();
            foreach (var drive in currentDrives)
                _ = UpdateCardStatusAsync(drive.RootPath, drive.DriveLetter);
        }

        if (_mtpDetection is not null)
            _ = PollMtpDevicesOnceAsync();
    }

    /// <summary>
    /// Polls MTP devices every 5 seconds and updates CameraViewModel +
    /// DevicesTabViewModel when devices connect or disconnect.
    /// Single-writer for _connectedMtpDeviceKeys — no locking needed.
    /// </summary>
    private async Task StartMtpPollingAsync(CancellationToken ct)
    {

        await PollMtpDevicesOnceAsync();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                await PollMtpDevicesOnceAsync();
            }
            catch (OperationCanceledException)
            {

                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in MTP polling loop — will retry in 5 seconds");
            }
        }
    }

    private async Task PollMtpDevicesOnceAsync()
    {

        if (Interlocked.CompareExchange(ref _activeMtpImports, 0, 0) > 0)
        {
            _logger?.LogDebug("MTP poll skipped — import active");
            return;
        }

        IReadOnlyList<MtpDetectionResult> results;
        try
        {

            var sw = System.Diagnostics.Stopwatch.StartNew();
            results = await _dispatcher.InvokeAsync(() => _mtpDetection!.DetectDevices());
            sw.Stop();
            if (sw.ElapsedMilliseconds > 500)
                _logger?.LogWarning("MTP poll took {Ms}ms (UI thread blocked)", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MTP DetectDevices failed");
            return;
        }

        var currentKeys = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {

            var isMatched = result.CameraId is not null &&
                            (result.Outcome == MtpDetectionOutcome.Registered ||
                             result.Outcome == MtpDetectionOutcome.ModelMatch);

            if (!isMatched)
            {
                _logger?.LogDebug("MTP device {Name} not matched to any camera — skipping status update",
                    result.Device.FriendlyName);
                continue;
            }

            var deviceKey = MtpDeviceDetectionService.GetDeviceKey(result.Device);
            currentKeys[deviceKey] = result.CameraId;

            if (!_connectedMtpDeviceKeys.ContainsKey(deviceKey))
            {
                _logger?.LogInformation("MTP device connected: {Name} → {CameraId}",
                    result.Device.FriendlyName, result.CameraId);
                await UpdateMtpStatusAsync(result, deviceKey, connected: true);

                if (IsWatching && _mtpService is not null)
                {
                    CameraViewModel? watchCameraVm = null;
                    await _dispatcher.InvokeAsync(() =>
                        watchCameraVm = Cameras?.FirstOrDefault(c =>
                            string.Equals(c.CameraId, result.CameraId, StringComparison.OrdinalIgnoreCase)));

                    if (watchCameraVm is not null)
                        _ = StartMtpImportAsync(watchCameraVm, result.Device);
                }
            }
        }

        var disconnectedEntries = _connectedMtpDeviceKeys
            .Where(kvp => !currentKeys.ContainsKey(kvp.Key))
            .ToList();

        foreach (var (key, storedCameraId) in disconnectedEntries)
        {
            _logger?.LogInformation("MTP device disconnected: key={Key}", key);
            await UpdateMtpStatusAsync(deviceKey: key, cameraId: storedCameraId, friendlyName: null, connected: false);
        }

        _connectedMtpDeviceKeys.Clear();
        foreach (var kvp in currentKeys)
            _connectedMtpDeviceKeys[kvp.Key] = kvp.Value;
    }

    /// <summary>
    /// Updates CameraViewModel and DevicesTabViewModel for a newly connected MTP device.
    /// </summary>
    private async Task UpdateMtpStatusAsync(MtpDetectionResult result, string deviceKey, bool connected)
    {
        await UpdateMtpStatusAsync(
            deviceKey: deviceKey,
            cameraId: connected ? result.CameraId : null,
            friendlyName: connected ? result.Device.FriendlyName : null,
            connected: connected);
    }

    /// <summary>
    /// Core dispatcher-bound update for MTP connection status.
    /// On connect: cameraId and friendlyName are provided.
    /// On disconnect: cameraId comes from _connectedMtpDeviceKeys (covers both Registered and
    /// ModelMatch devices). config.MtpDevices lookup is kept as a safety net for null cameraId.
    /// </summary>
    private async Task UpdateMtpStatusAsync(
        string deviceKey,
        string? cameraId,
        string? friendlyName,
        bool connected)
    {
        await _dispatcher.InvokeAsync(() =>
        {

            var resolvedCameraId = cameraId;
            if (!connected && resolvedCameraId is null)
            {

                _configWriter.Config.MtpDevices.TryGetValue(deviceKey, out var reg);
                resolvedCameraId = reg?.CameraId;
            }

            if (resolvedCameraId is not null)
            {
                var cameraVm = Cameras?.FirstOrDefault(c =>
                    string.Equals(c.CameraId, resolvedCameraId, StringComparison.OrdinalIgnoreCase));
                if (cameraVm is not null)
                {

                    cameraVm.SetDriveConnected(deviceKey, null, connected);
                    cameraVm.RefreshStorageSummary(_configWriter.Config);

                    cameraVm.ConnectedDriveLetter = connected ? friendlyName : null;
                    _logger?.LogInformation("MTP card status: {CameraId} -> {State}",
                        resolvedCameraId, connected ? "connected" : "disconnected");
                }
            }

            DevicesTab?.UpdateMtpStatus(deviceKey, friendlyName, connected);
        });
    }

    private void ExecuteWatch()
    {
        IsWatching = !IsWatching;
        _logger?.LogInformation("Watch mode {State}", IsWatching ? "started" : "stopped");

        if (IsWatching)
        {

            if (_driveWatcher is not null)
            {
                var currentDrives = _driveWatcher.GetCurrentDrives();
                _logger?.LogInformation("Watch started: {Count} removable drive(s) detected: {Drives}",
                    currentDrives.Count, string.Join(", ", currentDrives.Select(d => $"{d.DriveLetter}({d.VolumeLabel})")));
                _ = RunQuickImportSdAsync(currentDrives);
            }
            else
            {
                _logger?.LogWarning("DriveWatcherService not available — SD drive scan skipped");
            }

            if (_mtpDetection is not null && _mtpService is not null)
                _ = StartMtpQuickImportsAsync();

            StartFixedPathImports();
        }
    }

    /// <summary>
    /// Called on a WMI worker thread when a removable drive arrives.
    /// Always updates card status. Floating/Unknown cards trigger the FloatingCardDialog
    /// (only when watch mode is active). Fixed+Matched cards trigger auto-import.
    /// Uses the config label (SdCards[vsn].Label) instead of the WMI VolumeLabel when available.
    /// </summary>
    private async void OnDriveStatusChanged(object? sender, DriveChangedEventArgs e)
    {
        _logger?.LogInformation("Drive arrived: {Drive} ({Label})", e.DriveLetter, e.VolumeLabel);

        try
        {
            var outcome = await UpdateCardStatusAsync(e.RootPath, e.DriveLetter);
            _logger?.LogInformation("Drive status: {Drive} → Result={Result}, CameraId={CameraId}, VSN={Vsn}",
                e.DriveLetter, outcome?.Result, outcome?.CameraId, outcome?.MatchedVsn);
            if (outcome is null) return;

            if (outcome.Result == SdLookupResult.Unknown || (outcome.Result == SdLookupResult.Matched && outcome.CameraId is null))
            {
                if (IsWatching)
                    _ = ShowCardDialogAsync(e.RootPath, e.DriveLetter, e.VolumeLabel, outcome);
                return;
            }

            if (IsWatching && outcome.CameraId is not null)
            {
                CameraViewModel? cameraVm = null;
                await _dispatcher.InvokeAsync(() =>
                    cameraVm = Cameras?.FirstOrDefault(c => c.CameraId == outcome.CameraId));

                if (cameraVm is not null)
                {
                    var configLabel = outcome.MatchedVsn is not null
                        ? _configWriter.Config.SdCards.GetValueOrDefault(outcome.MatchedVsn)?.Label
                        : null;
                    await StartImportAsync(cameraVm, e.RootPath, configLabel ?? e.VolumeLabel);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling drive arrival for {Drive}", e.DriveLetter);
        }
    }

    /// <summary>
    /// Looks up the camera for a drive and updates its IsCardConnected status.
    /// Returns the full SdLookupOutcome so callers can use both CameraId and MatchedVsn,
    /// or null if the drive was Skipped (not an SD-relevant removable drive).
    /// For Unknown and Floating cards, returns the outcome without updating card status UI
    /// (caller is responsible for showing the FloatingCardDialog).
    /// </summary>
    private async Task<SdLookupOutcome?> UpdateCardStatusAsync(string rootPath, string driveLetter)
    {
        var outcome = await _sdCardRegistry.LookupCameraIdAsync(rootPath);

        if (outcome.Result == SdLookupResult.Skipped)
        {
            _logger?.LogDebug("Drive {Drive} skipped (not removable)", driveLetter);
            return null;
        }

        if (outcome.Result == SdLookupResult.Unknown)
        {
            _logger?.LogDebug("Drive {Drive} unknown — dialog required", driveLetter);
            if (outcome.MatchedVsn is not null)
                await _dispatcher.InvokeAsync(() => DevicesTab?.UpdateCardStatus(outcome.MatchedVsn, driveLetter, true));
            return outcome;
        }

        if (outcome.Result == SdLookupResult.Matched && outcome.CameraId is null)
        {
            _logger?.LogDebug("Drive {Drive} floating — dialog required", driveLetter);
            if (outcome.MatchedVsn is not null)
                await _dispatcher.InvokeAsync(() => DevicesTab?.UpdateCardStatus(outcome.MatchedVsn, driveLetter, true));
            return outcome;
        }

        if (outcome.CameraId is null)
        {
            _logger?.LogDebug("Drive {Drive} matched but no CameraId", driveLetter);
            return null;
        }

        var matchingVsn = outcome.MatchedVsn
            ?? _configWriter.Config.SdCards
                .FirstOrDefault(kvp => kvp.Value.CameraId == outcome.CameraId).Key;

        await _dispatcher.InvokeAsync(() =>
        {
            var cameraVm = Cameras?.FirstOrDefault(c => c.CameraId == outcome.CameraId);
            if (cameraVm is not null)
            {

                cameraVm.ConnectedDriveLetter = driveLetter;
                cameraVm.SetDriveConnected(driveLetter, matchingVsn, true);
                cameraVm.RefreshStorageSummary(_configWriter.Config);
                _logger?.LogInformation("Card status: {CameraId} -> connected ({Drive})",
                    outcome.CameraId, driveLetter);
            }

            DevicesTab?.UpdateCardStatus(matchingVsn, driveLetter, true);
        });

        return outcome;
    }

    /// <summary>
    /// Called on a WMI worker thread when a removable drive is removed.
    /// Iterates ALL cameras and resets any that had the removed drive letter registered.
    /// Uses HasConnectedDriveLetter instead of ConnectedDriveLetter so that multi-drive
    /// scenarios (last-wins overwrite of ConnectedDriveLetter) are handled correctly.
    /// </summary>
    private async void OnDriveRemoved(object? sender, DriveChangedEventArgs e)
    {
        _logger?.LogInformation("Drive removed: {Drive}", e.DriveLetter);

        await _dispatcher.InvokeAsync(() =>
        {
            foreach (var cam in Cameras ?? [])
            {
                if (!cam.HasConnectedDriveLetter(e.DriveLetter)) continue;

                var disconnectedVsn = cam.GetVsnForDriveLetter(e.DriveLetter);

                cam.SetDriveConnected(e.DriveLetter, null, false);
                cam.RefreshStorageSummary(_configWriter.Config);

                if (!cam.IsCardConnected) cam.ConnectedDriveLetter = null;

                _logger?.LogInformation("Card status: {CameraId} -> drive {Drive} removed",
                    cam.CameraId, e.DriveLetter);

                if (disconnectedVsn is not null)
                    DevicesTab?.UpdateCardStatus(disconnectedVsn, e.DriveLetter, false);
            }

            if (DevicesTab is not null)
            {
                foreach (var entry in DevicesTab.SdCards)
                {
                    if (entry.IsCardConnected && entry.ConnectedDriveLetter == e.DriveLetter)
                    {
                        entry.IsCardConnected = false;
                        entry.ConnectedDriveLetter = null;
                    }
                }
            }
        });
    }

    private void ExecuteQuickImport()
    {
        _logger?.LogInformation("Quick Import button clicked");

        if (_driveWatcher is not null)
        {
            var drives = _driveWatcher.GetCurrentDrives();
            if (drives.Count > 0)
            {
                _logger?.LogInformation("Quick Import: {Count} drive(s) detected", drives.Count);
                _ = RunQuickImportSdAsync(drives);
            }
            else
            {
                _logger?.LogInformation("Quick Import: no removable drives detected");
            }
        }
        else
        {
            _logger?.LogWarning("DriveWatcherService not available — IDriveWatcherService not resolved");
        }

        if (_mtpDetection is not null && _mtpService is not null)
        {
            _ = StartMtpQuickImportsAsync();
        }

        StartFixedPathImports();
    }

    /// <summary>
    /// Groups SD drives by camera and processes each camera's drives sequentially,
    /// different cameras in parallel. Floating/Unknown drives are queued for the
    /// FloatingCardDialog (sequentially, one dialog at a time).
    /// Prevents race conditions when a camera exposes multiple drives (e.g. internal + SD card via USB).
    /// </summary>
    private async Task RunQuickImportSdAsync(List<DetectedDrive> drives)
    {
        var driveGroups = new Dictionary<string, List<DetectedDrive>>(StringComparer.OrdinalIgnoreCase);
        var dialogDrives = new List<(DetectedDrive Drive, SdLookupOutcome Outcome)>();

        foreach (var drive in drives)
        {
            var outcome = await _sdCardRegistry.LookupCameraIdAsync(drive.RootPath);
            _logger?.LogInformation("Quick Import SD scan: {Drive} ({Label}) → {Result} (CameraId={CameraId}, VSN={Vsn})",
                drive.DriveLetter, drive.VolumeLabel, outcome.Result, outcome.CameraId, outcome.MatchedVsn);

            if (outcome.Result == SdLookupResult.Unknown || (outcome.Result == SdLookupResult.Matched && outcome.CameraId is null))
            {
                dialogDrives.Add((drive, outcome));
                continue;
            }

            if (outcome.Result == SdLookupResult.Matched && outcome.CameraId is not null)
            {
                if (!driveGroups.TryGetValue(outcome.CameraId, out var list))
                    driveGroups[outcome.CameraId] = list = [];
                list.Add(drive);
            }
        }

        var fixedTask = Task.WhenAll(driveGroups.Select(kvp => Task.Run(async () =>
        {
            var cameraId   = kvp.Key;
            var cameraDrives = kvp.Value;
            var results    = new List<ImportOrchestrationResult?>();

            foreach (var drive in cameraDrives)
                results.Add(await StartQuickImportDriveAsync(drive));

            var withFiles = results.Where(r => r is { Success: true, FilesCopied: > 0 }).ToList();
            if (withFiles.Count > 1)
            {
                var totalFiles = withFiles.Sum(r => r!.FilesCopied);
                var totalBytes = withFiles.Sum(r => r!.BytesCopied);
                var sizeGb   = totalBytes / (1024.0 * 1024.0 * 1024.0);
                var sizeText = sizeGb >= 1.0
                    ? $"{sizeGb:F1} GB"
                    : $"{totalBytes / (1024.0 * 1024.0):F0} MB";
                var summary = string.Format(Strings.Import_MultiDriveSummary, totalFiles, sizeText, withFiles.Count);

                await _dispatcher.InvokeAsync(() =>
                {
                    var vm = Cameras?.FirstOrDefault(c => c.CameraId == cameraId);
                    if (vm is not null)
                    {
                        vm.DriveCount  = withFiles.Count;
                        vm.ResultText  = summary;
                    }
                });
            }
        })));

        var dialogTask = Task.Run(async () =>
        {
            foreach (var (drive, outcome) in dialogDrives)
            {
                try
                {
                    await ShowCardDialogAsync(drive.RootPath, drive.DriveLetter, drive.VolumeLabel, outcome);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "ShowCardDialogAsync failed for {Drive}", drive.DriveLetter);
                }
            }
        });

        await Task.WhenAll(fixedTask, dialogTask);
    }

    /// <summary>
    /// Shows the FloatingCardDialog for a Floating or Unknown SD card.
    /// Serialised via _cardDialogSemaphore so only one dialog is shown at a time.
    /// On Import: updates config, card status UI, and starts import.
    /// On Skip: returns without action.
    /// </summary>
    private async Task ShowCardDialogAsync(
        string rootPath, string driveLetter, string? volumeLabel, SdLookupOutcome outcome)
    {
        await _cardDialogSemaphore.WaitAsync();
        try
        {
            try
            {
            _logger?.LogInformation("ShowCardDialogAsync: entering for {Drive} (Result={Result})", driveLetter, outcome.Result);

            long totalSizeBytes = 0;
            try
            {
                var driveInfo = new DriveInfo(driveLetter.TrimEnd(':'));
                totalSizeBytes = driveInfo.TotalSize;
            }
            catch {  }

            var cardVsn = outcome.MatchedVsn ?? "";
            if (string.IsNullOrEmpty(cardVsn))
            {
                try
                {
                    var cardInfo = VolumeInfoReader.ReadSdCardInfo(rootPath, _logger);
                    cardVsn = cardInfo.VolumeSerial;
                    if (totalSizeBytes == 0) totalSizeBytes = cardInfo.DiskSizeBytes;
                }
                catch {  }
            }

            string? exifModel = null;
            string? exifMatchedCameraId = null;
            try
            {
                var fingerprint = await _fingerprintService.IdentifyCardAsync(rootPath);
                exifModel = fingerprint?.Model;
                if (fingerprint is not null)
                    exifMatchedCameraId = _fingerprintService.MatchCamera(fingerprint, _configWriter.Config.Cameras);
            }
            catch {  }

            SdCardRegistration? registration = null;
            if (!string.IsNullOrEmpty(cardVsn))
                _configWriter.Config.SdCards.TryGetValue(cardVsn, out registration);

            FloatingCardDialogViewModel? dialogVm = null;
            bool dialogResult = false;

            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    var owner = Application.Current.MainWindow;

                    var isFloating = outcome.Result == SdLookupResult.Matched && outcome.CameraId is null;

                    dialogVm = new FloatingCardDialogViewModel(
                        isFloating: isFloating,
                        cardLabel: volumeLabel ?? Strings.Import_NoLabel,
                        cardVsn: cardVsn,
                        cardSizeBytes: totalSizeBytes,
                        exifModel: exifModel,
                        registration: registration,
                        cameras: _configWriter.Config.Cameras,
                        exifMatchedCameraId: exifMatchedCameraId);

                    var dialog = new FloatingCardDialog(dialogVm);
                    if (owner is not null && owner != dialog)
                        dialog.Owner = owner;
                    dialog.ShowDialog();
                    dialogResult = dialogVm.DialogResult;
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "FloatingCardDialog crashed for {Drive}", driveLetter);
                return;
            }

            _logger?.LogInformation("ShowCardDialogAsync: dialog result={DialogResult}, chosen={CameraId}", dialogResult, dialogVm?.ChosenCameraId);
            if (!dialogResult || dialogVm?.ChosenCameraId is null) return;

            var chosenCameraId = dialogVm.ChosenCameraId;

            if (!string.IsNullOrEmpty(cardVsn))
            {
                if (registration is not null)
                {

                    registration.RecordUsage(chosenCameraId);

                    if (dialogVm.AlwaysUseThisCamera && dialogVm.IsFloating)
                    {

                        registration.CameraId = chosenCameraId;
                    }

                    _configWriter.RegisterSdCard(cardVsn, registration);
                }
                else
                {

                    var registrationCameraId = SdCardRegistration.EffectiveCameraId(chosenCameraId, dialogVm.RegisterAsFloating);
                    var newCardInfo = await Task.Run(() => VolumeInfoReader.ReadSdCardInfo(rootPath, _logger));
                    var newReg = SdCardRegistrationHelper.Create(
                        registrationCameraId,
                        label: volumeLabel,
                        diskSerial: VolumeInfoReader.IsFakeDiskSerial(newCardInfo.DiskSerial) ? null : newCardInfo.DiskSerial,
                        sizeBytes: totalSizeBytes > 0 ? totalSizeBytes : newCardInfo.DiskSizeBytes);

                    newReg.RecordUsage(chosenCameraId);

                    _configWriter.RegisterSdCard(cardVsn, newReg);
                }

                await _configWriter.SaveAsync();
            }

            await _dispatcher.InvokeAsync(() =>
            {
                var cameraVm = Cameras?.FirstOrDefault(c => c.CameraId == chosenCameraId);
                if (cameraVm is not null)
                {
                    cameraVm.ConnectedDriveLetter = driveLetter;
                    cameraVm.SetDriveConnected(driveLetter, cardVsn, true);
                    cameraVm.RefreshStorageSummary(_configWriter.Config);
                }

                if (!string.IsNullOrEmpty(cardVsn))
                {
                    var newReg = _configWriter.Config.SdCards.GetValueOrDefault(cardVsn);
                    if (newReg is not null)
                        DevicesTab?.AddSdCardEntry(cardVsn, newReg, driveLetter);
                    else
                        DevicesTab?.UpdateCardStatus(cardVsn, driveLetter, true);
                }
            });

            CameraViewModel? importCameraVm = null;
            await _dispatcher.InvokeAsync(() =>
                importCameraVm = Cameras?.FirstOrDefault(c => c.CameraId == chosenCameraId));

            if (importCameraVm is not null)
            {
                var configLabel = !string.IsNullOrEmpty(cardVsn)
                    ? _configWriter.Config.SdCards.GetValueOrDefault(cardVsn)?.Label
                    : null;
                await StartImportAsync(importCameraVm, rootPath, configLabel ?? volumeLabel);
            }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ShowCardDialogAsync failed for {Drive}", driveLetter);
            }
        }
        finally
        {
            _cardDialogSemaphore.Release();
        }
    }

    /// <summary>
    /// Starts imports for all cameras with SourceType=FixedPath where the path exists.
    /// Used by Quick Import and Watch initial scan.
    /// </summary>
    private void StartFixedPathImports()
    {
        var config = _configWriter.Config;

        foreach (var (cameraId, camConfig) in config.Cameras)
        {
            if (!camConfig.Enabled) continue;
            if (camConfig.SourceType != SourceType.FixedPath) continue;
            if (string.IsNullOrWhiteSpace(camConfig.SourcePath)) continue;
            if (!Directory.Exists(camConfig.SourcePath)) continue;

            var cameraVm = Cameras?.FirstOrDefault(c =>
                string.Equals(c.CameraId, cameraId, StringComparison.OrdinalIgnoreCase));

            if (cameraVm is not null)
            {
                var label = camConfig.FolderName ?? Path.GetFileName(camConfig.SourcePath.TrimEnd('\\', '/'));
                _ = StartImportAsync(cameraVm, camConfig.SourcePath, label);
            }
        }
    }

    private async Task StartMtpQuickImportsAsync()
    {
        try
        {

            var results = await _dispatcher.InvokeAsync(() => _mtpDetection!.DetectDevices());

            foreach (var result in results)
            {
                if (result.CameraId is null) continue;
                if (result.Outcome != MtpDetectionOutcome.Registered
                    && result.Outcome != MtpDetectionOutcome.ModelMatch) continue;

                CameraViewModel? cameraVm = null;
                await _dispatcher.InvokeAsync(() =>
                    cameraVm = Cameras?.FirstOrDefault(c =>
                        string.Equals(c.CameraId, result.CameraId, StringComparison.OrdinalIgnoreCase)));

                if (cameraVm is not null)
                    _ = StartMtpImportAsync(cameraVm, result.Device);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during MTP quick import scan");
        }
    }

    private async Task<ImportOrchestrationResult?> StartQuickImportDriveAsync(DetectedDrive drive)
    {
        try
        {
            var outcome = await _sdCardRegistry.LookupCameraIdAsync(drive.RootPath);
            if (outcome.Result != SdLookupResult.Matched || outcome.CameraId is null)
                return null;

            CameraViewModel? cameraVm = null;
            await _dispatcher.InvokeAsync(() =>
            {
                cameraVm = Cameras?.FirstOrDefault(c => c.CameraId == outcome.CameraId);
            });

            if (cameraVm is null) return null;

            var configLabel = outcome.MatchedVsn is not null
                ? _configWriter.Config.SdCards.GetValueOrDefault(outcome.MatchedVsn)?.Label
                : null;
            return await StartImportAsync(cameraVm, drive.RootPath, configLabel ?? drive.VolumeLabel);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during quick import for drive {Drive}", drive.RootPath);
            return null;
        }
    }

    /// <summary>
    /// Starts an import for a camera from the given source path.
    /// Creates a WpfProgressReporter, builds the ImportContext, and
    /// runs the import on a background thread. UI updates happen via the
    /// WpfProgressReporter using the captured Dispatcher.
    /// Returns the ImportOrchestrationResult on success, or null when skipped/cancelled/error.
    /// </summary>
    private async Task<ImportOrchestrationResult?> StartImportAsync(
        CameraViewModel cameraVm, string sourcePath, string? sourceLabel = null)
    {

        if (cameraVm.IsImporting)
        {
            _logger?.LogDebug("Import already running for {CameraId} — skipping", cameraVm.CameraId);
            return null;
        }

        if (!cameraVm.IsEnabled)
        {
            _logger?.LogDebug("Camera {CameraId} is disabled — skipping import", cameraVm.CameraId);
            return null;
        }

        var cts = new CancellationTokenSource();
        var reporter = new WpfProgressReporter(_dispatcher, cameraVm);

        await _dispatcher.InvokeAsync(() =>
        {
            cameraVm.ResetImportState();
            cameraVm.SetImportCts(cts);
            cameraVm.Phase = ImportPhase.Scanning;
            cameraVm.CurrentSourceLabel = sourceLabel;
        });

        lock (_activeCts)
        {
            _activeCts[cameraVm.CameraId] = cts;
        }
        UpdateHasActiveImports();

        try
        {
            var config = _configWriter.Config;
            var cameraConfig = config.Cameras[cameraVm.CameraId];

            if (_historyService is not null)
            {
                var folderName = cameraConfig.FolderName ?? cameraVm.CameraId;
                var removed = _historyService.ReconcileHistory(
                    cameraVm.CameraId, config.GlobalPaths.Workbench, folderName);
                if (removed > 0)
                    _logger?.LogInformation("[{Camera}] History reconciled: {Count} veraltete Einträge entfernt",
                        cameraVm.CameraId, removed);
            }

            var needsEisSorting = cameraVm.EditEisDetection;
            var context = ImportContextFactory.Create(
                cameraVm.CameraId, cameraConfig, sourcePath, config.GlobalPaths.Workbench,
                new GlobalSettings { Paths = config.GlobalPaths },
                injectGps: cameraVm.EditGps,
                stabilize: cameraVm.EditGyroflow,
                noEisSort: !needsEisSorting,
                renameVideos: cameraVm.EditRenameVideos,
                goProRename: cameraVm.EditGoProRename,
                postProcess: cameraVm.EditPostProcess);

            context.DateFrom = DateFrom;
            context.DateTo   = DateTo;

            _logger?.LogInformation("Starting import for {CameraId} from {Source}",
                cameraVm.CameraId, sourcePath);

            var result = await Task.Run(
                () => _orchestration.RunImportAsync(context, reporter, cts.Token, cameraVm.PauseEvent),
                cts.Token);

            await ApplyImportResultAsync(cameraVm, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Import cancelled for {CameraId}", cameraVm.CameraId);
            await _dispatcher.InvokeAsync(() =>
            {
                cameraVm.Phase = ImportPhase.Cancelled;
                cameraVm.PhaseLabel = Strings.Import_Cancelled;
                cameraVm.ResultText = Strings.Import_Cancelled;
                cameraVm.SpeedText = string.Empty;
                cameraVm.EtaText = string.Empty;
                cameraVm.CurrentFile = string.Empty;
            });
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during import for {CameraId}", cameraVm.CameraId);
            await ApplyMtpResultAsync(cameraVm, ImportPhase.Error, Strings.Common_Error, ex.Message);
            return null;
        }
        finally
        {
            lock (_activeCts)
            {
                _activeCts.Remove(cameraVm.CameraId);
            }

            cts.Dispose();
            await _dispatcher.InvokeAsync(() => cameraVm.SetImportCts(null));
            UpdateHasActiveImports();
        }
    }

    /// <summary>
    /// Runs a full MTP import for a single camera device via <see cref="IMtpImportService"/>.
    /// Direct-Download into the Workbench target — NO staging directory.
    /// Runs fire-and-forget; multiple cameras can import in parallel.
    /// </summary>
    private async Task StartMtpImportAsync(CameraViewModel cameraVm, MtpDeviceInfo device)
    {
        if (cameraVm.IsImporting) return;
        if (!cameraVm.IsEnabled)
        {
            _logger?.LogDebug("Camera {CameraId} is disabled — skipping MTP import", cameraVm.CameraId);
            return;
        }
        if (_mtpImportService is null) return;

        Interlocked.Increment(ref _activeMtpImports);

        var cts = new CancellationTokenSource();

        await _dispatcher.InvokeAsync(() =>
        {
            cameraVm.ResetImportState();
            cameraVm.SetImportCts(cts);
            cameraVm.Phase = ImportPhase.Scanning;
        });

        lock (_activeCts) { _activeCts[cameraVm.CameraId] = cts; }
        UpdateHasActiveImports();

        try
        {
            var config = _configWriter.Config;
            var cameraConfig = config.Cameras[cameraVm.CameraId];

            var request = new MtpImportRequest(
                CameraId: cameraVm.CameraId,
                CameraConfig: cameraConfig,
                Device: device,
                WorkbenchPath: config.GlobalPaths.Workbench,
                GlobalSettings: new GlobalSettings { Paths = config.GlobalPaths },
                Stabilize: cameraVm.EditGyroflow,
                InjectGps: cameraVm.EditGps,
                RenameVideos: cameraVm.EditRenameVideos,
                GoProRename: cameraVm.EditGoProRename,
                EisDetection: cameraVm.EditEisDetection,
                PostProcess: cameraVm.EditPostProcess,
                DateFrom: DateFrom,
                DateTo: DateTo);

            var progress = new Progress<MtpImportProgress>(p =>
                _dispatcher.InvokeAsync(() =>
                {
                    cameraVm.Phase = p.Phase == "Downloading" ? ImportPhase.Copying : ImportPhase.Scanning;
                    cameraVm.ProgressPercent = p.Total > 0 ? (double)p.Current / p.Total * 100 : 0;
                    cameraVm.CurrentFile = p.CurrentFile;
                    cameraVm.PhaseLabel = string.Format(Strings.Import_MtpPhaseProgress, p.Phase, p.Current, p.Total);
                }));

            var result = await _mtpImportService!.ImportAsync(request, progress, cts.Token);

            await _dispatcher.InvokeAsync(() =>
            {
                cameraVm.Phase = result.Failed == 0 ? ImportPhase.Done : ImportPhase.Error;
                cameraVm.PhaseLabel = result.Failed == 0 ? Strings.Common_Done : string.Format(Strings.Import_MtpErrorFailed, result.Failed);
                cameraVm.ResultText = string.Format(Strings.Import_MtpResultFiles, result.Downloaded, result.TotalBytes / (1024 * 1024));
                cameraVm.SpeedText = string.Empty;
                cameraVm.EtaText = string.Empty;
                cameraVm.CurrentFile = string.Empty;
            });
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("MTP import cancelled for {Camera}", cameraVm.CameraId);
            await _dispatcher.InvokeAsync(() =>
            {
                cameraVm.Phase = ImportPhase.Cancelled;
                cameraVm.PhaseLabel = Strings.Common_Cancelled;
                cameraVm.ResultText = Strings.Import_Cancelled;
                cameraVm.SpeedText = string.Empty;
                cameraVm.EtaText = string.Empty;
                cameraVm.CurrentFile = string.Empty;
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during MTP import for {Camera}", cameraVm.CameraId);
            await ApplyMtpResultAsync(cameraVm, ImportPhase.Error, Strings.Common_Error, ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref _activeMtpImports);
            lock (_activeCts) { _activeCts.Remove(cameraVm.CameraId); }
            cts.Dispose();
            await _dispatcher.InvokeAsync(() => cameraVm.SetImportCts(null));
            UpdateHasActiveImports();

        }
    }

    /// <summary>
    /// Applies a simple phase/label/result update to the camera card on the dispatcher.
    /// Used for early-exit paths in StartMtpImportAsync (no files, download failed, etc.).
    /// </summary>
    private async Task ApplyMtpResultAsync(CameraViewModel cameraVm, ImportPhase phase, string label, string resultText)
    {
        await _dispatcher.InvokeAsync(() =>
        {
            cameraVm.Phase      = phase;
            cameraVm.PhaseLabel = label;
            cameraVm.ResultText = resultText;
            cameraVm.SpeedText  = string.Empty;
            cameraVm.EtaText    = string.Empty;
            cameraVm.CurrentFile = string.Empty;
        });
    }

    /// <summary>
    /// Applies an <see cref="ImportOrchestrationResult"/> to the camera card on the dispatcher.
    /// Shared between StartImportAsync and StartMtpImportAsync (DRY).
    /// </summary>
    private async Task ApplyImportResultAsync(CameraViewModel cameraVm, ImportOrchestrationResult result)
    {
        if (result.Success)
        {
            _logger?.LogInformation(
                "Import complete for {CameraId}: {Files} files, {Bytes} bytes in {Duration}",
                cameraVm.CameraId, result.FilesCopied, result.BytesCopied, result.Duration);

            var sizeGb   = result.BytesCopied / (1024.0 * 1024.0 * 1024.0);
            var sizeText = sizeGb >= 1.0
                ? $"{sizeGb:F1} GB"
                : $"{result.BytesCopied / (1024.0 * 1024.0):F0} MB";
            var duration = result.Duration;
            var durationText = duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                : $"{duration.Minutes}:{duration.Seconds:D2}";
            var summary = string.Format(Strings.Import_FileSummary, result.FilesCopied, sizeText, durationText);

            await _dispatcher.InvokeAsync(() =>
            {
                cameraVm.Phase      = ImportPhase.Done;
                cameraVm.PhaseLabel = Strings.Common_Done;
                cameraVm.ResultText = summary;
                cameraVm.SpeedText  = string.Empty;
                cameraVm.EtaText    = string.Empty;
                cameraVm.CurrentFile = string.Empty;
            });
        }
        else
        {
            var errorMsg = result.ErrorMessage ?? Strings.Import_Failed;
            _logger?.LogError("Import failed for {CameraId}: {Error}", cameraVm.CameraId, errorMsg);

            await _dispatcher.InvokeAsync(() =>
            {
                cameraVm.Phase      = ImportPhase.Error;
                cameraVm.PhaseLabel = Strings.Common_Error;
                cameraVm.ResultText = errorMsg;
                cameraVm.SpeedText  = string.Empty;
                cameraVm.EtaText    = string.Empty;
                cameraVm.CurrentFile = string.Empty;
            });
        }
    }

    private void ExecuteCancelAll()
    {
        List<CancellationTokenSource> toCancel;
        lock (_activeCts)
        {
            toCancel = _activeCts.Values.ToList();
        }

        foreach (var cts in toCancel)
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) {  }
        }

        _logger?.LogInformation("Cancel All requested — {Count} active import(s) cancelled",
            toCancel.Count);
    }

    private void UpdateHasActiveImports()
    {
        bool hasActive;
        lock (_activeCts)
        {
            hasActive = _activeCts.Count > 0;
        }

        _dispatcher.InvokeAsync(() =>
        {
            HasActiveImports = hasActive;
            UpdateIsAnyPaused();
        });
    }

    private void ExecutePauseResumeAll()
    {
        var activeCameras = Cameras?.Where(c => c.IsImporting).ToList();
        if (activeCameras is null || activeCameras.Count == 0) return;

        bool anyRunning = activeCameras.Any(c => !c.IsPaused);
        foreach (var cam in activeCameras)
        {
            if (anyRunning)
            {
                cam.PauseEvent.Reset();
                cam.IsPaused = true;
            }
            else
            {
                cam.PauseEvent.Set();
                cam.IsPaused = false;
            }
        }
        UpdateIsAnyPaused();
    }

    private void UpdateIsAnyPaused()
    {

        IsAnyPaused = Cameras?.Any(c => c.IsPaused) ?? false;
    }

    public void Dispose()
    {
        if (_driveWatcher is not null)
        {
            _driveWatcher.DriveArrived -= OnDriveStatusChanged;
            _driveWatcher.DriveRemoved -= OnDriveRemoved;
            _driveWatcher.StopWatching();
        }

        _mtpPollingCts?.Cancel();
        _mtpPollingCts?.Dispose();
        _mtpPollingCts = null;

        ExecuteCancelAll();

        lock (_activeCts)
        {
            foreach (var cts in _activeCts.Values)
            {
                try { cts.Dispose(); } catch {  }
            }
            _activeCts.Clear();
        }

        _cardDialogSemaphore.Dispose();
    }
}
