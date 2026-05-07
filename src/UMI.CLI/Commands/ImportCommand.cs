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
using Serilog.Events;
using Spectre.Console;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.CLI.Services;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.Data;
using UMI.Data.Models;

namespace UMI.CLI.Commands;

public static class ImportCommand
{
    [SupportedOSPlatform("windows")]
    public static Command Create()
    {
        var command = new Command("import", "Import media from SD card to workbench");

        var sourceOption = new Option<string>(
            "--source",
            getDefaultValue: () => "ALL",
            "Camera ID(s) comma-separated (GoPro11, MyDSLR, ALL)");

        var typeOption = new Option<string?>(
            "--type",
            "Camera type(s) comma-separated (Action, Drone, DSLR, Mirrorless, etc.)");

        var stabilizeOption = new Option<bool>(
            "--stabilize",
            "Stabilize videos with Gyroflow");

        var modeOption = new Option<string>(
            "--mode",
            getDefaultValue: () => "automatic",
            "Stabilization mode: 'automatic' (only without EIS) or 'all' (all videos)");

        var forceOption = new Option<bool>(
            "--force",
            "Also stabilize videos WITH EIS (only with automatic)");

        var noEisSortOption = new Option<bool>(
            "--no-eis-sort",
            getDefaultValue: () => false,
            "Disable EIS-based sorting (all videos to Video/)");

        var fullOption = new Option<bool>(
            "--full",
            getDefaultValue: () => false,
            "Import all files (ignores import history for fixed_path sources)");

        var resetHistoryOption = new Option<bool>(
            "--reset-history",
            getDefaultValue: () => false,
            "Clear import history before import (then normal history-filtered import)");

        var folderOption = new Option<string?>(
            "--folder",
            "Ad-hoc import from folder without camera configuration");

        var keepStructureOption = new Option<bool>(
            "--keep-structure",
            getDefaultValue: () => false,
            "Keep subfolder structure (only with --folder; default: flatten with _ separator)");

        var renameVideosOption = new Option<bool>(
            "--rename-videos",
            getDefaultValue: () => false,
            "Rename videos with timestamp prefix (yyyyMMdd_HHmmss_OriginalName); overrides rename_videos from config");

        var goProRenameOption = new Option<bool>(
            "--gopro-rename",
            getDefaultValue: () => false,
            "Rename GoPro files to sortable names (GoPro_0001_c01.MP4); overrides gopro_rename from config");

        command.AddOption(sourceOption);
        command.AddOption(typeOption);
        command.AddOption(stabilizeOption);
        command.AddOption(modeOption);
        command.AddOption(forceOption);
        command.AddOption(noEisSortOption);
        command.AddOption(fullOption);
        command.AddOption(resetHistoryOption);
        command.AddOption(folderOption);
        command.AddOption(keepStructureOption);
        command.AddOption(renameVideosOption);
        command.AddOption(goProRenameOption);

        command.SetHandler(async (context) =>
        {
            var source = context.ParseResult.GetValueForOption(sourceOption)!;
            var type = context.ParseResult.GetValueForOption(typeOption);
            var stabilize = context.ParseResult.GetValueForOption(stabilizeOption);
            var mode = context.ParseResult.GetValueForOption(modeOption)!;
            var force = context.ParseResult.GetValueForOption(forceOption);
            var noEisSort = context.ParseResult.GetValueForOption(noEisSortOption);
            var dryRun = context.ParseResult.GetValueForOption(Program.DryRunOption);
            var verbose = context.ParseResult.GetValueForOption(Program.VerboseOption);
            var quiet = context.ParseResult.GetValueForOption(Program.QuietOption);
            var fullImport = context.ParseResult.GetValueForOption(fullOption);
            var resetHistory = context.ParseResult.GetValueForOption(resetHistoryOption);
            var folderPath = context.ParseResult.GetValueForOption(folderOption);
            var keepStructure = context.ParseResult.GetValueForOption(keepStructureOption);
            var renameVideos = context.ParseResult.GetValueForOption(renameVideosOption);
            var goProRename = context.ParseResult.GetValueForOption(goProRenameOption);

            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            var profile = context.ParseResult.GetValueForOption(Program.ProfileOption);

            if (!string.IsNullOrEmpty(folderPath))
            {
                await ExecuteAdHocFolderAsync(folderPath, keepStructure, dryRun, quiet, renameVideos, goProRename, configPath, profile);
            }
            else
            {
                await ExecuteImportAsync(source, type, stabilize, mode, force, noEisSort, dryRun, configPath, profile, quiet, fullImport, resetHistory, renameVideos, goProRename);
            }
        });

        return command;
    }

