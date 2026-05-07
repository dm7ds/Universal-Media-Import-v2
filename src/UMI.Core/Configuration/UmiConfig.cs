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

using System.Text.Json.Serialization;

namespace UMI.Core.Configuration;

/// <summary>
/// Root-Konfigurationsklasse für UMI.
/// Enthält Kamera-Definitionen, globale Pfade und alle Feature-Konfigurationen.
/// </summary>
public class UmiConfig
{
    /// <summary>Aktuelle Config-Schemaversion. Einzige Quelle der Wahrheit für alle Versionsprüfungen.</summary>
    public const string CurrentVersion = "2.0";

    [JsonPropertyName("version")]
    public string Version { get; set; } = CurrentVersion;

    [JsonPropertyName("cameras")]
    public Dictionary<string, CameraConfig> Cameras { get; set; } = new();

    [JsonPropertyName("global_paths")]
    public required GlobalPaths GlobalPaths { get; set; }

    [JsonPropertyName("metadata_backup")]
    public MetadataBackupConfig MetadataBackup { get; set; } = new();

    [JsonPropertyName("gps_processing")]
    public GpsProcessingConfig GpsProcessing { get; set; } = new();

    [JsonPropertyName("gyroflow")]
    public GyroflowConfig Gyroflow { get; set; } = new();

    [JsonPropertyName("verification")]
    public VerificationConfig Verification { get; set; } = new();

    [JsonPropertyName("duplicate_handling")]
    public DuplicateHandlingConfig DuplicateHandling { get; set; } = new();

    [JsonPropertyName("lens_correction")]
    public LensCorrectionConfig LensCorrection { get; set; } = new();

    [JsonPropertyName("archiving")]
    public ArchivingConfig Archiving { get; set; } = new();

    [JsonPropertyName("logging")]
    public LoggingConfig Logging { get; set; } = new();

    [JsonPropertyName("workflow")]
    public WorkflowConfig Workflow { get; set; } = new();

    [JsonPropertyName("options")]
    public RunOptions Options { get; set; } = new();

    [JsonPropertyName("layout")]
    public LayoutConfig Layout { get; set; } = new();

    [JsonPropertyName("app_settings")]
    public AppSettings AppSettings { get; set; } = new();

    [JsonPropertyName("sd_cards")]
    public Dictionary<string, Models.SdCardRegistration> SdCards { get; set; } = new();

    [JsonPropertyName("mtp_devices")]
    public Dictionary<string, Models.MtpDeviceRegistration> MtpDevices { get; set; } = new();

    [JsonPropertyName("gpu_queue")]
    public GpuQueueConfig GpuQueue { get; set; } = new();
}
