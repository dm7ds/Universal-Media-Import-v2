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
using Microsoft.Extensions.DependencyInjection;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.Core.Utilities;

namespace UMI.CLI.Commands;

/// <summary>
/// GPS Command - GPX-Dateien erstellen, GPS injizieren, GPS-Status prüfen.
/// Drei Subcommands: create, inject, verify.
/// </summary>
public static class GpsCommand
{
    public static Command Create()
    {
        var command = new Command("gps", "Manage GPS data (create GPX, inject, verify)");

        var dateOption = new Option<string?>("--date", "Process specific date only (yyyy-MM-dd)");
        var sourceOption = new Option<string>("--source", () => "ALL", "Camera ID(s) comma-separated (e.g. GoPro11,DroneX or ALL)");
        var forceOption = new Option<bool>("--force", "Overwrite existing data");

        command.AddCommand(CreateCreateCommand(dateOption, sourceOption, forceOption));
        command.AddCommand(CreateInjectCommand(dateOption, sourceOption, forceOption));
        command.AddCommand(CreateVerifyCommand(dateOption, sourceOption));

        return command;
    }

    private static Command CreateCreateCommand(
        Option<string?> dateOption,
        Option<string> sourceOption,
        Option<bool> forceOption)
    {
        var command = new Command("create",
            "Build optimized GPX files from tracker data for videos in workbench");

        command.AddOption(dateOption);
        command.AddOption(sourceOption);
        command.AddOption(forceOption);

        command.SetHandler(async (context) =>
        {
            var date       = context.ParseResult.GetValueForOption(dateOption);
            var source     = context.ParseResult.GetValueForOption(sourceOption)!;
            var force      = context.ParseResult.GetValueForOption(forceOption);
            var dryRun     = context.ParseResult.GetValueForOption(Program.DryRunOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            var profile    = context.ParseResult.GetValueForOption(Program.ProfileOption);

            await ExecuteCreateAsync(date, source, force, dryRun, configPath, profile);
        });

        return command;
    }

    private static async Task ExecuteCreateAsync(
        string? date,
        string source,
        bool force,
        bool dryRun,
        string configPath,
        string? profile)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Gps_CreateBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath, profile);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var gpsService = serviceProvider.GetRequiredService<GpsService>();

        var workbench = config.GlobalPaths.Workbench;
        var gpxSource = config.GlobalPaths.GpxSource;

        PrintFilterInfo(date, source, dryRun);

