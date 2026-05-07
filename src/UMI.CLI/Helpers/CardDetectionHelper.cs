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
using UMI.CLI.Resources;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;

namespace UMI.CLI.Helpers;

/// <summary>
/// Shared Card/MTP-Detection und Kamera-Zuordnungs-Dialoge.
/// Genutzt von WatchCommand und QuickCommand.
/// </summary>
[SupportedOSPlatform("windows")]
public static class CardDetectionHelper
{
    /// <summary>Roh-Event: Erkannte Karte oder MTP-Gerät, noch nicht Kamera zugeordnet.</summary>
    public record DetectionEvent
    {
        public string? DrivePath { get; init; }
        public DriveChangedEventArgs? DriveEvent { get; init; }
        public MtpDetectionResult? MtpDetection { get; init; }
        public bool IsMtp => MtpDetection is not null;
    }

    /// <summary>Aufgelöster Import: Kamera bekannt, bereit zum Starten.</summary>
    public record ResolvedImport(string SourcePath, string CameraId, bool IsMtp, MtpDetectionResult? MtpDetection = null, string? VolumeSerial = null);

    /// <summary>
    /// Verarbeitet alle Einträge in der Detection Queue: Dialog pro Karte/Gerät,
    /// aufgelöste Imports in resolvedImports übertragen.
    /// </summary>
    /// <param name="detectionQueue">Eingehende Roh-Events (SD-Karten + MTP-Geräte).</param>
    /// <param name="resolvedImports">Aufgelöste Imports bereit zur Verarbeitung.</param>
    /// <param name="fixedCameraId">Wenn gesetzt: Fixed-Mode, kein Dialog — direkt diese Kamera verwenden. Null = Auto/Ask-Modus.</param>
    /// <param name="config">UMI-Konfiguration.</param>
    /// <param name="fingerprintService">EXIF-Fingerprint-Service für unbekannte Karten.</param>
    /// <param name="configWriter">Config-Writer für Karten-Registrierung.</param>
    /// <param name="importedDrivesThisSession">VSN-Set bereits importierter Karten dieser Session.</param>
    /// <param name="verbose">Verbose-Ausgabe aktivieren.</param>
    /// <param name="ct">CancellationToken.</param>
    public static async Task ProcessDetectionQueueAsync(
        Queue<DetectionEvent> detectionQueue,
        Queue<ResolvedImport> resolvedImports,
        string? fixedCameraId,
        UmiConfig config,
        ISdFingerprintService fingerprintService,
        IConfigWriterService configWriter,
        HashSet<string> importedDrivesThisSession,
        bool verbose,
        CancellationToken ct)
    {
        while (detectionQueue.Count > 0)
        {
            DetectionEvent detection;
            lock (detectionQueue)
            {
                if (detectionQueue.Count == 0) break;
                detection = detectionQueue.Dequeue();
            }

            if (detection.IsMtp)
            {

                var mtp = detection.MtpDetection!;
                Console.WriteLine($"  ┌ 📱 {mtp.Device.FriendlyName} → {mtp.CameraId}");
                resolvedImports.Enqueue(new ResolvedImport("", mtp.CameraId!, IsMtp: true, MtpDetection: mtp));
            }
            else
            {

                var driveEvent = detection.DriveEvent!;
                Console.WriteLine();
                Console.WriteLine($"  ┌ 💾 {detection.DrivePath} ({driveEvent.VolumeLabel ?? CliStrings.Common_NoLabel}, {FormatHelper.FormatBytes(driveEvent.TotalSizeBytes)})");

                string? cameraId;
                string? volumeSerial = null;
                if (fixedCameraId is not null)
                {
                    cameraId = fixedCameraId;
                    Console.WriteLine($"  └ → {cameraId} ({CliStrings.Detection_FixedLabel})");
                }
                else
                {
                    (cameraId, volumeSerial) = await ResolveCameraForCardAsync(
                        detection.DrivePath!, driveEvent,
                        config, fingerprintService, configWriter,
                        importedDrivesThisSession, verbose, ct);
                }

                if (cameraId != null)
                    resolvedImports.Enqueue(new ResolvedImport(detection.DrivePath!, cameraId, IsMtp: false, VolumeSerial: volumeSerial));
                else
                    Console.WriteLine($"  → {detection.DrivePath}: {CliStrings.Detection_Skipped}");
            }
        }
    }

