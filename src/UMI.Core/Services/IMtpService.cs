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

namespace UMI.Core.Services;

/// <summary>
/// Service für den Zugriff auf MTP-Geräte (Kameras, Smartphones) via Windows Portable Devices API.
/// </summary>
public interface IMtpService
{
    /// <summary>
    /// Gibt alle aktuell verbundenen MTP-Geräte zurück.
    /// </summary>
    IReadOnlyList<MtpDeviceInfo> GetConnectedDevices();

    /// <summary>
    /// Listet alle Dateien auf einem MTP-Gerät rekursiv auf.
    /// Optional gefiltert nach Dateierweiterungen (z.B. ".mp4", ".jpg").
    /// </summary>
    IReadOnlyList<MtpFileInfo> ListFiles(
        string deviceId,
        string? rootPath = null,
        IReadOnlySet<string>? extensions = null);

    /// <summary>
    /// Lädt eine einzelne Datei vom MTP-Gerät in ein lokales Verzeichnis herunter.
    /// Gibt den lokalen Dateipfad zurück, oder null bei Fehler.
    /// </summary>
    Task<string?> DownloadFileAsync(
        string deviceId,
        string mtpFilePath,
        string localTargetDir,
        CancellationToken ct = default);

    /// <summary>
    /// Lädt alle Dateien aus einer Liste herunter (Batch-Download).
    /// Gibt ein Result mit Erfolgs- und Fehlerzählung zurück.
    /// </summary>
    Task<MtpDownloadResult> DownloadBatchAsync(
        string deviceId,
        IReadOnlyList<MtpFileInfo> files,
        string localTargetDir,
        IProgress<MtpDownloadProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>Informationen über ein verbundenes MTP-Gerät.</summary>
public record MtpDeviceInfo(
    string DeviceId,
    string FriendlyName,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? WpdSerialNumber = null);

/// <summary>Datei auf einem MTP-Gerät.</summary>
public record MtpFileInfo(
    string FullPath,
    string Name,
    long Length,
    DateTime? DateModified);

/// <summary>Fortschritt beim Batch-Download.</summary>
public record MtpDownloadProgress(
    int Current,
    int Total,
    string CurrentFile,
    long BytesDownloaded,
    long BytesTotal);

/// <summary>Ergebnis eines Batch-Downloads.</summary>
public record MtpDownloadResult(
    int Downloaded,
    int Failed,
    int Skipped,
    long TotalBytes,
    IReadOnlyList<string> Errors);
