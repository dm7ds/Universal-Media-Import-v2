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
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;

namespace UMI.CLI.Commands;

/// <summary>
/// Cards Command - SD-Karten Verwaltung (list, add, remove, scan)
/// </summary>
public static class CardsCommand
{
    [SupportedOSPlatform("windows")]
    public static Command Create()
    {
        var command = new Command("cards", "SD card management");

        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateAddCommand());
        command.AddCommand(CreateRemoveCommand());
        command.AddCommand(CreateScanCommand());
        command.AddCommand(CreateSetCommand());
        command.AddCommand(CreateHistoryCommand());
        command.AddCommand(CreateAssignCommand());

        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "Show all registered SD cards");

        var cameraFilterOption = new Option<string?>(
            "--camera",
            "Show cards for a specific camera only");

        command.AddOption(cameraFilterOption);

        command.SetHandler(async (context) =>
        {
            var cameraFilter = context.ParseResult.GetValueForOption(cameraFilterOption);
            var configPath   = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteListAsync(cameraFilter, configPath);
        });

        return command;
    }

    private static async Task ExecuteListAsync(string? cameraFilter, string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();

        ConsoleHelper.WriteBanner(CliStrings.Cards_ListBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();

        var cards = config.SdCards;
        if (!string.IsNullOrEmpty(cameraFilter))
        {
            cards = cards.Where(kvp => kvp.Value.CameraId.Equals(cameraFilter, StringComparison.OrdinalIgnoreCase))
                         .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        if (cards.Count == 0)
        {
            if (!string.IsNullOrEmpty(cameraFilter))
            {
                Console.WriteLine(string.Format(CliStrings.Cards_NoCardsForCamera, cameraFilter));
            }
            else
            {
                Console.WriteLine(CliStrings.Cards_NoCardsRegistered);
            }
            Console.WriteLine();
            Console.WriteLine(CliStrings.Cards_TipScan);
            return;
        }

        if (!string.IsNullOrEmpty(cameraFilter))
        {
            var cameraName = config.Cameras.ContainsKey(cameraFilter) ? config.Cameras[cameraFilter].Name : CliStrings.Cards_Unknown;
            Console.WriteLine(string.Format(CliStrings.Cards_CardsForCamera, cameraFilter, cameraName));
        }
        else
        {
            Console.WriteLine(CliStrings.Cards_RegisteredCards);
        }

        ConsoleHelper.WriteSeparator();

        if (string.IsNullOrEmpty(cameraFilter))
        {
            Console.WriteLine($"  {"VSN",-14} {"Kamera",-8} {"Typ",-10} {"Label",-25} Zuletzt");
        }
        else
        {
            Console.WriteLine($"  {"VSN",-14} {"Typ",-10} {"Label",-30} Zuletzt");
        }

        foreach (var (vsn, reg) in cards.OrderBy(kvp => kvp.Value.CameraId).ThenByDescending(kvp => kvp.Value.LastSeen))
        {
            var label = reg.Label ?? CliStrings.Common_NoLabel;
            var lastSeen = DateTime.TryParse(reg.LastSeen, out var dt) ? dt.ToString("dd.MM.yy") : reg.LastSeen;
            var typeStr = reg.IsFloating ? "Floating" : "Fixed";

            if (string.IsNullOrEmpty(cameraFilter))
            {
                Console.WriteLine($"  {vsn,-14} {reg.CameraId,-8} {typeStr,-10} {label,-25} {lastSeen}");
            }
            else
            {
                Console.WriteLine($"  {vsn,-14} {typeStr,-10} {label,-30} {lastSeen}");
            }
        }

        ConsoleHelper.WriteSeparator();

        if (string.IsNullOrEmpty(cameraFilter))
        {
            var fixedCount = cards.Count(kvp => !kvp.Value.IsFloating);
            var floatingCount = cards.Count(kvp => kvp.Value.IsFloating);
            Console.WriteLine(string.Format(CliStrings.Cards_FooterTotal, cards.Count, fixedCount, floatingCount));
        }
        else
        {
            Console.WriteLine(string.Format(CliStrings.Cards_FooterSimple, cards.Count));
        }
    }

    private static Command CreateAddCommand()
    {
        var command = new Command("add", "Register an SD card manually");

        var vsnOption = new Option<string>(
            "--vsn",
            "Volume Serial Number (e.g. A4F2-8B31, comma-separated list for batch)")
        { IsRequired = true };

        var cameraOption = new Option<string>(
            "--camera",
            "Camera ID (e.g. GoPro11, MyDSLR)")
        { IsRequired = true };

        var labelOption = new Option<string?>(
            "--label",
            "Description (e.g. 'SanDisk Extreme 128GB')");

        var typeOption = new Option<string?>(
            "--type",
            "Card type: fixed (default) or floating");

        command.AddOption(vsnOption);
        command.AddOption(cameraOption);
        command.AddOption(labelOption);
        command.AddOption(typeOption);

        command.SetHandler(async (context) =>
        {
            var vsn        = context.ParseResult.GetValueForOption(vsnOption)!;
            var cameraId   = context.ParseResult.GetValueForOption(cameraOption)!;
            var label      = context.ParseResult.GetValueForOption(labelOption);
            var type       = context.ParseResult.GetValueForOption(typeOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteAddAsync(vsn, cameraId, label, type, configPath);
        });

        return command;
    }

    private static async Task ExecuteAddAsync(string vsnInput, string cameraId, string? label, string? typeOverride, string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Cards_AddBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();

        if (!config.Cameras.ContainsKey(cameraId))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Cards_CameraNotExists, cameraId));
            Console.WriteLine();
            Console.WriteLine(CliStrings.Common_AvailableCameras);
            foreach (var cam in config.Cameras.OrderBy(c => c.Key))
            {
                Console.WriteLine($"  {cam.Key} - {cam.Value.Name}");
            }
            return;
        }

        var vsns = vsnInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (vsns.Length > 1)
        {
            Console.WriteLine(string.Format(CliStrings.Cards_BatchRegistration, vsns.Length));
            Console.WriteLine();
        }

        bool? batchIsFloating = typeOverride?.ToLowerInvariant() switch
        {
            "floating" => true,
            "fixed" => false,
            _ => null
        };

        if (batchIsFloating == null && vsns.Length == 1)
        {
            Console.WriteLine();
            batchIsFloating = CameraMatchHelper.AskIsFloating(cameraId);
        }
        else if (batchIsFloating == null)
        {

            batchIsFloating = false;
        }

        Console.WriteLine();

        int registered = 0;
        int updated = 0;
        int overwritten = 0;

        foreach (var vsn in vsns)
        {

            if (!Regex.IsMatch(vsn, @"^[0-9A-F]{4}-[0-9A-F]{4}$", RegexOptions.IgnoreCase))
            {
                ConsoleHelper.WriteWarning(string.Format(CliStrings.Cards_UnusualVsn, vsn));
            }

            var existing = configWriter.GetSdCard(vsn);

            if (existing != null)
            {
                if (existing.CameraId.Equals(cameraId, StringComparison.OrdinalIgnoreCase))
                {

                    Console.WriteLine(string.Format(CliStrings.Cards_AlreadyRegistered, vsn, cameraId));
                    updated++;
                }
                else
                {

                    ConsoleHelper.WriteWarning(string.Format(CliStrings.Cards_AlreadyRegisteredOther, vsn, existing.CameraId));
                    Console.Write(string.Format(CliStrings.Cards_OverwritePrompt, cameraId));
                    var response = Console.ReadLine();

                    if (response?.Equals("y", StringComparison.OrdinalIgnoreCase) != true)
                    {
                        Console.WriteLine(string.Format("  {0}: {1}", vsn, CliStrings.Cards_Skipped));
                        continue;
                    }
                    overwritten++;
                }
            }
            else
            {
                registered++;
            }

            var effectiveCameraId = batchIsFloating.Value ? "" : cameraId;
            var registration = SdCardRegistrationHelper.Create(
                effectiveCameraId,
                label: label,
                existing: existing);

            configWriter.RegisterSdCard(vsn, registration);
        }

        await configWriter.SaveAsync(ct);

        Console.WriteLine();
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Cards_AddDone, registered, updated, overwritten));

        if (vsns.Length == 1)
        {
            var labelStr = !string.IsNullOrEmpty(label) ? $" ({label})" : "";
            Console.WriteLine(string.Format(CliStrings.Cards_CardRegisteredFull, vsns[0], cameraId, labelStr));
        }
    }

    private static Command CreateRemoveCommand()
    {
        var command = new Command("remove", "Remove an SD card registration");

        var vsnOption = new Option<string>(
            "--vsn",
            "Volume Serial Number")
        { IsRequired = true };

        var forceOption = new Option<bool>(
            "--force",
            "No confirmation");

        command.AddOption(vsnOption);
        command.AddOption(forceOption);

        command.SetHandler(async (context) =>
        {
            var vsn        = context.ParseResult.GetValueForOption(vsnOption)!;
            var force      = context.ParseResult.GetValueForOption(forceOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteRemoveAsync(vsn, force, configPath);
        });

        return command;
    }

    private static async Task ExecuteRemoveAsync(string vsn, bool force, string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Cards_RemoveBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();

        var existing = configWriter.GetSdCard(vsn);

        if (existing == null)
        {
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Cards_VsnNotRegistered, vsn));
            Console.WriteLine();
            Console.WriteLine(CliStrings.Cards_TipList);
            return;
        }

        if (!force)
        {
            var labelStr = !string.IsNullOrEmpty(existing.Label) ? $", {existing.Label}" : "";
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Cards_ConfirmRemove, vsn, existing.CameraId, labelStr));
            Console.Write(string.Format("{0} [y/N]: ", CliStrings.Cards_ConfirmPrompt));
            var response = Console.ReadLine();

            if (response?.Equals("y", StringComparison.OrdinalIgnoreCase) != true)
            {
                Console.WriteLine(CliStrings.Common_Cancelled);
                return;
            }
        }

        configWriter.UnregisterSdCard(vsn);
        await configWriter.SaveAsync(ct);

        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Cards_CardRemoved, vsn));
    }

    [SupportedOSPlatform("windows")]
    private static Command CreateScanCommand()
    {
        var command = new Command("scan", "Scan inserted SD card and register it");

        var pathOption = new Option<string?>(
            "--path",
            "Path to SD card (e.g. F:\\). Without: auto-detect inserted cards.");

        var cameraOption = new Option<string?>(
            "--camera",
            "Camera ID (skips selection dialog)");

        var forceOption = new Option<bool>(
            "--force",
            "Re-assign even if VSN is already registered");

        command.AddOption(pathOption);
        command.AddOption(cameraOption);
        command.AddOption(forceOption);

        command.SetHandler(async (context) =>
        {
            var path       = context.ParseResult.GetValueForOption(pathOption);
            var cameraId   = context.ParseResult.GetValueForOption(cameraOption);
            var force      = context.ParseResult.GetValueForOption(forceOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteScanAsync(path, cameraId, force, configPath);
        });

        return command;
    }

    [SupportedOSPlatform("windows")]
    private static async Task ExecuteScanAsync(string? sdPath, string? cameraId, bool force, string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Cards_ScanBanner);
        Console.WriteLine();

        if (string.IsNullOrEmpty(sdPath))
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Removable)
                .ToList();

            if (drives.Count == 0)
            {
                ConsoleHelper.WriteError(CliStrings.Cards_NoCardsDetected);
                Console.WriteLine(CliStrings.Cards_TipInsertOrPath);
                return;
            }

            if (drives.Count == 1)
            {
                sdPath = drives[0].RootDirectory.FullName;
                var driveLabel = !string.IsNullOrEmpty(drives[0].VolumeLabel) ? drives[0].VolumeLabel : CliStrings.Common_NoLabel;
                ConsoleHelper.WriteInfo(string.Format(CliStrings.Cards_CardDetected, sdPath, driveLabel));
                Console.WriteLine();
            }
            else
            {

                Console.WriteLine(string.Format(CliStrings.Cards_RemovableDrivesDetected, drives.Count));
                Console.WriteLine();
                for (int i = 0; i < drives.Count; i++)
                {
                    var d = drives[i];
                    var driveLabel = !string.IsNullOrEmpty(d.VolumeLabel) ? d.VolumeLabel : CliStrings.Common_NoLabel;
                    var size = FormatHelper.FormatBytes(d.TotalSize);
                    Console.WriteLine($"  ({i + 1}) {d.RootDirectory.FullName} — {driveLabel} ({size})");
                }
                Console.WriteLine();
                Console.Write(string.Format(CliStrings.Cards_WhichCard, drives.Count));

                var input = Console.ReadLine();
                if (!int.TryParse(input, out var choice) || choice < 1 || choice > drives.Count)
                {
                    Console.WriteLine(CliStrings.Common_Cancelled);
                    return;
                }
                sdPath = drives[choice - 1].RootDirectory.FullName;
            }
        }

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var fingerprintService = serviceProvider.GetRequiredService<ISdFingerprintService>();
        var logger = serviceProvider.GetService<ILogger<Program>>();

        var cardInfo = VolumeInfoReader.ReadSdCardInfo(sdPath, logger);

        if (string.IsNullOrEmpty(cardInfo.VolumeSerial))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Cards_CannotReadVsn, sdPath));
            return;
        }

        if (cardInfo.DriveType != DriveType.Removable)
        {
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Cards_NotRemovableDrive, sdPath, cardInfo.DriveType));
            Console.Write(string.Format("{0} [y/N]: ", CliStrings.Cards_ScanAnyway));
            var response = Console.ReadLine();

            if (response?.Equals("y", StringComparison.OrdinalIgnoreCase) != true)
            {
                Console.WriteLine(CliStrings.Common_Cancelled);
                return;
            }
        }

        Console.WriteLine(CliStrings.Cards_ScannedInfo);
        Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Cards_InfoPath, sdPath));
        Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Cards_InfoVsn, cardInfo.VolumeSerial));
        Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Cards_InfoDiskSerial, cardInfo.DiskSerial ?? CliStrings.Cards_NotAvailable));
        Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Cards_InfoLabel, cardInfo.VolumeLabel ?? CliStrings.Common_NoLabel));
        Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Cards_InfoSize, FormatHelper.FormatBytes(cardInfo.DiskSizeBytes)));
        Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Cards_InfoType, cardInfo.DriveType));

        var existing = config.SdCards.TryGetValue(cardInfo.VolumeSerial, out var reg) ? reg : null;
        string? registeredCameraId = existing?.CameraId;

        if (existing != null && !force)
        {
            Console.WriteLine();
            var existingLabel = !string.IsNullOrEmpty(existing.Label) ? $" ({existing.Label})" : "";
            ConsoleHelper.WriteSuccess(string.Format(CliStrings.Cards_KnownCard, existing.CameraId, cardInfo.VolumeSerial, existingLabel));
            Console.WriteLine(CliStrings.Cards_TipForce);
            return;
        }

        string? detectedCameraId = null;
        string? detectedModel = null;
        string? detectionMethod = null;

        try
        {
            var fingerprint = await fingerprintService.IdentifyCardAsync(sdPath, ct);
            if (fingerprint != null)
            {
                detectedCameraId = fingerprintService.MatchCamera(fingerprint, config.Cameras);
                detectedModel = fingerprint.Model;
                detectionMethod = fingerprint.DetectionMethod;
                Console.WriteLine(string.Format("  {0}: {1}", CliStrings.Cards_CameraExif, detectedModel ?? CliStrings.Cards_Unknown));

                if (detectionMethod == "version.txt" && !string.IsNullOrEmpty(cardInfo.VolumeLabel))
                {

                    var labelCameraMatch = CameraMatchHelper.MatchCameraFromLabel(cardInfo.VolumeLabel, config.Cameras.Keys);
                    if (labelCameraMatch != null && labelCameraMatch != detectedCameraId)
                    {
                        ConsoleHelper.WriteWarning(string.Format(CliStrings.Cards_VersionTxtConflict, detectedModel, cardInfo.VolumeLabel));
                        detectedCameraId = null;
                    }
                }

                if (detectionMethod == "version.txt" && registeredCameraId != null && registeredCameraId != detectedCameraId)
                {
                    ConsoleHelper.WriteWarning(string.Format(CliStrings.Cards_VersionTxtRegistryConflict, detectedModel, registeredCameraId));
                    detectedCameraId = null;
                }
            }
        }
        catch
        {

        }

        Console.WriteLine();

        string? selectedCameraId = null;

        if (!string.IsNullOrEmpty(cameraId))
        {

            if (!config.Cameras.ContainsKey(cameraId))
            {
                ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotInConfig, cameraId));
                return;
            }
            selectedCameraId = cameraId;
        }
        else
        {

            string? preselection = CameraMatchHelper.DeterminePreselection(
                registeredCameraId,
                detectedCameraId,
                cardInfo.VolumeLabel,
                config.Cameras);

            if (!string.IsNullOrEmpty(preselection) && config.Cameras.ContainsKey(preselection))
            {
                var source = registeredCameraId != null ? "Registry"
                           : detectedCameraId != null ? "EXIF"
                           : "Label";

                Console.WriteLine(string.Format(CliStrings.Cards_SourceSuggests, source, preselection, config.Cameras[preselection].Name));
                Console.Write(string.Format("{0} [Y/n]: ", CliStrings.Cards_AcceptPrompt));
                var response = Console.ReadLine();

                if (string.IsNullOrEmpty(response) || response.Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    selectedCameraId = preselection;
                }
            }

            if (string.IsNullOrEmpty(selectedCameraId))
            {
                Console.WriteLine(CliStrings.Cards_WhichCamera);
                var cameras = config.Cameras.Where(c => c.Value.Enabled).OrderBy(c => c.Key).ToList();

                int? defaultChoice = null;
                if (!string.IsNullOrEmpty(preselection))
                {
                    defaultChoice = cameras.FindIndex(c => c.Key == preselection);
                    if (defaultChoice >= 0)
                        defaultChoice++;
                }

                for (int i = 0; i < cameras.Count; i++)
                {
                    var cam = cameras[i];
                    var marker = defaultChoice == (i + 1) ? " (vorgeschlagen)" : "";
                    Console.WriteLine($"  ({i + 1}) {cam.Key} - {cam.Value.Name}{marker}");
                }
                Console.WriteLine(string.Format("  (0) {0}", CliStrings.Cards_DontRegister));
                Console.WriteLine();

                var prompt = defaultChoice.HasValue
                    ? string.Format("{0} [0-{1}] (Default: {2}): ", CliStrings.Cards_Selection, cameras.Count, defaultChoice)
                    : string.Format("{0} [0-{1}]: ", CliStrings.Cards_Selection, cameras.Count);
                Console.Write(prompt);

                var input = Console.ReadLine();
                int choice;

                if (string.IsNullOrWhiteSpace(input) && defaultChoice.HasValue)
                {
                    choice = defaultChoice.Value;
                }
                else if (int.TryParse(input, out choice) && choice > 0 && choice <= cameras.Count)
                {

                }
                else
                {
                    Console.WriteLine(CliStrings.Cards_RegistrationCancelled);
                    return;
                }

                selectedCameraId = cameras[choice - 1].Key;
            }
        }

        string? label = null;
        var suggestedLabel = VolumeInfoReader.BuildDisplayLabel(cardInfo);
        Console.Write(string.Format("{0} [{1}]: ", CliStrings.Cards_LabelPrompt, suggestedLabel));
        var labelInput = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(labelInput))
            labelInput = suggestedLabel;
        if (!string.IsNullOrWhiteSpace(labelInput))
        {
            label = labelInput.Trim();
        }

        Console.WriteLine();
        var isFloating = CameraMatchHelper.AskIsFloating(selectedCameraId);

        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();

        var effectiveCameraId = SdCardRegistration.EffectiveCameraId(selectedCameraId, isFloating);

        var registration = SdCardRegistrationHelper.Create(
            effectiveCameraId,
            label: label ?? cardInfo.VolumeLabel,
            diskSerial: cardInfo.DiskSerial,
            sizeBytes: cardInfo.DiskSizeBytes,
            model: detectedModel);

        configWriter.RegisterSdCard(cardInfo.VolumeSerial, registration);
        await configWriter.SaveAsync(ct);

        Console.WriteLine();
        var labelStr = !string.IsNullOrEmpty(label) ? $" ({label ?? cardInfo.VolumeLabel})" : "";
        var assignmentStr = isFloating ? " [Floating]" : " [Fixed]";
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Cards_CardRegisteredFor, cardInfo.VolumeSerial, selectedCameraId, labelStr, assignmentStr));
    }

    private static Command CreateSetCommand()
    {
        var command = new Command("set", "Change type of a registered SD card (fixed/floating)");

        var vsnArgument = new Argument<string>("vsn", "Volume Serial Number (e.g. 2083-0F64)");

        var typeOption = new Option<string>(
            "--type",
            "New type: fixed or floating")
        { IsRequired = true };

        command.AddArgument(vsnArgument);
        command.AddOption(typeOption);

        command.SetHandler(async (context) =>
        {
            var vsn        = context.ParseResult.GetValueForArgument(vsnArgument);
            var type       = context.ParseResult.GetValueForOption(typeOption)!;
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteSetAsync(vsn, type, configPath);
        });

        return command;
    }

    private static async Task ExecuteSetAsync(string vsn, string type, string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();

        bool? isFloating = type.ToLowerInvariant() switch
        {
            "floating" => true,
            "fixed" => false,
            _ => null
        };

        if (isFloating == null)
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Cards_InvalidType, type));
            return;
        }

        var existing = configWriter.GetSdCard(vsn);
        if (existing == null)
        {
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Cards_VsnNotRegistered, vsn));
            Console.WriteLine(CliStrings.Cards_TipList);
            return;
        }

        if (isFloating.Value)
            existing.CameraId = "";

        else if (string.IsNullOrEmpty(existing.CameraId))
            ConsoleHelper.WriteWarning(CliStrings.Cards_SetNoCamera);

        configWriter.RegisterSdCard(vsn, existing);
        await configWriter.SaveAsync(ct);

        var labelStr = !string.IsNullOrEmpty(existing.Label) ? $" ({existing.Label})" : "";
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Cards_SetDone, vsn, labelStr, isFloating.Value ? "Floating" : "Fixed"));
    }

    private static Command CreateHistoryCommand()
    {
        var command = new Command("history", "Show usage history of an SD card");

        var vsnArgument = new Argument<string>("vsn", "Volume Serial Number (z.B. 2083-0F64)");

        command.AddArgument(vsnArgument);

        command.SetHandler(async (context) =>
        {
            var vsn        = context.ParseResult.GetValueForArgument(vsnArgument);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteHistoryAsync(vsn, configPath);
        });

        return command;
    }

    private static async Task ExecuteHistoryAsync(string vsn, string configPath)
    {
        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();

        if (!config.SdCards.TryGetValue(vsn, out var reg))
        {
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Cards_VsnNotRegistered, vsn));
            Console.WriteLine(CliStrings.Cards_TipList);
            return;
        }

        var labelStr = !string.IsNullOrEmpty(reg.Label) ? $" ({reg.Label})" : "";
        var typeStr = reg.IsFloating ? "Floating" : "Fixed";
        var totalImports = reg.UsageHistory.Values.Sum();

        Console.WriteLine(string.Format(CliStrings.Cards_HistoryCardInfo, vsn, labelStr));
        Console.WriteLine(string.Format("{0}: {1}", CliStrings.Cards_HistoryType, typeStr));
        Console.WriteLine(string.Format(CliStrings.Cards_HistoryTotalImports, totalImports));

        if (totalImports == 0)
        {
            Console.WriteLine();
            Console.WriteLine(CliStrings.Cards_HistoryNone);
            return;
        }

        Console.WriteLine();
        ConsoleHelper.WriteSeparator();
        Console.WriteLine($"  {"Kamera",-10} {"Imports",8}   {"Anteil",7}   Zuletzt");

        var sorted = reg.UsageHistory.OrderByDescending(kvp => kvp.Value);
        foreach (var (cameraId, count) in sorted)
        {
            var percent = totalImports > 0 ? (count * 100.0 / totalImports) : 0;
            var lastUsedStr = reg.LastUsedWith.TryGetValue(cameraId, out var lastUsed)
                ? lastUsed.ToString("dd.MM.yyyy")
                : "";

            Console.WriteLine($"  {cameraId,-10} {count,8}   {percent,6:F0}%   {lastUsedStr}");
        }

        ConsoleHelper.WriteSeparator();
    }

    private static Command CreateAssignCommand()
    {
        var command = new Command("assign", "Assign an SD card to a camera (interactive)");
        var vsnArgument = new Argument<string>("vsn", "Volume Serial Number (z.B. A4F2-8B31)");
        command.AddArgument(vsnArgument);

        command.SetHandler(async (context) =>
        {
            var vsn = context.ParseResult.GetValueForArgument(vsnArgument);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteAssignAsync(vsn, configPath);
        });

        return command;
    }

    private static async Task ExecuteAssignAsync(string vsn, string configPath)
    {
        if (!ConsoleHelper.IsInteractiveTerminal())
        {
            ConsoleHelper.WriteError(CliStrings.Cards_AssignNeedInteractive);
            return;
        }

        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Cards_AssignBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();

        if (config.Cameras.Count == 0)
        {
            ConsoleHelper.WriteError(CliStrings.Cards_AssignNoCameras);
            return;
        }

        var existing = configWriter.GetSdCard(vsn);
        if (existing != null)
        {
            var camName = config.Cameras.TryGetValue(existing.CameraId, out var cam) ? cam.Name : CliStrings.Cards_Unknown;
            var typeStr = existing.IsFloating ? "Floating" : "Fixed";
            Console.WriteLine($"  {vsn}");
            Console.WriteLine(string.Format(CliStrings.Cards_AssignCurrentlyAssigned, existing.CameraId, camName, typeStr));
        }
        else
        {
            Console.WriteLine($"  {vsn}");
            Console.WriteLine(string.Format("  {0}", CliStrings.Cards_AssignNotAssigned));
        }
        Console.WriteLine();

        var choices = config.Cameras
            .OrderBy(c => c.Key)
            .Select(c => $"{c.Key} — {c.Value.Name}")
            .ToList();
        choices.Add(string.Format("({0})", CliStrings.Cards_AssignRemoveOption));

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(CliStrings.Cards_AssignWhichCamera)
                .AddChoices(choices));

        if (selected == string.Format("({0})", CliStrings.Cards_AssignRemoveOption))
        {
            if (existing == null)
            {
                ConsoleHelper.WriteInfo(string.Format(CliStrings.Cards_AssignNotRegistered, vsn));
                return;
            }

            configWriter.UnregisterSdCard(vsn);
            await configWriter.SaveAsync(ct);
            ConsoleHelper.WriteSuccess(string.Format(CliStrings.Cards_AssignRemoved, vsn));
            return;
        }

        var selectedCameraId = selected[..selected.IndexOf(" — ")];

        var typeChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(CliStrings.Cards_AssignCardType)
                .AddChoices(CliStrings.Cards_AssignFixedOption, CliStrings.Cards_AssignFloatingOption));

        var isFloating = typeChoice.StartsWith("Floating");

        var effectiveCameraId = SdCardRegistration.EffectiveCameraId(selectedCameraId, isFloating);

        var registration = SdCardRegistrationHelper.Create(
            effectiveCameraId,
            label: existing?.Label,
            existing: existing);

        configWriter.RegisterSdCard(vsn, registration);
        await configWriter.SaveAsync(ct);

        var assignmentStr = isFloating ? "Floating" : "Fixed";
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Cards_AssignDone, vsn, selectedCameraId, assignmentStr));
    }

}