    [SupportedOSPlatform("windows")]
    private static async Task ExecuteImportAsync(
        string source,
        string? type,
        bool stabilize,
        string mode,
        bool force,
        bool noEisSort,
        bool dryRun,
        string configPath,
        string? profile,
        bool quiet,
        bool fullImport = false,
        bool resetHistory = false,
        bool renameVideos = false,
        bool goProRename = false)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        if (!quiet)
        {
            ConsoleHelper.WriteBanner(CliStrings.Import_Banner);
        }

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath, profile);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var factory = serviceProvider.GetRequiredService<CameraHandlerFactory>();

        using var importLock = new ImportLock(logger);
        importLock.SetSource(source);
        importLock.SetCommand("import");

        if (!await TryAcquireLockAsync(importLock, config.GlobalPaths.Workbench, ct))
        {
            return;
        }

        var filter = CreateFilter(source, type);

        if (!quiet)
        {
            ConsoleHelper.WriteOption("Config", Path.GetFileName(configPath));
            if (!string.IsNullOrEmpty(profile))
            {
                ConsoleHelper.WriteOption("Profile", profile);
            }
            ConsoleHelper.WriteOption("Filter", filter.GetDescription());
            ConsoleHelper.WriteOption("Gyroflow", stabilize);
            if (stabilize)
            {
                ConsoleHelper.WriteOption("  Modus", mode);
                if (mode.Equals("automatic", StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleHelper.WriteOption("  Force", force ? CliStrings.Common_Yes : CliStrings.Common_No);
                }
            }
            if (dryRun)
            {
                ConsoleHelper.WriteWarning(CliStrings.Common_DryRunYes);
            }
            else
            {
                ConsoleHelper.WriteOption("Dry-Run", CliStrings.Common_No);
            }
            Console.WriteLine();
        }

        var cameras = config.Cameras
            .Where(kvp => kvp.Value.Enabled)
            .Select(kvp => (
                Handler: factory.GetHandler(kvp.Key),
                Config: kvp.Value,
                Key: kvp.Key
            ))
            .Where(c => c.Handler != null && filter.Matches(c.Handler!, c.Config))
            .ToList();

        if (filter.CameraIds.Any())
        {
            var foundIds = cameras.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unknownIds = filter.CameraIds.Where(id => !foundIds.Contains(id)).ToList();

            if (unknownIds.Any())
            {
                ConsoleHelper.WriteWarning(string.Format(CliStrings.Import_UnknownCameraIds, string.Join(", ", unknownIds)));
                logger?.LogWarning("Unbekannte CameraIds werden übersprungen: {UnknownIds}", string.Join(", ", unknownIds));
            }
        }

        if (cameras.Count == 0)
        {
            ConsoleHelper.WriteError(CliStrings.Import_NoCamerasFound);

            var availableCameras = config.Cameras
                .Where(kvp => kvp.Value.Enabled)
                .Select(kvp => $"{kvp.Key} ({kvp.Value.Name})")
                .ToList();

            if (availableCameras.Any() && !quiet)
            {
                Console.WriteLine();
                Console.WriteLine(CliStrings.Common_AvailableCameras);
                foreach (var cam in availableCameras)
                {
                    Console.WriteLine($"  • {cam}");
                }
            }

            return;
        }

        if (!quiet)
        {
            Console.WriteLine(string.Format(CliStrings.Import_ImportForCameras, cameras.Count));
            foreach (var (handler, cfg, key) in cameras)
            {
                Console.WriteLine($"  • {handler!.DisplayName} ({handler.CameraType})");
            }
            Console.WriteLine();
        }

        if (!force)
        {

            var registryService = serviceProvider.GetRequiredService<ISdCardRegistryService>();

            for (int i = 0; i < cameras.Count; i++)
            {
                var (handler, cfg, key) = cameras[i];
                var sdPath = cfg.Paths.SdSource;

                if (string.IsNullOrEmpty(sdPath) || !Directory.Exists(sdPath))
                    continue;

                var sdOutcome = await registryService.LookupCameraIdAsync(sdPath, ct);
                if (ct.IsCancellationRequested) return;

                switch (sdOutcome.Result)
                {
                    case SdLookupResult.Matched:

                        if (sdOutcome.CameraId != key)
                        {

                            if (!quiet)
                            {
                                Console.WriteLine();
                                ConsoleHelper.WriteWarning(string.Format(CliStrings.Import_SdCardBelongsTo, sdPath, sdOutcome.CameraId, key));
                                Console.Write(string.Format("  {0} [Y/n] ", CliStrings.Import_ContinueAnyway));

                                var response = Console.ReadKey(intercept: true);
                                Console.WriteLine();
                                if (ct.IsCancellationRequested) return;

                                if (response.KeyChar == 'n' || response.KeyChar == 'N')
                                {
                                    ConsoleHelper.WriteError(CliStrings.Import_Aborted);
                                    return;
                                }

                                Console.WriteLine();
                            }
                        }

                        break;

                    case SdLookupResult.Unknown:

                        var cardInfo = VolumeInfoReader.ReadSdCardInfo(sdPath, logger);

                        if (!string.IsNullOrEmpty(cardInfo.VolumeSerial) && !quiet)
                        {
                            Console.WriteLine();
                            ConsoleHelper.WriteInfo(string.Format(CliStrings.Import_UnknownCard, sdPath, cardInfo.VolumeSerial));
                            Console.Write(string.Format("  {0} [Y/n] ", string.Format(CliStrings.Import_RegisterAsCamera, key)));

                            var response = Console.ReadKey(intercept: true);
                            Console.WriteLine();
                            if (ct.IsCancellationRequested) return;

                            if (response.KeyChar != 'n' && response.KeyChar != 'N')
                            {
                                await registryService.RegisterCardAsync(sdPath, key, ct);
                                ConsoleHelper.WriteSuccess(string.Format(CliStrings.Import_CardRegistered, cardInfo.VolumeSerial, key));
                            }

                            Console.WriteLine();
                        }
                        break;

                    case SdLookupResult.Skipped:

                        logger?.LogDebug("SD-Registry übersprungen: {Reason}", sdOutcome.Reason);
                        break;
                }
            }
        }

        var pipeline = serviceProvider.GetRequiredService<ImportPipelineService>();
        var orchestrationService = serviceProvider.GetRequiredService<IImportOrchestrationService>();

        var gpuQueue = serviceProvider.GetRequiredService<IGpuTaskQueue>();
        var gpuConfig = serviceProvider.GetRequiredService<UmiConfig>().GpuQueue;
        if (gpuConfig.Enabled)
        {
            await gpuQueue.StartAsync(ct);
        }

        var isInteractive = ConsoleHelper.IsInteractiveTerminal();

            if (isInteractive && !quiet)
            {

                var consoleReporter = new SimpleProgressReporter();
                await consoleReporter.RunWithProgressAsync(async progress =>
                {
#pragma warning disable CS8604
                    await RunImportLoopAsync(
                        cameras, config, pipeline, orchestrationService, serviceProvider, logger,
                        source, stabilize, mode, force, noEisSort, dryRun, quiet,
                        reporter: progress, ct: ct, fullImport: fullImport, resetHistory: resetHistory, renameVideos: renameVideos, goProRename: goProRename);
#pragma warning restore CS8604
                }, dryRun);
            }
            else
            {

#pragma warning disable CS8604
                var logReporter = new LogProgressReporter(logger);
                await RunImportLoopAsync(
                    cameras, config, pipeline, orchestrationService, serviceProvider, logger,
                    source, stabilize, mode, force, noEisSort, dryRun, quiet,
                    reporter: logReporter, ct: ct, fullImport: fullImport, resetHistory: resetHistory, renameVideos: renameVideos, goProRename: goProRename);
#pragma warning restore CS8604
            }

        if (gpuConfig.Enabled && gpuQueue.IsRunning)
        {
            var stats = await gpuQueue.GetStatsAsync();
            if (stats.Pending > 0 || stats.InProgress > 0)
            {
                Console.WriteLine();
                Console.WriteLine(string.Format(CliStrings.Import_GpuQueueWaiting, stats.Pending + stats.InProgress));

                var drainDone = new TaskCompletionSource<bool>();
                gpuQueue.QueueEmpty += (s, e) => drainDone.TrySetResult(true);
                using var reg = ct.Register(() => drainDone.TrySetCanceled());

                var updatedStats = await gpuQueue.GetStatsAsync();
                if (updatedStats.Pending > 0 || updatedStats.InProgress > 0)
                    await drainDone.Task;
            }

            await gpuQueue.StopAsync(ct);
        }
    }

    /// <summary>
    /// MTP Direct-Download Import: Delegiert an IMtpImportService.
    /// Device-Erkennung (DetectDevices + Console-Output) bleibt hier — ist CLI-spezifisch.
    /// </summary>
    private static async Task HandleMtpImportAsync(
        string cameraId,
        CameraConfig cfg,
        ImportContext context,
        UmiConfig config,
        ServiceProvider serviceProvider,
        ILogger<Program> logger,
        bool quiet,
        CancellationToken ct)
    {
        var mtpDetectionService = serviceProvider.GetRequiredService<IMtpDeviceDetectionService>();
        var mtpImportService = serviceProvider.GetRequiredService<IMtpImportService>();

        var detectionResults = mtpDetectionService.DetectDevices();
        var match = detectionResults.FirstOrDefault(r =>
            r.CameraId == cameraId && r.Outcome == MtpDetectionOutcome.Registered);

        if (match is null)
        {
            foreach (var ur in detectionResults.Where(r => r.Outcome != MtpDetectionOutcome.Registered))
            {
                logger.LogWarning(
                    "MTP: Unregistriertes Gerät: {Name} ({Model}, Serial: {Serial})",
                    ur.Device.FriendlyName, ur.Device.Model ?? "–",
                    ur.Device.SerialNumber ?? "unbekannt");
            }

            if (!quiet)
                ConsoleHelper.WriteWarning(string.Format(CliStrings.Import_MtpNoDevice, cameraId));
            return;
        }

        if (!quiet)
            Console.WriteLine(string.Format("  → {0}: {1}", CliStrings.Import_MtpDevice, match.Device.FriendlyName));

        var request = new MtpImportRequest(
            CameraId: cameraId,
            CameraConfig: cfg,
            Device: match.Device,
            WorkbenchPath: context.WorkbenchPath,
            GlobalSettings: new GlobalSettings { Paths = config.GlobalPaths },
            Stabilize: context.Stabilize,
            StabilizeMode: context.StabilizeMode,
            InjectGps: context.InjectGps,
            RenameVideos: context.RenameVideos,
            GoProRename: context.GoProRename,
            DryRun: context.DryRun);

        IProgress<MtpImportProgress>? progress = quiet ? null : new Progress<MtpImportProgress>(p =>
            Console.Write($"\r  {p.Current}/{p.Total}: {p.CurrentFile}                    "));

        var result = await mtpImportService.ImportAsync(request, progress, ct);

        if (!quiet)
        {
            Console.WriteLine();
            Console.WriteLine(string.Format(CliStrings.Import_MtpDownloaded, result.Downloaded, FormatHelper.FormatBytes(result.TotalBytes)));
            if (result.Failed > 0)
                ConsoleHelper.WriteWarning(string.Format(CliStrings.Import_MtpErrors, result.Failed));
        }
    }

    private static async Task<List<FileInfo>> GetImportedFileInfos(ImportDatabase db, string cameraId)
    {

        var destPaths = await db.GetCopiedDestPaths(cameraId);
        return destPaths
            .Where(p => File.Exists(p))
            .Select(p => new FileInfo(p))
            .ToList();
    }

    /// <summary>
    /// Führt Import-Loop für alle Kameras aus mit optionalem Progress Reporting.
    /// </summary>
    private static async Task RunImportLoopAsync(
        List<(ICameraHandler?, CameraConfig, string)> cameras,
        UmiConfig config,
        ImportPipelineService pipeline,
        IImportOrchestrationService orchestrationService,
        ServiceProvider serviceProvider,
        ILogger<Program> logger,
        string source,
        bool stabilize,
        string mode,
        bool force,
        bool noEisSort,
        bool dryRun,
        bool quiet,
        IProgressReporter? reporter,
        CancellationToken ct = default,
        bool fullImport = false,
        bool resetHistory = false,
        bool isAdHocFolder = false,
        bool renameVideos = false,
        bool goProRename = false)
    {

        foreach (var (handler, cfg, key) in cameras)
        {
            if (handler == null && !isAdHocFolder) continue;

            if (!quiet)
            {
                Console.WriteLine();
                Console.WriteLine(string.Format("▶ {0}: {1}", CliStrings.Import_Importing, handler?.DisplayName ?? cfg.Name));
                ConsoleHelper.WriteSeparator();
            }

            var historyService = serviceProvider.GetService<IImportHistoryService>();
            if (resetHistory && historyService != null)
            {
                historyService.ClearHistory(key);
                if (!quiet)
                    ConsoleHelper.WriteInfo(string.Format(CliStrings.Import_HistoryReset, key));
            }

            var sourcePath = cfg.SourceType == SourceType.FixedPath
                ? (cfg.SourcePath ?? "")
                : cfg.SourceType == SourceType.MTP
                    ? ""
                    : (cfg.Paths.SdSource ?? "");

            var context = ImportContextFactory.Create(
                key, cfg, sourcePath, config.GlobalPaths.Workbench,
                new GlobalSettings { Paths = config.GlobalPaths },
                injectGps: false,
                stabilize: stabilize,
                stabilizeMode: mode,
                forceStabilize: force,
                noEisSort: noEisSort,
                dryRun: dryRun,
                fullImport: fullImport,
                resetHistory: resetHistory,
                isAdHocFolder: isAdHocFolder,
                renameVideos: renameVideos ? true : (bool?)null,
                goProRename: goProRename ? true : (bool?)null);

            if (cfg.SourceType == SourceType.MTP)
            {
                await HandleMtpImportAsync(key, cfg, context, config, serviceProvider, logger, quiet, ct);
                continue;
            }

            if (historyService is not null && !isAdHocFolder)
            {
                var folderName = cfg.FolderName ?? key;
                var removed = historyService.ReconcileHistory(key, config.GlobalPaths.Workbench, folderName);
                if (removed > 0 && !quiet)
                    ConsoleHelper.WriteInfo(string.Format(CliStrings.Import_HistoryReconciled, removed, key));
            }

            if (dryRun)
            {

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

                var previousLogLevel = Program.ConsoleLogLevel.MinimumLevel;
                if (!quiet)
                    Program.ConsoleLogLevel.MinimumLevel = Serilog.Events.LogEventLevel.Fatal;

                var scanResult = await pipeline.ScanSourceAsync(
                    context, db,
                    progress: quiet ? null : new Progress<ScanProgress>(p =>
                    {
                        Console.Write($"\r  {p.Current}/{p.Total} - {p.Operation}: {p.CurrentFile}                    ");
                    }));

                if (!quiet)
                {
                    Program.ConsoleLogLevel.MinimumLevel = previousLogLevel;
                    Console.WriteLine();
                }

                reporter?.OnScanComplete(key, scanResult.TotalFiles, scanResult.TotalBytes);

                if (!quiet)
                {
                    Console.WriteLine(string.Format(CliStrings.Import_ScanResult, scanResult.Photos, scanResult.Videos, scanResult.Sequences.Count));
                    if (scanResult.Sequences.Count > 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine(string.Format("  {0}:", CliStrings.Import_Sequences));
                        foreach (var seq in scanResult.Sequences)
                        {
                            Console.WriteLine($"    {seq.FolderName}  │ {seq.PhotoCount} Fotos │ Modus: {seq.Mode}");
                        }
                    }
                    Console.WriteLine();

                    if (scanResult.LayoutConflicts.Any() && !force)
                    {
                        Console.WriteLine($"  ⚠ {CliStrings.Import_LayoutConflict}");
                    }

                    Console.WriteLine($"▶ {CliStrings.Import_SimulationPreview}");
                    Console.WriteLine();
                    await PrintSimulationSummaryAsync(db, key, context.WorkbenchPath);
                }

                bool proceedWithImport = false;
                if (ConsoleHelper.IsInteractiveTerminal() && !quiet)
                {
                    Console.WriteLine();
                    Console.Write(string.Format("  {0} [Y/n] ", CliStrings.Import_ConfirmImport));
                    var keyPress = Console.ReadKey(intercept: true);
                    Console.WriteLine();
                    if (ct.IsCancellationRequested) return;
                    proceedWithImport = keyPress.KeyChar is 'y' or 'Y' or '\r';
                }

                if (!proceedWithImport)
                {
                    if (!quiet)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"  {CliStrings.Import_SimulationDone}");
                    }
                    continue;
                }

                if (!quiet)
                {
                    Console.WriteLine();
                    Console.WriteLine($"▶ {CliStrings.Import_CopyingToWorkbench}");
                }

                context.DryRun = false;
            }
            else
            {

                if (!quiet)
                    Console.WriteLine();
            }

            reporter?.OnScanStart(key, handler?.CameraType ?? cfg.CameraType);

            var importResult = await orchestrationService.RunImportAsync(context, reporter, ct);

            if (!quiet)
                Console.WriteLine();

            if (!quiet && importResult.VideoCount > 0)
            {

            }

            if (importResult.Warnings.Any() && !quiet)
            {
                foreach (var warning in importResult.Warnings)
                    ConsoleHelper.WriteWarning(warning);
            }

            if (!importResult.Success && !quiet)
            {
                if (!string.IsNullOrEmpty(importResult.ErrorMessage))
                    ConsoleHelper.WriteError(importResult.ErrorMessage);
            }

            if (!quiet)
                Console.WriteLine();
        }

        if (!quiet)
        {
            ConsoleHelper.WriteSuccess(CliStrings.Import_Completed);
        }
    }

    /// <summary>
    /// </summary>
    private static LayoutConflictResolution PromptConflictResolution(LayoutConflict conflict)
    {
        Console.WriteLine();
        ConsoleHelper.WriteSeparator();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(string.Format(CliStrings.Import_ConflictHeader, conflict.DateFolder, conflict.CameraId));
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine(string.Format(CliStrings.Import_ConflictExisting, conflict.ExistingFiles.Count));
        Console.WriteLine(CliStrings.Import_ConflictNewType);
        Console.WriteLine();
        Console.WriteLine(CliStrings.Import_ConflictOption1);
        Console.WriteLine(CliStrings.Import_ConflictOption1Detail1);
        Console.WriteLine(CliStrings.Import_ConflictOption1Detail2);
        Console.WriteLine();
        Console.WriteLine(CliStrings.Import_ConflictOption2);
        Console.WriteLine(CliStrings.Import_ConflictOption2Detail);
        Console.WriteLine();
        Console.WriteLine(CliStrings.Import_ConflictOption3);
        Console.WriteLine(CliStrings.Import_ConflictOption3Detail1);
        Console.WriteLine(CliStrings.Import_ConflictOption3Detail2);
        Console.WriteLine();
        Console.Write(CliStrings.Import_ConflictPrompt);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.KeyChar)
            {
                case '1':
                case '\r':
                case '\n':
                    Console.WriteLine("1");
                    return LayoutConflictResolution.AddSubfolderForNewType;

                case '2':
                    Console.WriteLine("2");
                    return LayoutConflictResolution.KeepFlat;

                case '3':
                    Console.WriteLine("3");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(CliStrings.Import_ConflictReorgWarning);
                    Console.ResetColor();
                    return LayoutConflictResolution.ReorganizeAll;
            }
        }
    }

    /// <summary>
    /// Ad-hoc Import aus beliebigem Ordner ohne Kamera-Konfiguration.
    /// Nutzt EXIF-Datum für Sortierung, kein History-Tracking.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static async Task ExecuteAdHocFolderAsync(
        string folderPath,
        bool keepStructure,
        bool dryRun,
        bool quiet,
        bool renameVideos,
        bool goProRename,
        string configPath,
        string? profile)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        if (!Directory.Exists(folderPath))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Import_FolderNotFound, folderPath));
            return;
        }

        if (!quiet)
        {
            ConsoleHelper.WriteBanner(CliStrings.Import_AdHocBanner);
            Console.WriteLine(string.Format("  {0}: {1}", CliStrings.Import_FolderLabel, folderPath));
            Console.WriteLine();
        }

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath, profile);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var pipeline = serviceProvider.GetRequiredService<ImportPipelineService>();
        var orchestrationService = serviceProvider.GetRequiredService<IImportOrchestrationService>();

        var adHocId = "AdHoc";
        var adHocConfig = new CameraConfig
        {
            Name = Path.GetFileName(folderPath.TrimEnd('/', '\\')),
            SourceType = SourceType.FixedPath,
            SourcePath = folderPath,
            FlattenSource = !keepStructure,
            FileTypes = new CameraFileTypes
            {
                Video = new[] { ".mp4", ".mov", ".mxf", ".mkv", ".m2ts" },
                Photo = new[] { ".jpg", ".jpeg", ".png", ".dng", ".cr3", ".cr2", ".nef", ".arw", ".raw" }
            }
        };

        if (!quiet)
        {
            ConsoleHelper.WriteOption(CliStrings.Import_FolderLabel, folderPath);
            ConsoleHelper.WriteOption("Structure", keepStructure ? CliStrings.Import_StructureKeep : CliStrings.Import_StructureFlatten);
            ConsoleHelper.WriteOption("Dry-Run", dryRun ? CliStrings.Common_Yes : CliStrings.Common_No);
            Console.WriteLine();
        }

        var cameras = new List<(ICameraHandler?, CameraConfig, string)> { (null, adHocConfig, adHocId) };