        if (string.IsNullOrEmpty(gpxSource) || !Directory.Exists(gpxSource))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Gps_GpxSourceNotFound, gpxSource ?? CliStrings.Gps_GpxNotConfigured));
            return;
        }

        var videos = FindVideos(workbench, date, source, includeGyroflow: false);

        if (videos.Count == 0)
        {
            ConsoleHelper.WriteWarning(CliStrings.Gps_NoVideosFound);
            return;
        }

        Console.WriteLine(string.Format(CliStrings.Gps_VideosFound, videos.Count));
        Console.WriteLine();
        ConsoleHelper.WriteSeparator();

        int created = 0, skipped = 0, noMatch = 0;

        foreach (var video in videos)
        {
            ct.ThrowIfCancellationRequested();

            var metaPath = PathHelper.GetUmiPath(workbench, video.FullName, FolderNameConstants.UmiSubDir.Gps, "_optimized.gpx");
            var alreadyExists = File.Exists(metaPath);

            if (alreadyExists && !force)
            {
                skipped++;
                continue;
            }

            Console.Write($"  {video.Name,-45} ");

            if (dryRun)
            {
                ConsoleHelper.WriteColored("[DRY-RUN]", ConsoleColor.Yellow);
                Console.WriteLine();
                created++;
                continue;
            }

            var result = await gpsService.OptimizeGpsForVideoAsync(video.FullName, gpxSource, ct);

            if (result != null)
            {
                ConsoleHelper.WriteColored(CliStrings.Gps_GpxCreated, ConsoleColor.Green);
                Console.WriteLine();
                created++;
            }
            else
            {
                ConsoleHelper.WriteColored(CliStrings.Gps_NoGpsMatch, ConsoleColor.Gray);
                Console.WriteLine();
                noMatch++;
            }
        }

        Console.WriteLine();
        ConsoleHelper.WriteSeparator();
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Gps_CreateSummary, videos.Count, created, skipped, noMatch));
    }

    private static Command CreateInjectCommand(
        Option<string?> dateOption,
        Option<string> sourceOption,
        Option<bool> forceOption)
    {
        var command = new Command("inject",
            "Inject GPS data from pre-built GPX files into videos (incl. finalize workflow for DVR exports)");

        command.AddOption(dateOption);
        command.AddOption(sourceOption);
        command.AddOption(forceOption);

        command.SetHandler(async (context) =>
        {
            var date       = context.ParseResult.GetValueForOption(dateOption);
            var source     = context.ParseResult.GetValueForOption(sourceOption)!;
            var force      = context.ParseResult.GetValueForOption(forceOption);
            var dryRun     = context.ParseResult.GetValueForOption(Program.DryRunOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            var profile    = context.ParseResult.GetValueForOption(Program.ProfileOption);

            await ExecuteInjectAsync(date, source, force, dryRun, configPath, profile);
        });

        return command;
    }

    private static async Task ExecuteInjectAsync(
        string? date,
        string source,
        bool force,
        bool dryRun,
        string configPath,
        string? profile)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Gps_InjectBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath, profile);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var gpsService = serviceProvider.GetRequiredService<GpsService>();
        var postProcessingService = serviceProvider.GetRequiredService<IPostProcessingService>();
        var exifTool = serviceProvider.GetRequiredService<IExifToolWrapper>();

        var workbench = config.GlobalPaths.Workbench;
        var gpxSource = config.GlobalPaths.GpxSource;

        PrintFilterInfo(date, source, dryRun);

        if (string.IsNullOrEmpty(gpxSource) || !Directory.Exists(gpxSource))
        {
            Console.WriteLine(CliStrings.Gps_NoGpxSourceWarning);
            Console.WriteLine(string.Format(CliStrings.Gps_ExpectedPath, gpxSource ?? CliStrings.Gps_GpxNotConfigured));
            return;
        }

        var postProcessingOptions = new PostProcessingOptions
        {
            Workbench = workbench,
            Source = source,
            Date = date,
            Mode = "automatic",
            Force = force,
            DryRun = dryRun,
            GpxSource = gpxSource
        };

        ConsoleHelper.WriteSeparator();
        Console.WriteLine(CliStrings.Gps_FinalizeCheck);

        var exportedPairs = postProcessingService.FindExportedVideosForFinalize(workbench, date, source);

        if (exportedPairs.Count > 0)
        {
            Console.WriteLine(string.Format(CliStrings.Gps_ExportedFound, exportedPairs.Count));

            if (dryRun)
            {
                ConsoleHelper.WriteWarning(string.Format(CliStrings.Gps_DryRunFinalize, exportedPairs.Count));
            }
            else
            {
                var (finalized, finFailed) = await postProcessingService.FinalizeExportedVideosAsync(
                    exportedPairs, postProcessingOptions, ct);

                ConsoleHelper.WriteSuccess(string.Format(CliStrings.Gps_FinalizeSummary, finalized, finFailed));
            }
        }
        else
        {
            Console.WriteLine(CliStrings.Gps_NoExportedVideos);
        }

        Console.WriteLine();
        ConsoleHelper.WriteSeparator();
        Console.WriteLine(CliStrings.Gps_InjectionHeader);
        Console.WriteLine();

        var videos = FindVideos(workbench, date, source, includeGyroflow: false);

        if (videos.Count == 0)
        {
            ConsoleHelper.WriteWarning(CliStrings.Gps_NoInjectionVideos);
            return;
        }

        Console.WriteLine(string.Format(CliStrings.Gps_VideosFound, videos.Count));
        Console.WriteLine();

        int injected = 0, skippedGps = 0, noGpx = 0;

        foreach (var video in videos)
        {
            ct.ThrowIfCancellationRequested();

            Console.Write($"  {video.Name,-45} ");

            if (!force)
            {
                var hasGps = await HasGpsDataAsync(exifTool, video.FullName, ct);
                if (hasGps)
                {
                    ConsoleHelper.WriteColored(CliStrings.Gps_SkippedGpsPresent, ConsoleColor.Gray);
                    Console.WriteLine();
                    skippedGps++;
                    continue;
                }
            }

            if (dryRun)
            {
                ConsoleHelper.WriteColored("[DRY-RUN]", ConsoleColor.Yellow);
                Console.WriteLine();
                injected++;
                continue;
            }

            var success = await gpsService.InjectOptimizedGpsAsync(video.FullName, gpxSource, ct);

            if (success)
            {
                ConsoleHelper.WriteColored(CliStrings.Gps_GpsInjected, ConsoleColor.Green);
                Console.WriteLine();
                injected++;
            }
            else
            {
                ConsoleHelper.WriteColored(CliStrings.Gps_NoGpxMatch, ConsoleColor.Gray);
                Console.WriteLine();
                noGpx++;
            }
        }

        Console.WriteLine();
        ConsoleHelper.WriteSeparator();
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Gps_InjectSummary, injected, skippedGps, noGpx));
    }

    private static Command CreateVerifyCommand(
        Option<string?> dateOption,
        Option<string> sourceOption)
    {
        var command = new Command("verify",
            "Show GPS status of all videos in workbench (GPX present, GPS in video)");

        command.AddOption(dateOption);
        command.AddOption(sourceOption);

        command.SetHandler(async (context) =>
        {
            var date       = context.ParseResult.GetValueForOption(dateOption);
            var source     = context.ParseResult.GetValueForOption(sourceOption)!;
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            var profile    = context.ParseResult.GetValueForOption(Program.ProfileOption);

            await ExecuteVerifyAsync(date, source, configPath, profile);
        });

        return command;
    }

    private static async Task ExecuteVerifyAsync(
        string? date,
        string source,
        string configPath,
        string? profile)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Gps_VerifyBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath, profile);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var exifTool = serviceProvider.GetRequiredService<IExifToolWrapper>();

        var workbench = config.GlobalPaths.Workbench;

        var filterLabel = BuildFilterLabel(date, source);
        Console.WriteLine(string.Format("{0}: {1}", CliStrings.Gps_VerifyLabel, filterLabel));
        Console.WriteLine();

        var videos = FindVideos(workbench, date, source, includeGyroflow: false);

        if (videos.Count == 0)
        {
            ConsoleHelper.WriteWarning(CliStrings.Gps_NoVideosFound);
            return;
        }

        var rows = new List<(string Name, bool HasGps, bool HasGpx, string? Note)>();

        foreach (var video in videos)
        {
            ct.ThrowIfCancellationRequested();

            Console.Write($"\r  {video.Name,-40}");

            var metaPath = PathHelper.GetUmiPath(workbench, video.FullName, FolderNameConstants.UmiSubDir.Gps, "_optimized.gpx");
            var hasGpx = File.Exists(metaPath);
            var hasGps = await HasGpsDataAsync(exifTool, video.FullName, ct);

            string? note = (!hasGps && hasGpx) ? CliStrings.Gps_NoteInjectNeeded
                         : (!hasGps && !hasGpx) ? CliStrings.Gps_NoteNoGpsMatch
                         : null;

            rows.Add((video.Name, hasGps, hasGpx, note));
        }

        Console.Write("\r" + new string(' ', 60) + "\r");

        ConsoleHelper.WriteSeparator();

        const int nameWidth = 35;
        Console.WriteLine($"  {"Video",-nameWidth} {"GPS",5}  {"GPX",5}");
        Console.WriteLine($"  {new string('─', nameWidth)} {"─────",5}  {"─────",5}");

        foreach (var (name, hasGps, hasGpx, note) in rows)
        {
            var gpsSymbol = hasGps ? "✓" : "✗";
            var gpxSymbol = hasGpx ? "✓" : "✗";
            var noteStr = note != null ? $"  ← {note}" : "";

            Console.Write($"  {name,-nameWidth} ");

            Console.ForegroundColor = hasGps ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write($"{gpsSymbol,5}");
            Console.ResetColor();

            Console.Write("  ");

            Console.ForegroundColor = hasGpx ? ConsoleColor.Green : ConsoleColor.Gray;
            Console.Write($"{gpxSymbol,5}");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(noteStr);
            Console.ResetColor();
        }

        ConsoleHelper.WriteSeparator();

        var withGps = rows.Count(r => r.HasGps);
        var withGpx = rows.Count(r => r.HasGpx);

        Console.WriteLine();
        Console.WriteLine(string.Format(CliStrings.Gps_VerifySummary, withGps, rows.Count, withGpx));

        if (withGps < rows.Count)
        {
            Console.WriteLine();
            ConsoleHelper.WriteInfo(CliStrings.Gps_TipInject);
        }
    }

    /// <summary>
    /// Findet Videos in der Workbench mit optionalem Datum- und Source-Filter.
    /// Überspringt Gyroflow/, postprocess/, .metadata/ Ordner.
    /// </summary>
    private static List<FileInfo> FindVideos(
        string workbench,
        string? date,
        string source,
        bool includeGyroflow)
    {
        var searchPath = !string.IsNullOrEmpty(date)
            ? Path.Combine(workbench, date)
            : workbench;

        if (!Directory.Exists(searchPath))
            return [];

        var skipFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FolderNameConstants.PostProcess, FolderNameConstants.MetadataDir, FolderNameConstants.Stabilized, FolderNameConstants.TimeLapse, FolderNameConstants.Export, FolderNameConstants.Gps
        };

        if (!includeGyroflow)
            skipFolders.Add(FolderNameConstants.Gyroflow);

        var sourceIds = source.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            ? null
            : source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allMp4 = Directory.EnumerateFiles(searchPath, "*.mp4", SearchOption.AllDirectories);
        var allMov = Directory.EnumerateFiles(searchPath, "*.mov", SearchOption.AllDirectories);

        return allMp4.Concat(allMov)
            .Where(path =>
            {

                var dir = Path.GetDirectoryName(path) ?? "";
                var parts = dir.Split(Path.DirectorySeparatorChar);
                if (parts.Any(p => skipFolders.Contains(p)))
                    return false;

                if (sourceIds != null)
                {
                    return parts.Any(p => sourceIds.Contains(p));
                }

                return true;
            })
            .Select(p => new FileInfo(Path.GetFullPath(p)))
            .OrderBy(f => f.FullName)
            .ToList();
    }

    /// <summary>
    /// Prüft ob ein Video bereits GPS-Daten enthält (via ExifTool).
    /// </summary>
    private static async Task<bool> HasGpsDataAsync(
        IExifToolWrapper exifTool,
        string videoPath,
        CancellationToken ct)
    {
        try
        {
            var metadata = await exifTool.ReadMetadataAsync(
                videoPath, ["GPSLatitude"], ct);

            return metadata.TryGetValue("GPSLatitude", out var lat)
                && lat != null
                && !string.IsNullOrWhiteSpace(lat.ToString());
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gibt Filter-Informationen auf der Konsole aus.
    /// </summary>
    private static void PrintFilterInfo(string? date, string source, bool dryRun)
    {
        Console.WriteLine(string.Format("{0}: {1}", CliStrings.Gps_FilterLabel, source));
        Console.WriteLine(string.Format("{0}: {1}", CliStrings.Gps_DateLabel, date ?? CliStrings.Gps_DateAll));

        if (dryRun)
            ConsoleHelper.WriteWarning(CliStrings.Common_DryRunYes);
        else
            Console.WriteLine(CliStrings.Common_DryRunNo);

        Console.WriteLine();
    }

    /// <summary>
    /// Baut einen lesbaren Filter-Label-String (z.B. "2026-02-20/GoPro11").
    /// </summary>
    private static string BuildFilterLabel(string? date, string source)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(date)) parts.Add(date);
        if (!source.Equals("ALL", StringComparison.OrdinalIgnoreCase)) parts.Add(source);
        return parts.Count > 0 ? string.Join("/", parts) : CliStrings.Gps_AllDatesSources;
    }
}
