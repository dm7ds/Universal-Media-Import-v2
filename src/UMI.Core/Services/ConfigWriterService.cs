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

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service für granulare Config-Bearbeitung mit verlustfreiem Speichern.
/// Dual-State: Typisiertes UmiConfig (für Binding) + Raw JsonNode (für unbekannte Felder).
/// </summary>
public class ConfigWriterService : IConfigWriterService
{
    private readonly ILogger<ConfigWriterService>? _logger;

    private const string ERROR_CONFIG_NOT_LOADED = "Config nicht geladen";

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private UmiConfig? _config;
    private JsonNode? _rawNode;
    private string? _configPath;
    private bool _hasUnsavedChanges;

    public ConfigWriterService(ILogger<ConfigWriterService>? logger = null)
    {
        _logger = logger;
    }

    public bool HasUnsavedChanges => _hasUnsavedChanges;

    public string ConfigPath => _configPath ?? throw new InvalidOperationException("Config nicht geladen");

    /// <summary>
    /// True wenn eine Config bereits erfolgreich geladen wurde.
    /// Erlaubt Callers (z.B. MainViewModel) den zweiten LoadAsync-Aufruf zu überspringen
    /// wenn App.xaml.cs den Pre-Load bereits durchgeführt hat.
    /// </summary>
    public bool IsConfigLoaded => _config != null && _config != _lazyDefault;

    /// <summary>
    /// Returns the loaded config, or a safe empty default if not yet loaded.
    /// DI singletons that depend on UmiConfig sub-objects (LayoutConfig etc.)
    /// are resolved before LoadAsync() — they get the default until config is loaded.
    /// </summary>
    public UmiConfig Config => _config ?? _lazyDefault;

    private static readonly UmiConfig _lazyDefault = new()
    {
        GlobalPaths = new GlobalPaths
        {
            Workbench = string.Empty,
            Projects = string.Empty,
            GpxSource = string.Empty,
            Tools = new ToolPaths
            {
                ExifTool = string.Empty,
                Gyroflow = string.Empty,
                FFprobe = string.Empty,
            },
        },
    };

    /// <summary>
    /// Initialisiert eine neue leere Config ohne eine Datei zu lesen.
    /// Setzt sinnvolle Defaults und bereitet SaveAsync vor.
    /// </summary>
    public void InitializeNew(string configPath)
    {
        _config = new UmiConfig
        {
            Version = UmiConfig.CurrentVersion,
            GlobalPaths = new GlobalPaths
            {
                Workbench = string.Empty,
                Projects = string.Empty,
                GpxSource = string.Empty,
                Tools = new ToolPaths
                {
                    ExifTool = string.Empty,
                    Gyroflow = string.Empty,
                    FFprobe = string.Empty
                }
            }
        };

        _rawNode = JsonNode.Parse(
            JsonSerializer.Serialize(_config, JsonDefaults.WriteOptions));

        _configPath = configPath;
        _hasUnsavedChanges = true;

        _logger?.LogDebug("Neue Config initialisiert: {Path}", configPath);
    }

    /// <summary>
    /// Lädt config.json in Dual-State.
    /// </summary>
    public async Task<UmiConfig> LoadAsync(string? configPath = null, CancellationToken ct = default)
    {
        configPath ??= "config.json";

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config nicht gefunden: {configPath}");
        }

        _logger?.LogDebug("Lade Config: {Path}", configPath);

        var json = await File.ReadAllTextAsync(configPath, ct);

        _rawNode = JsonNode.Parse(json);

        if (_rawNode == null)
        {
            throw new InvalidOperationException("Config konnte nicht geparsed werden");
        }

        _config = JsonSerializer.Deserialize<UmiConfig>(json, JsonDefaults.WriteOptions);

        if (_config == null)
        {
            throw new InvalidOperationException("Config konnte nicht deserialisiert werden");
        }

