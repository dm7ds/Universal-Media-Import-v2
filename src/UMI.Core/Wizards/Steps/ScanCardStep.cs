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

using System.Runtime.Versioning;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;

namespace UMI.Core.Wizards.Steps;

/// <summary>
/// Optionaler Wizard-Step: SD-Karte jetzt einstecken und registrieren.
/// Nur relevant wenn SourceType = SdCard.
/// Nutzt VolumeInfoReader fuer VSN-Auslesen und IConfigWriterService fuer Registrierung.
/// </summary>
[SupportedOSPlatform("windows")]
public class ScanCardStep : IWizardStep
{
    private const string ScanToggleKey = "scan_card";

    private readonly IConfigWriterService _configWriter;
    private readonly Func<string> _getCameraId;

    public ScanCardStep(IConfigWriterService configWriter, Func<string> getCameraId)
    {
        _configWriter = configWriter;
        _getCameraId  = getCameraId;
    }

    /// <inheritdoc/>
    public string Title => "SD-Karte registrieren (optional)";

    /// <inheritdoc/>
    public string Description => "Registriere die SD-Karte dieser Kamera fuer automatische Erkennung.";

    /// <inheritdoc/>
    public bool CanSkip => true;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields =>
    [
        new WizardField(
            Key:          ScanToggleKey,
            Label:        "SD-Karte jetzt einstecken und registrieren?",
            Type:         WizardFieldType.Toggle,
            DefaultValue: true,
            HelpText:     "Eingesteckte SD-Karte wird erkannt, VSN ausgelesen und fest dieser Kamera zugeordnet")
    ];

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
        => Task.FromResult(new WizardStepResult(true));

    /// <inheritdoc/>
    public async Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var wantsRegistration = values.GetValueOrDefault(ScanToggleKey) is bool b && b;

        if (!wantsRegistration)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  Info: SD-Karte kann jederzeit mit 'umi cards scan' registriert werden.");
            Console.ResetColor();
            return;
        }

        await RegisterCardAsync(ct);
    }

    private async Task RegisterCardAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Stecke die SD-Karte ein und druecke Enter...");
        Console.ResetColor();

        await Task.Run(() =>
        {
            while (Console.KeyAvailable) Console.ReadKey(true);
            Console.ReadLine();
        }, ct);

        ct.ThrowIfCancellationRequested();

        var removableDrives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
            .ToList();

        if (removableDrives.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Keine Wechseldatentraeger gefunden. Karte registrieren mit 'umi cards scan'.");
            Console.ResetColor();
            return;
        }

        DriveInfo? selectedDrive;

        if (removableDrives.Count == 1)
        {
            selectedDrive = removableDrives[0];
            Console.WriteLine($"  Laufwerk erkannt: {selectedDrive.Name} ({selectedDrive.VolumeLabel})");
        }
        else
        {
            selectedDrive = AskSelectDrive(removableDrives);
            if (selectedDrive == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Abgebrochen. Karte registrieren mit 'umi cards scan'.");
                Console.ResetColor();
                return;
            }
        }

        var driveLetter = selectedDrive.Name.TrimEnd('\\', '/');
        var cardInfo    = VolumeInfoReader.ReadSdCardInfo(driveLetter);

        if (string.IsNullOrEmpty(cardInfo.VolumeSerial))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Volume Serial Number konnte nicht ausgelesen werden.");
            Console.WriteLine("  Karte manuell registrieren mit 'umi cards scan'.");
            Console.ResetColor();
            return;
        }

        var existingReg = _configWriter.GetSdCard(cardInfo.VolumeSerial);
        if (existingReg != null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  SD-Karte ist bereits registriert (Kamera: {existingReg.CameraId}, VSN: {cardInfo.VolumeSerial}).");
            Console.ResetColor();
            return;
        }

        var cameraId = _getCameraId();

        var registration = SdCardRegistrationHelper.Create(
            cameraId,
            label: cardInfo.VolumeLabel,
            diskSerial: cardInfo.DiskSerial,
            sizeBytes: cardInfo.DiskSizeBytes);

        _configWriter.RegisterSdCard(cardInfo.VolumeSerial, registration);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Karte registriert: VSN {cardInfo.VolumeSerial} → {cameraId} (Fixed)");
        Console.ResetColor();

        await Task.CompletedTask;
    }

    private static DriveInfo? AskSelectDrive(List<DriveInfo> drives)
    {
        Console.WriteLine("  Mehrere Wechseldatentraeger gefunden. Bitte waehlen:");

        for (int i = 0; i < drives.Count; i++)
        {
            var d = drives[i];
            Console.WriteLine($"    ({i + 1}) {d.Name} — {d.VolumeLabel ?? "Kein Label"} ({d.TotalSize / 1_073_741_824L} GB)");
        }
        Console.WriteLine("    (0) Abbrechen");
        Console.Write($"  Auswahl [0-{drives.Count}]: ");

        var input = Console.ReadLine();
        if (int.TryParse(input, out var choice) && choice > 0 && choice <= drives.Count)
            return drives[choice - 1];

        return null;
    }
}
