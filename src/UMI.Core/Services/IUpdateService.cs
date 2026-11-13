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
    /// Lädt die Setup-EXE von <see cref="SetupAsset.Url"/> nach <paramref name="targetPath"/> herunter
    /// und prüft sie gegen <see cref="SetupAsset.Digest"/>.
    /// Meldet Fortschritt (0.0–1.0) via <paramref name="progress"/>.
    /// Ist der Digest nicht brauchbar, wird gar nicht erst heruntergeladen
    /// (<see cref="InstallerVerdict.NotVerifiable"/>); weicht die Datei ab, wird sie gelöscht
    /// (<see cref="InstallerVerdict.Mismatch"/>). Nur bei <see cref="InstallerVerdict.Valid"/> liegt die Datei am Ziel.
    /// Bei Abbruch oder Fehler wird auch die Teil-Datei gelöscht.
    /// Wirft <see cref="OperationCanceledException"/> bei Abbruch, <see cref="HttpRequestException"/> bei Netzwerkfehler.
    /// </summary>
    Task<InstallerVerdict> DownloadSetupAsync(
        SetupAsset asset, string targetPath, IProgress<double>? progress, CancellationToken ct);

    /// <summary>
    /// Der einzige Weg, den heruntergeladenen Installer zu starten: prüft die Setup-EXE
    /// <paramref name="path"/> unmittelbar vor dem Start erneut (SHA256 gegen <see cref="SetupAsset.Digest"/>)
    /// und startet sie nur bei <see cref="InstallerVerdict.Valid"/> per Shell (UAC) — während die Datei mit
    /// <see cref="System.IO.FileShare.Read"/> offen gehalten wird, sodass sie zwischen Prüfung und Start
    /// niemand ersetzen kann. Danach wird der Handle freigegeben, auch wenn der Start wirft
    /// (die Exception wird weitergereicht, ein zweiter Startversuch findet nicht statt).
    /// Bei Abweichung ist die Datei gelöscht; bei allem außer Valid wird nichts gestartet.
    /// </summary>
    Task<InstallerVerdict> LaunchVerifiedInstallerAsync(SetupAsset asset, string path, CancellationToken ct);
}

/// <summary>
/// Das Setup-Asset eines Releases: Download-URL und erwarteter Digest aus demselben GitHub-Asset
/// als Paar, damit beide nie auseinanderfallen können. Nur Core kann das Paar bilden
/// (Konstruktor <c>internal</c>, keine <c>with</c>-Änderung); GUI und CLI reichen es nur durch.
/// </summary>
public sealed record SetupAsset
{
    internal SetupAsset(string url, string? digest)
    {
        Url = url;
        Digest = digest;
    }

    /// <summary>Direkte Download-URL der Setup-EXE.</summary>
    public string Url { get; }

    /// <summary>
    /// Roher <c>digest</c>-Wert der API (<c>sha256:&lt;64 Hex&gt;</c>). Null = die API lieferte keinen
    /// (älteres Release) — dann ist der Installer nicht prüfbar.
    /// </summary>
    public string? Digest { get; }
}

/// <summary>
/// Ergebnis einer Update-Prüfung.
/// </summary>
/// <param name="IsUpdateAvailable">True wenn eine neuere Version verfügbar ist.</param>
/// <param name="CurrentVersion">Aktuell installierte Version (aus Assembly).</param>
/// <param name="LatestVersion">Neueste verfügbare Version laut GitHub.</param>
/// <param name="ReleaseNotesUrl">URL zur GitHub-Release-Seite.</param>
/// <param name="Setup">
/// Setup-Asset (erstes Asset das auf .exe endet und "Setup" im Namen hat). Null = das Release hat keins.
/// </param>
public record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseNotesUrl,
    SetupAsset? Setup = null);