        // Schema migration. ConfigMigrator's chain is empty until a real schema
        // change ships — when that day comes, append the migration step there
        // and this code path activates automatically.
        if (ConfigMigrator.NeedsMigration(_config.Version))
        {
            var fromVersion = _config.Version;

            // Backup the pre-migration file so the user can roll back manually.
            var backupPath = configPath + $".pre-migration-{fromVersion}";
            try { await File.WriteAllTextAsync(backupPath, json, ct).ConfigureAwait(false); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Migration-Backup konnte nicht geschrieben werden: {Path}", backupPath); }

            var applied = ConfigMigrator.Migrate(_rawNode, fromVersion);
            foreach (var step in applied)
                _logger?.LogInformation("Config migriert: {Step}", step);

            // Re-deserialize the migrated rawNode so _config carries the new shape.
            _config = JsonSerializer.Deserialize<UmiConfig>(_rawNode.ToJsonString(), JsonDefaults.WriteOptions)
                ?? throw new InvalidOperationException("Migrated config konnte nicht re-deserialisiert werden");
            _hasUnsavedChanges = true;   // force re-save with the migrated version field
        }

        _configPath = configPath;
        if (!_hasUnsavedChanges) _hasUnsavedChanges = false;

        _logger?.LogDebug("Config geladen: v{Version}, {Count} Kameras", _config.Version, _config.Cameras.Count);

        return _config;
    }

    /// <summary>
    /// Speichert Config verlustfrei (merged typisiert → raw).
    /// Erstellt Backup vor dem Überschreiben.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_config == null || _rawNode == null || _configPath == null)
        {
            throw new InvalidOperationException("Config nicht geladen");
        }

