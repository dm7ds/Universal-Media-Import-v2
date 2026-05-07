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

namespace UMI.Core.Services;

/// <summary>
/// Service für granulare Config-Bearbeitung (GUI-ready).
/// Arbeitet mit Dual-State (typisiert + raw JSON) für verlustfreies Speichern.
/// </summary>
public interface IConfigWriterService
{

    /// <summary>
    /// Lädt config.json in Dual-State (typisiert + raw JSON).
    /// </summary>
    Task<UmiConfig> LoadAsync(string? configPath = null, CancellationToken ct = default);

    /// <summary>
    /// Initialisiert eine neue leere Config (kein Lesen einer Datei nötig).
    /// Wird vom First-Run-Wizard verwendet wenn noch keine config.json existiert.
    /// </summary>
    /// <param name="configPath">Ziel-Pfad für späteres SaveAsync.</param>
    void InitializeNew(string configPath);

    /// <summary>
    /// Speichert Config verlustfrei (merged typisiert → raw, schreibt raw).
    /// Erstellt Backup (config.json.bak) vor dem Überschreiben.
    /// </summary>
    Task SaveAsync(CancellationToken ct = default);

    void AddCamera(string cameraId, CameraConfig config);
    void UpdateCamera(string cameraId, Action<CameraConfig> modifier);
    void RemoveCamera(string cameraId);

    /// <summary>
    /// Setzt Feature für eine Kamera (gps_injection, gyroflow, burst_detection, etc.).
    /// </summary>
    void SetFeature(string cameraId, string featureName, bool enabled);

    /// <summary>
    /// Setzt Tool-Pfad (exiftool, gyroflow, ffprobe).
    /// </summary>
    void SetToolPath(string toolName, string path);

    void SetWorkbenchPath(string path);
    void SetProjectsPath(string path);
    void SetGpxSourcePath(string path);

    /// <summary>
    /// Aktualisiert eine Config-Section via Modifier.
    /// Beispiel: UpdateSection&lt;GpsProcessingConfig&gt;("gps_processing", cfg => cfg.TimeBufferSeconds = 60)
    /// </summary>
    void UpdateSection<T>(string sectionName, Action<T> modifier) where T : class;

    /// <summary>
    /// Registriert SD-Karte in config.json (Key: Volume Serial Number).
    /// </summary>
    void RegisterSdCard(string volumeSerial, SdCardRegistration registration);

    /// <summary>
    /// Entfernt SD-Karten-Registrierung aus config.json.
    /// </summary>
    void UnregisterSdCard(string volumeSerial);

    /// <summary>
    /// Holt registrierte SD-Karte oder null wenn nicht gefunden.
    /// </summary>
    SdCardRegistration? GetSdCard(string volumeSerial);

    /// <summary>
    /// Registriert MTP-Gerät in config.json (Key: Geräte-Seriennummer).
    /// </summary>
    void RegisterMtpDevice(string serialNumber, MtpDeviceRegistration registration);

    /// <summary>
    /// Entfernt MTP-Geräte-Registrierung aus config.json.
    /// </summary>
    void UnregisterMtpDevice(string serialNumber);

    /// <summary>
    /// Holt registriertes MTP-Gerät oder null wenn nicht gefunden.
    /// </summary>
    MtpDeviceRegistration? GetMtpDevice(string serialNumber);

    /// <summary>Setzt SortOrder auf allen Kameras basierend auf der übergebenen ID-Reihenfolge.</summary>
    void ReorderCameras(IReadOnlyList<string> orderedCameraIds);

    /// <summary>Setzt CameraTypeOrder in LayoutConfig.</summary>
    void ReorderCameraTypes(IReadOnlyList<string> orderedTypes);

    /// <summary>Setzt SortOrder auf allen SD-Karten basierend auf der übergebenen VSN-Reihenfolge.</summary>
    void ReorderSdCards(IReadOnlyList<string> orderedVsns);

    /// <summary>Setzt SortOrder auf allen MTP-Geräten basierend auf der übergebenen Key-Reihenfolge.</summary>
    void ReorderMtpDevices(IReadOnlyList<string> orderedKeys);

    bool HasUnsavedChanges { get; }
    string ConfigPath { get; }

    /// <summary>
    /// True wenn eine Config bereits geladen wurde (LoadAsync wurde erfolgreich aufgerufen).
    /// Verhindert dass eine zweite LoadAsync-Anfrage die DI-Singleton-Referenzen invalidiert.
    /// </summary>
    bool IsConfigLoaded { get; }

    /// <summary>
    /// Gibt das aktuell geladene Config-Objekt zurück (für Binding).
    /// </summary>
    UmiConfig Config { get; }
}
