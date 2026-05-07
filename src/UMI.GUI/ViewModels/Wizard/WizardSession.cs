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

using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard;

/// <summary>
/// Shared state container passed to every wizard step.
/// Plain POCO — no INotifyPropertyChanged, no commands.
/// Lives as long as the wizard is open.
/// </summary>
public class WizardSession
{
    /// <summary>App mode chosen by the user in the Welcome step.</summary>
    public AppMode Mode { get; set; } = AppMode.Simple;

    /// <summary>Absolute path to the workbench root chosen in the Workbench step.</summary>
    public string WorkbenchPath { get; set; } = string.Empty;

    /// <summary>All cameras the user has configured during this wizard run.</summary>
    public List<WizardCameraEntry> Cameras { get; } = new();

    /// <summary>
    /// Absolute path to the folder containing GPX/GPS tracks.
    /// Only relevant when Mode == Advanced.
    /// </summary>
    public string GpsFolder { get; set; } = string.Empty;

    /// <summary>Fingerprint detected for the currently inserted SD card.</summary>
    public SdFingerprint? DetectedFingerprint { get; set; }

    /// <summary>Camera model matched against the detected fingerprint.</summary>
    public CameraModelMatch? DetectedModel { get; set; }

    /// <summary>Drive letter of the currently inserted SD card (e.g. "F:").</summary>
    public string? DetectedDriveLetter { get; set; }
}

/// <summary>
/// One source assignment — a single SD card or MTP device linked to a camera.
/// Serialized into SD card / MTP registration by SetupWizardViewModel.FinishAsync().
/// </summary>
public class SourceAssignment
{
    /// <summary>Drive letter for SD cards (e.g. "F:"). Empty for MTP devices.</summary>
    public string DriveLetter { get; set; } = string.Empty;

    /// <summary>Volume serial number (e.g. "A4F2-8B31"). Null when not yet read.</summary>
    public string? VolumeSerial { get; set; }

    /// <summary>Volume label (e.g. "DJI_ACTION"). Null when not available.</summary>
    public string? VolumeLabel { get; set; }

    /// <summary>Disk serial number. Null when not available or fake (DJI).</summary>
    public string? DiskSerial { get; set; }

    /// <summary>Disk size in bytes. 0 when not available.</summary>
    public long DiskSizeBytes { get; set; }

    /// <summary>Source type: "sd" for SD cards, "mtp" for MTP devices.</summary>
    public string SourceType { get; set; } = "sd";
}

/// <summary>
/// One camera entry accumulated during a wizard run.
/// Serialized into CameraConfig by SetupWizardViewModel.FinishAsync().
/// </summary>
public class WizardCameraEntry
{
    /// <summary>Unique camera identifier (used as config dictionary key).</summary>
    public string CameraId { get; set; } = string.Empty;

    /// <summary>Human-readable display name shown in the UI.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Camera type string matching a .umi type preset (e.g. "Action", "Drone").</summary>
    public string CameraType { get; set; } = string.Empty;

    /// <summary>Optional burst/processing profile name.</summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// All SD cards and MTP devices assigned to this camera.
    /// Replaces the old single SdVolumeSerial field.
    /// </summary>
    public List<SourceAssignment> Sources { get; } = new();

    /// <summary>
    /// Custom folder name inside the workbench (falls back to CameraId when null).
    /// </summary>
    public string? FolderName { get; set; }

    /// <summary>
    /// Returns a display string summarising assigned sources.
    /// Used in the Summary step.
    /// </summary>
    public string SourcesSummary
    {
        get
        {
            if (Sources.Count == 0) return Strings.Wizard_NoCardsAssigned;
            var labels = Sources.Select(s =>
                string.IsNullOrWhiteSpace(s.VolumeLabel)
                    ? s.DriveLetter
                    : $"{s.DriveLetter} ({s.VolumeLabel})");
            return string.Join(", ", labels);
        }
    }
}
