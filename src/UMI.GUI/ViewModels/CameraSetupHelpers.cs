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

using System.Text.RegularExpressions;
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Shared helpers for camera setup flows (AddCameraDialog + Wizard CameraConfirmStep).
/// SSOT: Profile loading and camera ID validation exist EXACTLY ONCE here.
/// </summary>
internal static class CameraSetupHelpers
{
    /// <summary>
    /// Loads all camera profiles from <see cref="CameraTypeLoader"/>, sorted by key.
    /// Returns a list of <see cref="CameraProfileItem"/> ready for use in an ObservableCollection.
    /// </summary>
    public static List<CameraProfileItem> LoadProfiles(CameraTypeLoader typeLoader)
    {
        var types = typeLoader.LoadAllTypes();
        return types
            .OrderBy(kv => kv.Key)
            .Select(kv => new CameraProfileItem(kv.Key, kv.Value.Description, kv.Value.Color))
            .ToList();
    }

    /// <summary>
    /// Validates a camera ID.
    /// Returns null when valid, or an error message string when invalid.
    /// Empty IDs return null (no error message, but caller must still treat them as invalid / not-ready).
    /// </summary>
    /// <param name="cameraId">The raw camera ID to validate (will be trimmed internally).</param>
    /// <param name="configWriter">Config service used to check for duplicate IDs.</param>
    /// <param name="additionalExistingIds">
    /// Optional set of IDs already in use by the current wizard session (or other in-memory state)
    /// that has not yet been persisted to config.
    /// </param>
    public static string? ValidateCameraId(
        string? cameraId,
        IConfigWriterService configWriter,
        IEnumerable<string>? additionalExistingIds = null)
    {
        var id = cameraId?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(id))
            return null;

        if (id.Contains(' '))
            return Strings.CameraSetup_NoSpaces;

        if (configWriter.Config.Cameras.ContainsKey(id))
            return string.Format(Strings.CameraSetup_AlreadyRegistered, id);

        if (additionalExistingIds is not null)
        {
            foreach (var existing in additionalExistingIds)
            {
                if (string.Equals(existing, id, StringComparison.OrdinalIgnoreCase))
                    return string.Format(Strings.CameraSetup_AlreadyAddedWizard, id);
            }
        }

        return null;
    }

    /// <summary>Generate a config-safe camera ID from a display name.</summary>
    /// <remarks>
    /// Lowercases the input, replaces runs of non-alphanumeric characters with a single dash,
    /// and trims leading/trailing dashes. Falls back to "camera-1" for empty results.
    /// </remarks>
    public static string GenerateCameraId(string displayName)
    {
        var id = Regex.Replace(displayName.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(id) ? "camera-1" : id;
    }

    /// <summary>Generate a folder name from a display name (keeps original case).</summary>
    /// <remarks>
    /// Replaces whitespace and filesystem-unsafe characters with dashes, trims edges.
    /// Falls back to "Camera" for empty results.
    /// </remarks>
    public static string GenerateFolderName(string displayName)
    {
        var name = Regex.Replace(displayName.Trim(), @"[\s/\\:*?""<>|]+", "-").Trim('-');
        return string.IsNullOrEmpty(name) ? "Camera" : name;
    }
}
