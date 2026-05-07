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

using System.Management;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Event-Args für Laufwerks-Änderungen.
/// </summary>
public class DriveChangedEventArgs : EventArgs
{
    public required string DriveLetter { get; init; }
    public required string RootPath { get; init; }
    public string? VolumeLabel { get; init; }
    public long TotalSizeBytes { get; init; }
}

/// <summary>
/// Event-Args für Ordner-Änderungen (Fixed-Path Quellen).
/// </summary>
public class FolderChangedEventArgs : EventArgs
{
    /// <summary>Kamera-ID dieser Fixed-Path Quelle.</summary>
    public required string CameraId { get; init; }

    /// <summary>Vollständiger Pfad des überwachten Ordners.</summary>
    public required string FolderPath { get; init; }
}

/// <summary>
/// Service für Event-basierte Laufwerks-Überwachung (Windows WMI).
/// Ersetzt Polling durch echte OS-Events – 0% CPU im Idle.
/// Unterstützt zusätzlich FileSystemWatcher für Fixed-Path Quellen.
/// </summary>
public interface IDriveWatcherService : IDisposable
{
    /// <summary>
    /// Feuert wenn ein neuer Removable Drive erkannt wird.
    /// EventArgs enthält Drive-Info (Letter, Label, Size).
    /// WICHTIG: Event kommt auf WMI-Thread, nicht auf UI-Thread!
    /// </summary>
    event EventHandler<DriveChangedEventArgs>? DriveArrived;

    /// <summary>
    /// Feuert wenn ein Removable Drive entfernt wird.
    /// </summary>
    event EventHandler<DriveChangedEventArgs>? DriveRemoved;

    /// <summary>
    /// Feuert wenn neue Dateien in einem überwachten Fixed-Path Ordner erscheinen.
    /// Debounce: 5 Sekunden nach letzter Änderung.
    /// </summary>
    event EventHandler<FolderChangedEventArgs>? FolderChanged;

    /// <summary>Überwachung aktiv?</summary>
    bool IsWatching { get; }

    /// <summary>Startet die Überwachung (WMI Events).</summary>
    void StartWatching();

    /// <summary>Stoppt die Überwachung und räumt auf.</summary>
    void StopWatching();

    /// <summary>
    /// Gibt alle aktuell verbundenen Removable Drives zurück (Snapshot).
    /// Delegiert an DriveDetectionService.
    /// </summary>
    List<DetectedDrive> GetCurrentDrives();

    /// <summary>
    /// Startet FileSystemWatcher für einen Fixed-Path Ordner.
    /// FolderChanged wird gefeuert wenn neue Dateien erscheinen (Debounce: 5s).
    /// </summary>
    void WatchFolder(string cameraId, string path);

    /// <summary>
    /// Stoppt den FileSystemWatcher für eine Kamera.
    /// </summary>
    void UnwatchFolder(string cameraId);
}

/// <summary>
/// Implementierung für Windows (WMI-basiert).
/// </summary>
[SupportedOSPlatform("windows")]
public class DriveWatcherService : IDriveWatcherService
{
    private ManagementEventWatcher? _arrivalWatcher;
    private ManagementEventWatcher? _removalWatcher;
    private readonly IDriveDetectionService _driveDetectionService;
    private readonly ILogger<DriveWatcherService>? _logger;

    private Task? _pollingTask;
    private CancellationTokenSource? _pollingCts;
    private readonly Dictionary<string, (string? VolumeLabel, bool IsReady)> _driveStates = new();

    private readonly Dictionary<string, FileSystemWatcher> _folderWatchers = new();

    private readonly Dictionary<string, System.Timers.Timer> _debounceTimers = new();
    private const double DebounceMs = 5000;

    public event EventHandler<DriveChangedEventArgs>? DriveArrived;
    public event EventHandler<DriveChangedEventArgs>? DriveRemoved;
    public event EventHandler<FolderChangedEventArgs>? FolderChanged;

    public bool IsWatching { get; private set; }

    public DriveWatcherService(
        IDriveDetectionService driveDetectionService,
        ILogger<DriveWatcherService>? logger = null)
    {
        _driveDetectionService = driveDetectionService;
        _logger = logger;
    }

