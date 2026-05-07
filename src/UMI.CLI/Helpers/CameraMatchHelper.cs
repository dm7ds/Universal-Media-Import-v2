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

using UMI.CLI.Resources;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;

namespace UMI.CLI.Helpers;

/// <summary>
/// Helper für Kamera-Matching aus Label, EXIF, Registry.
/// DRY: Wird von CardsCommand und WatchCommand genutzt.
/// </summary>
public static class CameraMatchHelper
{
    /// <summary>
    /// Versucht eine Kamera aus dem Volume-Label zu matchen (nur gegen Camera-IDs).
    /// Beispiel: Label "DJI M2P" enthält "M2P" → Return "M2P" wenn M2P in cameras existiert.
    /// </summary>
    public static string? MatchCameraFromLabel(string? label, IEnumerable<string> availableCameras)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var labelUpper = label.ToUpperInvariant();

        return availableCameras
            .Where(cam => labelUpper.Contains(cam.ToUpperInvariant()))
            .OrderByDescending(cam => cam.Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// Erweitertes Label-Matching: Prüft Camera-ID UND Camera-Name gegen das Volume-Label.
    /// Beispiel: Label "DJI OA5" → matcht "OA5" (ID) oder "Osmo Action 5" (im Name).
    /// </summary>
    public static string? MatchCameraFromLabel(string? label, Dictionary<string, CameraConfig> cameras)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var labelUpper = label.ToUpperInvariant();

        var idMatch = cameras.Keys
            .Where(cam => labelUpper.Contains(cam.ToUpperInvariant()))
            .OrderByDescending(cam => cam.Length)
            .FirstOrDefault();

        if (idMatch != null)
            return idMatch;

        foreach (var (camId, camCfg) in cameras)
        {
            if (string.IsNullOrEmpty(camCfg.Name)) continue;
            var nameUpper = camCfg.Name.ToUpperInvariant();

            if (nameUpper.Contains(labelUpper))
                return camId;

            var nameWords = nameUpper.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in nameWords)
            {

                if (word.Length <= 3) continue;
                if (labelUpper.Contains(word))
                    return camId;
            }
        }

        return null;
    }

    /// <summary>
    /// Bestimmt Vorauswahl mit Priorität: Registry > EXIF > Label.
    /// </summary>
    public static string? DeterminePreselection(
        string? registeredCameraId,
        string? exifMatchedCameraId,
        string? volumeLabel,
        IEnumerable<string> availableCameras)
    {

        if (!string.IsNullOrEmpty(registeredCameraId))
            return registeredCameraId;

        if (!string.IsNullOrEmpty(exifMatchedCameraId))
            return exifMatchedCameraId;

        return MatchCameraFromLabel(volumeLabel, availableCameras);
    }

    /// <summary>
    /// Erweiterte Vorauswahl mit Kamera-Config (Label-Match gegen IDs + Namen).
    /// </summary>
    public static string? DeterminePreselection(
        string? registeredCameraId,
        string? exifMatchedCameraId,
        string? volumeLabel,
        Dictionary<string, CameraConfig> cameras)
    {
        if (!string.IsNullOrEmpty(registeredCameraId))
            return registeredCameraId;

        if (!string.IsNullOrEmpty(exifMatchedCameraId))
            return exifMatchedCameraId;

        return MatchCameraFromLabel(volumeLabel, cameras);
    }

    /// <summary>
    /// Fragt den User ob eine Karte Floating oder Fixed ist.
    /// DRY: Wird von CardsCommand (scan, add) und WatchCommand genutzt.
    /// </summary>
    /// <param name="cameraId">Kamera-ID für die Beschreibung im Dialog</param>
    /// <returns>true = Floating (kein feste Kamera), false = Fixed (immer diese Kamera)</returns>
    public static bool AskIsFloating(string cameraId)
    {
        Console.WriteLine($"  {CliStrings.Match_CardType}");
        Console.WriteLine($"    (1) {string.Format(CliStrings.Match_FixedOption, cameraId)}");
        Console.WriteLine($"    (2) {CliStrings.Match_FloatingOption}");
        Console.Write($"  {CliStrings.Match_Selection} [1]: ");

        var input = Console.ReadLine();
        return input?.Trim() == "2";
    }
}
