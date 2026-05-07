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

using System.CommandLine;
using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using UMI.Cameras;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.CLI.Services;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.Data;

namespace UMI.CLI.Commands;

/// <summary>
/// Watch Command - Überwacht Laufwerke und importiert automatisch bei SD-Karten-Erkennung
/// </summary>
[SupportedOSPlatform("windows")]
public static class WatchCommand
{
    public static Command Create()
    {
        var command = new Command("watch", "Watch drives and auto-import when SD cards are detected");

        var cameraOption = new Option<string?>(
            "--camera",
            "Fixed mode: import every card as this camera (e.g. --camera MyDSLR)");

        var stabilizeOption = new Option<bool>(
            "--stabilize",
            "Enable Gyroflow (overrides profile)");

        var renameVideosOption = new Option<bool>(
            "--rename-videos",
            getDefaultValue: () => false,
            "Rename videos with timestamp prefix (yyyyMMdd_HHmmss_OriginalName); overrides rename_videos from config");

        var goProRenameOption = new Option<bool>(
            "--gopro-rename",
            getDefaultValue: () => false,
            "Rename GoPro files to sortable names (GoPro_0001_c01.MP4); overrides gopro_rename from config");

        command.AddOption(cameraOption);
        command.AddOption(stabilizeOption);
        command.AddOption(renameVideosOption);
        command.AddOption(goProRenameOption);

        command.SetHandler(async (context) =>
        {
            var configPath   = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            var camera       = context.ParseResult.GetValueForOption(cameraOption);
            var profile      = context.ParseResult.GetValueForOption(Program.ProfileOption);
            var stabilize    = context.ParseResult.GetValueForOption(stabilizeOption);
            var dryRun       = context.ParseResult.GetValueForOption(Program.DryRunOption);
            var verbose      = context.ParseResult.GetValueForOption(Program.VerboseOption);
            var quiet        = context.ParseResult.GetValueForOption(Program.QuietOption);
            var renameVideos = context.ParseResult.GetValueForOption(renameVideosOption);
            var goProRename  = context.ParseResult.GetValueForOption(goProRenameOption);

            await ExecuteAsync(configPath, camera, profile, stabilize, dryRun, verbose, quiet, renameVideos, goProRename);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        string configPath,
        string? cameraId,
        string? profileName,
        bool stabilize,
        bool dryRun,
        bool verbose,
        bool quiet,
        bool renameVideos = false,
        bool goProRename = false)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath: configPath, profileName: profileName);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var driveWatcher = serviceProvider.GetRequiredService<IDriveWatcherService>();
        var fingerprintService = serviceProvider.GetRequiredService<ISdFingerprintService>();
        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();
        var mtpDetectionService = serviceProvider.GetRequiredService<IMtpDeviceDetectionService>();
        var logger = serviceProvider.GetService<ILogger<Program>>();

        var mode = DetermineMode(cameraId, config);

        var interactive = ConsoleHelper.IsInteractiveTerminal();
        var dashboard = new DashboardRenderer("UMI - Drive-Watcher", interactive);

        var profileStr = !string.IsNullOrEmpty(profileName) ? profileName : "Default";
        var modeDescription = BuildModeDescription(mode, config);
        dashboard.SetModeInfo(
            $"👁 Drive-Watcher active (Mode: {modeDescription})",
            $"  Profile: {profileStr}",
            "  Ctrl+C to exit");

        if (mode is WatchMode.Fixed fixedMode && !config.Cameras.ContainsKey(fixedMode.CameraId))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotInConfig, fixedMode.CameraId));
            return;
        }

        var detectionQueue = new Queue<CardDetectionHelper.DetectionEvent>();
        var resolvedImports = new Queue<CardDetectionHelper.ResolvedImport>();
        var folderQueue = new Queue<(string CameraId, string FolderPath)>();
        var knownMtpDevices = new HashSet<string>();
        var activeImport = 0;
        var currentCameraId = cameraId;
        var importedDrivesThisSession = new HashSet<string>();

        var driveSlotMap = new Dictionary<string, int>();

        var fixedPathCameras = config.Cameras
            .Where(kvp => kvp.Value.Enabled
                && kvp.Value.SourceType == SourceType.FixedPath
                && !string.IsNullOrEmpty(kvp.Value.SourcePath))
            .ToList();

        var mtpCameras = config.Cameras
            .Where(kvp => kvp.Value.Enabled && kvp.Value.SourceType == SourceType.MTP)
            .ToList();

        var sourceLines = new List<string>();

        if (mode is WatchMode.Fixed fixedModeForSources)
        {
            sourceLines.Add($"  Fixed mode: All cards → {fixedModeForSources.CameraId}");
        }

        if (fixedPathCameras.Any())
        {
            sourceLines.Add("  Fixed-Path sources:");
            foreach (var (camId, camCfg) in fixedPathCameras)
            {
                sourceLines.Add($"  📁 {camId} → {camCfg.SourcePath}");
            }
        }

        if (mtpCameras.Any())
        {
            sourceLines.Add("  MTP sources (polling every 5s):");
            foreach (var (camId, camCfg) in mtpCameras)
                sourceLines.Add($"  📱 {camId} → {camCfg.Name}");
        }

        if (mode is WatchMode.Fixed && config.Cameras.Any(c => c.Value.Enabled))
        {
            sourceLines.Add("  Hotkeys:");
            var cameras = config.Cameras.Where(c => c.Value.Enabled).Take(9).ToList();
            for (int i = 0; i < cameras.Count; i++)
                sourceLines.Add($"    [{i + 1}] → {cameras[i].Key}");
        }

        dashboard.SetConfiguredSources(sourceLines.ToArray());

        dashboard.RenderHeader();

        foreach (var (camId, camCfg) in fixedPathCameras)
        {
            driveWatcher.WatchFolder(camId, camCfg.SourcePath!);

            lock (folderQueue)
            {
                folderQueue.Enqueue((camId, camCfg.SourcePath!));
            }
        }

        driveWatcher.DriveArrived += (sender, e) =>
        {

            try
            {
                var driveInfo = new DriveInfo(e.DriveLetter);
                if (!driveInfo.IsReady || e.TotalSizeBytes == 0)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            int driveSlotId;
            lock (detectionQueue)
            {
                detectionQueue.Enqueue(new CardDetectionHelper.DetectionEvent { DrivePath = e.RootPath, DriveEvent = e });
                driveSlotId = dashboard.AddSlot(e.RootPath, "SD", DashboardRenderer.SlotPhase.Detected,
                    $"{e.VolumeLabel ?? CliStrings.Common_NoLabel}, {FormatHelper.FormatBytes(e.TotalSizeBytes)}");
            }
            lock (driveSlotMap) driveSlotMap[e.RootPath] = driveSlotId;
        };

        driveWatcher.DriveRemoved += (sender, e) =>
        {
            if (verbose)
            {
                logger?.LogDebug("Karte entfernt: {Drive}", e.DriveLetter);
            }
        };

        driveWatcher.FolderChanged += (sender, e) =>
        {
            int folderSlotId;
            lock (folderQueue)
            {

                if (folderQueue.Any(q => q.CameraId == e.CameraId))
                    return;

                folderQueue.Enqueue((e.CameraId, e.FolderPath));
                folderSlotId = dashboard.AddSlot(e.CameraId, "Path", DashboardRenderer.SlotPhase.Detected, e.FolderPath);
            }
            lock (driveSlotMap) driveSlotMap[e.CameraId] = folderSlotId;
        };

        driveWatcher.StartWatching();

        var existingDrives = driveWatcher.GetCurrentDrives()
            .Where(d => d.IsReady && d.TotalSizeBytes > 0)
            .ToList();

        if (existingDrives.Count > 0)
        {
            foreach (var drive in existingDrives)
            {
                var eventArgs = new DriveChangedEventArgs
                {
                    DriveLetter = drive.DriveLetter,
                    RootPath = drive.RootPath,
                    VolumeLabel = drive.VolumeLabel,
                    TotalSizeBytes = drive.TotalSizeBytes
                };
                detectionQueue.Enqueue(new CardDetectionHelper.DetectionEvent { DrivePath = drive.RootPath, DriveEvent = eventArgs });
                var slotId = dashboard.AddSlot(drive.RootPath, "SD", DashboardRenderer.SlotPhase.Detected,
                    $"{drive.VolumeLabel ?? CliStrings.Common_NoLabel}, {FormatHelper.FormatBytes(drive.TotalSizeBytes)} – {CliStrings.Quick_AlreadyInserted}");
                lock (driveSlotMap) driveSlotMap[drive.RootPath] = slotId;
            }
        }

        if (mtpCameras.Any())
        {
            try
            {
                var initialDetections = mtpDetectionService.DetectDevices();
                foreach (var detection in initialDetections)
                {
                    if (detection.Outcome != MtpDetectionOutcome.Registered) continue;
                    var deviceKey = MtpDeviceDetectionService.GetDeviceKey(detection.Device);
                    knownMtpDevices.Add(deviceKey);
                    detectionQueue.Enqueue(new CardDetectionHelper.DetectionEvent { MtpDetection = detection });
                    var slotId = dashboard.AddSlot(detection.CameraId!, "MTP", DashboardRenderer.SlotPhase.Detected,
                        detection.Device.FriendlyName + $" ({CliStrings.Quick_AlreadyConnected})");
                    lock (driveSlotMap) driveSlotMap[deviceKey] = slotId;
                }
            }
            catch (Exception ex) { logger?.LogDebug(ex, "MTP Initial-Poll Fehler"); }
        }

        if (detectionQueue.Count > 0)
        {
            dashboard.BeginDialog();
            await CardDetectionHelper.ProcessDetectionQueueAsync(
                detectionQueue, resolvedImports,
                fixedCameraId: mode is WatchMode.Fixed ? currentCameraId : null,
                config, fingerprintService, configWriter, importedDrivesThisSession, verbose, ct);

            foreach (var resolved in resolvedImports)
            {
                var key = resolved.IsMtp
                    ? (resolved.MtpDetection != null ? MtpDeviceDetectionService.GetDeviceKey(resolved.MtpDetection.Device) : resolved.SourcePath)
                    : resolved.SourcePath;
                int resolvedSlotId;
                lock (driveSlotMap)
                {
                    if (driveSlotMap.TryGetValue(key, out resolvedSlotId))
                    {
                        var sourceType = resolved.IsMtp ? "MTP" : "SD";
                        var displayKey = resolved.IsMtp && resolved.MtpDetection != null
                            ? resolved.MtpDetection.Device.FriendlyName
                            : resolved.SourcePath;
                        var cameraLabel = $"{displayKey} \u2192 {resolved.CameraId} ({sourceType})";
                        dashboard.UpdateSlot(resolvedSlotId, DashboardRenderer.SlotPhase.Queued, label: cameraLabel);
                    }
                }
            }
            dashboard.EndDialog();
        }

        dashboard.SetStatusLine($"👁 {CliStrings.Quick_WaitingForCards}");

        try
        {
            var mtpPollStopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (!ct.IsCancellationRequested)
            {

                if (mode is WatchMode.Fixed && !Console.IsInputRedirected && Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    var newCamera = HandleHotkey(key, config);
                    if (newCamera != null)
                    {
                        currentCameraId = newCamera;
                        dashboard.SetStatusLine($"👁 {string.Format(CliStrings.Watch_WaitingForNextCardCamera, currentCameraId)}");
                    }
                }

                if (mtpCameras.Any() && mtpPollStopwatch.ElapsedMilliseconds >= 5000
                    && Interlocked.CompareExchange(ref activeImport, 0, 0) == 0)
                {
                    mtpPollStopwatch.Restart();
                    try
                    {
                        var detections = mtpDetectionService.DetectDevices();

                        foreach (var detection in detections)
                        {
                            if (detection.Outcome != MtpDetectionOutcome.Registered)
                                continue;

                            var deviceKey = MtpDeviceDetectionService.GetDeviceKey(detection.Device);
                            if (knownMtpDevices.Add(deviceKey))
                            {

                                var slotId = dashboard.AddSlot(detection.CameraId!, "MTP", DashboardRenderer.SlotPhase.Detected,
                                    detection.Device.FriendlyName);
                                lock (driveSlotMap) driveSlotMap[deviceKey] = slotId;

                                lock (detectionQueue)
                                    detectionQueue.Enqueue(new CardDetectionHelper.DetectionEvent { MtpDetection = detection });
                            }
                        }

                        var currentKeys = detections
                            .Select(d => MtpDeviceDetectionService.GetDeviceKey(d.Device))
                            .ToHashSet();
                        knownMtpDevices.IntersectWith(currentKeys);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogDebug(ex, "MTP Polling Fehler");
                    }
                }

                if (detectionQueue.Count > 0)
                {
                    dashboard.BeginDialog();
                    await CardDetectionHelper.ProcessDetectionQueueAsync(
                        detectionQueue, resolvedImports,
                        fixedCameraId: mode is WatchMode.Fixed ? currentCameraId : null,
                        config, fingerprintService, configWriter, importedDrivesThisSession, verbose, ct);

                    foreach (var resolved in resolvedImports)
                    {
                        var key = resolved.IsMtp
                            ? (resolved.MtpDetection != null ? MtpDeviceDetectionService.GetDeviceKey(resolved.MtpDetection.Device) : resolved.SourcePath)
                            : resolved.SourcePath;
                        int resolvedSlotId;
                        lock (driveSlotMap)
                        {
                            if (driveSlotMap.TryGetValue(key, out resolvedSlotId))
                            {
                                var sourceType = resolved.IsMtp ? "MTP" : "SD";
                                var displayKey = resolved.IsMtp && resolved.MtpDetection != null
                                    ? resolved.MtpDetection.Device.FriendlyName
                                    : resolved.SourcePath;
                                var cameraLabel = $"{displayKey} \u2192 {resolved.CameraId} ({sourceType})";
                                dashboard.UpdateSlot(resolvedSlotId, DashboardRenderer.SlotPhase.Queued, label: cameraLabel);
                            }
                        }
                    }
                    dashboard.EndDialog();
                }

                if (Interlocked.CompareExchange(ref activeImport, 0, 0) == 0 && resolvedImports.Count > 0)
                {
                    Interlocked.Exchange(ref activeImport, 1);
                    var next = resolvedImports.Dequeue();

                    if (next.IsMtp)
                    {
                        var capturedMtp = next.MtpDetection!;
                        var capturedDeviceKey = MtpDeviceDetectionService.GetDeviceKey(capturedMtp.Device);
                        int capturedSlotId = -1;
                        lock (driveSlotMap) driveSlotMap.TryGetValue(capturedDeviceKey, out capturedSlotId);

                        _ = Task.Run(async () =>
                        {
                            WatchImportResult? mtpResult = null;
                            try
                            {
                                if (capturedSlotId >= 0)
                                    dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Scanning);

                                mtpResult = await RunMtpImportAsync(
                                    capturedMtp, config,
                                    stabilize, dryRun, verbose, quiet,
                                    renameVideos, goProRename, serviceProvider, ct,
                                    dashboard, capturedSlotId >= 0 ? capturedSlotId : null);

                                if (capturedSlotId >= 0)
                                {
                                    if (mtpResult.ErrorFiles == 0)
                                        dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Done,
                                            $"{mtpResult.CopiedFiles} Dateien, {FormatHelper.FormatBytes(mtpResult.CopiedBytes)}");
                                    else
                                        dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Error,
                                            $"{mtpResult.CopiedFiles} kopiert, {mtpResult.ErrorFiles} Fehler",
                                            subLines: mtpResult.ErrorDetails);
                                }

                                dashboard.AddSessionEntry(new ConsoleHelper.SessionEntry(
                                    capturedMtp.CameraId!, "MTP", 0, 0, mtpResult.CopiedBytes));
                            }
                            catch (Exception ex)
                            {
                                logger?.LogError(ex, "Fehler beim MTP-Import von {Camera}", capturedMtp.CameraId);
                                if (capturedSlotId >= 0)
                                    dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Error, ex.Message);
                            }
                            finally
                            {
                                Interlocked.Exchange(ref activeImport, 0);
                                dashboard.SetStatusLine($"👁 {CliStrings.Quick_WaitingForNextCardDevice}");
                            }
                        });
                    }
                    else
                    {
                        var capturedDrivePath = next.SourcePath;
                        var capturedCameraId = next.CameraId;
                        var capturedVsn = next.VolumeSerial;
                        int capturedSlotId = -1;
                        lock (driveSlotMap) driveSlotMap.TryGetValue(capturedDrivePath, out capturedSlotId);

                        _ = Task.Run(async () =>
                        {
                            WatchImportResult? importResult = null;
                            try
                            {
                                importResult = await RunImportAsync(
                                    capturedDrivePath, capturedCameraId,
                                    profileName, stabilize, dryRun, verbose, quiet,
                                    renameVideos, goProRename, serviceProvider, ct,
                                    dashboard, capturedSlotId >= 0 ? capturedSlotId : null);

                                if (capturedSlotId >= 0)
                                {
                                    if (importResult.ErrorFiles == 0)
                                        dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Done,
                                            $"{importResult.Videos} Videos, {importResult.Photos} Fotos, {FormatHelper.FormatBytes(importResult.CopiedBytes)}");
                                    else
                                        dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Error,
                                            $"{importResult.CopiedFiles} kopiert, {importResult.ErrorFiles} Fehler",
                                            subLines: importResult.ErrorDetails);
                                }

                                if (importResult.ErrorFiles == 0)
                                {
                                    if (!dryRun)
                                        await CardDetectionHelper.UpdateCardHistoryAsync(capturedDrivePath, capturedCameraId, config, configWriter, ct);
                                    if (capturedVsn != null)
                                        importedDrivesThisSession.Add(capturedVsn);
                                }

                                dashboard.AddSessionEntry(new ConsoleHelper.SessionEntry(
                                    capturedCameraId, "SD", importResult.Videos, importResult.Photos, importResult.CopiedBytes));
                            }
                            catch (Exception ex)
                            {
                                logger?.LogError(ex, "Fehler beim Import von {Drive}", capturedDrivePath);
                                if (capturedSlotId >= 0)
                                    dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Error, ex.Message);
                            }
                            finally
                            {
                                Interlocked.Exchange(ref activeImport, 0);
                                dashboard.SetStatusLine($"👁 {CliStrings.Quick_WaitingForNextCard}");
                            }
                        });
                    }
                }

                if (Interlocked.CompareExchange(ref activeImport, 0, 0) == 0 && folderQueue.Count > 0)
                {
                    Interlocked.Exchange(ref activeImport, 1);

                    (string camId, string folderPath) folderItem;
                    lock (folderQueue)
                        folderItem = folderQueue.Dequeue();

                    int folderSlotId;
                    lock (driveSlotMap)
                    {
                        if (!driveSlotMap.TryGetValue(folderItem.camId, out folderSlotId))
                        {
                            folderSlotId = dashboard.AddSlot(folderItem.camId, "Path", DashboardRenderer.SlotPhase.Detected, folderItem.folderPath);
                            driveSlotMap[folderItem.camId] = folderSlotId;
                        }
                    }

                    dashboard.UpdateSlot(folderSlotId, DashboardRenderer.SlotPhase.Queued);

                    WatchImportResult? folderResult = null;
                    try
                    {
                        folderResult = await RunImportAsync(
                            folderItem.folderPath, folderItem.camId,
                            profileName, stabilize, dryRun, verbose, quiet, renameVideos, goProRename,
                            serviceProvider, ct,
                            dashboard, folderSlotId);

                        if (folderResult.ErrorFiles == 0)
                            dashboard.UpdateSlot(folderSlotId, DashboardRenderer.SlotPhase.Done,
                                $"{folderResult.Videos} Videos, {folderResult.Photos} Fotos, {FormatHelper.FormatBytes(folderResult.CopiedBytes)}");
                        else
                            dashboard.UpdateSlot(folderSlotId, DashboardRenderer.SlotPhase.Error,
                                $"{folderResult.CopiedFiles} kopiert, {folderResult.ErrorFiles} Fehler",
                                subLines: folderResult.ErrorDetails);

                        dashboard.AddSessionEntry(new ConsoleHelper.SessionEntry(
                            folderItem.camId, "SD", folderResult.Videos, folderResult.Photos, folderResult.CopiedBytes));
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Fehler beim Fixed-Path Import von {Camera}", folderItem.camId);
                        dashboard.UpdateSlot(folderSlotId, DashboardRenderer.SlotPhase.Error, ex.Message);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref activeImport, 0);
                        if (!quiet)
                            dashboard.SetStatusLine($"👁 {CliStrings.Watch_WaitingForChanges}");
                    }
                }

                await Task.Delay(100, ct);
            }
        }
        finally
        {
            driveWatcher.StopWatching();
        }
    }

    private static WatchMode DetermineMode(string? cameraId, UmiConfig config)
    {
        if (!string.IsNullOrEmpty(cameraId))
        {
            return new WatchMode.Fixed(cameraId);
        }

        if (config.SdCards.Count > 0)
        {
            return new WatchMode.Auto();
        }

        return new WatchMode.Ask();
    }

    private static string BuildModeDescription(WatchMode mode, UmiConfig config)
    {
        return mode switch
        {
            WatchMode.Fixed fixedMode => $"All cards → {fixedMode.CameraId}",
            WatchMode.Auto => "Auto-detect + MTP polling",
            WatchMode.Ask => "Always ask + MTP polling",
            _ => "Unknown"
        };
    }

    private static string? HandleHotkey(ConsoleKeyInfo key, UmiConfig config)
    {
        if (key.Key >= ConsoleKey.D1 && key.Key <= ConsoleKey.D9)
        {
            var index = key.Key - ConsoleKey.D1;
            var cameras = config.Cameras.Where(c => c.Value.Enabled).ToList();

            if (index < cameras.Count)
            {
                return cameras[index].Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Führt MTP-Import für ein erkanntes Gerät aus.
    /// Delegiert an IMtpImportService — Device-Erkennung ist bereits durch den Caller erfolgt.
    /// </summary>
    private static async Task<WatchImportResult> RunMtpImportAsync(
        MtpDetectionResult detection,
        UmiConfig config,
        bool stabilize, bool dryRun, bool verbose, bool quiet,
        bool renameVideos, bool goProRename,
        ServiceProvider serviceProvider,
        CancellationToken ct,
        DashboardRenderer? dashboard = null,
        int? slotId = null)
    {
        var cameraId = detection.CameraId!;

        if (!config.Cameras.TryGetValue(cameraId, out var cameraConfig))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotInConfig, cameraId));
            return new WatchImportResult(0, 1, 0);
        }

        var mtpImportService = serviceProvider.GetRequiredService<IMtpImportService>();

        var request = new MtpImportRequest(
            CameraId: cameraId,
            CameraConfig: cameraConfig,
            Device: detection.Device,
            WorkbenchPath: config.GlobalPaths.Workbench,
            GlobalSettings: new GlobalSettings { Paths = config.GlobalPaths },
            Stabilize: stabilize,
            InjectGps: false,
            RenameVideos: renameVideos || cameraConfig.Features.RenameVideos,
            GoProRename: goProRename || cameraConfig.Features.GoProRename,
            DryRun: dryRun);

        IProgress<MtpImportProgress>? progress = null;
        if (slotId.HasValue && dashboard != null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long totalDownloaded = 0;
            progress = new Progress<MtpImportProgress>(p =>
            {
                totalDownloaded = p.BytesDownloaded;
                var elapsed = sw.Elapsed.TotalSeconds;
                var speed = elapsed > 0 ? (double)totalDownloaded / elapsed : 0;
                dashboard.UpdateSlot(slotId.Value, DashboardRenderer.SlotPhase.Copying,
                    $"{p.CurrentFile} ({p.Current}/{p.Total})");
                dashboard.UpdateProgress(slotId.Value, p.Current, p.Total,
                    p.BytesDownloaded, p.BytesTotal, speed > 0 ? speed : null, null);
            });
        }

        var result = await mtpImportService.ImportAsync(request, progress, ct);

        if (slotId.HasValue)
            dashboard?.UpdateSlot(slotId.Value, DashboardRenderer.SlotPhase.Done,
                $"{result.Downloaded} Dateien ({FormatHelper.FormatBytes(result.TotalBytes)})");

        return new WatchImportResult(
            result.Downloaded, result.Failed, result.TotalBytes,
            ErrorDetails: result.Errors?.Count > 0 ? result.Errors.ToList() : null);
    }

    private static async Task<WatchImportResult> RunImportAsync(
        string sourcePath,
        string cameraId,
        string? profileName,
        bool stabilize,
        bool dryRun,
        bool verbose,
        bool quiet,
        bool renameVideos,
        bool goProRename,
        ServiceProvider serviceProvider,
        CancellationToken ct,
        DashboardRenderer? dashboard = null,
        int? slotId = null)
    {
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var pipeline = serviceProvider.GetRequiredService<ImportPipelineService>();
        var fileCopyService = serviceProvider.GetRequiredService<FileCopyService>();
        var factory = serviceProvider.GetRequiredService<CameraHandlerFactory>();
        var logger = serviceProvider.GetService<ILogger<Program>>();

        if (!config.Cameras.TryGetValue(cameraId, out var cameraConfig))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotInConfig, cameraId));
            return new WatchImportResult(0, 1, 0);
        }

        var handler = factory.GetHandler(cameraId);
        if (handler == null)
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_NoHandlerForCamera, cameraId));
            return new WatchImportResult(0, 1, 0);
        }

        if (slotId.HasValue)
            dashboard?.UpdateSlot(slotId.Value, DashboardRenderer.SlotPhase.Scanning);

        var context = ImportContextFactory.Create(
            cameraId, cameraConfig, sourcePath, config.GlobalPaths.Workbench,
            new GlobalSettings { Paths = config.GlobalPaths },
            injectGps: false,
            stabilize: stabilize,
            stabilizeMode: "automatic",
            dryRun: dryRun,
            renameVideos: renameVideos ? true : (bool?)null,
            goProRename: goProRename ? true : (bool?)null);

        var umiDir = Path.Combine(config.GlobalPaths.Workbench, FolderNameConstants.UmiDir);
        Directory.CreateDirectory(umiDir);
        var dbPath = Path.Combine(umiDir, ".umi.db");
        if (File.Exists(dbPath))
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }

        using var db = new ImportDatabase(dbPath, serviceProvider.GetService<ILogger<ImportDatabase>>());
        await db.InitializeAsync();

        var historyService = serviceProvider.GetService<IImportHistoryService>();
        if (historyService is not null)
        {
            var folderName = cameraConfig.FolderName ?? cameraId;
            var removed = historyService.ReconcileHistory(cameraId, config.GlobalPaths.Workbench, folderName);
            if (removed > 0)
                logger?.LogInformation("[{Camera}] History reconciled: {Count} veraltete Einträge entfernt",
                    cameraId, removed);
        }

        var previousLogLevel = Program.ConsoleLogLevel.MinimumLevel;
        if (!quiet)
        {
            Program.ConsoleLogLevel.MinimumLevel = Serilog.Events.LogEventLevel.Fatal;
        }

        var scanResult = await pipeline.ScanSourceAsync(
            context, db,
            progress: quiet ? null : new Progress<ScanProgress>(p =>
            {

                if (slotId.HasValue && dashboard != null)
                    dashboard.UpdateSlot(slotId.Value, DashboardRenderer.SlotPhase.Scanning,
                        $"{p.Current}/{p.Total} — {p.CurrentFile}");
            }));

        if (!quiet)
        {
            Program.ConsoleLogLevel.MinimumLevel = previousLogLevel;
        }

        if (slotId.HasValue && dashboard != null)
            dashboard.UpdateSlot(slotId.Value, DashboardRenderer.SlotPhase.Copying,
                $"{scanResult.Photos} Fotos, {scanResult.Videos} Videos");

        if (!dryRun)
        {
            CopyResult copyResult;

            if (dashboard != null && slotId.HasValue)
            {

                var capturedSlotId = slotId.Value;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var copyProgress = new Progress<CopyProgress>(p =>
                {
                    var elapsed = sw.Elapsed.TotalSeconds;
                    double? speed = null;
                    double? eta = null;

                    if (elapsed > 0 && p.TotalCopiedBytes > 0)
                    {
                        speed = p.TotalCopiedBytes / elapsed;
                        if (speed > 0 && p.TotalBytes > p.TotalCopiedBytes)
                            eta = (p.TotalBytes - p.TotalCopiedBytes) / speed;
                    }

                    dashboard.UpdateProgress(capturedSlotId,
                        p.CompletedFiles, p.TotalFiles,
                        p.TotalCopiedBytes, p.TotalBytes,
                        speed, eta);
                });

                copyResult = await fileCopyService.CopyFilesAsync(db, dryRun: false, progress: copyProgress);
            }
            else
            {

                var reporter = new SimpleProgressReporter();
                copyResult = await reporter.RunCopyWithTextProgressAsync(
                    progress => fileCopyService.CopyFilesAsync(db, dryRun: false, progress: progress));
            }

            if (copyResult.CopiedFiles > 0 && copyResult.ErrorFiles == 0)
            {
                await pipeline.AppendHistoryIfNeededAsync(context, scanResult, ct);
            }

            if (copyResult.CopiedFiles > 0)
            {
                try
                {

                    var destPaths = await db.GetCopiedDestPaths(cameraId);
                    var importedFiles = destPaths
                        .Where(p => File.Exists(p))
                        .Select(p => new FileInfo(p))
                        .Select(fi => new ImportedFileInfo(
                            SourcePath: fi.FullName,
                            DestPath: fi.FullName,
                            FileName: fi.Name,
                            FileSize: fi.Length,
                            IsPhoto: !cameraConfig.FileTypes.Video.Any(ext =>
                                ext.Equals(fi.Extension, StringComparison.OrdinalIgnoreCase)),
                            IsVideo: cameraConfig.FileTypes.Video.Any(ext =>
                                ext.Equals(fi.Extension, StringComparison.OrdinalIgnoreCase))))
                        .ToList();

                    if (importedFiles.Count > 0)
                    {
                        var preOrchestrator = serviceProvider.GetRequiredService<IPreProcessingOrchestrator>();
                        await preOrchestrator.RunAsync(importedFiles, context, ct);

                        var postOrchestrator = serviceProvider.GetRequiredService<IPostProcessingOrchestrator>();
                        await postOrchestrator.RunAsync(importedFiles, context, ct: ct);
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Fehler beim Pre/Post-Processing für {Camera}", cameraId);

                }
            }

            List<string>? errorDetails = null;
            if (copyResult.ErrorFiles > 0)
            {
                var failedFiles = await db.GetFailedFilesAsync();
                errorDetails = failedFiles
                    .Select(f => $"\u26A0 {Path.GetFileName(f.Filename)}: {f.ErrorMessage}")
                    .ToList();
            }

            return new WatchImportResult(copyResult.CopiedFiles, copyResult.ErrorFiles, copyResult.CopiedBytes,
                scanResult.Videos, scanResult.Photos, errorDetails);
        }

        return new WatchImportResult(0, 0, 0, scanResult.Videos, scanResult.Photos);
    }

    private abstract record WatchMode
    {
        public record Fixed(string CameraId) : WatchMode;
        public record Auto : WatchMode;
        public record Ask : WatchMode;
    }

    /// <summary>Import-Ergebnis für Watch-Fehlerauswertung.</summary>
    private record WatchImportResult(int CopiedFiles, int ErrorFiles, long CopiedBytes, int Videos = 0, int Photos = 0, List<string>? ErrorDetails = null);
}
