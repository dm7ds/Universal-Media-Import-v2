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

using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Erkennt verbundene MTP-Geräte und matcht sie gegen die config.mtp_devices Registry.
/// 2-Tier Detection: Registry Lookup → Model Match → Unknown.
/// Analogon zum SdCardRegistryService für SD-Karten.
/// </summary>
public interface IMtpDeviceDetectionService
{
    /// <summary>
    /// Erkennt alle verbundenen MTP-Geräte und matcht sie gegen die config.mtp_devices Registry.
    /// Gibt eine Liste von erkannten Geräten mit ihrem Match-Status zurück.
    /// </summary>
    IReadOnlyList<MtpDetectionResult> DetectDevices();
}

/// <summary>Ergebnis der MTP-Geräteerkennung für ein einzelnes Gerät.</summary>
public record MtpDetectionResult(
    MtpDeviceInfo Device,
    MtpDetectionOutcome Outcome,
    string? CameraId,
    string? Message);

/// <summary>Match-Status bei der MTP-Geräteerkennung.</summary>
public enum MtpDetectionOutcome
{
    /// <summary>Gerät in config.mtp_devices registriert → CameraId bekannt.</summary>
    Registered,

    /// <summary>Gerät nicht registriert, aber Model-Match in config.cameras → Vorschlag.</summary>
    ModelMatch,

    /// <summary>Gerät komplett unbekannt → muss manuell zugewiesen werden.</summary>
    Unknown
}

/// <summary>
/// Implementierung der 2-Tier MTP-Geräteerkennung.
/// </summary>
public class MtpDeviceDetectionService : IMtpDeviceDetectionService
{
    private readonly IMtpService _mtpService;
    private readonly IConfigWriterService _configWriter;
    private readonly ILogger<MtpDeviceDetectionService>? _logger;

