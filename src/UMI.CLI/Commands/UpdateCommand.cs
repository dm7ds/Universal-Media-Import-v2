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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using UMI.CLI.Resources;
using UMI.Core.Services;

namespace UMI.CLI.Commands;

/// <summary>
/// Update Command — prüft auf neue UMI-Versionen und lädt die Setup-EXE herunter.
/// umi update --check  → zeigt aktuelle + neueste Version, Update-Verfügbarkeit, Download-URL
/// umi update --apply  → check + download + UAC-Start des Installers + CLI beendet sich
/// </summary>
public static class UpdateCommand
{
    // Exit codes
    private const int ExitOk = 0;
    private const int ExitCheckFailed = 1;
    private const int ExitDownloadFailed = 2;
    private const int ExitLaunchFailed = 3;

    public static Command Create()
    {
        var command = new Command("update", "Check for UMI updates or download and apply the latest release");

        var checkOption = new Option<bool>(
            "--check",
            getDefaultValue: () => false,
            "Check if a newer version is available");

        var applyOption = new Option<bool>(
            "--apply",
            getDefaultValue: () => false,
            "Download and install the latest version (implies --check)");

        command.AddOption(checkOption);
        command.AddOption(applyOption);

#pragma warning disable CA1416 // UMI.CLI is Windows-only (win-x64 publish profile)
        command.SetHandler(async (context) =>
        {
            var doCheck = context.ParseResult.GetValueForOption(checkOption);
            var doApply = context.ParseResult.GetValueForOption(applyOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;

            // --apply implies --check
            if (!doCheck && !doApply)
                doCheck = true;

            var ct = context.GetCancellationToken();
            context.ExitCode = await ExecuteUpdateAsync(doCheck || doApply, doApply, configPath, ct);
        });
#pragma warning restore CA1416

        return command;
    }

    [SupportedOSPlatform("windows")]
    [SuppressMessage("Interoperability", "CA1416", Justification = "UMI.CLI targets Windows only (win-x64 publish profile).")]
    private static async Task<int> ExecuteUpdateAsync(bool doCheck, bool doApply, string configPath, CancellationToken ct)
    {
        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var updateService = serviceProvider.GetRequiredService<IUpdateService>();

        // ── Check ────────────────────────────────────────────────────────────
        Console.WriteLine(CliStrings.Update_CheckTitle);
        Console.WriteLine();

        UpdateCheckResult result;
        try
        {
            result = await updateService.CheckForUpdatesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine(CliStrings.Update_Canceled);
            return ExitOk;
        }
        catch (Exception)
        {
            Console.Error.WriteLine(CliStrings.Update_CheckFailed);
            return ExitCheckFailed;
        }

        // If check returned without exception but we suspect network failure:
        // CheckForUpdatesAsync swallows errors and returns IsUpdateAvailable=false.
        // We trust the result — if no update, tell user they're up to date.

        Console.WriteLine(string.Format(CliStrings.Update_CurrentVersion, result.CurrentVersion));
        Console.WriteLine(string.Format(CliStrings.Update_LatestVersion, result.LatestVersion));
        Console.WriteLine();

        if (result.IsUpdateAvailable)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(CliStrings.Update_Available);
            Console.ResetColor();

            if (!string.IsNullOrEmpty(result.DownloadUrl))
                Console.WriteLine(string.Format(CliStrings.Update_DownloadUrl, result.DownloadUrl));
        }
        else
        {
            Console.WriteLine(CliStrings.Update_UpToDate);
            return ExitOk;
        }

        if (!doApply)
            return ExitOk;

        // ── Apply ─────────────────────────────────────────────────────────────
        if (string.IsNullOrEmpty(result.DownloadUrl))
        {
            Console.Error.WriteLine(CliStrings.Update_NoDownloadUrl);
            return ExitDownloadFailed;
        }

        var version = result.LatestVersion;
        var targetPath = Path.Combine(Path.GetTempPath(), $"UMI_Setup_{version}.exe");

        // Simple console progress reporter — overwrites the current line
        int lastPercent = -1;
        var progress = new Progress<double>(p =>
        {
            var percent = (int)(p * 100);
            if (percent == lastPercent)
                return;
            lastPercent = percent;
            Console.Write($"\r{string.Format(CliStrings.Update_Downloading, percent)}");
        });

        try
        {
            await updateService.DownloadSetupAsync(result.DownloadUrl, targetPath, progress, ct);
            Console.WriteLine(); // newline after progress line
            Console.WriteLine(CliStrings.Update_DownloadComplete);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine(CliStrings.Update_Canceled);
            return ExitOk;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.Error.WriteLine(string.Format(CliStrings.Update_DownloadFailed, ex.Message));
            return ExitDownloadFailed;
        }

        // ── Launch ────────────────────────────────────────────────────────────
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(string.Format(CliStrings.Update_LaunchFailed, ex.Message));
            return ExitLaunchFailed;
        }

        // CLI exits cleanly — installer takes over
        return ExitOk;
    }
}