    /// <summary>
    /// Bestimmt die Kamera für eine erkannte SD-Karte.
    /// Fixed-Karte → direkt, kein Dialog.
    /// Floating-Karte → Dialog mit Nutzungs-Historie als Vorschlag.
    /// Unbekannte Karte → Dialog mit EXIF/Label-Vorauswahl + Registrierungs-Option.
    /// Gibt (CameraId, VolumeSerial) zurück. VSN wird für Session-Tracking nach erfolgreichem Import benötigt.
    /// </summary>
    public static async Task<(string? CameraId, string? VolumeSerial)> ResolveCameraForCardAsync(
        string drivePath,
        DriveChangedEventArgs eventArgs,
        UmiConfig config,
        ISdFingerprintService fingerprintService,
        IConfigWriterService configWriter,
        HashSet<string> importedDrivesThisSession,
        bool verbose,
        CancellationToken ct)
    {

        var cardInfo = VolumeInfoReader.ReadSdCardInfo(drivePath, null);
        if (string.IsNullOrEmpty(cardInfo.VolumeSerial))
        {
            ConsoleHelper.WriteWarning(CliStrings.Detection_CannotReadVsn);
            return (null, null);
        }

        if (importedDrivesThisSession.Contains(cardInfo.VolumeSerial))
        {
            Console.WriteLine($"  → ⚠ {string.Format(CliStrings.Detection_AlreadyImported, cardInfo.VolumeSerial)}");
            Console.Write($"  {CliStrings.Detection_ImportAgain} [y/N]: ");
            var response = Console.ReadLine();

            if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                return (null, cardInfo.VolumeSerial);
        }

        if (config.SdCards.TryGetValue(cardInfo.VolumeSerial, out var registration))
        {
            if (!registration.IsFloating)
            {

                Console.WriteLine($"  → {registration.CameraId} ({CliStrings.Detection_FixedAssignment})");
                return (registration.CameraId, cardInfo.VolumeSerial);
            }
            else
            {

                Console.WriteLine($"  → {CliStrings.Detection_FloatingCard}");
                return (AskCameraWithHistory(registration, config.Cameras), cardInfo.VolumeSerial);
            }
        }

        Console.WriteLine($"  → ⚠ {CliStrings.Detection_UnknownCard}");
        Console.WriteLine();

        string? detectedCameraId = null;
        try
        {
            var fingerprint = await fingerprintService.IdentifyCardAsync(drivePath, ct);
            if (fingerprint != null)
            {
                detectedCameraId = fingerprintService.MatchCamera(fingerprint, config.Cameras);
                if (verbose && fingerprint.Model != null)
                    Console.WriteLine($"  EXIF: {fingerprint.Model}");
            }
        }
        catch
        {

        }

        var suggestion = CameraMatchHelper.DeterminePreselection(
            registeredCameraId: null,
            exifMatchedCameraId: detectedCameraId,
            volumeLabel: cardInfo.VolumeLabel,
            cameras: config.Cameras);

        var selected = AskCameraWithSuggestion(config.Cameras, suggestion);
        if (selected == null)
            return (null, cardInfo.VolumeSerial);

        Console.Write($"  {CliStrings.Detection_RegisterCard} [Y/n]: ");
        var regResponse = Console.ReadLine();

        if (string.IsNullOrEmpty(regResponse) || regResponse.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            var isFloating = CameraMatchHelper.AskIsFloating(selected);

            var registeredCameraId = SdCardRegistration.EffectiveCameraId(selected, isFloating);

            var newReg = SdCardRegistrationHelper.Create(
                registeredCameraId,
                label: cardInfo.VolumeLabel,
                diskSerial: VolumeInfoReader.IsFakeDiskSerial(cardInfo.DiskSerial) ? null : cardInfo.DiskSerial,
                sizeBytes: cardInfo.DiskSizeBytes);

            configWriter.RegisterSdCard(cardInfo.VolumeSerial, newReg);
            await configWriter.SaveAsync(ct);

            config.SdCards[cardInfo.VolumeSerial] = newReg;

            var typeStr = isFloating ? "Floating" : "Fixed";
            Console.WriteLine($"  ✓ {string.Format(CliStrings.Detection_CardRegistered, typeStr)}");
        }

        return (selected, cardInfo.VolumeSerial);
    }

    /// <summary>
    /// Dialog für unbekannte Karten: Kamera-Liste alphabetisch sortiert mit optionalem Vorschlag.
    /// </summary>
    /// <param name="cameras">Alle konfigurierten Kameras.</param>
    /// <param name="suggestion">Vorgeschlagene Kamera-ID (EXIF oder Label-Match), oder null.</param>
    /// <returns>Gewählte Kamera-ID oder null wenn übersprungen.</returns>
    public static string? AskCameraWithSuggestion(Dictionary<string, CameraConfig> cameras, string? suggestion)
    {
        Console.WriteLine($"  {CliStrings.Detection_WhichCamera}");

        var cameraList = cameras.Where(c => c.Value.Enabled).OrderBy(c => c.Key).ToList();
        int? defaultChoice = null;

        for (int i = 0; i < cameraList.Count; i++)
        {
            var cam = cameraList[i];
            var marker = suggestion == cam.Key ? $" ← {CliStrings.Detection_Suggestion}" : "";
            Console.WriteLine($"    ({i + 1}) {cam.Key} - {cam.Value.Name}{marker}");

            if (suggestion == cam.Key)
            {
                defaultChoice = i + 1;
            }
        }

        Console.WriteLine($"    (0) {CliStrings.Detection_Skip}");
        Console.WriteLine();

        var prompt = defaultChoice.HasValue
            ? $"  {CliStrings.Match_Selection} [0-{cameraList.Count}] (Default: {defaultChoice}): "
            : $"  {CliStrings.Match_Selection} [0-{cameraList.Count}]: ";
        Console.Write(prompt);

        var input = Console.ReadLine();
        int choice;

        if (string.IsNullOrWhiteSpace(input) && defaultChoice.HasValue)
        {
            choice = defaultChoice.Value;
        }
        else if (int.TryParse(input, out choice))
        {

        }
        else
        {
            return null;
        }

        if (choice == 0)
        {
            return null;
        }

        if (choice > 0 && choice <= cameraList.Count)
        {
            return cameraList[choice - 1].Key;
        }

        return null;
    }