    public MtpDeviceDetectionService(
        IMtpService mtpService,
        IConfigWriterService configWriter,
        ILogger<MtpDeviceDetectionService>? logger = null)
    {
        _mtpService = mtpService;
        _configWriter = configWriter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<MtpDetectionResult> DetectDevices()
    {
        var devices = _mtpService.GetConnectedDevices();
        var results = new List<MtpDetectionResult>(devices.Count);

        foreach (var device in devices)
        {
            var result = MatchDevice(device);
            results.Add(result);
        }

        return results;
    }

    private MtpDetectionResult MatchDevice(MtpDeviceInfo device)
    {

        var deviceKey = GetDeviceKey(device);
        var config = _configWriter.Config;
        if (config.MtpDevices.TryGetValue(deviceKey, out var registration))
        {
            _logger?.LogDebug(
                "MTP: {FriendlyName} → {CameraId} (registriert, Key={Key})",
                device.FriendlyName, registration.CameraId, deviceKey);

            return new MtpDetectionResult(
                Device: device,
                Outcome: MtpDetectionOutcome.Registered,
                CameraId: registration.CameraId,
                Message: $"Registriert als '{registration.CameraId}'");
        }

        var legacySerial1 = device.WpdSerialNumber ?? device.SerialNumber;
        if (!string.IsNullOrEmpty(legacySerial1)
            && config.MtpDevices.TryGetValue(legacySerial1, out var legacyReg1))
        {
            _logger?.LogInformation(
                "MTP: Legacy-Key (v1) '{OldKey}' → migriere zu '{NewKey}' für {CameraId}",
                legacySerial1, deviceKey, legacyReg1.CameraId);

            config.MtpDevices[deviceKey] = legacyReg1;
            config.MtpDevices.Remove(legacySerial1);
            _ = _configWriter.SaveAsync(CancellationToken.None);

            return new MtpDetectionResult(
                Device: device,
                Outcome: MtpDetectionOutcome.Registered,
                CameraId: legacyReg1.CameraId,
                Message: $"Registriert als '{legacyReg1.CameraId}' (migriert v1)");
        }

        if (!string.IsNullOrEmpty(device.WpdSerialNumber))
        {
            var manufacturer = (device.Manufacturer ?? "Unknown").Replace(" ", "_");
            var model = (device.Model ?? "Unknown").Replace(" ", "_");
            var task158Key = $"{device.WpdSerialNumber}_{manufacturer}_{model}";

            if (config.MtpDevices.TryGetValue(task158Key, out var legacyReg2))
            {
                _logger?.LogInformation(
                    "MTP: Legacy-Key (v2/TASK-158) '{OldKey}' → migriere zu '{NewKey}' für {CameraId}",
                    task158Key, deviceKey, legacyReg2.CameraId);

                config.MtpDevices[deviceKey] = legacyReg2;
                config.MtpDevices.Remove(task158Key);
                _ = _configWriter.SaveAsync(CancellationToken.None);

                return new MtpDetectionResult(
                    Device: device,
                    Outcome: MtpDetectionOutcome.Registered,
                    CameraId: legacyReg2.CameraId,
                    Message: $"Registriert als '{legacyReg2.CameraId}' (migriert v2)");
            }
        }

        if (!string.IsNullOrEmpty(device.Model))
        {
            var modelMatch = FindCameraByModel(device.Model);
            if (modelMatch is not null)
            {
                _logger?.LogInformation(
                    "MTP: {FriendlyName} → Vorschlag: {CameraId} (Model-Match: '{Model}')",
                    device.FriendlyName, modelMatch, device.Model);

                return new MtpDetectionResult(
                    Device: device,
                    Outcome: MtpDetectionOutcome.ModelMatch,
                    CameraId: modelMatch,
                    Message: $"Vorschlag via Model-Match: '{modelMatch}'");
            }
        }

        _logger?.LogDebug(
            "MTP: Unbekanntes Gerät: {FriendlyName} (Model='{Model}', SerialNumber='{Serial}')",
            device.FriendlyName, device.Model ?? "–", device.SerialNumber ?? "–");

        return new MtpDetectionResult(
            Device: device,
            Outcome: MtpDetectionOutcome.Unknown,
            CameraId: null,
            Message: "Unbekanntes Gerät — manuelle Zuweisung erforderlich");
    }

    /// <summary>
    /// Erzeugt den Registry-Schlüssel für ein Gerät.
    /// Format: "{Serial}_{Manufacturer}_{Model}" wenn Serial vorhanden,
    /// sonst "{Manufacturer}_{Model}" als Fallback.
    /// Enthält immer Manufacturer+Model, da manche Hersteller (z.B. DJI)
    /// identische Fake-Serials für verschiedene Kameramodelle liefern.
    /// SerialNumber ist bevorzugt die USB-Serial aus der WPD DeviceId (stabiler als WPD-Serial,
    /// eindeutig pro physischem Gerät auch bei mehreren identischen Kameras).
    /// Public static: Wird von ImportCommand und Detection-Service gemeinsam genutzt (DRY).
    /// </summary>
    public static string GetDeviceKey(MtpDeviceInfo device)
    {
        var manufacturer = (device.Manufacturer ?? "Unknown").Replace(" ", "_");
        var model = (device.Model ?? "Unknown").Replace(" ", "_");

        if (!string.IsNullOrEmpty(device.SerialNumber))
            return $"{device.SerialNumber}_{manufacturer}_{model}";

        return $"{manufacturer}_{model}";
    }

    /// <summary>
    /// Sucht eine Kamera anhand des Modellnamens (case-insensitive Contains, beide Richtungen).
    /// Gibt die Kamera-ID zurück oder null wenn kein Match.
    /// </summary>
    private string? FindCameraByModel(string deviceModel)
    {
        foreach (var (cameraId, cameraConfig) in _configWriter.Config.Cameras)
        {
            var cameraName = cameraConfig.Name;
            if (string.IsNullOrEmpty(cameraName))
                continue;

            if (deviceModel.Contains(cameraName, StringComparison.OrdinalIgnoreCase) ||
                cameraName.Contains(deviceModel, StringComparison.OrdinalIgnoreCase))
            {
                return cameraId;
            }
        }

        return null;
    }
}
