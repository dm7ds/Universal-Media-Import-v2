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
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;

namespace UMI.CLI.Commands;

/// <summary>
/// EXIF-Scan Command - Analysiert EXIF-Felder in einem Ordner
/// </summary>
public static class ExifScanCommand
{
    public static Command Create()
    {
        var command = new Command("exif-scan", "Scan EXIF fields in photos and show fields present in all images");

        var pathArgument = new Argument<string>(
            "path",
            "Folder with photos (recursive)");

        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "table",
            "Output format (table|json)");

        var categoryOption = new Option<string?>(
            "--category",
            "Filter by category (Shooting, Exposure, Focus, Camera, Image, Time, File, Other)");

        var minCoverageOption = new Option<int>(
            "--min-coverage",
            getDefaultValue: () => 100,
            "Minimum coverage in % (default: 100 = in all images)");

        command.AddArgument(pathArgument);
        command.AddOption(formatOption);
        command.AddOption(categoryOption);
        command.AddOption(minCoverageOption);

        command.SetHandler(async (path, format, category, minCoverage) =>
        {
            await ExecuteAsync(path, format, category, minCoverage);
        }, pathArgument, formatOption, categoryOption, minCoverageOption);

        return command;
    }

    private static async Task ExecuteAsync(string path, string format, string? category, int minCoverage)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        if (format != "table" && format != "json")
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.ExifScan_InvalidFormat, format));
            return;
        }

        if (!Directory.Exists(path))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.ExifScan_FolderNotFound, path));
            return;
        }

        if (format == "table")
        {
            ConsoleHelper.WriteBanner(CliStrings.ExifScan_Banner);
            Console.WriteLine();
        }

        await using var serviceProvider = await Program.BuildServiceProviderAsync();
        var analyzer = serviceProvider.GetRequiredService<IExifFieldAnalyzerService>();

        ExifAnalysisResult result;

        if (format == "table")
        {
            result = await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask($"[green]{CliStrings.ExifScan_ScanningFields}[/]", maxValue: 100);

                    var progress = new Progress<ExifScanProgress>(p =>
                    {
                        task.Description = $"[green]{CliStrings.ExifScan_ScanningFields}[/] ({p.ScannedFiles}/{p.TotalFiles})";
                        task.Value = p.TotalFiles > 0 ? (p.ScannedFiles * 100.0 / p.TotalFiles) : 0;
                    });

                    return await analyzer.AnalyzeFolderAsync(path, progress, ct);
                });
        }
        else
        {

            result = await analyzer.AnalyzeFolderAsync(path, null, ct);
        }

        var filteredGroups = result.FieldGroups;

        if (!string.IsNullOrEmpty(category))
        {
            filteredGroups = filteredGroups
                .Where(g => g.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filteredGroups.Count == 0 && format == "table")
            {
                ConsoleHelper.WriteWarning(string.Format(CliStrings.ExifScan_NoCategoryFields, category));
                return;
            }
        }

        if (format == "json")
        {
            OutputJson(result, filteredGroups);
        }
        else
        {
            OutputTable(result, filteredGroups, path);
        }
    }

    private static void OutputTable(ExifAnalysisResult result, List<ExifFieldGroup> groups, string path)
    {
        Console.WriteLine();

        if (result.TotalPhotos == 0)
        {
            ConsoleHelper.WriteWarning(CliStrings.ExifScan_NoPhotos);
            return;
        }

        var totalFields = groups.Sum(g => g.Fields.Count);

        var panel = new Panel(BuildTableContent(groups))
        {
            Header = new PanelHeader(string.Format(CliStrings.ExifScan_PanelHeader, result.TotalPhotos, totalFields)),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);

        Console.WriteLine();
        Console.WriteLine(CliStrings.ExifScan_Tip);
        Console.WriteLine($"   umi exif-scan \"{path}\" --format json > exif_fields.json");
    }

    private static string BuildTableContent(List<ExifFieldGroup> groups)
    {
        var lines = new List<string>();

        foreach (var group in groups)
        {

            lines.Add($"[bold yellow]{group.Category.ToUpperInvariant()}[/]");

            foreach (var field in group.Fields)
            {
                var value = field.SampleValue;

                if (value.Length > 50)
                {
                    value = value.Substring(0, 47) + "...";
                }

                lines.Add($"  [cyan]{field.FieldName,-30}[/] = {value}");
            }

            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    private static void OutputJson(ExifAnalysisResult result, List<ExifFieldGroup> filteredGroups)
    {
        var output = new
        {
            total_photos = result.TotalPhotos,
            field_groups = filteredGroups.Select(g => new
            {
                category = g.Category,
                fields = g.Fields.Select(f => new
                {
                    name = f.FieldName,
                    directory = f.Directory,
                    sample_value = f.SampleValue,
                    numeric_value = f.NumericValue,
                    is_present_in_all = f.IsPresentInAll
                }).ToList()
            }).ToList()
        };

        var json = JsonSerializer.Serialize(output, JsonDefaults.WriteOptions);
        Console.WriteLine(json);
    }
}
