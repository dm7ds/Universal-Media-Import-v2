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
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;

namespace UMI.CLI.Commands;

/// <summary>
/// Archive Command - Archiviert Workbench in Projekt-Struktur.
/// </summary>
public static class ArchiveCommand
{
    public static Command Create()
    {
        var command = new Command("archive", "Archive workbench to project structure");

        var projectOption = new Option<string>(
            "--project",
            "Projekt-Name (erforderlich)")
        {
            IsRequired = true
        };

        var includeDeliveryOption = new Option<bool>(
            "--include-delivery",
            getDefaultValue: () => false,
            "DVR-Exports auch archivieren");

        command.AddOption(projectOption);
        command.AddOption(includeDeliveryOption);

        command.SetHandler(async (context) =>
        {
            var project         = context.ParseResult.GetValueForOption(projectOption)!;
            var includeDelivery = context.ParseResult.GetValueForOption(includeDeliveryOption);
            var dryRun          = context.ParseResult.GetValueForOption(Program.DryRunOption);
            var configPath      = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            var quiet           = context.ParseResult.GetValueForOption(Program.QuietOption);
            await ExecuteArchiveAsync(project, includeDelivery, dryRun, configPath, quiet);
        });

        return command;
    }

    private static async Task ExecuteArchiveAsync(
        string projectName,
        bool includeDelivery,
        bool dryRun,
        string configPath,
        bool quiet)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        if (!quiet)
        {
            ConsoleHelper.WriteBanner(CliStrings.Archive_Banner);
            Console.WriteLine();
        }

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var archiveService = serviceProvider.GetRequiredService<ArchiveService>();

#pragma warning disable CS8604
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
#pragma warning restore CS8604

        using var importLock = new ImportLock(logger);
        importLock.SetSource("ALL");
        importLock.SetCommand("archive");

        if (!importLock.TryAcquire(config.GlobalPaths.Workbench, out var blockedByInfo))
        {
            ConsoleHelper.WriteError(CliStrings.Common_BlockedByProcess);
            if (!quiet)
            {
                Console.WriteLine();
                ConsoleHelper.PrintLockInfo(blockedByInfo);
                Console.WriteLine();
                ConsoleHelper.WriteWarning(CliStrings.Common_WaitForProcess);
            }
            return;
        }

        var effectiveIncludeDelivery = includeDelivery || config.Archiving.IncludeDeliveryByDefault;

        if (!quiet)
        {
            Console.WriteLine($"Projekt: {projectName}");
            Console.WriteLine($"Include Delivery: {(effectiveIncludeDelivery ? CliStrings.Common_Yes : CliStrings.Common_No)}");
            if (dryRun)
            {
                ConsoleHelper.WriteWarning(CliStrings.Common_DryRunYes);
            }
            Console.WriteLine();
        }

        var validation = await archiveService.ValidateArchiveTargetAsync(projectName, ct);

        if (!validation.IsValid)
        {
            ConsoleHelper.WriteError(validation.ErrorMessage!);
            return;
        }

        if (!quiet)
        {
            Console.WriteLine($"Ziel: {validation.ProjectPath}");
            Console.WriteLine($"Gefunden: {validation.DateFolders.Count} Datum-Ordner");
            foreach (var folder in validation.DateFolders)
            {
                Console.WriteLine($"  • {folder.Name}");
            }
            Console.WriteLine();
        }

        if (!dryRun && !quiet)
        {
            Console.Write(CliStrings.Archive_ConfirmProceed);
            var response = Console.ReadLine();
            if (response?.Equals("Y", StringComparison.OrdinalIgnoreCase) != true)
            {
                Console.WriteLine(CliStrings.Common_Cancelled);
                return;
            }
            Console.WriteLine();
        }

        var options = new ArchiveOptions
        {
            ProjectName = projectName,
            IncludeDelivery = effectiveIncludeDelivery,
            DryRun = dryRun
        };

        var progress = new Progress<ArchiveProgress>(p =>
        {
            if (!quiet)
            {
                Console.WriteLine($"  [{p.Current}/{p.Total}] {p.CurrentFolder}");
            }
        });

        var result = await archiveService.CreateArchiveAsync(options, progress, ct);

        if (!quiet)
        {
            Console.WriteLine();
            if (dryRun)
            {
                Console.WriteLine("[DRY-RUN] Würde erstellen:");
                foreach (var folder in result.CreatedFolders)
                {
                    Console.WriteLine($"  • {folder}/");
                }
            }
            else
            {
                ConsoleHelper.WriteSuccess(string.Format(CliStrings.Archive_Created, validation.ProjectPath));
                Console.WriteLine($"  Ordner: {result.FoldersCopied}");
                Console.WriteLine($"  Dateien: {result.FilesCopied}");
                Console.WriteLine($"  Größe: {FormatHelper.FormatBytes(result.BytesCopied)}");
            }

            if (!effectiveIncludeDelivery)
            {
                Console.WriteLine();
                ConsoleHelper.WriteWarning(CliStrings.Archive_DeliveryNotIncluded);
                ConsoleHelper.WriteWarning(CliStrings.Archive_DeliveryHint);
            }
        }
    }
}
