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
/// Prüft ob eine neuere Version von UMI verfügbar ist und lädt die Setup-EXE herunter.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Fragt die GitHub-Releases-API ab und vergleicht mit der aktuellen Version.
    /// Bei Netzwerkfehler wird kein Exception geworfen — stattdessen IsUpdateAvailable=false.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Lädt die Setup-EXE von <paramref name="url"/> nach <paramref name="targetPath"/> herunter.
    /// Meldet Fortschritt (0.0–1.0) via <paramref name="progress"/>.
    /// Wirft <see cref="OperationCanceledException"/> bei Abbruch, <see cref="HttpRequestException"/> bei Netzwerkfehler.
    /// </summary>
    Task DownloadSetupAsync(string url, string targetPath, IProgress<double>? progress, CancellationToken ct);
}

/// <summary>
/// Ergebnis einer Update-Prüfung.
/// </summary>
/// <param name="IsUpdateAvailable">True wenn eine neuere Version verfügbar ist.</param>
/// <param name="CurrentVersion">Aktuell installierte Version (aus Assembly).</param>
/// <param name="LatestVersion">Neueste verfügbare Version laut GitHub.</param>
/// <param name="DownloadUrl">Direkte Download-URL der Setup-EXE (erstes Asset das auf .exe endet und "Setup" im Namen hat).</param>
/// <param name="ReleaseNotesUrl">URL zur GitHub-Release-Seite.</param>
public record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string DownloadUrl,
    string ReleaseNotesUrl);
