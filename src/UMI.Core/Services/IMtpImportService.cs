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
/// Service für den MTP-Import. Kapselt die bewährte Direct-Download-Logik
/// aus CLI (ImportCommand + WatchCommand) als wiederverwendbaren Core-Service.
/// GUI und CLI nutzen denselben Service.
/// </summary>
/// <remarks>
/// LEITPLANKE: NIEMALS %TEMP% oder Staging! Dateien landen direkt im Workbench-Ziel:
/// {workbench}/{datum}/{folderName}/{Video|Photo}/
/// </remarks>
public interface IMtpImportService
{
    /// <summary>
    /// Führt den MTP-Import durch (Direct-Download → EXIF-Korrektur → Post-Processing → History).
    /// </summary>
    /// <param name="request">Import-Konfiguration (Kamera, Gerät, Workbench, Optionen).</param>
    /// <param name="progress">Optionaler Fortschritts-Callback.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Import-Ergebnis mit Zähler und ggf. Fehlerliste.</returns>
    Task<MtpImportResult> ImportAsync(
        MtpImportRequest request,
        IProgress<MtpImportProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Alle Parameter für einen MTP-Import-Aufruf.
/// </summary>
public record MtpImportRequest(
    string CameraId,
    CameraConfig CameraConfig,
    MtpDeviceInfo Device,
    string WorkbenchPath,
    GlobalSettings GlobalSettings,
    bool Stabilize = false,
    string StabilizeMode = "automatic",
    bool InjectGps = false,
    bool RenameVideos = false,
    bool GoProRename = false,
    bool DryRun = false,
    /// <summary>
    /// EIS-Detection flag — independent of Gyroflow/Stabilize.
    /// When true, downloaded videos are analysed and EIS-Off videos moved to Gyroflow/.
    /// </summary>
    bool EisDetection = false,
    /// <summary>
    /// When true, videos are routed to Video/postprocess/ and GPS-Injection is deferred (DVR loses GPS data).
    /// Overrides CameraConfig.Features.PostProcess for the current import session.
    /// </summary>
    bool PostProcess = false,
    /// <summary>
    /// Optional lower bound of the import date range filter (session-only).
    /// Only files with DateModified >= DateFrom are downloaded. Null = no lower limit.
    /// </summary>
    DateTime? DateFrom = null,
    /// <summary>
    /// Optional upper bound of the import date range filter (session-only).
    /// Only files with DateModified <= DateTo are downloaded. Null = no upper limit.
    /// </summary>
    DateTime? DateTo = null);

/// <summary>
/// Ergebnis eines MTP-Imports.
/// </summary>
public record MtpImportResult(
    int Downloaded,
    int Failed,
    int Skipped,
    long TotalBytes,
    IReadOnlyList<string>? Errors = null);

/// <summary>
/// Fortschritt während eines MTP-Imports.
/// </summary>
public record MtpImportProgress(
    int Current,
    int Total,
    string CurrentFile,
    long BytesDownloaded,
    long BytesTotal,
    /// <summary>Aktuelle Phase: "Listing", "Downloading", "Processing".</summary>
    string Phase);
