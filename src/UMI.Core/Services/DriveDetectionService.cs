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
/// Erkannter Removable Drive (SD-Karte, USB-Stick, etc.)
/// </summary>
public class DetectedDrive
{
    /// <summary>
    /// Drive Letter mit Doppelpunkt (z.B. "F:")
    /// </summary>
    public string DriveLetter { get; set; } = "";

    /// <summary>
    /// Root-Pfad mit Backslash (z.B. "F:\")
    /// </summary>
    public string RootPath { get; set; } = "";

    /// <summary>
    /// Volume Label (z.B. "EOS_DIGITAL", "DJI_ACTION")
    /// </summary>
    public string? VolumeLabel { get; set; }

    /// <summary>
    /// Disk-Größe in Bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Drive ist bereit (gemounted und lesbar)
    /// </summary>
    public bool IsReady { get; set; }
}

/// <summary>
/// Service für Erkennung von Removable Drives (SD-Karten, USB-Sticks).
/// Nutzt simples Polling (keine WMI Events) für Robustheit im CLI.
/// </summary>
public interface IDriveDetectionService
{
    /// <summary>
    /// Gibt alle aktuell eingesteckten Removable Drives zurück.
    /// </summary>
    List<DetectedDrive> GetRemovableDrives();

    /// <summary>
    /// Wartet auf einen neuen Removable Drive (Polling-Loop).
    /// Returns: Neu erkannter Drive oder null bei Cancellation.
    /// </summary>
    /// <param name="knownDrives">Set von bereits bekannten Drive Letters (z.B. "F:")</param>
    /// <param name="ct">Cancellation Token</param>
    DetectedDrive? WaitForNewDrive(HashSet<string> knownDrives, CancellationToken ct);
}

public class DriveDetectionService : IDriveDetectionService
{
    private readonly ILogger<DriveDetectionService>? _logger;

    public DriveDetectionService(ILogger<DriveDetectionService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gibt alle aktuell eingesteckten Removable Drives zurück.
    /// </summary>
    public List<DetectedDrive> GetRemovableDrives()
    {
        var drives = new List<DetectedDrive>();

        try
        {
            var allDrives = DriveInfo.GetDrives();

            foreach (var drive in allDrives)
            {

                if (drive.DriveType != DriveType.Removable)
                    continue;

                if (!drive.IsReady)
                    continue;

                long totalSize = 0;
                try { totalSize = drive.TotalSize; }
                catch (Exception ex) { _logger?.LogDebug(ex, "TotalSize nicht lesbar für {Drive}", drive.Name); }
                if (totalSize <= 0)
                    continue;

                var driveLetter = drive.Name.TrimEnd('\\', '/');

                drives.Add(new DetectedDrive
                {
                    DriveLetter = driveLetter,
                    RootPath = drive.RootDirectory.FullName,
                    VolumeLabel = GetVolumeLabel(drive),
                    TotalSizeBytes = totalSize,
                    IsReady = drive.IsReady
                });
            }

            _logger?.LogDebug("Removable Drives gefunden: {Count} ({Drives})",
                drives.Count, string.Join(", ", drives.Select(d => d.DriveLetter)));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Auflisten der Removable Drives");
        }

        return drives;
    }

    /// <summary>
    /// Wartet auf einen neuen Removable Drive (Polling-Loop).
    /// Prüft alle 1 Sekunde ob ein neuer Drive erschienen ist.
    /// </summary>
    public DetectedDrive? WaitForNewDrive(HashSet<string> knownDrives, CancellationToken ct)
    {
        _logger?.LogDebug("Warte auf neuen Removable Drive (bekannt: {Known})",
            string.Join(", ", knownDrives));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var currentDrives = GetRemovableDrives();

                foreach (var drive in currentDrives)
                {
                    if (!knownDrives.Contains(drive.DriveLetter))
                    {
                        _logger?.LogDebug("Neuer Removable Drive erkannt: {Drive} ({Label})",
                            drive.DriveLetter, drive.VolumeLabel ?? "(kein Label)");
                        return drive;
                    }
                }

                Thread.Sleep(1000);
            }
            catch (OperationCanceledException)
            {

                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Fehler beim Drive-Polling");

                if (ct.IsCancellationRequested) break;
                Thread.Sleep(1000);
            }
        }

        _logger?.LogDebug("Drive-Polling abgebrochen (Cancellation)");
        return null;
    }

    /// <summary>
    /// Liest Volume Label sicher aus (graceful degradation).
    /// </summary>
    private static string? GetVolumeLabel(DriveInfo drive)
    {
        try
        {
            var label = drive.VolumeLabel?.Trim();
            return string.IsNullOrEmpty(label) ? null : label;
        }
        catch
        {
            return null;
        }
    }
}
