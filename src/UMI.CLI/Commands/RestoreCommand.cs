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
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;

namespace UMI.CLI.Commands;

public static class RestoreCommand
{
    public static Command Create()
    {
        var command = new Command("restore", "Restore metadata from backups");

        var sourceOption = new Option<string>(
            "--source",
            getDefaultValue: () => "ALL",
            "Camera ID (GoPro11, MyDSLR, ALL)");

        var forceOption = new Option<bool>(
            "--force",
            "Restore even if metadata is OK");

        command.AddOption(sourceOption);
        command.AddOption(forceOption);

        command.SetHandler(async (context) =>
        {
            var source     = context.ParseResult.GetValueForOption(sourceOption)!;
            var force      = context.ParseResult.GetValueForOption(forceOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteRestoreAsync(source, force, configPath);
        });

        return command;
    }

    private static async Task ExecuteRestoreAsync(string source, bool force, string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Restore_Banner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var metadataService = serviceProvider.GetRequiredService<MetadataService>();

#pragma warning disable CS8604
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
#pragma warning restore CS8604

        using var importLock = new ImportLock(logger);
        importLock.SetSource(source);
        importLock.SetCommand("restore");

        if (!importLock.TryAcquire(config.GlobalPaths.Workbench, out var blockedByInfo))
        {
            ConsoleHelper.WriteError(CliStrings.Common_BlockedByProcess);
            Console.WriteLine();
            ConsoleHelper.PrintLockInfo(blockedByInfo);
            Console.WriteLine();
            ConsoleHelper.WriteWarning(CliStrings.Common_WaitForProcess);
            return;
        }

        var workbench = config.GlobalPaths.Workbench;

        if (!Directory.Exists(workbench))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Restore_WorkbenchNotFound, workbench));
            return;
        }

        var videos = Directory.GetFiles(workbench, "*.mp4", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(workbench, "*.mov", SearchOption.AllDirectories))
            .Where(v => !v.Contains(FolderNameConstants.Stabilized))
            .ToList();

        Console.WriteLine(string.Format(CliStrings.Restore_VideosFound, videos.Count));
        Console.WriteLine();

        int restored = 0;
        int failed = 0;

        foreach (var video in videos)
        {
            if (ct.IsCancellationRequested)
            {
                ConsoleHelper.WriteWarning(CliStrings.Restore_CancelledByUser);
                break;
            }

            var fileName = Path.GetFileName(video);
            Console.Write($"  {fileName}... ");

            var result = await metadataService.RestoreMetadataAsync(video, force, ct);

            if (result == MetadataRestoreResult.Restored)
            {
                ConsoleHelper.WriteSuccess("");
                restored++;
            }
            else
            {
                ConsoleHelper.WriteError(string.Format(CliStrings.Restore_Error, result));
                failed++;
            }
        }

        Console.WriteLine();
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Restore_Done, restored, failed));
    }
}
