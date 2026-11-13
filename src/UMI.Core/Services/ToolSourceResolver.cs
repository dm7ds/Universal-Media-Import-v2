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

using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Download-URL und erwarteter Digest eines Werkzeug-Archivs, aus derselben Antwort der Quelle
/// gelesen und als Paar weitergereicht (wie <see cref="SetupAsset"/> beim Installer), damit sie nie
/// auf verschiedene Versionen zeigen. <see cref="Digest"/> ist roh (<c>sha256:&lt;hex&gt;</c> oder null);
/// ob er brauchbar ist, entscheidet allein <see cref="InstallerIntegrity.TryParseDigest"/>.
/// </summary>
internal sealed record ToolSource(string Url, string? Digest);

/// <summary>
/// Beantwortet für ein Werkzeug: „welche Datei lade ich, und welchen SHA256 hat der Herausgeber
/// dafür veröffentlicht?". Liefert nur das Paar; Laden, Prüfen und Entpacken macht
/// <see cref="ToolInstallService"/>. Kein Prüfwert gefunden heißt <c>Digest == null</c>, nicht „ohne Prüfung".
/// </summary>
internal static class ToolSourceResolver
{
    private const string GitHubApiBaseUrl = "https://api.github.com/repos/";
    private const string LatestReleaseSuffix = "/releases/latest";
    private const string ChecksumSuffix = ".sha256";
    private const int MaxChecksumFileBytes = 256;

    /// <summary>
    /// Jüngstes GitHub-Release von <paramref name="repo"/> (<c>owner/name</c>): das Asset mit genau dem
    /// Namen <paramref name="assetName"/>, URL und <c>digest</c> aus demselben Asset-Eintrag.
    /// Null, wenn das Release das Asset nicht hat oder keine URL für es liefert.
    /// </summary>
    internal static async Task<ToolSource?> FromGitHubReleaseAsync(
        HttpClient httpClient, string repo, string assetName, ILogger? logger, CancellationToken ct)
    {
        var release = await httpClient
            .GetFromJsonAsync(GitHubApiBaseUrl + repo + LatestReleaseSuffix, GitHubReleaseContext.Default.GitHubRelease, ct)
            .ConfigureAwait(false);

        var asset = release?.Assets?.FirstOrDefault(
            a => string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase));

        if (asset is null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
        {
            logger?.LogWarning("Release-Asset {Asset} in {Repo} nicht gefunden.", assetName, repo);
            return null;
        }

        return new ToolSource(asset.BrowserDownloadUrl, asset.Digest);
    }

    /// <summary>
    /// Vom Herausgeber neben dem Archiv veröffentlichte <c>.sha256</c>-Datei (nur der Hex-Wert).
    /// <paramref name="checksumUrl"/> darf auf die „jüngste Version" umleiten; maßgeblich ist die
    /// <b>aufgelöste</b> Adresse der Prüfsummen-Datei, aus der die Archiv-URL gebildet wird
    /// (Endung <c>.sha256</c> entfernt). So gehören Archiv und Prüfwert zur selben Version, auch wenn
    /// zwischen zwei Abfragen ein neues Release erscheint.
    /// Null, wenn die aufgelöste Adresse kein https-<c>….sha256</c> auf demselben Host wie
    /// <paramref name="checksumUrl"/> ist: eine Weiterleitung zu einem fremden Host wird nicht befolgt.
    /// </summary>
    internal static async Task<ToolSource?> FromPublishedChecksumAsync(
        HttpClient httpClient, string checksumUrl, ILogger? logger, CancellationToken ct)
    {
        var checksum = await HttpFileDownload.GetTextAsync(httpClient, checksumUrl, MaxChecksumFileBytes, ct)
            .ConfigureAwait(false);

        var resolved = checksum.ResolvedUri;
        if (resolved is null || resolved.Scheme != Uri.UriSchemeHttps
            || !string.Equals(resolved.Host, new Uri(checksumUrl).Host, StringComparison.OrdinalIgnoreCase)
            || !resolved.AbsolutePath.EndsWith(ChecksumSuffix, StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("Pruefsummen-Adresse aufgeloest zu unerwartetem Ziel: {Url}", resolved);
            return null;
        }

        // Eine überlange Antwort (Text == null) ist keine Prüfsummen-Datei, z. B. eine HTML-Fehlerseite.
        var archiveUrl = resolved.GetLeftPart(UriPartial.Path)[..^ChecksumSuffix.Length];
        return new ToolSource(archiveUrl, InstallerIntegrity.FromBareHex(checksum.Text));
    }
}
