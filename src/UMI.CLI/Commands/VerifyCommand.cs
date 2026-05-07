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
using UMI.Data;

namespace UMI.CLI.Commands;

/// <summary>
/// Verify Command - Post-Import Verification oder Standalone Workbench-Check.
/// </summary>
public static class VerifyCommand
{
    public static Command Create()
    {
        var command = new Command("verify", "Verify integrity and metadata (post-import or workbench scan)");

        var sourceOption = new Option<string>(
            "--source",
            getDefaultValue: () => "ALL",
            "Camera ID (GoPro11, MyDSLR, ALL)");

        var postImportOption = new Option<bool>(
            "--post-import",
            getDefaultValue: () => false,
            "Post-import mode (uses .umi.db as reference)");

        command.AddOption(sourceOption);
        command.AddOption(postImportOption);

        command.SetHandler(async (context) =>
        {
            var source     = context.ParseResult.GetValueForOption(sourceOption)!;
            var postImport = context.ParseResult.GetValueForOption(postImportOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            var quiet      = context.ParseResult.GetValueForOption(Program.QuietOption);
            await ExecuteVerifyAsync(source, postImport, configPath, quiet);
        });

        return command;
    }

    private static async Task ExecuteVerifyAsync(string source, bool postImport, string configPath, bool quiet)
    {
        if (!quiet)
        {
            ConsoleHelper.WriteBanner(CliStrings.Verify_Banner);
        }

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var verificationService = serviceProvider.GetRequiredService<VerificationService>();
        var ffprobe = serviceProvider.GetService<FFprobeWrapper>();

        if (postImport)
        {

            var dbPath = Path.Combine(config.GlobalPaths.Workbench, FolderNameConstants.UmiDir, ".umi.db");
            if (!File.Exists(dbPath))
            {
                ConsoleHelper.WriteError(string.Format(CliStrings.Verify_DbNotFound, dbPath));
                if (!quiet)
                {
                    Console.WriteLine(CliStrings.Verify_RunImportFirst);
                }
                return;
            }

            await using var db = serviceProvider.GetRequiredService<ImportDatabase>();
            await db.InitializeAsync();

            var cameraId = source == "ALL" ? null : source;
            if (!quiet)
            {
                Console.WriteLine(string.Format(CliStrings.Verify_PostImport, cameraId != null ? string.Format(CliStrings.Verify_ForCamera, cameraId) : ""));
                Console.WriteLine();
            }

            var result = await verificationService.VerifyImportAsync(db, cameraId);

            PrintResult(result, quiet);
        }
        else
        {

            var workbench = config.GlobalPaths.Workbench;

            if (!Directory.Exists(workbench))
            {
                ConsoleHelper.WriteError(string.Format(CliStrings.Verify_WorkbenchNotFound, workbench));
                return;
            }

            if (ffprobe == null && !quiet)
            {
                ConsoleHelper.WriteWarning(CliStrings.Verify_FfprobeNotAvailable);
                Console.WriteLine();
            }

            var cameraId = source == "ALL" ? null : source;
            if (!quiet)
            {
                Console.WriteLine(string.Format(CliStrings.Verify_WorkbenchCheck, cameraId != null ? string.Format(CliStrings.Verify_ForCamera, cameraId) : ""));
                Console.WriteLine();
            }

            var result = await verificationService.VerifyWorkbenchAsync(workbench, config, cameraId, ffprobe);

            PrintResult(result, quiet);
        }
    }

    private static void PrintResult(UMI.Core.Models.VerifyResult result, bool quiet)
    {
        if (!quiet)
        {
            Console.WriteLine(string.Format(CliStrings.Verify_FilesChecked, result.TotalFiles));
            Console.WriteLine();

            if (result.Verified > 0)
            {
                Console.Write("  ");
                ConsoleHelper.WriteColored(string.Format("✓ {0} {1}", result.Verified, CliStrings.Verify_Ok), ConsoleColor.Green);
                Console.WriteLine();
            }

            var warningCount = result.NoBackup + result.TimestampMismatch;
            if (warningCount > 0)
            {
                Console.Write("  ");
                ConsoleHelper.WriteColored(string.Format("⚠ {0} {1}", warningCount, CliStrings.Verify_Warnings), ConsoleColor.Yellow);
                Console.WriteLine();
            }

            var errorCount = result.Missing + result.SizeMismatch + result.Corrupt;
            if (errorCount > 0)
            {
                Console.Write("  ");
                ConsoleHelper.WriteColored(string.Format("✗ {0} {1}", errorCount, CliStrings.Verify_Errors), ConsoleColor.Red);
                Console.WriteLine();
            }

            Console.WriteLine();

            var errors = result.Issues.Where(i => i.Severity == "Error").ToList();
            if (errors.Any())
            {
                Console.WriteLine(CliStrings.Verify_ErrorsHeader);
                foreach (var issue in errors)
                {
                    Console.Write("  ");
                    ConsoleHelper.WriteColored("✗", ConsoleColor.Red);
                    Console.Write($" {Path.GetFileName(issue.FilePath)} - {issue.Message}");
                    Console.WriteLine();
                }
                Console.WriteLine();
            }

            var warnings = result.Issues.Where(i => i.Severity == "Warning").ToList();
            if (warnings.Any())
            {
                Console.WriteLine(CliStrings.Verify_WarningsHeader);
                foreach (var issue in warnings.Take(10))
                {
                    Console.Write("  ");
                    ConsoleHelper.WriteColored("⚠", ConsoleColor.Yellow);
                    Console.Write($" {Path.GetFileName(issue.FilePath)} - {issue.Message}");
                    Console.WriteLine();
                }

                if (warnings.Count > 10)
                {
                    Console.WriteLine(string.Format(CliStrings.Verify_MoreWarnings, warnings.Count - 10));
                }
                Console.WriteLine();
            }

            if (result.IsClean)
            {
                ConsoleHelper.WriteSuccess(CliStrings.Verify_AllCorrect);
            }
            else
            {
                ConsoleHelper.WriteError(string.Format(CliStrings.Verify_Failed, errorCount));
            }
        }
        else
        {

            var errorCount = result.Missing + result.SizeMismatch + result.Corrupt;
            if (errorCount > 0)
            {
                ConsoleHelper.WriteError(string.Format(CliStrings.Verify_Failed, errorCount));
            }
        }
    }
}