        await _saveLock.WaitAsync(ct);
        try
        {
            _logger?.LogDebug("Speichere Config: {Path}", _configPath);

            MergeTypedIntoRaw();

            await CreateBackupAsync(ct);

            var json = _rawNode.ToJsonString(JsonDefaults.WriteOptions);
            await File.WriteAllTextAsync(_configPath, json, ct);

            _hasUnsavedChanges = false;

            _logger?.LogDebug("Config gespeichert: {Path}", _configPath);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public void AddCamera(string cameraId, CameraConfig config)
    {
        EnsureConfigLoaded();

        if (_config!.Cameras.ContainsKey(cameraId))
            throw new InvalidOperationException($"Kamera '{cameraId}' existiert bereits");

        _config.Cameras[cameraId] = config;
        _hasUnsavedChanges = true;

        _logger?.LogDebug("Kamera hinzugefügt: {CameraId}", cameraId);
    }

    public void UpdateCamera(string cameraId, Action<CameraConfig> modifier)
    {
        EnsureConfigLoaded();

        if (!_config!.Cameras.TryGetValue(cameraId, out var config))
            throw new KeyNotFoundException($"Kamera '{cameraId}' nicht gefunden");

        modifier(config);
        _hasUnsavedChanges = true;

        _logger?.LogDebug("Kamera aktualisiert: {CameraId}", cameraId);
    }

    /// <summary>
    /// learn-on-first-use: Füllt leere <see cref="CameraConfig.SerialNumber"/> und
    /// <see cref="CameraConfig.CameraModel"/> aus dem <paramref name="fingerprint"/>.
    /// Bestehende Werte werden NIEMALS überschrieben (AK2).
    /// Gibt die gelernte SerialNumber zurück (oder null wenn nichts gesetzt wurde).
    /// Diese Methode ist bewusst statisch und pure — keine Seiteneffekte außer dem
    /// direkten Setzen der Properties, damit sie in Unit-Tests ohne Service-Infrastruktur
    /// aufgerufen werden kann (TASK-222, AK5).
    /// </summary>
    public static string? LearnCameraIdentity(
        CameraConfig cam,
        SdFingerprint fingerprint,
        ILogger? logger = null)
    {
        string? learnedSerial = null;

        if (string.IsNullOrEmpty(cam.SerialNumber) && !string.IsNullOrEmpty(fingerprint.SerialNumber))
        {
            cam.SerialNumber = fingerprint.SerialNumber;
            learnedSerial    = fingerprint.SerialNumber;
            logger?.LogInformation(
                "Body-Serial {Serial} für Kamera {CameraId} gelernt",
                fingerprint.SerialNumber,
                cam.Name ?? "(unbenannt)");
        }
        else if (!string.IsNullOrEmpty(cam.SerialNumber)
              && !string.IsNullOrEmpty(fingerprint.SerialNumber)
              && cam.SerialNumber != fingerprint.SerialNumber)
        {
            // Konflikt: gespeicherte Serial ≠ gelesene Serial — nur loggen, kein Überschreiben
            logger?.LogWarning(
                "Body-Serial-Konflikt für Kamera {CameraId}: Config={ConfigSerial}, Fingerprint={FingerprintSerial} — kein Überschreiben",
                cam.Name ?? "(unbenannt)",
                cam.SerialNumber,
                fingerprint.SerialNumber);
        }

        if (string.IsNullOrEmpty(cam.CameraModel) && !string.IsNullOrEmpty(fingerprint.Model))
        {
            cam.CameraModel = fingerprint.Model;
            logger?.LogInformation(
                "camera_model '{Model}' für Kamera {CameraId} gelernt",
                fingerprint.Model,
                cam.Name ?? "(unbenannt)");
        }

        return learnedSerial;
    }

    public void RemoveCamera(string cameraId)
    {
        EnsureConfigLoaded();

        if (!_config!.Cameras.Remove(cameraId))
            throw new KeyNotFoundException($"Kamera '{cameraId}' nicht gefunden");

        var orphanedCards = _config.SdCards
            .Where(kv => kv.Value.CameraId == cameraId)
            .ToList();
        foreach (var kv in orphanedCards)
            kv.Value.CameraId = string.Empty;

        _hasUnsavedChanges = true;

        _logger?.LogDebug("Kamera entfernt: {CameraId} ({OrphanCount} SD-Karten auf Floating gesetzt)", cameraId, orphanedCards.Count);
    }

    public void SetFeature(string cameraId, string featureName, bool enabled)
    {
        UpdateCamera(cameraId, config =>
        {
            config.Features.SetByKey(featureName, enabled);
        });

        _logger?.LogDebug("Feature gesetzt: {CameraId}.{Feature} = {Enabled}", cameraId, featureName, enabled);
    }

    public void SetToolPath(string toolName, string path)
    {
        EnsureConfigLoaded();

        switch (toolName.ToLowerInvariant())
        {
            case ToolKeys.ExifTool:
                _config!.GlobalPaths.Tools.ExifTool = path;
                break;
            case ToolKeys.Gyroflow:
                _config!.GlobalPaths.Tools.Gyroflow = path;
                break;
            case ToolKeys.FFprobe:
                _config!.GlobalPaths.Tools.FFprobe = path;
                break;
            default:
                throw new ArgumentException($"Unbekanntes Tool: {toolName}");
        }

        _hasUnsavedChanges = true;

        _logger?.LogDebug("Tool-Pfad gesetzt: {Tool} = {Path}", toolName, path);
    }

    public void SetWorkbenchPath(string path)
    {
        EnsureConfigLoaded();

        _config!.GlobalPaths.Workbench = path;
        _hasUnsavedChanges = true;

        _logger?.LogDebug("Workbench-Pfad gesetzt: {Path}", path);
    }

    public void SetProjectsPath(string path)
    {
        EnsureConfigLoaded();

        _config!.GlobalPaths.Projects = path;
        _hasUnsavedChanges = true;

        _logger?.LogDebug("Projects-Pfad gesetzt: {Path}", path);
    }

    public void SetGpxSourcePath(string path)
    {
        EnsureConfigLoaded();

        _config!.GlobalPaths.GpxSource = path;
        _hasUnsavedChanges = true;

        _logger?.LogDebug("GpxSource-Pfad gesetzt: {Path}", path);
    }

    public void UpdateSection<T>(string sectionName, Action<T> modifier) where T : class
    {
        EnsureConfigLoaded();

        var section = sectionName.ToLowerInvariant() switch
        {
            FeatureKeys.MetadataBackup => _config!.MetadataBackup as T,
            "gps_processing" => _config!.GpsProcessing as T,
            ToolKeys.Gyroflow => _config!.Gyroflow as T,
            "verification" => _config!.Verification as T,
            "duplicate_handling" => _config!.DuplicateHandling as T,
            FeatureKeys.LensCorrection => _config!.LensCorrection as T,
            "archiving" => _config!.Archiving as T,
            "logging" => _config!.Logging as T,
            "workflow" => _config!.Workflow as T,
            "options" => _config!.Options as T,
            "layout" => _config!.Layout as T,
            "app_settings" => _config!.AppSettings as T,
            _ => throw new ArgumentException($"Unbekannte Section: {sectionName}")
        };

        if (section == null)
            throw new InvalidOperationException($"Section '{sectionName}' nicht als Typ '{typeof(T).Name}' verfügbar");

        modifier(section);
        _hasUnsavedChanges = true;

        _logger?.LogDebug("Section aktualisiert: {Section}", sectionName);
    }

    public void RegisterSdCard(string volumeSerial, SdCardRegistration registration)
    {
        EnsureConfigLoaded();

        _config!.SdCards[volumeSerial] = registration;
        _hasUnsavedChanges = true;

        _logger?.LogDebug("SD-Karte registriert: {VSN} → {CameraId}", volumeSerial, registration.CameraId);
    }

    public void UnregisterSdCard(string volumeSerial)
    {
        EnsureConfigLoaded();

        if (!_config!.SdCards.Remove(volumeSerial))
            _logger?.LogWarning("SD-Karte nicht gefunden: {VSN}", volumeSerial);
        else
        {
            _hasUnsavedChanges = true;
            _logger?.LogDebug("SD-Karte entfernt: {VSN}", volumeSerial);
        }
    }

    public SdCardRegistration? GetSdCard(string volumeSerial)
    {
        EnsureConfigLoaded();

        return _config!.SdCards.TryGetValue(volumeSerial, out var reg) ? reg : null;
    }

    public void RegisterMtpDevice(string serialNumber, MtpDeviceRegistration registration)
    {
        EnsureConfigLoaded();

        _config!.MtpDevices[serialNumber] = registration;
        _hasUnsavedChanges = true;

        _logger?.LogDebug("MTP-Gerät registriert: {Serial} → {CameraId}", serialNumber, registration.CameraId);
    }

    public void UnregisterMtpDevice(string serialNumber)
    {
        EnsureConfigLoaded();

        if (!_config!.MtpDevices.Remove(serialNumber))
            _logger?.LogWarning("MTP-Gerät nicht gefunden: {Serial}", serialNumber);
        else
        {
            _hasUnsavedChanges = true;
            _logger?.LogDebug("MTP-Gerät entfernt: {Serial}", serialNumber);
        }
    }

    public MtpDeviceRegistration? GetMtpDevice(string serialNumber)
    {
        EnsureConfigLoaded();

        return _config!.MtpDevices.TryGetValue(serialNumber, out var reg) ? reg : null;
    }

    public void ReorderCameras(IReadOnlyList<string> orderedCameraIds)
    {
        EnsureConfigLoaded();
        for (int i = 0; i < orderedCameraIds.Count; i++)
        {
            if (_config!.Cameras.TryGetValue(orderedCameraIds[i], out var cam))
                cam.SortOrder = i;
        }
        _hasUnsavedChanges = true;
        _logger?.LogDebug("Kameras neu geordnet: {Count} IDs", orderedCameraIds.Count);
    }

    public void ReorderCameraTypes(IReadOnlyList<string> orderedTypes)
    {
        EnsureConfigLoaded();
        _config!.Layout.CameraTypeOrder = orderedTypes.ToList();
        _hasUnsavedChanges = true;
        _logger?.LogDebug("CameraType-Reihenfolge gesetzt: {Types}", string.Join(", ", orderedTypes));
    }

    public void ReorderSdCards(IReadOnlyList<string> orderedVsns)
    {
        EnsureConfigLoaded();
        for (int i = 0; i < orderedVsns.Count; i++)
        {
            if (_config!.SdCards.TryGetValue(orderedVsns[i], out var reg))
                reg.SortOrder = i;
        }
        _hasUnsavedChanges = true;
        _logger?.LogDebug("SD-Karten neu geordnet: {Count} VSNs", orderedVsns.Count);
    }

    public void ReorderMtpDevices(IReadOnlyList<string> orderedKeys)
    {
        EnsureConfigLoaded();
        for (int i = 0; i < orderedKeys.Count; i++)
        {
            if (_config!.MtpDevices.TryGetValue(orderedKeys[i], out var reg))
                reg.SortOrder = i;
        }
        _hasUnsavedChanges = true;
        _logger?.LogDebug("MTP-Geräte neu geordnet: {Count} Keys", orderedKeys.Count);
    }

    /// <summary>
    /// Guard: Prüft ob Config geladen ist.
    /// DRY-Helper für 11 duplicate validation checks (Finding 2).
    /// </summary>
    private void EnsureConfigLoaded()
    {
        if (_config == null)
            throw new InvalidOperationException(ERROR_CONFIG_NOT_LOADED);
    }

    /// <summary>
    /// Merged typisiertes Config zurück in Raw JSON-Node.
    /// Nur bekannte Felder überschreiben, unbekannte bleiben erhalten.
    /// </summary>
    private void MergeTypedIntoRaw()
    {
        if (_config == null || _rawNode == null)
            throw new InvalidOperationException("Config nicht geladen");

        var typedJson = JsonSerializer.SerializeToNode(_config, JsonDefaults.WriteOptions);

        if (typedJson == null)
            throw new InvalidOperationException("Config konnte nicht serialisiert werden");

        foreach (var property in typedJson.AsObject())
        {

            _rawNode[property.Key] = property.Value?.DeepClone();
        }

        _logger?.LogDebug("Typed config merged into raw JSON");
    }

    /// <summary>
    /// Erstellt Backup (config.json → config.json.bak). Best-effort: ein
    /// fehlgeschlagenes Backup darf das eigentliche SaveAsync nicht killen,
    /// sonst verliert der User seine ganze Wizard-Eingabe nur weil das
    /// Install-Verzeichnis unter "Program Files" für die Backup-Datei kein
    /// Schreibrecht hergibt (z.B. wenn UMI nicht als Admin läuft).
    /// </summary>
    private async Task CreateBackupAsync(CancellationToken ct)
    {
        if (_configPath == null)
            return;

        var backupPath = _configPath + ".bak";

        if (!File.Exists(_configPath))
            return;

        try
        {
            await Task.Run(() => File.Copy(_configPath, backupPath, overwrite: true), ct);
            _logger?.LogDebug("Config-Backup erstellt: {Path}", backupPath);
            LastBackupError = null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                    || ex is IOException
                                    || ex is System.Security.SecurityException)
        {
            LastBackupError = $"{ex.GetType().Name}: {ex.Message} ({backupPath})";
            _logger?.LogWarning(ex, "Config-Backup fehlgeschlagen — überspringe (Speicher geht trotzdem durch): {Path}", backupPath);
        }
    }

    /// <summary>
    /// Last error message produced by <see cref="CreateBackupAsync"/>, or null when
    /// the most recent backup succeeded. Surface this to the user (e.g. the import
    /// summary) so a failing backup is visible without crashing the save.
    /// </summary>
    public string? LastBackupError { get; private set; }
}