#pragma warning disable CS8604
        var logReporter = new LogProgressReporter(logger);
        await RunImportLoopAsync(
            cameras, config, pipeline, orchestrationService, serviceProvider, logger,
            source: adHocId, stabilize: false, mode: "automatic",
            force: false, noEisSort: true, dryRun: dryRun, quiet: quiet,
            reporter: logReporter, ct: ct, isAdHocFolder: true, renameVideos: renameVideos, goProRename: goProRename);
#pragma warning restore CS8604
    }

    /// <summary>
    /// </summary>
    private static async Task<bool> TryAcquireLockAsync(
        ImportLock importLock,
        string workbenchPath,
        CancellationToken ct)
    {
        if (importLock.TryAcquire(workbenchPath, out var blockedBy))
        {

            return true;
        }

        ConsoleHelper.WriteError(CliStrings.Import_AnotherImportRunning);
        Console.WriteLine();

        if (blockedBy != null)
        {
            try
            {
                var lockInfo = System.Text.Json.JsonSerializer.Deserialize<LockInfo>(blockedBy);
                if (lockInfo != null)
                {
                    Console.WriteLine(string.Format(CliStrings.Import_PidInfo, lockInfo.Pid, lockInfo.Started));
                    Console.WriteLine(string.Format("  {0}: {1}, {2}: {3}", CliStrings.Import_SourceCommand, lockInfo.Source, "Command", lockInfo.Command));
                }
                else
                {
                    Console.WriteLine($"  Details: {blockedBy}");
                }
            }
            catch
            {
                Console.WriteLine($"  Details: {blockedBy}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(CliStrings.Import_WaitOrDelete);
        Console.WriteLine(string.Format(CliStrings.Import_DeleteLockManually, Path.Combine(workbenchPath, FolderNameConstants.UmiLock)));

        return false;
    }

    private static ImportFilter CreateFilter(string source, string? type)
    {
        if (source.Equals("ALL", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(type))
        {
            return ImportFilter.All();
        }

        var filter = new ImportFilter();

        if (!source.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var sourceIds = source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var id in sourceIds)
            {
                filter.CameraIds.Add(id);
            }
        }

        if (!string.IsNullOrEmpty(type))
        {
            var types = type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var t in types)
            {

                filter.CameraTypes.Add(t);
            }
        }

        return filter;
    }

    /// <summary>
    /// </summary>
    private static async Task PrintSimulationSummaryAsync(
        ImportDatabase db,
        string cameraId,
        string workbenchPath)
    {
        var summary = await db.GetSimulationSummaryAsync(cameraId);

        if (!summary.Any())
        {
            Console.WriteLine($"  ({CliStrings.Import_NoFilesToImport})");
            return;
        }

        foreach (var day in summary.GroupBy(s => s.CaptureDate))
        {
            var dateStr = day.Key;
            Console.WriteLine($"  📁 {dateStr}/{cameraId}/");

            var photos = day.Where(s => s.MediaType == "photo").Sum(s => s.Count);
            var photosSize = day.Where(s => s.MediaType == "photo").Sum(s => s.TotalSize);
            var videos = day.Where(s => s.MediaType == "video").Sum(s => s.Count);
            var videosSize = day.Where(s => s.MediaType == "video").Sum(s => s.TotalSize);

            if (photos > 0)
            {

                var sequences = await db.GetSequencesForDayAsync(cameraId, dateStr);
                var seqPhotos = sequences.Sum(s => s.PhotoCount);
                var singles = photos - seqPhotos;

                var photoPrefix = (videos == 0 && sequences.Count == 0) ? "└─" : "├─";
                Console.WriteLine($"     {photoPrefix} {photos}x Fotos ({FormatHelper.FormatBytes(photosSize)})");

                if (sequences.Count > 0)
                {
                    foreach (var seq in sequences)
                    {
                        var isLastSeq = seq == sequences.Last();
                        var prefix = (isLastSeq && singles == 0 && videos == 0) ? "└─" : "├─";
                        Console.WriteLine($"     │  {prefix} {seq.FolderName}/  ({seq.PhotoCount} Fotos)");
                    }

                    if (singles > 0)
                    {
                        var prefix = videos == 0 ? "└─" : "├─";
                        Console.WriteLine(string.Format("     │  {0} {1}x {2}", prefix, singles, CliStrings.Import_SinglePhotos));
                    }
                }
            }

            if (videos > 0)
            {
                Console.WriteLine($"     └─ {videos}x Video{(videos > 1 ? "s" : "")} ({FormatHelper.FormatBytes(videosSize)})");
            }

            Console.WriteLine();
        }

        var totalFiles = summary.Sum(s => s.Count);
        var totalSize = summary.Sum(s => s.TotalSize);
        Console.WriteLine($"  ─────────────────────────────");
        Console.WriteLine(string.Format(CliStrings.Import_SimTotal, totalFiles, FormatHelper.FormatBytes(totalSize), workbenchPath));
    }

}
