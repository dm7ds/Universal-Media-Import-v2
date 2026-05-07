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

using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;

namespace UMI.Core.Utilities;

/// <summary>
/// Liest Volume Serial Number und Disk Serial Number von SD-Karten.
/// Windows-only (P/Invoke + WMI).
/// </summary>
[SupportedOSPlatform("windows")]
public static class VolumeInfoReader
{

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder? fileSystemNameBuffer,
        int fileSystemNameSize);

    /// <summary>
    /// Erkennt bekannte Fake-DiskSerials die von manchen USB-Geräten gemeldet werden.
    /// DJI Osmo Action / Mavic: "Linux File-Stor Gadget" meldet 123456789ABCDE0/1 usw.
    /// Diese sind NICHT eindeutig zwischen Geräten und dürfen NICHT für Re-Mapping verwendet werden.
    /// </summary>
    public static bool IsFakeDiskSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return true;

        if (serial.StartsWith("123456789", StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// Returns the volume serial number for the drive, falling back to the drive letter if VSN is unavailable.
    /// Use as the registry key for SD card lookups.
    /// </summary>
    /// <param name="driveLetter">z.B. "F:" oder "F:\"</param>
    /// <returns>VSN as Hex-String (e.g. "A4F2-8B31") or drive letter as fallback</returns>
    public static string GetRegistryKey(string driveLetter)
    {
        var vsn = GetVolumeSerial(driveLetter);
        return !string.IsNullOrWhiteSpace(vsn) ? vsn : driveLetter;
    }

    /// <summary>
    /// Liest Volume Serial Number als Hex-String (z.B. "A4F2-8B31").
    /// Ändert sich bei Formatierung!
    /// </summary>
    /// <param name="driveLetter">z.B. "F:" oder "F:\"</param>
    /// <returns>VSN als Hex-String oder null bei Fehler</returns>
    public static string? GetVolumeSerial(string driveLetter)
    {
        try
        {

            var normalised = driveLetter.TrimEnd('/', '\\') + "\\";
            var rootPath = Path.GetPathRoot(normalised);
            if (string.IsNullOrEmpty(rootPath))
                return null;

            var result = GetVolumeInformation(
                rootPath,
                null, 0,
                out var volumeSerial,
                out _,
                out _,
                null, 0);

            if (!result)
                return null;

            var high = (volumeSerial >> 16) & 0xFFFF;
            var low = volumeSerial & 0xFFFF;
            return $"{high:X4}-{low:X4}";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Liest Disk Serial Number via WMI (bleibt bei Formatierung erhalten).
    /// Viele USB-Reader liefern null oder leere Strings.
    /// Backward-Compatible Wrapper um ReadDiskDriveProperties().
    /// </summary>
    /// <param name="driveLetter">z.B. "F:" oder "F:\"</param>
    /// <returns>Disk Serial oder null</returns>
    public static string? GetDiskSerial(string driveLetter)
    {
        var (serial, _, _) = ReadDiskDriveProperties(driveLetter);
        return serial;
    }

    /// <summary>
    /// Liest Serial, Model und Manufacturer aus Win32_DiskDrive in einem WMI-Durchlauf.
    /// </summary>
    /// <param name="driveLetter">z.B. "F:" oder "F:\"</param>
    /// <returns>Tuple mit (serial, model, manufacturer) — Felder können null sein</returns>
    private static (string? serial, string? model, string? manufacturer) ReadDiskDriveProperties(string driveLetter)
    {
        try
        {
            var driveLetterClean = driveLetter.TrimEnd('\\', '/').ToUpper();
            if (!driveLetterClean.EndsWith(':'))
                driveLetterClean += ":";

            var query = $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetterClean}'}} WHERE AssocClass = Win32_LogicalDiskToPartition";
            using var searcher = new ManagementObjectSearcher(query);
            using var partitions = searcher.Get();

            foreach (ManagementObject partition in partitions)
            {
                var diskQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";
                using var diskSearcher = new ManagementObjectSearcher(diskQuery);
                using var disks = diskSearcher.Get();

                foreach (ManagementObject disk in disks)
                {
                    var serial = disk["SerialNumber"]?.ToString()?.Trim();
                    var model = disk["Model"]?.ToString()?.Trim();
                    var manufacturer = disk["Manufacturer"]?.ToString()?.Trim();

                    if (string.IsNullOrEmpty(serial) || IsFakeDiskSerial(serial)) serial = null;
                    if (string.IsNullOrEmpty(model)) model = null;
                    if (string.IsNullOrEmpty(manufacturer)) manufacturer = null;

                    return (serial, model, manufacturer);
                }
            }

            return (null, null, null);
        }
        catch
        {
            return (null, null, null);
        }
    }

    /// <summary>
    /// Liest Disk-Größe in Bytes.
    /// </summary>
    public static long GetDiskSizeBytes(string driveLetter)
    {
        try
        {
            var normalised = driveLetter.TrimEnd('/', '\\') + "\\";
            var rootPath = Path.GetPathRoot(normalised);
            if (string.IsNullOrEmpty(rootPath))
                return 0;

            var driveInfo = new DriveInfo(rootPath);
            return driveInfo.TotalSize;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Liest Volume Label (z.B. "EOS_DIGITAL", "DJI_ACTION").
    /// </summary>
    public static string? GetVolumeLabel(string driveLetter)
    {
        try
        {
            var normalised = driveLetter.TrimEnd('/', '\\') + "\\";
            var rootPath = Path.GetPathRoot(normalised);
            if (string.IsNullOrEmpty(rootPath))
                return null;

            var driveInfo = new DriveInfo(rootPath);
            var label = driveInfo.VolumeLabel?.Trim();
            return string.IsNullOrEmpty(label) ? null : label;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Liest alle Karten-Infos in einem Rutsch.
    /// Graceful Degradation: Felder können null/0 sein wenn Auslesen fehlschlägt.
    /// </summary>
    /// <param name="sdRootPath">SD-Karten Root (z.B. "F:\DCIM" oder "F:\")</param>
    /// <param name="logger">Optional für Warnings</param>
    public static SdCardInfo ReadSdCardInfo(string sdRootPath, ILogger? logger = null)
    {
        try
        {
            var rootPath = Path.GetPathRoot(sdRootPath);
            if (string.IsNullOrEmpty(rootPath))
            {
                logger?.LogWarning("Kein gültiger Root-Pfad: {Path}", sdRootPath);
                return new SdCardInfo { DriveLetter = "" };
            }

            var driveLetter = rootPath.TrimEnd('\\', '/');

            var vsn = GetVolumeSerial(driveLetter);
            var (diskSerial, diskModel, diskManufacturer) = ReadDiskDriveProperties(driveLetter);
            var sizeBytes = GetDiskSizeBytes(driveLetter);
            var label = GetVolumeLabel(driveLetter);

            DriveType driveType = DriveType.Unknown;
            try
            {
                var driveInfo = new DriveInfo(rootPath);
                driveType = driveInfo.DriveType;
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "DriveType konnte nicht ausgelesen werden: {Drive}", driveLetter);
            }

            if (string.IsNullOrEmpty(vsn))
            {
                logger?.LogWarning("Volume Serial Number konnte nicht ausgelesen werden: {Drive}", driveLetter);
            }

            if (string.IsNullOrEmpty(diskSerial))
            {
                logger?.LogDebug("Disk Serial Number nicht verfügbar (viele USB-Reader supporten es nicht): {Drive}", driveLetter);
            }

            return new SdCardInfo
            {
                VolumeSerial = vsn ?? "",
                DiskSerial = diskSerial,
                DiskSizeBytes = sizeBytes,
                VolumeLabel = label,
                DriveLetter = driveLetter,
                DriveType = driveType,
                DiskModel = diskModel,
                DiskManufacturer = diskManufacturer
            };
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Fehler beim Auslesen der SD-Karten-Info: {Path}", sdRootPath);
            return new SdCardInfo { DriveLetter = "" };
        }
    }

    private static readonly string[] KnownBrands =
    [
        "SanDisk", "Samsung", "Lexar", "Kingston", "Sony",
        "Transcend", "PNY", "Angelbird", "ProGrade", "Delkin"
    ];

    private static readonly string[] WmiNoise =
    [
        "(Standard disk drives)", "Generic-", "USB ", "Mass Storage"
    ];

    /// <summary>
    /// Normalisiert den Hersteller-Namen aus WMI-Daten.
    /// Filtert WMI-Müll heraus und matcht bekannte Marken.
    /// </summary>
    private static string? NormalizeBrand(string? manufacturer, string? model)
    {

        foreach (var source in new[] { manufacturer, model })
        {
            if (string.IsNullOrWhiteSpace(source))
                continue;

            var isNoise = false;
            foreach (var noise in WmiNoise)
            {
                if (source.Contains(noise, StringComparison.OrdinalIgnoreCase))
                {
                    isNoise = true;
                    break;
                }
            }
            if (isNoise)
                continue;

            foreach (var brand in KnownBrands)
            {
                if (source.Contains(brand, StringComparison.OrdinalIgnoreCase))
                    return brand;
            }
        }

        return null;
    }

    /// <summary>
    /// Ermittelt die UHS-Klasse aus dem Disk-Model-String.
    /// </summary>
    private static string? ParseUhsClass(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;

        var match = Regex.Match(model, @"UHS[\s-]?(I{1,3})", RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        var roman = match.Groups[1].Value.ToUpperInvariant();
        return $"UHS-{roman}";
    }

    /// <summary>
    /// Rundet einen Byte-Wert auf die nächste Standard-Kartengröße (Marketing-GB).
    /// Gibt einen formatierten String zurück (z.B. "512 GB", "1 TB").
    /// </summary>
    private static string? FormatCapacity(long sizeBytes)
    {
        if (sizeBytes <= 0)
            return null;

        var gb = sizeBytes / 1_000_000_000.0;
        int[] standards = [8, 16, 32, 64, 128, 256, 512, 1024];

        var closest = standards[0];
        var minDiff = Math.Abs(gb - standards[0]);
        foreach (var s in standards)
        {
            var diff = Math.Abs(gb - s);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = s;
            }
        }

        return closest >= 1024
            ? $"{closest / 1024} TB"
            : $"{closest} GB";
    }

    /// <summary>
    /// Generiert einen lesbaren Label-Vorschlag aus den WMI-Daten der SD-Karte.
    /// Graceful Degradation: Fallback auf Kapazität + VolumeLabel, dann DriveLetter.
    /// </summary>
    /// <param name="info">Aktuell eingelesene SD-Karten-Infos</param>
    /// <returns>Label-Vorschlag, z.B. "SanDisk UHS-II 512 GB"</returns>
    public static string BuildDisplayLabel(SdCardInfo info)
    {
        var brand = NormalizeBrand(info.DiskManufacturer, info.DiskModel);
        var uhs = ParseUhsClass(info.DiskModel);
        var capacity = FormatCapacity(info.DiskSizeBytes);

        if (!string.IsNullOrEmpty(brand) || !string.IsNullOrEmpty(uhs))
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(brand))
                parts.Add(brand);

            if (!string.IsNullOrEmpty(uhs))
                parts.Add(uhs);

            if (!string.IsNullOrEmpty(capacity))
                parts.Add(capacity);

            return string.Join(" ", parts);
        }

        if (!string.IsNullOrEmpty(capacity) && !string.IsNullOrEmpty(info.VolumeLabel))
            return $"{capacity} ({info.VolumeLabel})";

        if (!string.IsNullOrEmpty(capacity))
            return capacity;

        return info.DriveLetter;
    }
}
