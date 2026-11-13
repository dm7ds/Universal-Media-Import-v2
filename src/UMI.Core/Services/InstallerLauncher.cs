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

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Die eine Start-Sequenz für den heruntergeladenen Installer (GUI und CLI): erneut prüfen,
/// nur bei <see cref="InstallerVerdict.Valid"/> starten, den Prüf-Handle bis nach dem Start halten.
/// </summary>
internal static class InstallerLauncher
{
    /// <summary>
    /// Prüft <paramref name="path"/> gegen <see cref="SetupAsset.Digest"/> und ruft bei
    /// <see cref="InstallerVerdict.Valid"/> <paramref name="start"/> auf, <b>während</b> die Datei mit
    /// <see cref="System.IO.FileShare.Read"/> offen gehalten wird. Der Handle wird erst nach Rückkehr von
    /// <paramref name="start"/> freigegeben — auch wenn <paramref name="start"/> wirft (die Exception wird
    /// weitergereicht, ein zweiter Startversuch findet nicht statt). Bei Mismatch (Datei gelöscht) und
    /// NotVerifiable wird <paramref name="start"/> nicht aufgerufen.
    /// </summary>
    internal static async Task<InstallerVerdict> RunVerifiedAsync(
        SetupAsset asset, string path, Action<string> start, ILogger? logger, CancellationToken ct)
    {
        using var check = await InstallerIntegrity.CheckAsync(path, asset.Digest, logger, ct)
            .ConfigureAwait(false);

        if (check.Verdict == InstallerVerdict.Valid)
            start(path);

        return check.Verdict;
    }

    /// <summary>
    /// Standard-Startaktion: startet <paramref name="path"/> per Shell (UAC-Abfrage für den Installer).
    /// Nur intern erreichbar: von außen führt der Weg ausschließlich über
    /// <see cref="IUpdateService.LaunchVerifiedInstallerAsync"/>, also über die Prüfung.
    /// </summary>
    internal static void ShellStart(string path)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
