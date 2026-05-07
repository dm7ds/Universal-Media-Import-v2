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
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service für SD-Karten-Registrierung und -Erkennung.
/// 4-Tier Detection: VSN Lookup → Re-Mapping → EXIF Fingerprint → Unbekannt
/// </summary>
public interface ISdCardRegistryService
{
    /// <summary>
    /// Identifiziert Kamera-ID für SD-Karte (4-Tier Detection).
    /// Returns: SdLookupOutcome mit Result (Matched/Unknown/Skipped), CameraId und Reason.
    /// </summary>
    Task<SdLookupOutcome> LookupCameraIdAsync(string sdRootPath, CancellationToken ct = default);

    /// <summary>
    /// Registriert SD-Karte zu einer Kamera in config.json.
    /// </summary>
    Task RegisterCardAsync(string sdRootPath, string cameraId, CancellationToken ct = default);
}

public class SdCardRegistryService : ISdCardRegistryService
{
    private readonly UmiConfig _config;
    private readonly IConfigWriterService _configWriter;
    private readonly ISdFingerprintService _fingerprintService;
    private readonly ILogger<SdCardRegistryService>? _logger;

    public SdCardRegistryService(
        UmiConfig config,
        IConfigWriterService configWriter,
        ISdFingerprintService fingerprintService,
        ILogger<SdCardRegistryService>? logger = null)
    {
        _config = config;
        _configWriter = configWriter;
        _fingerprintService = fingerprintService;
        _logger = logger;
    }

    /// <summary>
    /// 4-Tier Detection Kaskade:
    /// Tier 0: VSN Lookup (schnellster Weg)
    /// Tier 1: Re-Mapping nach Formatierung (DiskSerial + SizeBytes)
    /// Tier 2: EXIF Fingerprint (bestehender Service)
    /// Tier 3: Unbekannt → Unknown
    /// </summary>
    [SupportedOSPlatform("windows")]
    public async Task<SdLookupOutcome> LookupCameraIdAsync(string sdRootPath, CancellationToken ct = default)
    {

        var cardInfo = VolumeInfoReader.ReadSdCardInfo(sdRootPath, _logger);

        if (cardInfo.DriveType != DriveType.Removable)
        {
            _logger?.LogDebug(
                "Überspringe SD-Card Registry: {Path} ist kein Removable Drive (Typ: {Type})",
                sdRootPath, cardInfo.DriveType);
            return new SdLookupOutcome(
                SdLookupResult.Skipped,
                Reason: $"Kein Removable Drive (Typ: {cardInfo.DriveType})");
        }

        if (string.IsNullOrEmpty(cardInfo.VolumeSerial))
        {
            _logger?.LogWarning("Kein Volume Serial für {Path}, Fallback auf EXIF", sdRootPath);
            var exifCameraId = await TryExifFingerprintAsync(sdRootPath, ct);

            if (exifCameraId != null)
            {
                return new SdLookupOutcome(SdLookupResult.Matched, CameraId: exifCameraId);
            }

            return new SdLookupOutcome(
                SdLookupResult.Unknown,
                Reason: "Kein Volume Serial, EXIF-Erkennung fehlgeschlagen");
        }

        _config.SdCards.TryGetValue(cardInfo.VolumeSerial, out var registration);

        if (registration != null)
        {

            _logger?.LogDebug("SD-Karte bekannt (Tier 0): {VSN} → {CameraId}", cardInfo.VolumeSerial, registration.CameraId);

            if (VolumeInfoReader.IsFakeDiskSerial(registration.DiskSerial))
            {
                _logger?.LogInformation(
                    "Fake-DiskSerial in bestehender Registrierung bereinigt: {VSN} (war: {FakeSerial})",
                    cardInfo.VolumeSerial, registration.DiskSerial);
                registration.DiskSerial = null;
            }

            registration.LastSeen = DateTime.UtcNow.ToString("o");
            _configWriter.RegisterSdCard(cardInfo.VolumeSerial, registration);
            await _configWriter.SaveAsync(ct);

            if (registration.IsFloating)
            {

                return new SdLookupOutcome(SdLookupResult.Matched, CameraId: null, MatchedVsn: cardInfo.VolumeSerial);
            }

            return new SdLookupOutcome(SdLookupResult.Matched, CameraId: registration.CameraId, MatchedVsn: cardInfo.VolumeSerial);
        }

        if (!string.IsNullOrEmpty(cardInfo.DiskSerial) && cardInfo.DiskSizeBytes > 0)
        {
            var remappedCameraId = await TryReMappingAsync(cardInfo, ct);

            if (remappedCameraId != null)
            {
                _logger?.LogDebug("SD-Karte neu formatiert erkannt (Tier 1), Re-Mapping durchgeführt: {CameraId}", remappedCameraId);
                return new SdLookupOutcome(SdLookupResult.Matched, CameraId: remappedCameraId);
            }
        }

        var exifCameraId2 = await TryExifFingerprintAsync(sdRootPath, ct);

        if (exifCameraId2 != null)
        {
            _logger?.LogDebug("EXIF Fingerprint Match (Tier 2): {CameraId} — NUR Vorschlag, keine Registration", exifCameraId2);

        }

        _logger?.LogDebug("Unbekannte SD-Karte: {VSN} ({Label}, {Size:N0} Bytes)",
            cardInfo.VolumeSerial, cardInfo.VolumeLabel ?? "(kein Label)", cardInfo.DiskSizeBytes);

        return new SdLookupOutcome(
            SdLookupResult.Unknown,
            Reason: "Keine Registrierung gefunden");
    }

