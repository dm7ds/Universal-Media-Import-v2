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
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.CLI.Services;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;

namespace UMI.CLI.Commands;

/// <summary>
/// Quick-Import Command — schnelles Sichern von Media-Daten ohne Pipeline-Overhead.
/// Karte rein → Daten in Zielordner kopieren → nächste Karte.
/// Post-Processing (GPS, Gyroflow, Sortierung) separat via <c>umi process</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public static class QuickCommand
{
    /// <summary>Kopier-Ergebnis pro Kamera für die Summary-Tabelle.</summary>
    private record CameraResult(string CameraId, int Videos, int Photos, long Bytes, bool IsMtp);

    public static Command Create()
    {
        var command = new Command("quick", "Quick media import: insert card → copy → next card (no pipeline overhead)");

        var targetOption = new Option<string?>(
            "--target",
            "Target folder (skips interactive prompt when set)");

        var cameraOption = new Option<string?>(
            "--camera",
            "Fixed mode: import all cards as this camera (e.g. --camera MyDSLR)");

        var goProRenameOption = new Option<bool>(
            "--gopro-rename",
            getDefaultValue: () => false,
            "Rename GoPro files to sortable names (GoPro_0001_c01.MP4)");

        var renameVideosOption = new Option<bool>(
            "--rename-videos",
            getDefaultValue: () => false,
            "Rename videos with timestamp prefix (yyyyMMdd_HHmmss_OriginalName)");

        var stabilizeOption = new Option<bool>(
            "--stabilize",
            getDefaultValue: () => false,
            "Run Gyroflow after copy (move videos to Gyroflow/, invoke ProcessCommand)");

        var noMetadataOption = new Option<bool>(
            "--no-metadata",
            getDefaultValue: () => false,
            "Disable metadata backup (default: enabled)");

        command.AddOption(targetOption);
        command.AddOption(cameraOption);
        command.AddOption(goProRenameOption);
        command.AddOption(renameVideosOption);
        command.AddOption(stabilizeOption);
        command.AddOption(noMetadataOption);

        command.SetHandler(async (context) =>
        {
            var target      = context.ParseResult.GetValueForOption(targetOption);
            var camera      = context.ParseResult.GetValueForOption(cameraOption);
            var goProRename = context.ParseResult.GetValueForOption(goProRenameOption);
            var renameVideos = context.ParseResult.GetValueForOption(renameVideosOption);
            var stabilize   = context.ParseResult.GetValueForOption(stabilizeOption);
            var noMetadata  = context.ParseResult.GetValueForOption(noMetadataOption);
            var dryRun      = context.ParseResult.GetValueForOption(Program.DryRunOption);
            var verbose     = context.ParseResult.GetValueForOption(Program.VerboseOption);
            var quiet       = context.ParseResult.GetValueForOption(Program.QuietOption);

            await ExecuteAsync(target, camera, goProRename, renameVideos, stabilize,
                noMetadata, dryRun, verbose, quiet);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        string? targetArg,
        string? cameraId,
        bool goProRename,
        bool renameVideos,
        bool stabilize,
        bool noMetadata,
        bool dryRun,
        bool verbose,
        bool quiet)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        await using var serviceProvider = await Program.BuildServiceProviderAsync();
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var driveWatcher = serviceProvider.GetRequiredService<IDriveWatcherService>();
        var fingerprintService = serviceProvider.GetRequiredService<ISdFingerprintService>();
        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();
        var mtpDetectionService = serviceProvider.GetRequiredService<IMtpDeviceDetectionService>();
        var mtpService = serviceProvider.GetRequiredService<IMtpService>();
        var metadataService = serviceProvider.GetRequiredService<MetadataService>();
        var goProRenameService = serviceProvider.GetRequiredService<GoProRenameService>();
        var logger = serviceProvider.GetService<ILogger<Program>>();

        if (!string.IsNullOrEmpty(cameraId) && !config.Cameras.ContainsKey(cameraId))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotInConfig, cameraId));
            return;
        }

        var targetPath = ResolveTargetPath(targetArg, config);
        if (targetPath is null)
        {
            ConsoleHelper.WriteError(CliStrings.Quick_NoTarget);
            return;
        }

        var interactive = ConsoleHelper.IsInteractiveTerminal();
        var dashboard = new DashboardRenderer("UMI - Quick Import", interactive);

        dashboard.SetModeInfo(
            $"  {CliStrings.Quick_TargetFolder}: {targetPath}",
            dryRun ? $"  \u26a0 {CliStrings.Common_DryRunYes}" : "");

        dashboard.RenderHeader();

        var driveSlotMap = new Dictionary<string, int>();

        var detectionQueue = new Queue<CardDetectionHelper.DetectionEvent>();
        var resolvedImports = new Queue<CardDetectionHelper.ResolvedImport>();
        var knownMtpDevices = new HashSet<string>();
        var importedDrivesThisSession = new HashSet<string>();
        var activeImport = 0;

        var summaryResults = new List<CameraResult>();
        var summaryLock = new object();

        var mtpCameras = config.Cameras
            .Where(kvp => kvp.Value.Enabled && kvp.Value.SourceType == SourceType.MTP)
            .ToList();

        driveWatcher.DriveArrived += (sender, e) =>
        {
            try
            {
                var driveInfo = new DriveInfo(e.DriveLetter);
                if (!driveInfo.IsReady || e.TotalSizeBytes == 0)
                    return;
            }
            catch { return; }

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
                logger?.LogDebug("Karte entfernt: {Drive}", e.DriveLetter);
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
                fixedCameraId: cameraId,
                config, fingerprintService, configWriter,
                importedDrivesThisSession, verbose, ct);

            foreach (var resolved in resolvedImports)
            {
                var key = resolved.IsMtp
                    ? (resolved.MtpDetection != null ? MtpDeviceDetectionService.GetDeviceKey(resolved.MtpDetection.Device) : resolved.SourcePath)
                    : resolved.SourcePath;
                int resolvedSlotId;
                lock (driveSlotMap)
                {
                    if (driveSlotMap.TryGetValue(key, out resolvedSlotId))
                        dashboard.UpdateSlot(resolvedSlotId, DashboardRenderer.SlotPhase.Queued);
                }
            }
            dashboard.EndDialog();
        }

        dashboard.SetStatusLine($"\U0001f441 {CliStrings.Quick_WaitingForCards}");

        try
        {
            var mtpPollStopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (!ct.IsCancellationRequested)
            {

                if (mtpCameras.Any() && mtpPollStopwatch.ElapsedMilliseconds >= 5000)
                {
                    mtpPollStopwatch.Restart();
                    try
                    {
                        var detections = mtpDetectionService.DetectDevices();
                        foreach (var detection in detections)
                        {
                            if (detection.Outcome != MtpDetectionOutcome.Registered) continue;
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
                    catch (Exception ex) { logger?.LogDebug(ex, "MTP Polling Fehler"); }
                }

                if (detectionQueue.Count > 0)
                {
                    dashboard.BeginDialog();
                    await CardDetectionHelper.ProcessDetectionQueueAsync(
                        detectionQueue, resolvedImports,
                        fixedCameraId: cameraId,
                        config, fingerprintService, configWriter,
                        importedDrivesThisSession, verbose, ct);

                    foreach (var resolved in resolvedImports)
                    {
                        var key = resolved.IsMtp
                            ? (resolved.MtpDetection != null ? MtpDeviceDetectionService.GetDeviceKey(resolved.MtpDetection.Device) : resolved.SourcePath)
                            : resolved.SourcePath;
                        int resolvedSlotId;
                        lock (driveSlotMap)
                        {
                            if (driveSlotMap.TryGetValue(key, out resolvedSlotId))
                                dashboard.UpdateSlot(resolvedSlotId, DashboardRenderer.SlotPhase.Queued);
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
                            CameraResult? result = null;
                            try
                            {
                                if (capturedSlotId >= 0)
                                    dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Copying,
                                        CliStrings.Quick_MtpTransferStarted);

                                result = await RunMtpQuickCopyAsync(
                                    capturedMtp, targetPath, config, mtpService,
                                    metadataService, goProRenameService,
                                    goProRename, noMetadata, dryRun, quiet,
                                    dashboard, capturedSlotId >= 0 ? capturedSlotId : null,
                                    ct);

                                lock (summaryLock)
                                    summaryResults.Add(result);

                                if (capturedSlotId >= 0)
                                {
                                    if (result.Videos + result.Photos > 0)
                                        dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Done,
                                            $"{result.Videos} V, {result.Photos} F, {FormatHelper.FormatBytes(result.Bytes)}");
                                    else
                                        dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Done,
                                            CliStrings.Quick_NoFilesImported);
                                }

                                dashboard.AddSessionEntry(new ConsoleHelper.SessionEntry(
                                    capturedMtp.CameraId!, "MTP", result.Videos, result.Photos, result.Bytes));
                            }
                            catch (Exception ex)
                            {
                                logger?.LogError(ex, "Fehler beim MTP Quick-Import von {Camera}", capturedMtp.CameraId);
                                if (capturedSlotId >= 0)
                                    dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Error, ex.Message);
                            }
                            finally
                            {
                                Interlocked.Exchange(ref activeImport, 0);
                                dashboard.SetStatusLine($"\U0001f441 {CliStrings.Quick_WaitingForNextCardDevice}");
                            }
                        }, ct);
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
                            CameraResult? result = null;
                            try
                            {
                                if (capturedSlotId >= 0)
                                    dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Copying);

                                result = await RunSdQuickCopyAsync(
                                    capturedDrivePath, capturedCameraId, targetPath,
                                    config, metadataService, goProRenameService,
                                    goProRename, renameVideos, noMetadata, dryRun, quiet,
                                    dashboard, capturedSlotId >= 0 ? capturedSlotId : null,
                                    ct);

                                lock (summaryLock)
                                    summaryResults.Add(result);

                                if (capturedSlotId >= 0)
                                {
                                    if (result.Videos + result.Photos > 0)
                                    {
                                        if (capturedVsn != null)
                                            importedDrivesThisSession.Add(capturedVsn);
                                        dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Done,
                                            $"{result.Videos} V, {result.Photos} F, {FormatHelper.FormatBytes(result.Bytes)}");
                                    }
                                    else
                                    {
                                        dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Done,
                                            CliStrings.Quick_NoFilesImported);
                                    }
                                }

                                dashboard.AddSessionEntry(new ConsoleHelper.SessionEntry(
                                    capturedCameraId, "SD", result.Videos, result.Photos, result.Bytes));
                            }
                            catch (Exception ex)
                            {
                                logger?.LogError(ex, "Fehler beim Quick-Import von {Drive}", capturedDrivePath);
                                if (capturedSlotId >= 0)
                                    dashboard.UpdateSlot(capturedSlotId, DashboardRenderer.SlotPhase.Error, ex.Message);
                            }
                            finally
                            {
                                Interlocked.Exchange(ref activeImport, 0);
                                dashboard.SetStatusLine($"\U0001f441 {CliStrings.Quick_WaitingForNextCard}");
                            }
                        }, ct);
                    }
                }

                await Task.Delay(100, ct);
            }
        }
        catch (OperationCanceledException)
        {

        }
        finally
        {
            driveWatcher.StopWatching();
        }

        if (stabilize && !dryRun && summaryResults.Any(r => r.Videos > 0))
        {
            Console.WriteLine();
            Console.WriteLine($"\u25b6 {CliStrings.Quick_StartingGyroflow}");
            try
            {
                await ProcessCommand.ExecuteProcessAsync(
                    source: targetPath,
                    stabilize: true,
                    mode: "automatic",
                    force: false,
                    date: null,
                    dryRun: false,
                    configPath: "config.json");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Fehler bei Gyroflow nach Quick-Import");
                ConsoleHelper.WriteError(string.Format(CliStrings.Quick_GyroflowFailed, ex.Message));
            }
        }

        PrintSummary(summaryResults, targetPath);
    }

    /// <summary>
    /// Ermittelt den Zielordner: --target-Argument, interaktiver Prompt, oder Smart-Date-Default.
    /// </summary>
    private static string? ResolveTargetPath(string? targetArg, UmiConfig config)
    {

        var now = DateTime.Now;
        var effectiveDate = now.Hour < 8 ? now.AddDays(-1) : now;
        var defaultTarget = Path.Combine(
            config.GlobalPaths.Workbench,
            effectiveDate.ToString(DateFormatConstants.FolderFormat, CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(targetArg))
            return ResolvePathInput(targetArg, config.GlobalPaths.Workbench);

        Console.WriteLine($"  {CliStrings.Quick_Mode}");
        Console.WriteLine($"  {CliStrings.Quick_TargetFolder}: {defaultTarget}");
        Console.Write($"  {CliStrings.Quick_ChangeTarget} ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            return defaultTarget;

        return ResolvePathInput(input.Trim(), config.GlobalPaths.Workbench);
    }

    /// <summary>
    /// Löst Pfad-Eingabe auf: absoluter Pfad direkt, sonst relativ zu Workbench.
    /// </summary>
    private static string ResolvePathInput(string input, string workbench)
    {
        if (Path.IsPathRooted(input))
            return Path.GetFullPath(input);

        return Path.GetFullPath(Path.Combine(workbench, input));
    }

    /// <summary>
    /// Quick-Copy von SD-Karte in {target}/{CameraId}/{Video|Photo}/.
    /// Kein Datum-Subfolder, kein Pipeline-Overhead.
    /// Im Dashboard-Modus: IProgress<CopyProgress> der dashboard.UpdateProgress() aufruft.
    /// Im Non-Interactive Fallback: SimpleProgressReporter mit Text-Progress.
    /// </summary>
    private static async Task<CameraResult> RunSdQuickCopyAsync(
        string sourcePath,
        string cameraId,
        string targetPath,
        UmiConfig config,
        MetadataService metadataService,
        GoProRenameService goProRenameService,
        bool goProRename,
        bool renameVideos,
        bool noMetadata,
        bool dryRun,
        bool quiet,
        DashboardRenderer? dashboard,
        int? slotId,
        CancellationToken ct)
    {
        if (!config.Cameras.TryGetValue(cameraId, out var cameraConfig))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotInConfig, cameraId));
            return new CameraResult(cameraId, 0, 0, 0, IsMtp: false);
        }

        var videoExtensions = cameraConfig.FileTypes.Video
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();
        var photoExtensions = cameraConfig.FileTypes.Photo
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();
        var allExtensions = new HashSet<string>(videoExtensions.Concat(photoExtensions));

        var allFiles = Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories)
            .Where(f => allExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => !config.Workflow.IgnoreFolders.Any(ig =>
                f.Contains(Path.DirectorySeparatorChar + ig + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)))
            .Select(f => Path.GetFullPath(f))
            .ToList();

        if (allFiles.Count == 0)
        {
            if (slotId.HasValue && dashboard != null)
                dashboard.UpdateSlot(slotId.Value, DashboardRenderer.SlotPhase.Done, CliStrings.Quick_NoImportableFiles);
            else if (!quiet)
                ConsoleHelper.WriteInfo(string.Format(CliStrings.Quick_NoImportableFilesSource, sourcePath));
            return new CameraResult(cameraId, 0, 0, 0, IsMtp: false);
        }

        var copied = new List<ImportedFileInfo>(allFiles.Count);
        long totalBytes = 0;
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

            copyResult = await CopyFilesAsync(allFiles, cameraId, targetPath, videoExtensions, photoExtensions,
                goProRename, goProRenameService, dryRun, copied, copyProgress, ct);
        }
        else
        {

            if (!quiet)
            {
                Console.WriteLine();
                Console.WriteLine($"\u25b6 Quick-Import: {cameraConfig.Name} \u2192 {targetPath}");
                ConsoleHelper.WriteSeparator();
                Console.WriteLine($"  \u2192 {allFiles.Count} {CliStrings.Quick_FilesFound}");
            }

            var quickFolderName = cameraConfig.FolderName ?? cameraId;
            var reporter = new SimpleProgressReporter();
            copyResult = await reporter.RunCopyWithTextProgressAsync(progress =>
                CopyFilesAsync(allFiles, quickFolderName, targetPath, videoExtensions, photoExtensions,
                    goProRename, goProRenameService, dryRun, copied, progress, ct));

            if (!quiet)
                Console.WriteLine($"  \u2192 {copied.Count} {CliStrings.Quick_FilesCopied} ({FormatHelper.FormatBytes(copyResult.CopiedBytes)})");
        }

        totalBytes = copyResult.CopiedBytes;
        var videoCount = copied.Count(f => f.IsVideo);
        var photoCount = copied.Count(f => f.IsPhoto);

        if (!noMetadata && !dryRun && copied.Any(f => f.IsVideo))
        {
            if (slotId.HasValue && dashboard != null)
                dashboard.UpdateSlot(slotId.Value, DashboardRenderer.SlotPhase.Copying, CliStrings.Quick_MetadataBackup);
            else if (!quiet)
                Console.WriteLine("  \u2192 Metadata-Backup...");

            foreach (var file in copied.Where(f => f.IsVideo))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await metadataService.CreateBackupAsync(file.DestPath, cameraId, ct);
                }
                catch (Exception ex)
                {

                    ConsoleHelper.WriteWarning(string.Format(CliStrings.Quick_MetadataBackupFailed, file.FileName, ex.Message));
                }
            }
        }

        return new CameraResult(cameraId, videoCount, photoCount, totalBytes, IsMtp: false);
    }

    /// <summary>
    /// Kopiert Dateien aus sourcePath nach {target}/{cameraId}/{Video|Photo}/.
    /// Duplikat-Check via Dateiname + Größe.
    /// </summary>
    private static async Task<CopyResult> CopyFilesAsync(
        IReadOnlyList<string> files,
        string cameraId,
        string targetPath,
        HashSet<string> videoExtensions,
        HashSet<string> photoExtensions,
        bool goProRename,
        GoProRenameService goProRenameService,
        bool dryRun,
        List<ImportedFileInfo> copied,
        IProgress<CopyProgress> progress,
        CancellationToken ct)
    {
        var totalBytes = files.Sum(f =>
        {
            try { return new FileInfo(f).Length; }
            catch { return 0L; }
        });

        long copiedBytes = 0;
        var copiedFiles = 0;
        var skippedFiles = 0;
        var errorFiles = 0;

        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var sourceFile = files[i];
            var ext = Path.GetExtension(sourceFile).ToLowerInvariant();
            var isVideo = videoExtensions.Contains(ext);
            var typeFolder = isVideo ? FolderNameConstants.Video : FolderNameConstants.Photo;

            var destDir = Path.Combine(targetPath, cameraId, typeFolder);
            var fileName = ResolveQuickFileName(
                Path.GetFileName(sourceFile), goProRename, goProRenameService);

            var destFile = Path.Combine(destDir, fileName);

            long fileSize = 0;
            try { fileSize = new FileInfo(sourceFile).Length; }
            catch { }

            if (File.Exists(destFile))
            {
                try
                {
                    var existingSize = new FileInfo(destFile).Length;
                    if (existingSize == fileSize)
                    {
                        skippedFiles++;
                        copiedBytes += fileSize;
                        progress.Report(new CopyProgress
                        {
                            TotalFiles = files.Count,
                            CompletedFiles = copiedFiles + skippedFiles,
                            TotalBytes = totalBytes,
                            TotalCopiedBytes = copiedBytes,
                        });
                        continue;
                    }
                }
                catch { }
            }

            progress.Report(new CopyProgress
            {
                TotalFiles = files.Count,
                CompletedFiles = copiedFiles + skippedFiles,
                TotalBytes = totalBytes,
                TotalCopiedBytes = copiedBytes,
                CurrentFile = fileName,
                CurrentFileSize = fileSize,
                CurrentFileCopiedBytes = 0,
            });

            if (!dryRun)
            {
                try
                {
                    Directory.CreateDirectory(destDir);
                    await CopyFileWithProgressAsync(sourceFile, destFile, fileSize,
                        fileName, files.Count, copiedFiles + skippedFiles, totalBytes,
                        copiedBytes, progress, ct);

                    copied.Add(new ImportedFileInfo(
                        SourcePath: sourceFile,
                        DestPath: destFile,
                        FileName: fileName,
                        FileSize: fileSize,
                        IsPhoto: !isVideo,
                        IsVideo: isVideo));

                    copiedBytes += fileSize;
                    copiedFiles++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    errorFiles++;
                }
            }
            else
            {

                copied.Add(new ImportedFileInfo(
                    SourcePath: sourceFile,
                    DestPath: destFile,
                    FileName: fileName,
                    FileSize: fileSize,
                    IsPhoto: !isVideo,
                    IsVideo: isVideo));
                copiedBytes += fileSize;
                copiedFiles++;
            }

            progress.Report(new CopyProgress
            {
                TotalFiles = files.Count,
                CompletedFiles = copiedFiles + skippedFiles,
                TotalBytes = totalBytes,
                TotalCopiedBytes = copiedBytes,
                CurrentFile = fileName,
                CurrentFileSize = fileSize,
                CurrentFileCopiedBytes = fileSize,
            });
        }

        return new CopyResult
        {
            CopiedFiles = copiedFiles,
            ErrorFiles = errorFiles,
            CopiedBytes = copiedBytes,
            TotalFiles = files.Count,
            SkippedFiles = skippedFiles,
            TotalBytes = totalBytes,
        };
    }

    /// <summary>
    /// Kopiert eine einzelne Datei mit Progress-Reporting (64KB Chunks).
    /// </summary>
    private static async Task CopyFileWithProgressAsync(
        string source,
        string dest,
        long fileSize,
        string fileName,
        int totalFiles,
        int completedFiles,
        long totalBytes,
        long copiedBytesSoFar,
        IProgress<CopyProgress> progress,
        CancellationToken ct)
    {
        const int bufferSize = 65536;
        using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        using var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);

        var buffer = new byte[bufferSize];
        long fileCopied = 0;

        int read;
        while ((read = await sourceStream.ReadAsync(buffer, ct)) > 0)
        {
            await destStream.WriteAsync(buffer.AsMemory(0, read), ct);
            fileCopied += read;

            progress.Report(new CopyProgress
            {
                TotalFiles = totalFiles,
                CompletedFiles = completedFiles,
                TotalBytes = totalBytes,
                TotalCopiedBytes = copiedBytesSoFar + fileCopied,
                CurrentFile = fileName,
                CurrentFileSize = fileSize,
                CurrentFileCopiedBytes = fileCopied,
            });
        }
    }

    /// <summary>
    /// Quick-Copy von MTP-Gerät in {target}/{CameraId}/{Video|Photo}/.
    /// Kein Datum-Subfolder (anders als WatchCommand)!
    /// Im Dashboard-Modus: dashboard.UpdateProgress() statt Console.Write.
    /// </summary>
    private static async Task<CameraResult> RunMtpQuickCopyAsync(
        MtpDetectionResult detection,
        string targetPath,
        UmiConfig config,
        IMtpService mtpService,
        MetadataService metadataService,
        GoProRenameService goProRenameService,
        bool goProRename,
        bool noMetadata,
        bool dryRun,
        bool quiet,
        DashboardRenderer? dashboard,
        int? slotId,
        CancellationToken ct)
    {
        var cameraId = detection.CameraId!;

        if (!config.Cameras.TryGetValue(cameraId, out var cameraConfig))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotInConfig, cameraId));
            return new CameraResult(cameraId, 0, 0, 0, IsMtp: true);
        }

        if (dashboard == null || !slotId.HasValue)
        {

            Console.WriteLine();
            Console.WriteLine($"\u25b6 MTP Quick-Import: {cameraConfig.Name} \u2192 {targetPath}");
            ConsoleHelper.WriteSeparator();
        }

        var videoExtensions = cameraConfig.FileTypes.Video
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();
        var allExtensions = cameraConfig.FileTypes.Video
            .Concat(cameraConfig.FileTypes.Photo)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        var mtpFiles = mtpService.ListFiles(detection.Device.DeviceId, extensions: allExtensions);

        if (mtpFiles.Count == 0)
        {
            if (slotId.HasValue && dashboard != null)
                dashboard.UpdateSlot(slotId.Value, DashboardRenderer.SlotPhase.Done, CliStrings.Quick_NoImportableFiles);
            else if (!quiet)
                ConsoleHelper.WriteInfo(CliStrings.Quick_NoImportableFilesDevice);
            return new CameraResult(cameraId, 0, 0, 0, IsMtp: true);
        }

        if (dashboard == null && !quiet)
            Console.WriteLine($"  \u2192 {mtpFiles.Count} {CliStrings.Quick_FilesFound}");

        var downloaded = new List<ImportedFileInfo>(mtpFiles.Count);
        var totalBytes = 0L;
        var downloadErrors = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var totalMtpBytes = mtpFiles.Sum(f => f.Length);

        for (var i = 0; i < mtpFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var file = mtpFiles[i];
            var isVideo = videoExtensions.Contains(
                Path.GetExtension(file.Name).ToLowerInvariant());
            var typeFolder = isVideo ? FolderNameConstants.Video : FolderNameConstants.Photo;

            var fileName = ResolveQuickFileName(file.Name, goProRename, goProRenameService);

            var mtpFolderName = cameraConfig.FolderName ?? cameraId;
            var destDir = Path.Combine(targetPath, mtpFolderName, typeFolder);
            var destPath = Path.Combine(destDir, fileName);

            if (File.Exists(destPath))
            {
                try
                {
                    if (new FileInfo(destPath).Length == file.Length)
                        continue;
                }
                catch { }
            }

            if (slotId.HasValue && dashboard != null)
            {
                var elapsed = sw.Elapsed.TotalSeconds;
                var speed = elapsed > 0 ? (double)totalBytes / elapsed : 0;
                dashboard.UpdateProgress(slotId.Value, i, mtpFiles.Count,
                    totalBytes, totalMtpBytes, speed > 0 ? speed : null, null);
            }
            else if (!quiet)
            {
                Console.Write($"\r  {i + 1}/{mtpFiles.Count}: {file.Name}                    ");
            }

            string? localPath = null;
            if (!dryRun)
            {
                Directory.CreateDirectory(destDir);
                localPath = await mtpService.DownloadFileAsync(
                    detection.Device.DeviceId, file.FullPath, destDir, ct);

                if (localPath != null && fileName != file.Name)
                {
                    var renamedDest = Path.Combine(destDir, fileName);
                    if (!File.Exists(renamedDest))
                        File.Move(localPath, renamedDest);
                    localPath = renamedDest;
                }
            }
            else
            {
                localPath = destPath;
            }

            if (localPath is not null)
            {
                downloaded.Add(new ImportedFileInfo(
                    SourcePath: file.FullPath,
                    DestPath: localPath,
                    FileName: fileName,
                    FileSize: file.Length,
                    IsPhoto: !isVideo,
                    IsVideo: isVideo));
                totalBytes += file.Length;
            }
            else if (!dryRun)
            {
                downloadErrors++;
            }
        }

        if (dashboard == null && !quiet)
        {
            Console.WriteLine();
            Console.WriteLine($"  \u2192 {downloaded.Count} {CliStrings.Quick_FilesDownloaded} ({FormatHelper.FormatBytes(totalBytes)})");
        }

        if (!noMetadata && !dryRun && downloaded.Any(f => f.IsVideo))
        {
            if (slotId.HasValue && dashboard != null)
                dashboard.UpdateSlot(slotId.Value, DashboardRenderer.SlotPhase.Copying, CliStrings.Quick_MetadataBackup);
            else if (!quiet)
                Console.WriteLine("  \u2192 Metadata-Backup...");

            foreach (var file in downloaded.Where(f => f.IsVideo))
            {
                ct.ThrowIfCancellationRequested();
                try { await metadataService.CreateBackupAsync(file.DestPath, cameraId, ct); }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteWarning(string.Format(CliStrings.Quick_MetadataBackupFailed, file.FileName, ex.Message));
                }
            }
        }

        return new CameraResult(
            cameraId,
            downloaded.Count(f => f.IsVideo),
            downloaded.Count(f => f.IsPhoto),
            totalBytes,
            IsMtp: true);
    }

    /// <summary>
    /// Wendet GoProRename auf einen Dateinamen an.
    /// SSOT innerhalb von QuickCommand — SD- und MTP-Quick-Pfad rufen diese Methode auf
    /// statt IsGoProFile/GetRenamedFileName inline zu duplizieren (DRY).
    /// Pendant zu ImportPipelineService.ResolveFileName für den leichtgewichtigen Quick-Copy-Pfad.
    /// </summary>
    private static string ResolveQuickFileName(
        string fileName,
        bool goProRename,
        GoProRenameService goProRenameService)
    {
        if (goProRename && goProRenameService.IsGoProFile(fileName))
            return goProRenameService.GetRenamedFileName(fileName);
        return fileName;
    }

    /// <summary>
    /// Gibt die Summary-Tabelle mit Spectre.Console aus.
    /// </summary>
    private static void PrintSummary(IReadOnlyList<CameraResult> results, string targetPath)
    {
        Console.WriteLine();

        if (results.Count == 0)
        {
            ConsoleHelper.WriteInfo(CliStrings.Quick_NoFilesImported);
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]Camera[/]"));
        table.AddColumn(new TableColumn("[bold]Videos[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Photos[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Size[/]").RightAligned());

        var totalVideos = 0;
        var totalPhotos = 0;
        var totalBytes = 0L;

        foreach (var r in results)
        {
            var label = r.IsMtp ? $"{r.CameraId} (MTP)" : r.CameraId;
            table.AddRow(
                label,
                r.Videos.ToString(),
                r.Photos.ToString(),
                FormatHelper.FormatBytes(r.Bytes));

            totalVideos += r.Videos;
            totalPhotos += r.Photos;
            totalBytes += r.Bytes;
        }

        if (results.Count > 1)
        {
            table.AddRow(
                "[bold]Total[/]",
                $"[bold]{totalVideos}[/]",
                $"[bold]{totalPhotos}[/]",
                $"[bold]{FormatHelper.FormatBytes(totalBytes)}[/]");
        }

        AnsiConsole.WriteLine($"  {CliStrings.Quick_ImportCompleted}:");
        AnsiConsole.Write(table);
        Console.WriteLine($"  {CliStrings.Quick_TargetFolder}: {targetPath}");
        Console.WriteLine();
    }
}
