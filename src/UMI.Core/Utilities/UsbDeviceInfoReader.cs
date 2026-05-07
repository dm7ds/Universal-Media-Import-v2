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
using System.Runtime.Versioning;

namespace UMI.Core.Utilities;

/// <summary>
/// Reads USB device model information for a given drive letter via WMI.
///
/// <see cref="GetUsbDeviceModel"/> is Windows-only (WMI).
/// <see cref="SplitMakeModel"/> is platform-independent (pure string logic).
///
/// Primary lookup chain (Windows only):
///   1. Win32_LogicalDisk → Win32_DiskDrive → DiskDrive.PNPDeviceID
///   2. Convert PNPDeviceID backslashes to '#' to form the USBSTOR fragment
///   3. Query Win32_PnPEntity WHERE DeviceID LIKE 'SWD\WPDBUSENUM\%'
///   4. Find WPD entity whose DeviceID contains the USBSTOR fragment
///   5. Return WPD entity Name (e.g. "DJI OA6", "OsmoAction") — already clean, no stripping needed
///
/// Fallback: Win32_DiskDrive.Model (cleaned with CleanUsbModelString)
/// </summary>
public static class UsbDeviceInfoReader
{

    private static readonly string[] KnownBrandPrefixes =
    [
        "Insta360",
        "GoPro",
        "Sony",
        "Canon",
        "Nikon",
        "Fujifilm",
        "Panasonic",
        "Olympus",
        "Garmin",
        "Rollei",
        "VIOFO",
        "DJI",
    ];

    /// <summary>
    /// Gets the USB device model name for a drive letter (e.g. "F:").
    /// Returns null if the drive is not a USB device or the WMI query fails.
    ///
    /// Tries WPD (Win32_PnPEntity) first to get the real friendly name (e.g. "DJI OA6").
    /// Falls back to Win32_DiskDrive.Model (cleaned) when no WPD match is found.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static string? GetUsbDeviceModel(string driveLetter)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var driveLetterClean = driveLetter.TrimEnd('\\', '/').ToUpperInvariant();
            if (!driveLetterClean.EndsWith(':'))
                driveLetterClean += ":";

            var partitionQuery = $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetterClean}'}} WHERE AssocClass = Win32_LogicalDiskToPartition";
            using var partitionSearcher = new ManagementObjectSearcher(partitionQuery);
            using var partitions = partitionSearcher.Get();

            foreach (ManagementObject partition in partitions)
            {

                var diskQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";
                using var diskSearcher = new ManagementObjectSearcher(diskQuery);
                using var disks = diskSearcher.Get();

                foreach (ManagementObject disk in disks)
                {
                    var pnpDeviceId = disk["PNPDeviceID"]?.ToString()?.Trim();
                    var fallbackModel = disk["Model"]?.ToString()?.Trim();

                    if (!string.IsNullOrEmpty(pnpDeviceId))
                    {

                        var wpd = TryGetWpdFriendlyName(pnpDeviceId);
                        if (wpd != null)
                            return wpd;
                    }

                    if (!string.IsNullOrEmpty(fallbackModel))
                        return CleanUsbModelString(fallbackModel);
                }
            }

            return null;
        }
        catch
        {

            return null;
        }
    }

    /// <summary>
    /// Queries Win32_PnPEntity for WPD (Windows Portable Devices) entries and finds the one
    /// whose DeviceID contains the USBSTOR fragment derived from the DiskDrive PNPDeviceID.
    ///
    /// Returns the WPD entity's Name property (e.g. "DJI OA6", "OsmoAction"), or null if not found.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string? TryGetWpdFriendlyName(string diskPnpDeviceId)
    {
        try
        {

            var usbstorFragment = diskPnpDeviceId.Replace('\\', '#');

            var wpdQuery = "SELECT DeviceID, Name FROM Win32_PnPEntity WHERE DeviceID LIKE 'SWD\\\\WPDBUSENUM\\\\%'";
            using var wpdSearcher = new ManagementObjectSearcher(wpdQuery);
            using var wpdEntities = wpdSearcher.Get();

            foreach (ManagementObject entity in wpdEntities)
            {
                var wpdDeviceId = entity["DeviceID"]?.ToString();
                if (string.IsNullOrEmpty(wpdDeviceId))
                    continue;

                if (wpdDeviceId.Contains(usbstorFragment, StringComparison.OrdinalIgnoreCase))
                {
                    var name = entity["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }

            return null;
        }
        catch
        {

            return null;
        }
    }

    /// <summary>
    /// Strips the " USB Device" / " USB" suffix that Windows appends to USB device model strings
    /// (e.g. "DJI OSMO ACTION 5 Pro USB Device" → "DJI OSMO ACTION 5 Pro").
    /// </summary>
    private static string CleanUsbModelString(string model)
    {

        if (model.EndsWith(" USB Device", StringComparison.OrdinalIgnoreCase))
            model = model[..^" USB Device".Length].TrimEnd();

        if (model.EndsWith(" USB", StringComparison.OrdinalIgnoreCase))
            model = model[..^" USB".Length].TrimEnd();

        return model.Trim();
    }

    /// <summary>
    /// Splits a cleaned USB model string into (make, model) components.
    ///
    /// Strategy:
    /// 1. If the string starts with a known brand prefix → (prefix, rest)
    /// 2. Otherwise → (first word, rest) so "MYSTERY CAM X1" → ("MYSTERY", "CAM X1")
    ///
    /// Both values are trimmed. The model part may be empty when the string is a single word.
    /// </summary>
    public static (string make, string model) SplitMakeModel(string usbModelString)
    {
        var trimmed = usbModelString.Trim();

        foreach (var brand in KnownBrandPrefixes)
        {
            if (trimmed.StartsWith(brand, StringComparison.OrdinalIgnoreCase))
            {
                var rest = trimmed[brand.Length..].TrimStart();
                return (brand, rest);
            }
        }

        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex < 0)
            return (trimmed, string.Empty);

        return (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..].TrimStart());
    }
}