    public void StartWatching()
    {
        if (IsWatching)
        {
            _logger?.LogDebug("Drive-Watcher läuft bereits");
            return;
        }

        try
        {
            // Win32_VolumeChangeEvent fires for every volume mount/unmount, including
            // SD-card swaps inside a card reader that is already connected. The previous
            // query (__InstanceCreationEvent on Win32_LogicalDisk DriveType=2) only fires
            // when a *new* Logical-Disk object is created — for a permanently mounted
            // card reader the drive letter persists across card swaps, so the creation
            // event never fired and arrivals were missed.
            var arrivalQuery = new WqlEventQuery(
                "SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
            _arrivalWatcher = new ManagementEventWatcher(arrivalQuery);
            _arrivalWatcher.EventArrived += OnVolumeArrived;
            _arrivalWatcher.Start();

            var removalQuery = new WqlEventQuery(
                "SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3");
            _removalWatcher = new ManagementEventWatcher(removalQuery);
            _removalWatcher.EventArrived += OnVolumeRemoved;
            _removalWatcher.Start();

            // Initial scan: fire DriveArrived for already-mounted removable drives so
            // subscribers that only listen for events see the current state. Has to run
            // BEFORE the polling task starts so _driveStates is pre-populated and the
            // polling loop's first tick treats every existing drive as "known, no change".
            EmitInitialDriveArrivals();

            _pollingCts = new CancellationTokenSource();
            _pollingTask = Task.Run(() => PollDriveChangesAsync(_pollingCts.Token), _pollingCts.Token);

            IsWatching = true;
            _logger?.LogInformation("Drive-Watcher gestartet (Win32_VolumeChangeEvent + initial scan + polling)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Starten des Drive-Watchers");
            StopWatching();
            throw;
        }
    }

    /// <summary>
    /// Emits <see cref="DriveArrived"/> for every removable drive that is already mounted
    /// when the watcher starts. Pre-populates <see cref="_driveStates"/> so the polling
    /// loop won't re-fire on its first tick.
    /// </summary>
    private void EmitInitialDriveArrivals()
    {
        try
        {
            foreach (var drive in _driveDetectionService.GetRemovableDrives())
            {
                _driveStates[drive.DriveLetter] = (drive.VolumeLabel, drive.IsReady);

                if (drive.IsReady && drive.TotalSizeBytes > 0)
                {
                    _logger?.LogInformation("Drive bereits eingesteckt: {Letter} ({Label}, {Size:N0} bytes)",
                        drive.DriveLetter, drive.VolumeLabel ?? "(kein Label)", drive.TotalSizeBytes);

                    DriveArrived?.Invoke(this, new DriveChangedEventArgs
                    {
                        DriveLetter = drive.DriveLetter,
                        RootPath = drive.RootPath,
                        VolumeLabel = drive.VolumeLabel,
                        TotalSizeBytes = drive.TotalSizeBytes
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Initial drive scan fehlgeschlagen");
        }
    }

    public void StopWatching()
    {
        if (!IsWatching) return;

        try
        {
            _arrivalWatcher?.Stop();
            _arrivalWatcher?.Dispose();
            _arrivalWatcher = null;

            _removalWatcher?.Stop();
            _removalWatcher?.Dispose();
            _removalWatcher = null;

            _pollingCts?.Cancel();
            _pollingTask?.Wait(TimeSpan.FromSeconds(1));
            _pollingCts?.Dispose();
            _pollingCts = null;
            _pollingTask = null;

            foreach (var key in _folderWatchers.Keys.ToList())
            {
                UnwatchFolder(key);
            }

            IsWatching = false;
            _logger?.LogInformation("Drive-Watcher gestoppt");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Stoppen des Drive-Watchers");
        }
    }

    public List<DetectedDrive> GetCurrentDrives()
    {

        return _driveDetectionService.GetRemovableDrives();
    }

    public void WatchFolder(string cameraId, string path)
    {
        if (_folderWatchers.ContainsKey(cameraId))
        {
            _logger?.LogDebug("Ordner-Watcher für {Camera} läuft bereits", cameraId);
            return;
        }

        if (!Directory.Exists(path))
        {
            _logger?.LogWarning("Ordner existiert nicht, kein Watcher gestartet: {Path}", path);
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            watcher.Created += (s, e) => OnFolderActivity(cameraId, path);
            watcher.Renamed += (s, e) => OnFolderActivity(cameraId, path);
            watcher.Error += (s, e) => _logger?.LogWarning("FileSystemWatcher Fehler für {Camera}: {Error}",
                cameraId, e.GetException().Message);

            _folderWatchers[cameraId] = watcher;
            _logger?.LogInformation("Ordner-Watcher gestartet: {Camera} → {Path}", cameraId, path);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Starten des Ordner-Watchers für {Camera}", cameraId);
        }
    }

    public void UnwatchFolder(string cameraId)
    {
        if (_folderWatchers.TryGetValue(cameraId, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _folderWatchers.Remove(cameraId);
        }

        if (_debounceTimers.TryGetValue(cameraId, out var timer))
        {
            timer.Stop();
            timer.Dispose();
            _debounceTimers.Remove(cameraId);
        }

        _logger?.LogInformation("Ordner-Watcher gestoppt: {Camera}", cameraId);
    }

    /// <summary>
    /// Debounce-Handler: Startet/resettet den Timer für eine Kamera.
    /// FolderChanged wird erst nach 5s Inaktivität gefeuert.
    /// </summary>
    private void OnFolderActivity(string cameraId, string folderPath)
    {
        lock (_debounceTimers)
        {
            if (_debounceTimers.TryGetValue(cameraId, out var existing))
            {

                existing.Stop();
                existing.Start();
            }
            else
            {

                var timer = new System.Timers.Timer(DebounceMs) { AutoReset = false };
                timer.Elapsed += (s, e) =>
                {
                    _logger?.LogDebug("Ordner-Aktivität erkannt (nach Debounce): {Camera}", cameraId);
                    FolderChanged?.Invoke(this, new FolderChangedEventArgs
                    {
                        CameraId = cameraId,
                        FolderPath = folderPath
                    });

                    lock (_debounceTimers)
                    {
                        if (_debounceTimers.TryGetValue(cameraId, out var t))
                        {
                            t.Dispose();
                            _debounceTimers.Remove(cameraId);
                        }
                    }
                };
                _debounceTimers[cameraId] = timer;
                timer.Start();
            }
        }
    }

    /// <summary>
    /// Handler for Win32_VolumeChangeEvent EventType=2 (arrival).
    /// The event itself only carries DriveName ("G:") — size and volume label are
    /// queried via DriveInfo. Filters out non-removable drives so we don't surface
    /// fixed disks or network shares to subscribers.
    /// </summary>
    private void OnVolumeArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var driveName = e.NewEvent["DriveName"]?.ToString();
            if (string.IsNullOrEmpty(driveName)) return;
            var driveLetter = driveName.TrimEnd('\\');

            DriveInfo? drive;
            try { drive = new DriveInfo(driveLetter); }
            catch { return; }

            if (!drive.IsReady) return;
            if (drive.DriveType != DriveType.Removable) return;

            long size = 0;
            try { size = drive.TotalSize; }
            catch (Exception ex) { _logger?.LogDebug(ex, "TotalSize nicht lesbar für {Drive}", driveLetter); }
            if (size <= 0) return;

            string label = "";
            try { label = drive.VolumeLabel ?? ""; }
            catch (Exception ex) { _logger?.LogDebug(ex, "VolumeLabel nicht lesbar für {Drive}", driveLetter); }

            _driveStates[driveLetter] = (label, true);
            _logger?.LogInformation("Drive eingesteckt: {Letter} ({Label}, {Size:N0} bytes)",
                driveLetter, label, size);

            DriveArrived?.Invoke(this, new DriveChangedEventArgs
            {
                DriveLetter = driveLetter,
                RootPath = drive.RootDirectory.FullName,
                VolumeLabel = label,
                TotalSizeBytes = size
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Verarbeiten von VolumeArrival Event");
        }
    }

    /// <summary>
    /// Handler for Win32_VolumeChangeEvent EventType=3 (removal).
    /// We can't query DriveInfo here — the drive is already gone. Use the cached
    /// state to filter: only emit DriveRemoved for drives we previously tracked
    /// as removable, ignore unmounts of fixed disks or network shares.
    /// </summary>
    private void OnVolumeRemoved(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var driveName = e.NewEvent["DriveName"]?.ToString();
            if (string.IsNullOrEmpty(driveName)) return;
            var driveLetter = driveName.TrimEnd('\\');

            if (!_driveStates.ContainsKey(driveLetter)) return;
            _driveStates.Remove(driveLetter);

            _logger?.LogInformation("Drive entfernt: {Letter}", driveLetter);
            DriveRemoved?.Invoke(this, new DriveChangedEventArgs
            {
                DriveLetter = driveLetter,
                RootPath = $"{driveLetter}\\",
                VolumeLabel = null,
                TotalSizeBytes = 0
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Verarbeiten von VolumeRemoval Event");
        }
    }

    /// <summary>
    /// Fix 3: Polling-Loop für Karten-Wechsel in bestehenden Cardreadern.
    /// Prüft alle 4 Sekunden alle Removable Drives auf IsReady + VolumeLabel Änderungen.
    /// </summary>
    private async Task PollDriveChangesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4), ct);

                var allDrives = DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Removable)
                    .ToList();

                foreach (var drive in allDrives)
                {
                    var driveLetter = drive.Name.TrimEnd('\\');
                    var currentLabel = drive.IsReady ? drive.VolumeLabel : null;
                    var currentIsReady = drive.IsReady;

                    if (_driveStates.TryGetValue(driveLetter, out var previousState))
                    {

                        bool labelChanged = previousState.VolumeLabel != currentLabel;
                        bool readyChanged = previousState.IsReady != currentIsReady;

                        if (readyChanged || labelChanged)
                        {

                            _driveStates[driveLetter] = (currentLabel, currentIsReady);

                            if (currentIsReady && drive.TotalSize > 0)
                            {
                                _logger?.LogDebug("Karten-Wechsel erkannt via Polling: {Drive} (Label: {Label})",
                                    driveLetter, currentLabel ?? "(kein Label)");

                                var eventArgs = new DriveChangedEventArgs
                                {
                                    DriveLetter = driveLetter,
                                    RootPath = drive.Name,
                                    VolumeLabel = currentLabel,
                                    TotalSizeBytes = drive.TotalSize
                                };

                                DriveArrived?.Invoke(this, eventArgs);
                            }

                            else if (!currentIsReady && previousState.IsReady)
                            {
                                var eventArgs = new DriveChangedEventArgs
                                {
                                    DriveLetter = driveLetter,
                                    RootPath = drive.Name,
                                    VolumeLabel = null,
                                    TotalSizeBytes = 0
                                };

                                DriveRemoved?.Invoke(this, eventArgs);
                            }
                        }
                    }
                    else
                    {
                        _driveStates[driveLetter] = (currentLabel, currentIsReady);

                        // First time the polling loop sees this drive. Normally WMI volume
                        // events have already fired (and EmitInitialDriveArrivals pre-populated
                        // the state for already-mounted drives), so this branch only triggers
                        // when WMI missed an event. Emit DriveArrived as a safety net so
                        // subscribers never lose track of a removable drive.
                        if (currentIsReady && drive.TotalSize > 0)
                        {
                            _logger?.LogDebug("Drive via Polling-Fallback erkannt: {Drive}", driveLetter);
                            DriveArrived?.Invoke(this, new DriveChangedEventArgs
                            {
                                DriveLetter = driveLetter,
                                RootPath = drive.Name,
                                VolumeLabel = currentLabel,
                                TotalSizeBytes = drive.TotalSize
                            });
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {

                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler im Drive-Polling");
            }
        }
    }

    public void Dispose()
    {
        StopWatching();
        GC.SuppressFinalize(this);
    }
}
