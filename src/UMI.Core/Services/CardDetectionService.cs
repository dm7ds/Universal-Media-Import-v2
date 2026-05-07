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
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// SSOT implementation of <see cref="ICardDetectionService"/>.
///
/// 4-stage detection pipeline:
///   Stage 1 — USB descriptor via WMI: instant, no card I/O, Windows-only.
///             Reads Win32_DiskDrive.Model (e.g. "DJI OSMO ACTION 5 Pro USB Device")
///             and maps it via CameraModelDatabase.
///   Stage 2 — Volume label: instant, no I/O.
///             Many manufacturers write labels like "DJI_ACTION", "HERO12", "EOS_DIGITAL".
///   Stage 3 — EXIF fingerprint: reads Make/Model/SerialNumber from the first media files.
///             Includes .mp4/.mov so DJI Action Cams (video-only) are detected too.
///   Stage 4 — CameraModelDatabase: matches Make+Model (EXIF) or falls back to volume-label match.
///             EXIF has priority over volume label — more precise.
///
/// Priority order (highest to lowest): EXIF > USB > volume label
/// The USB match is used only when EXIF is unavailable.
/// </summary>
public sealed class CardDetectionService : ICardDetectionService
{
    private readonly ISdFingerprintService _sdFingerprint;
    private readonly ICameraModelDatabase _cameraModelDb;
    private readonly ILogger<CardDetectionService>? _logger;

    public CardDetectionService(
        ISdFingerprintService sdFingerprint,
        ICameraModelDatabase cameraModelDb,
        ILogger<CardDetectionService>? logger = null)
    {
        _sdFingerprint = sdFingerprint;
        _cameraModelDb = cameraModelDb;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CardDetectionResult> DetectCameraAsync(
        string driveLetter,
        string rootPath,
        string? volumeLabel,
        CancellationToken ct = default)
    {

        string? usbDeviceModel = null;
        CameraModelMatch? usbMatch = null;

        if (OperatingSystem.IsWindows())
        {
            usbDeviceModel = UsbDeviceInfoReader.GetUsbDeviceModel(driveLetter);
            if (!string.IsNullOrWhiteSpace(usbDeviceModel))
            {

                var cleanedUsb = usbDeviceModel.Trim();
                bool isGenericFallback =
                    cleanedUsb.StartsWith("Linux", StringComparison.OrdinalIgnoreCase) ||
                    cleanedUsb.Contains("File-Stor Gadget", StringComparison.OrdinalIgnoreCase) ||
                    cleanedUsb.StartsWith("Generic", StringComparison.OrdinalIgnoreCase);

                if (isGenericFallback)
                {
                    _logger?.LogDebug(
                        "CardDetection [{Drive}]: USB fallback name '{Usb}' is generic (WPD lookup failed) — skipping USB stage",
                        driveLetter, usbDeviceModel);
                    usbDeviceModel = null;
                }
                else
                {

                    usbMatch = _cameraModelDb.Identify(string.Empty, usbDeviceModel);

                    if (usbMatch == null)
                    {
                        var (usbMake, usbModel) = UsbDeviceInfoReader.SplitMakeModel(usbDeviceModel);
                        if (!string.IsNullOrWhiteSpace(usbModel))
                            usbMatch = _cameraModelDb.Identify(usbMake, usbModel);

                        if (usbMatch == null && !string.IsNullOrWhiteSpace(usbModel))
                            usbMatch = _cameraModelDb.Identify(string.Empty, usbModel);
                    }

                    if (usbMatch != null)
                    {
                        _logger?.LogDebug(
                            "CardDetection [{Drive}]: USB descriptor '{Usb}' matched → {Type}",
                            driveLetter, usbDeviceModel, usbMatch.CameraType);
                    }
                    else
                    {
                        _logger?.LogDebug(
                            "CardDetection [{Drive}]: USB descriptor '{Usb}' — no DB match",
                            driveLetter, usbDeviceModel);
                    }
                }
            }
        }

        CameraModelMatch? labelMatch = null;

        if (!string.IsNullOrWhiteSpace(volumeLabel))
        {
            labelMatch = _cameraModelDb.IdentifyByVolumeLabel(volumeLabel);
            if (labelMatch != null)
            {
                _logger?.LogDebug(
                    "CardDetection [{Drive}]: volume label '{Label}' matched → {Type}",
                    driveLetter, volumeLabel, labelMatch.CameraType);
            }
        }

        SdFingerprint? fp = null;
        try
        {
            fp = await _sdFingerprint.IdentifyCardAsync(rootPath, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "CardDetection [{Drive}]: EXIF fingerprint failed", driveLetter);
        }

        ct.ThrowIfCancellationRequested();

        CameraModelMatch? exifMatch = null;
        if (fp != null && (!string.IsNullOrEmpty(fp.Make) || !string.IsNullOrEmpty(fp.Model)))
        {
            exifMatch = _cameraModelDb.Identify(fp.Make ?? string.Empty, fp.Model ?? string.Empty);
            if (exifMatch != null)
            {
                _logger?.LogDebug(
                    "CardDetection [{Drive}]: EXIF Make='{Make}' Model='{Model}' matched → {Type}",
                    driveLetter, fp.Make, fp.Model, exifMatch.CameraType);
            }
        }

        var finalMatch = exifMatch ?? usbMatch ?? labelMatch;
        var detectionSource = exifMatch != null  ? "exif"
                            : usbMatch != null   ? "usb"
                            : labelMatch != null ? "volume_label"
                            : "none";

        string? detectedMake  = fp?.Make;
        string? detectedModel = fp?.Model;
        if (detectedMake == null && detectedModel == null && !string.IsNullOrWhiteSpace(usbDeviceModel))
        {
            var (usbMake, usbModel) = UsbDeviceInfoReader.SplitMakeModel(usbDeviceModel);
            detectedMake  = usbMake;
            detectedModel = string.IsNullOrEmpty(usbModel) ? usbDeviceModel : usbModel;
        }

        if (detectionSource == "none")
        {
            _logger?.LogDebug(
                "CardDetection [{Drive}]: no camera identified (label: '{Label}', USB: '{Usb}', EXIF: Make='{Make}' Model='{Model}')",
                driveLetter, volumeLabel, usbDeviceModel, fp?.Make, fp?.Model);
        }

        return new CardDetectionResult
        {
            DriveLetter      = driveLetter,
            VolumeLabel      = volumeLabel,
            UsbDeviceModel   = usbDeviceModel,
            Fingerprint      = fp,
            ModelMatch       = finalMatch,
            DetectedMake     = detectedMake,
            DetectedModel    = detectedModel,
            SuggestedCameraType  = finalMatch?.CameraType,
            SuggestedDisplayName = finalMatch?.DisplayName ?? detectedModel,
            DetectionSource  = detectionSource,
        };
    }
}