    /// <summary>
    /// Registriert SD-Karte zu einer Kamera.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public async Task RegisterCardAsync(string sdRootPath, string cameraId, CancellationToken ct = default)
    {
        var cardInfo = VolumeInfoReader.ReadSdCardInfo(sdRootPath, _logger);

        if (cardInfo.DriveType != DriveType.Removable)
        {
            var errorMsg = $"SD-Karte kann nicht registriert werden: {sdRootPath} ist kein Removable Drive (Typ: {cardInfo.DriveType})";
            _logger?.LogWarning(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(cardInfo.VolumeSerial))
        {
            throw new InvalidOperationException($"Kann Karte nicht registrieren, kein Volume Serial: {sdRootPath}");
        }

        string? model = null;
        try
        {
            var fingerprint = await _fingerprintService.IdentifyCardAsync(sdRootPath, ct);
            model = fingerprint?.Model;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "EXIF-Fingerprint bei Registrierung fehlgeschlagen: {Path}", sdRootPath);
        }

        var registration = SdCardRegistrationHelper.Create(
            cameraId,
            label: cardInfo.VolumeLabel,
            diskSerial: cardInfo.DiskSerial,
            sizeBytes: cardInfo.DiskSizeBytes,
            model: model);

        _configWriter.RegisterSdCard(cardInfo.VolumeSerial, registration);
        await _configWriter.SaveAsync(ct);

        _logger?.LogDebug("SD-Karte registriert: {VSN} → {CameraId} ({Label}, {Size:N0} Bytes)",
            cardInfo.VolumeSerial, cameraId, cardInfo.VolumeLabel ?? "(kein Label)", cardInfo.DiskSizeBytes);
    }

    /// <summary>
    /// Tier 1: Re-Mapping nach Formatierung.
    /// Sucht nach alter Registration mit gleichem DiskSerial + SizeBytes (±5%).
    /// </summary>
    [SupportedOSPlatform("windows")]
    private async Task<string?> TryReMappingAsync(SdCardInfo cardInfo, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(cardInfo.DiskSerial))
            return null;

        var sizeMin = cardInfo.DiskSizeBytes * 0.95;
        var sizeMax = cardInfo.DiskSizeBytes * 1.05;

        foreach (var (oldVsn, oldReg) in _config.SdCards)
        {

            if (VolumeInfoReader.IsFakeDiskSerial(oldReg.DiskSerial)) continue;

            if (oldReg.DiskSerial == cardInfo.DiskSerial &&
                oldReg.SizeBytes >= sizeMin &&
                oldReg.SizeBytes <= sizeMax)
            {
                _logger?.LogDebug("Re-Mapping: {OldVSN} → {NewVSN} (DiskSerial: {DiskSerial}, Size: {Size:N0} Bytes)",
                    oldVsn, cardInfo.VolumeSerial, cardInfo.DiskSerial, cardInfo.DiskSizeBytes);

                _configWriter.UnregisterSdCard(oldVsn);

                var newReg = SdCardRegistrationHelper.Create(
                    oldReg.CameraId,
                    label: cardInfo.VolumeLabel,
                    diskSerial: cardInfo.DiskSerial,
                    sizeBytes: cardInfo.DiskSizeBytes,
                    model: oldReg.Model,
                    existing: oldReg);

                _configWriter.RegisterSdCard(cardInfo.VolumeSerial, newReg);

                await _configWriter.SaveAsync(ct);

                return oldReg.CameraId;
            }
        }

        return null;
    }

    /// <summary>
    /// Tier 2: EXIF Fingerprint via bestehender Service.
    /// </summary>
    private async Task<string?> TryExifFingerprintAsync(string sdRootPath, CancellationToken ct)
    {
        try
        {
            var fingerprint = await _fingerprintService.IdentifyCardAsync(sdRootPath, ct);

            if (fingerprint == null)
                return null;

            var cameraId = _fingerprintService.MatchCamera(fingerprint, _config.Cameras);

            if (cameraId != null)
            {
                _logger?.LogDebug("EXIF Fingerprint Match: {CameraId} (Model: {Model}, Serial: {Serial})",
                    cameraId, fingerprint.Model, fingerprint.SerialNumber);
                return cameraId;
            }

            _logger?.LogDebug("EXIF Fingerprint gefunden, aber keine passende Kamera (Model: {Model}, Serial: {Serial})",
                fingerprint.Model, fingerprint.SerialNumber);

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "EXIF Fingerprint fehlgeschlagen: {Path}", sdRootPath);
            return null;
        }
    }
}
