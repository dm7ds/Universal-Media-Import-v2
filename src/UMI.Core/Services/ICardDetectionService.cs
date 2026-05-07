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

using UMI.Core.Models;

namespace UMI.Core.Services;

/// <summary>
/// Result of a camera detection attempt on a drive.
/// Carries all information gathered during the 4-stage detection pipeline.
/// </summary>
public record CardDetectionResult
{
    /// <summary>Drive letter of the detected drive (e.g. "F:").</summary>
    public string DriveLetter { get; init; } = string.Empty;

    /// <summary>Volume label of the drive (e.g. "DJI_ACTION"). Null when not available.</summary>
    public string? VolumeLabel { get; init; }

    /// <summary>
    /// USB device model string as reported by Windows WMI (e.g. "DJI OSMO ACTION 5 Pro").
    /// The trailing "USB Device" suffix is already stripped.
    /// Null when the drive is not a USB device or when WMI is unavailable.
    /// </summary>
    public string? UsbDeviceModel { get; init; }

    /// <summary>EXIF fingerprint read from media files on the card. Null when no media found.</summary>
    public SdFingerprint? Fingerprint { get; init; }

    /// <summary>The matched camera model entry (from USB, EXIF or volume label). Null when unknown.</summary>
    public CameraModelMatch? ModelMatch { get; init; }

    /// <summary>Detected camera make (from USB descriptor, EXIF or volume label rule). Null when unknown.</summary>
    public string? DetectedMake { get; init; }

    /// <summary>Detected camera model string (from USB descriptor or EXIF). Null when no data found.</summary>
    public string? DetectedModel { get; init; }

    /// <summary>Suggested UMI camera type (e.g. "drone", "action_cam"). Null when unknown.</summary>
    public string? SuggestedCameraType { get; init; }

    /// <summary>Suggested display name for the camera. Null when unknown.</summary>
    public string? SuggestedDisplayName { get; init; }

    /// <summary>
    /// Which detection stage identified the camera.
    /// "usb"          — identified by USB device descriptor via WMI (fastest, no I/O on card).
    /// "volume_label" — identified by SD card volume label only.
    /// "exif"         — identified by EXIF metadata from media files (highest confidence).
    /// "none"         — camera could not be identified; user must enter details manually.
    /// </summary>
    public string DetectionSource { get; init; } = "none";
}

/// <summary>
/// SSOT for camera detection from removable drives.
/// Encapsulates the 4-stage detection pipeline:
///   Stage 1 — USB descriptor via WMI (instant, no card I/O — Windows only)
///   Stage 2 — Volume label (instant, no I/O)
///   Stage 3 — EXIF fingerprint via SdFingerprintService (reads media files)
///   Stage 4 — CameraModelDatabase lookup (EXIF has priority over volume label)
///
/// Use this service everywhere a drive needs to be identified as a camera.
/// Do NOT duplicate this logic in individual ViewModels.
/// </summary>
public interface ICardDetectionService
{
    /// <summary>
    /// Detects the camera on the given drive using a 4-stage pipeline.
    /// Always returns a result; check <see cref="CardDetectionResult.DetectionSource"/> for confidence.
    /// </summary>
    /// <param name="driveLetter">Drive letter, e.g. "F:".</param>
    /// <param name="rootPath">Root path of the drive, e.g. "F:\".</param>
    /// <param name="volumeLabel">Volume label of the drive. Null when not available.</param>
    /// <param name="ct">Cancellation token — passed through to EXIF reads.</param>
    Task<CardDetectionResult> DetectCameraAsync(
        string driveLetter,
        string rootPath,
        string? volumeLabel,
        CancellationToken ct = default);
}