    /// <summary>
    /// Dialog für Floating-Karten: Kamera-Liste sortiert nach Nutzungshäufigkeit + Statistiken.
    /// </summary>
    /// <param name="registration">Karten-Registrierung mit Nutzungs-Historie.</param>
    /// <param name="cameras">Alle konfigurierten Kameras.</param>
    /// <returns>Gewählte Kamera-ID oder null wenn übersprungen.</returns>
    public static string? AskCameraWithHistory(SdCardRegistration registration, Dictionary<string, CameraConfig> cameras)
    {

        var cameraList = cameras
            .Where(c => c.Value.Enabled)
            .Select(c =>
            {
                registration.UsageHistory.TryGetValue(c.Key, out var count);
                registration.LastUsedWith.TryGetValue(c.Key, out var lastUsed);
                return (CameraId: c.Key, Config: c.Value, Count: count, LastUsed: lastUsed);
            })
            .OrderByDescending(c => c.Count)
            .ThenByDescending(c => c.LastUsed)
            .ToList();

        var suggestion = GetSuggestionFromHistory(registration);

        Console.WriteLine($"  {CliStrings.Detection_WhichCamera}");

        int? defaultChoice = null;
        for (int i = 0; i < cameraList.Count; i++)
        {
            var (camId, camConfig, count, lastUsed) = cameraList[i];

            string stats;
            if (count > 0)
            {
                var lastUsedStr = lastUsed != default ? lastUsed.ToString("dd.MM.yyyy") : "";
                stats = $"  {count,3}x │ last {lastUsedStr}";
            }
            else
            {
                stats = "    - │";
            }

            var marker = suggestion == camId ? $" ← {CliStrings.Detection_Suggestion}" : "";
            Console.WriteLine($"    ({i + 1}) {camId,-4} - {camConfig.Name,-30} {stats}{marker}");

            if (suggestion == camId)
                defaultChoice = i + 1;
        }

        Console.WriteLine($"    (0) {CliStrings.Detection_Skip}");
        Console.WriteLine();

        var prompt = defaultChoice.HasValue
            ? $"  {CliStrings.Match_Selection} [0-{cameraList.Count}] (Default: {defaultChoice}): "
            : $"  {CliStrings.Match_Selection} [0-{cameraList.Count}]: ";
        Console.Write(prompt);

        var input = Console.ReadLine();
        int choice;

        if (string.IsNullOrWhiteSpace(input) && defaultChoice.HasValue)
            choice = defaultChoice.Value;
        else if (int.TryParse(input, out choice))
        {  }
        else
            return null;

        if (choice == 0) return null;
        if (choice > 0 && choice <= cameraList.Count)
            return cameraList[choice - 1].CameraId;
        return null;
    }

    /// <summary>
    /// Häufigste Kamera aus der Nutzungs-Historie als Vorschlag.
    /// Fallback: CameraId der Registrierung (letzte/feste Zuordnung).
    /// </summary>
    /// <param name="registration">Karten-Registrierung mit Nutzungs-Historie.</param>
    /// <returns>Vorgeschlagene Kamera-ID oder null.</returns>
    public static string? GetSuggestionFromHistory(SdCardRegistration registration)
    {
        if (registration.UsageHistory.Count == 0)
            return registration.CameraId;

        return registration.UsageHistory
            .OrderByDescending(kvp => kvp.Value)
            .First().Key;
    }

    /// <summary>
    /// Aktualisiert UsageHistory + LastUsedWith nach erfolgreichem Import.
    /// </summary>
    /// <param name="drivePath">Pfad zur SD-Karte (für VSN-Lookup).</param>
    /// <param name="cameraId">Kamera-ID mit der importiert wurde.</param>
    /// <param name="config">UMI-Konfiguration (in-memory).</param>
    /// <param name="configWriter">Config-Writer für Persistierung.</param>
    /// <param name="ct">CancellationToken.</param>
    public static async Task UpdateCardHistoryAsync(
        string drivePath,
        string cameraId,
        UmiConfig config,
        IConfigWriterService configWriter,
        CancellationToken ct)
    {
        var cardInfo = VolumeInfoReader.ReadSdCardInfo(drivePath, null);
        if (string.IsNullOrEmpty(cardInfo.VolumeSerial))
            return;

        if (!config.SdCards.TryGetValue(cardInfo.VolumeSerial, out var registration))
            return;

        registration.RecordUsage(cameraId);
        registration.CameraId = cameraId;

        await configWriter.SaveAsync(ct);
    }
}
