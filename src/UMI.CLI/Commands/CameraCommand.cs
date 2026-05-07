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
/// Camera Command — Kamera-Verwaltung (list, show, add, remove, enable, disable, assign)
/// </summary>
public static class CameraCommand
{
    [SupportedOSPlatform("windows")]
    public static Command Create()
    {
        var command = new Command("camera", "Camera management");

        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateShowCommand());
        command.AddCommand(CreateAddCommand());
        command.AddCommand(CreateRemoveCommand());
        command.AddCommand(CreateEnableCommand());
        command.AddCommand(CreateDisableCommand());
        command.AddCommand(CreateAssignCommand());

        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "Show all configured cameras");

        command.SetHandler(async (context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteListAsync(configPath);
        });

        return command;
    }

    private static async Task ExecuteListAsync(string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();

        ConsoleHelper.WriteBanner(CliStrings.Camera_ListBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();

        if (config.Cameras.Count == 0)
        {
            Console.WriteLine(CliStrings.Common_NoCamerasConfigured);
            Console.WriteLine();
            Console.WriteLine(CliStrings.Camera_TipAdd);
            return;
        }

        Console.WriteLine(CliStrings.Camera_ConfiguredCameras);
        ConsoleHelper.WriteSeparator();
        Console.WriteLine($"  {"ID",-8} {"Name",-30} {"Typ",-12} {"Source",-8} {"Aktiv",-8} {"Karten",-10} Features");

        foreach (var (id, cam) in config.Cameras.OrderBy(c => c.Key))
        {

            var sdCount = config.SdCards.Count(kvp => kvp.Value.CameraId.Equals(id, StringComparison.OrdinalIgnoreCase));
            var mtpCount = config.MtpDevices.Count(kvp => kvp.Value.CameraId.Equals(id, StringComparison.OrdinalIgnoreCase));

            var cardsStr = (sdCount, mtpCount) switch
            {
                (0, 0) => "-",
                (> 0, 0) => $"{sdCount} SD",
                (0, > 0) => $"{mtpCount} MTP",
                _ => $"{sdCount} SD, {mtpCount} MTP"
            };

            var sourceStr = cam.SourceType switch
            {
                SourceType.SdCard => "SD",
                SourceType.FixedPath => "Fixed",
                SourceType.MTP => "MTP",
                _ => "?"
            };

            var enabledStr = cam.Enabled ? CliStrings.Common_Yes : CliStrings.Common_No;

            var enabledFeatures = FeatureRegistry.All
                .Where(f => cam.Features.GetByKey(f.Key))
                .Select(f => f.ShortLabel);
            var featuresStr = string.Join(", ", enabledFeatures);
            if (string.IsNullOrEmpty(featuresStr)) featuresStr = "-";

            var folderStr = cam.FolderName ?? "";
            Console.WriteLine($"  {id,-8} {cam.Name,-30} {cam.CameraType,-12} {sourceStr,-8} {enabledStr,-8} {cardsStr,-10} {featuresStr}");
            if (!string.IsNullOrEmpty(folderStr))
                Console.WriteLine($"  {"",8} Folder: {folderStr}");
        }

        ConsoleHelper.WriteSeparator();
        var enabledCount = config.Cameras.Count(c => c.Value.Enabled);
        Console.WriteLine(string.Format(CliStrings.Camera_FooterTotal, config.Cameras.Count, enabledCount));
    }

    private static Command CreateShowCommand()
    {
        var command = new Command("show", "Show camera details");
        var idArgument = new Argument<string>("id", "Camera ID (e.g. GoPro11, MyDSLR)");
        command.AddArgument(idArgument);

        command.SetHandler(async (context) =>
        {
            var id = context.ParseResult.GetValueForArgument(idArgument);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteShowAsync(id, configPath);
        });

        return command;
    }

    private static async Task ExecuteShowAsync(string id, string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();

        if (!config.Cameras.TryGetValue(id, out var cam))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotFound, id));
            PrintAvailableCameras(config);
            return;
        }

        Console.WriteLine();
        Console.WriteLine(string.Format(CliStrings.Camera_ShowHeader, id, cam.Name));
        ConsoleHelper.WriteSeparator();

        if (!string.IsNullOrEmpty(cam.Manufacturer))
            Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Camera_ShowManufacturer, cam.Manufacturer));
        Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Camera_ShowType, cam.CameraType));

        var sourceStr = cam.SourceType switch
        {
            SourceType.SdCard => $"SD ({cam.Paths?.SdSource ?? "-"})",
            SourceType.FixedPath => $"Fixed ({cam.SourcePath ?? "-"})",
            SourceType.MTP => "MTP",
            _ => "?"
        };
        Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Camera_ShowSource, sourceStr));
        Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Camera_ShowEnabled, cam.Enabled ? CliStrings.Common_Yes : CliStrings.Common_No));
        if (!string.IsNullOrEmpty(cam.SerialNumber))
            Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Camera_ShowSerial, cam.SerialNumber));
        if (!string.IsNullOrEmpty(cam.FolderName))
            Console.WriteLine(string.Format("  {0,-14} {1}", CliStrings.Camera_ShowFolder, cam.FolderName));

        Console.WriteLine();
        Console.WriteLine(string.Format("  {0}:", CliStrings.Camera_ShowFeatures));
        foreach (var feature in FeatureRegistry.All)
        {
            var enabled = cam.Features.GetByKey(feature.Key);
            Console.WriteLine($"    {feature.Label,-20} {(enabled ? "✓" : "✗")}");
        }

        var videoTypes = cam.FileTypes.Video.Length > 0 ? string.Join(" ", cam.FileTypes.Video) : "-";
        var photoTypes = cam.FileTypes.Photo.Length > 0 ? string.Join(" ", cam.FileTypes.Photo) : "-";
        Console.WriteLine(string.Format("  {0,-14} Video: {1} | Photo: {2}", CliStrings.Camera_ShowFileTypes, videoTypes, photoTypes));

        Console.WriteLine();
        var sdCards = config.SdCards
            .Where(kvp => kvp.Value.CameraId.Equals(id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kvp => kvp.Value.LastSeen)
            .ToList();

        var mtpDevices = config.MtpDevices
            .Where(kvp => kvp.Value.CameraId.Equals(id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sdCards.Count == 0 && mtpDevices.Count == 0)
        {
            Console.WriteLine(string.Format("  {0}: -", CliStrings.Camera_ShowAssignedCards));
        }
        else
        {
            Console.WriteLine(string.Format("  {0}:", CliStrings.Camera_ShowAssignedCards));
            foreach (var (vsn, reg) in sdCards)
            {
                var label = reg.Label ?? CliStrings.Common_NoLabel;
                var typeStr = reg.IsFloating ? "Floating" : "Fixed";
                Console.WriteLine($"    SD   {vsn,-12} {label,-25} [{typeStr}]");
            }
            foreach (var (serial, reg) in mtpDevices)
            {
                var label = reg.Label ?? CliStrings.Common_NoLabel;

                Console.WriteLine($"    MTP  {serial,-12} {label,-25} [Fixed]");
            }
        }

        Console.WriteLine();
    }

    private static Command CreateAddCommand()
    {
        var command = new Command("add", "Add a new camera (starts camera setup wizard)");

        command.SetHandler(async (context) =>
        {
            var ct = context.GetCancellationToken();
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteAddAsync(configPath, ct);
        });

        return command;
    }

    private static async Task ExecuteAddAsync(string configPath, CancellationToken ct)
    {
        if (!ConsoleHelper.IsInteractiveTerminal())
        {
            ConsoleHelper.WriteError(CliStrings.Camera_AddNeedInteractive);
            return;
        }

        ConsoleHelper.WriteBanner(CliStrings.Camera_AddBanner);

        await SetupCommand.RunCameraWizardAsync(configPath, ct);
    }

    private static Command CreateRemoveCommand()
    {
        var command = new Command("remove", "Remove a camera configuration");
        var idArgument = new Argument<string>("id", "Camera ID (e.g. GoPro11, MyDSLR)");
        var forceOption = new Option<bool>("--force", "No confirmation");

        command.AddArgument(idArgument);
        command.AddOption(forceOption);

        command.SetHandler(async (context) =>
        {
            var id = context.ParseResult.GetValueForArgument(idArgument);
            var force = context.ParseResult.GetValueForOption(forceOption);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteRemoveAsync(id, force, configPath);
        });

        return command;
    }

    private static async Task ExecuteRemoveAsync(string id, bool force, string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Camera_RemoveBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();

        if (!config.Cameras.TryGetValue(id, out var cam))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotFound, id));
            PrintAvailableCameras(config);
            return;
        }

        var sdCount = config.SdCards.Count(kvp => kvp.Value.CameraId.Equals(id, StringComparison.OrdinalIgnoreCase));
        var mtpCount = config.MtpDevices.Count(kvp => kvp.Value.CameraId.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (sdCount > 0 || mtpCount > 0)
        {
            var parts = new List<string>();
            if (sdCount > 0) parts.Add(string.Format(CliStrings.Camera_RemoveSdCards, sdCount));
            if (mtpCount > 0) parts.Add(string.Format(CliStrings.Camera_RemoveMtpDevices, mtpCount));
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Camera_RemoveHasDevices, id, string.Join(" + ", parts)));
        }

        if (!force)
        {
            Console.Write(string.Format(CliStrings.Camera_ConfirmRemove, id, cam.Name));
            var response = Console.ReadLine();
            if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                Console.WriteLine(CliStrings.Common_Cancelled);
                return;
            }
        }

        configWriter.RemoveCamera(id);
        await configWriter.SaveAsync(ct);

        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Camera_Removed, id, cam.Name));

        if (sdCount > 0 || mtpCount > 0)
        {
            ConsoleHelper.WriteInfo(CliStrings.Camera_RemoveCleanupHint);
        }
    }

    private static Command CreateEnableCommand()
    {
        var command = new Command("enable", "Enable a camera");
        var idArgument = new Argument<string>("id", "Kamera-ID");
        command.AddArgument(idArgument);

        command.SetHandler(async (context) =>
        {
            var id = context.ParseResult.GetValueForArgument(idArgument);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteToggleAsync(id, true, configPath);
        });

        return command;
    }

    private static Command CreateDisableCommand()
    {
        var command = new Command("disable", "Disable a camera");
        var idArgument = new Argument<string>("id", "Kamera-ID");
        command.AddArgument(idArgument);

        command.SetHandler(async (context) =>
        {
            var id = context.ParseResult.GetValueForArgument(idArgument);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteToggleAsync(id, false, configPath);
        });

        return command;
    }

    private static async Task ExecuteToggleAsync(string id, bool enable, string configPath)
    {
        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();

        if (!config.Cameras.TryGetValue(id, out var cam))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotFound, id));
            PrintAvailableCameras(config);
            return;
        }

        if (cam.Enabled == enable)
        {
            ConsoleHelper.WriteInfo(string.Format(CliStrings.Camera_AlreadyState, id, enable ? CliStrings.Camera_StateEnabled : CliStrings.Camera_StateDisabled));
            return;
        }

        configWriter.UpdateCamera(id, c => c.Enabled = enable);
        await configWriter.SaveAsync(ct);

        if (enable)
            ConsoleHelper.WriteSuccess(string.Format(CliStrings.Camera_Enabled, id, cam.Name));
        else
            ConsoleHelper.WriteSuccess(string.Format(CliStrings.Camera_Disabled, id, cam.Name));
    }

    [SupportedOSPlatform("windows")]
    private static Command CreateAssignCommand()
    {
        var command = new Command("assign", "Assign cards and MTP devices to a camera (interactive)");
        var idArgument = new Argument<string>("id", "Kamera-ID (z.B. GoPro11, MyDSLR)");
        command.AddArgument(idArgument);

        command.SetHandler(async (context) =>
        {
            var id = context.ParseResult.GetValueForArgument(idArgument);
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;
            await ExecuteAssignAsync(id, configPath);
        });

        return command;
    }

    [SupportedOSPlatform("windows")]
    private static async Task ExecuteAssignAsync(string id, string configPath)
    {
        if (!ConsoleHelper.IsInteractiveTerminal())
        {
            ConsoleHelper.WriteError(CliStrings.Camera_AssignNeedInteractive);
            return;
        }

        using var cts = ConsoleHelper.CreateCancellationSource();
        var ct = cts.Token;

        ConsoleHelper.WriteBanner(CliStrings.Camera_AssignBanner);
        Console.WriteLine();

        await using var serviceProvider = await Program.BuildServiceProviderAsync(configPath);
        var config = serviceProvider.GetRequiredService<UmiConfig>();
        var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();
        var logger = serviceProvider.GetService<ILogger<Program>>();

        if (!config.Cameras.TryGetValue(id, out var cam))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Common_CameraNotFound, id));
            PrintAvailableCameras(config);
            return;
        }

        Console.WriteLine($"  Kamera {id} — {cam.Name}");

        var changed = false;
        while (!ct.IsCancellationRequested)
        {

            var sdCards = config.SdCards
                .Where(kvp => kvp.Value.CameraId.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var mtpDevices = config.MtpDevices
                .Where(kvp => kvp.Value.CameraId.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine(string.Format(CliStrings.Camera_AssignCurrent, sdCards.Count, mtpDevices.Count));
            Console.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(CliStrings.Camera_AssignWhat)
                    .AddChoices(
                        CliStrings.Camera_AssignScanSd,
                        CliStrings.Camera_AssignManualVsn,
                        CliStrings.Camera_AssignMtp,
                        CliStrings.Camera_AssignRemove,
                        CliStrings.Camera_AssignDone_Label));

            if (choice == CliStrings.Camera_AssignDone_Label)
                break;

            if (choice == CliStrings.Camera_AssignScanSd)
            {
                changed |= await AssignSdCardScanAsync(id, config, configWriter, logger, ct);
            }
            else if (choice == CliStrings.Camera_AssignManualVsn)
            {
                changed |= AssignSdCardManual(id, config, configWriter);
            }
            else if (choice == CliStrings.Camera_AssignMtp)
            {
                changed |= AssignMtpDevice(id, config, configWriter, serviceProvider);
            }
            else if (choice == CliStrings.Camera_AssignRemove)
            {
                changed |= RemoveAssignment(id, config, configWriter);
            }

            Console.WriteLine();
        }

        if (changed)
        {
            await configWriter.SaveAsync(ct);
            ConsoleHelper.WriteSuccess(CliStrings.Camera_AssignSaved);
        }
        else
        {
            ConsoleHelper.WriteInfo(CliStrings.Camera_AssignNoChanges);
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task<bool> AssignSdCardScanAsync(
        string cameraId,
        UmiConfig config,
        IConfigWriterService configWriter,
        ILogger? logger,
        CancellationToken ct)
    {

        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Removable)
            .ToList();

        if (drives.Count == 0)
        {
            ConsoleHelper.WriteWarning(CliStrings.Camera_ScanNoRemovable);
            return false;
        }

        var driveChoices = drives.Select(d =>
        {
            var label = !string.IsNullOrEmpty(d.VolumeLabel) ? d.VolumeLabel : CliStrings.Common_NoLabel;
            var size = FormatHelper.FormatBytes(d.TotalSize);
            return $"{d.RootDirectory.FullName} — {label} ({size})";
        }).ToList();

        var selectedDrive = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(CliStrings.Camera_ScanWhichDrive)
                .AddChoices(driveChoices));

        var driveIndex = driveChoices.IndexOf(selectedDrive);
        var sdPath = drives[driveIndex].RootDirectory.FullName;

        var cardInfo = VolumeInfoReader.ReadSdCardInfo(sdPath, logger);
        if (string.IsNullOrEmpty(cardInfo.VolumeSerial))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Detection_CannotReadVsn, sdPath));
            return false;
        }

        Console.WriteLine($"  VSN:    {cardInfo.VolumeSerial}");
        Console.WriteLine($"  Label:  {cardInfo.VolumeLabel ?? CliStrings.Common_NoLabel}");
        Console.WriteLine($"  Size:   {FormatHelper.FormatBytes(cardInfo.DiskSizeBytes)}");

        var existing = configWriter.GetSdCard(cardInfo.VolumeSerial);
        if (existing != null)
        {
            if (existing.CameraId.Equals(cameraId, StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.WriteInfo(string.Format(CliStrings.Camera_ScanAlreadyRegistered, cardInfo.VolumeSerial, cameraId));
                return false;
            }
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Camera_ScanRegisteredOther, cardInfo.VolumeSerial, existing.CameraId));
            Console.Write(string.Format(CliStrings.Camera_ScanReassign, cameraId));
            var resp = Console.ReadLine();
            if (!resp?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                return false;
        }

        var typeChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(CliStrings.Match_CardType)
                .AddChoices(CliStrings.Match_FixedOption, CliStrings.Match_FloatingOption));

        var isFloating = typeChoice.StartsWith("Floating");

        var suggestedLabel = VolumeInfoReader.BuildDisplayLabel(cardInfo);
        var label = AnsiConsole.Prompt(
            new TextPrompt<string>(string.Format("{0}:", CliStrings.Cards_LabelPrompt))
                .DefaultValue(suggestedLabel)
                .AllowEmpty());

        var effectiveCameraId = SdCardRegistration.EffectiveCameraId(cameraId, isFloating);

        var registration = SdCardRegistrationHelper.Create(
            effectiveCameraId,
            label: string.IsNullOrWhiteSpace(label) ? cardInfo.VolumeLabel : label,
            diskSerial: cardInfo.DiskSerial,
            sizeBytes: cardInfo.DiskSizeBytes,
            model: existing?.Model,
            existing: existing);

        configWriter.RegisterSdCard(cardInfo.VolumeSerial, registration);
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Camera_ScanAssigned, cardInfo.VolumeSerial, cameraId, isFloating ? "Floating" : "Fixed"));
        return true;
    }

    private static bool AssignSdCardManual(
        string cameraId,
        UmiConfig config,
        IConfigWriterService configWriter)
    {
        var vsn = AnsiConsole.Prompt(
            new TextPrompt<string>(CliStrings.Camera_ManualVsnPrompt));

        if (string.IsNullOrWhiteSpace(vsn))
            return false;

        vsn = vsn.Trim().ToUpperInvariant();

        var existing = configWriter.GetSdCard(vsn);
        if (existing != null)
        {
            if (existing.CameraId.Equals(cameraId, StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.WriteInfo(string.Format(CliStrings.Camera_ScanAlreadyRegistered, vsn, cameraId));
                return false;
            }
            ConsoleHelper.WriteWarning(string.Format(CliStrings.Camera_ScanRegisteredOther, vsn, existing.CameraId));
            Console.Write(string.Format(CliStrings.Camera_ScanReassign, cameraId));
            var resp = Console.ReadLine();
            if (!resp?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                return false;
        }

        var typeChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(CliStrings.Match_CardType)
                .AddChoices(CliStrings.Match_FixedOption, CliStrings.Match_FloatingOption));

        var isFloating = typeChoice.StartsWith("Floating");

        var label = AnsiConsole.Prompt(
            new TextPrompt<string>(CliStrings.Camera_ManualLabelPrompt)
                .AllowEmpty());

        var effectiveCameraId = SdCardRegistration.EffectiveCameraId(cameraId, isFloating);

        var registration = SdCardRegistrationHelper.Create(
            effectiveCameraId,
            label: string.IsNullOrWhiteSpace(label) ? null : label,
            existing: existing);

        configWriter.RegisterSdCard(vsn, registration);
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Camera_ScanAssigned, vsn, cameraId, isFloating ? "Floating" : "Fixed"));
        return true;
    }

    private static bool AssignMtpDevice(
        string cameraId,
        UmiConfig config,
        IConfigWriterService configWriter,
        ServiceProvider serviceProvider)
    {
        IMtpService? mtpService;
        try
        {
            mtpService = serviceProvider.GetService<IMtpService>();
        }
        catch
        {
            mtpService = null;
        }

        if (mtpService == null)
        {
            ConsoleHelper.WriteWarning(CliStrings.Camera_MtpNotAvailable);
            return false;
        }

        IReadOnlyList<MtpDeviceInfo> devices;
        try
        {
            devices = mtpService.GetConnectedDevices();
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Camera_MtpDetectError, ex.Message));
            return false;
        }

        if (devices.Count == 0)
        {
            ConsoleHelper.WriteWarning(CliStrings.Camera_MtpNoDevices);
            return false;
        }

        var deviceChoices = devices.Select(d =>
        {
            var name = d.FriendlyName;
            var serial = !string.IsNullOrEmpty(d.SerialNumber) ? $" (SN: {d.SerialNumber})" : "";
            return $"{name}{serial}";
        }).ToList();

        var selectedDevice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(CliStrings.Camera_MtpWhichDevice)
                .AddChoices(deviceChoices));

        var deviceIndex = deviceChoices.IndexOf(selectedDevice);
        var device = devices[deviceIndex];

        var deviceKey = MtpDeviceDetectionService.GetDeviceKey(device);
        var existing = configWriter.GetMtpDevice(deviceKey);
        if (existing != null && existing.CameraId.Equals(cameraId, StringComparison.OrdinalIgnoreCase))
        {
            ConsoleHelper.WriteInfo(string.Format(CliStrings.Camera_MtpAlreadyRegistered, cameraId));
            return false;
        }

        var registration = MtpRegistrationHelper.Create(
            cameraId, device.FriendlyName);

        configWriter.RegisterMtpDevice(deviceKey, registration);
        var snInfo = !string.IsNullOrEmpty(device.SerialNumber) ? $" (SN: {device.SerialNumber})" : string.Empty;
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Camera_MtpAssigned, device.FriendlyName, snInfo, cameraId));
        return true;
    }

    private static bool RemoveAssignment(
        string cameraId,
        UmiConfig config,
        IConfigWriterService configWriter)
    {

        var sdCards = config.SdCards
            .Where(kvp => kvp.Value.CameraId.Equals(cameraId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var mtpDevices = config.MtpDevices
            .Where(kvp => kvp.Value.CameraId.Equals(cameraId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sdCards.Count == 0 && mtpDevices.Count == 0)
        {
            ConsoleHelper.WriteInfo(CliStrings.Camera_RemoveNoAssignments);
            return false;
        }

        var choices = new List<string>();
        foreach (var (vsn, reg) in sdCards)
        {
            var label = reg.Label ?? CliStrings.Common_NoLabel;
            var typeStr = reg.IsFloating ? "Floating" : "Fixed";
            choices.Add($"SD  {vsn} — {label} [{typeStr}]");
        }
        foreach (var (serial, reg) in mtpDevices)
        {
            var label = reg.Label ?? CliStrings.Common_NoLabel;

            choices.Add($"MTP {serial} — {label} [Fixed]");
        }

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title(CliStrings.Camera_RemoveWhich)
                .AddChoices(choices));

        if (selected.Count == 0)
            return false;

        foreach (var item in selected)
        {
            if (item.StartsWith("SD "))
            {

                var vsn = item[4..item.IndexOf(" — ")];
                configWriter.UnregisterSdCard(vsn);
                ConsoleHelper.WriteSuccess(string.Format(CliStrings.Camera_SdCardRemoved, vsn));
            }
            else if (item.StartsWith("MTP"))
            {
                var serial = item[4..item.IndexOf(" — ")];
                configWriter.UnregisterMtpDevice(serial);
                ConsoleHelper.WriteSuccess(string.Format(CliStrings.Camera_MtpDeviceRemoved, serial));
            }
        }

        return true;
    }

    private static void PrintAvailableCameras(UmiConfig config)
    {
        if (config.Cameras.Count == 0)
        {
            Console.WriteLine(CliStrings.Common_NoCamerasConfigured);
            return;
        }

        Console.WriteLine();
        Console.WriteLine(CliStrings.Camera_AvailableCameras);
        foreach (var (camId, camCfg) in config.Cameras.OrderBy(c => c.Key))
        {
            Console.WriteLine($"  {camId} — {camCfg.Name}");
        }
    }
}
