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

using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Prüft GitHub-Releases auf neue UMI-Versionen.
/// </summary>
public sealed class GitHubReleaseChecker : IUpdateService
{
    // Update-Check liest Releases vom Public-Repo. Der Dev-Repo
    // (Universal-Media-Import-v2-dev, privat) wird via scripts/publish-to-public.ps1
    // als Snapshot-Mirror auf den Public-Repo gespiegelt; dort baut die CI
    // (.github/workflows/release.yml) die Setup-EXE und legt sie an's Release.
    private const string ReleasesApiUrl =
        "https://api.github.com/repos/dm7ds/Universal-Media-Import-v2/releases/latest";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubReleaseChecker>? _logger;
    private readonly string _currentVersion;

    public GitHubReleaseChecker(HttpClient httpClient, ILogger<GitHubReleaseChecker>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _currentVersion = GetCurrentVersion();
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var noUpdate = new UpdateCheckResult(
            IsUpdateAvailable: false,
            CurrentVersion: _currentVersion,
            LatestVersion: _currentVersion,
            ReleaseNotesUrl: string.Empty);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(RequestTimeout);

            var release = await _httpClient
                .GetFromJsonAsync(ReleasesApiUrl, GitHubReleaseContext.Default.GitHubRelease, cts.Token)
                .ConfigureAwait(false);

            if (release is null)
            {
                _logger?.LogWarning("GitHub releases API lieferte null-Antwort.");
                return noUpdate;
            }

            var latestVersion = NormalizeVersion(release.TagName);
            var releaseNotesUrl = release.HtmlUrl ?? string.Empty;

            if (!IsUpdateAvailable(_currentVersion, latestVersion))
                return noUpdate with { LatestVersion = latestVersion, ReleaseNotesUrl = releaseNotesUrl };

            return new UpdateCheckResult(
                IsUpdateAvailable: true,
                CurrentVersion: _currentVersion,
                LatestVersion: latestVersion,
                ReleaseNotesUrl: releaseNotesUrl,
                Setup: FindSetupAsset(release.Assets));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger?.LogWarning("Update-Check Timeout nach {Seconds}s.", RequestTimeout.TotalSeconds);
            return noUpdate;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Update-Check fehlgeschlagen (Netzwerkfehler oder ungültige Antwort).");
            return noUpdate;
        }
    }

    /// <summary>
    /// Vergleicht zwei Versions-Strings. Gibt true zurück wenn latestVersion neuer ist als currentVersion.
    /// Pure function — testbar ohne HTTP.
    /// </summary>
    internal static bool IsUpdateAvailable(string currentVersion, string latestVersion)
    {
        if (!System.Version.TryParse(currentVersion, out var current))
            return false;
        if (!System.Version.TryParse(latestVersion, out var latest))
            return false;

        return latest > current;
    }

    /// <summary>
    /// Findet das erste Asset das auf .exe endet und "Setup" im Namen hat. URL und Digest
    /// werden aus demselben Asset gelesen und reisen als <see cref="SetupAsset"/> weiter,
    /// damit sie nie auseinanderlaufen. Null, wenn es kein Setup-Asset mit URL gibt.
    /// </summary>
    internal static SetupAsset? FindSetupAsset(IReadOnlyList<GitHubAsset>? assets)
    {
        if (assets is null || assets.Count == 0)
            return null;

        foreach (var asset in assets)
        {
            if (asset.Name is not null
                && asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrEmpty(asset.BrowserDownloadUrl)
                    ? null
                    : new SetupAsset(asset.BrowserDownloadUrl, asset.Digest);
            }
        }

        return null;
    }

    /// <summary>
    /// Entfernt führendes 'v' aus Tag-Namen (z.B. "v2.1.0" → "2.1.0").
    /// </summary>
    internal static string NormalizeVersion(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return "0.0.0";

        return tagName.TrimStart('v', 'V');
    }

    /// <inheritdoc/>
    public async Task<InstallerVerdict> DownloadSetupAsync(
        SetupAsset asset, string targetPath, IProgress<double>? progress, CancellationToken ct)
    {
        // Ohne brauchbaren Digest gibt es nichts zu vergleichen -> gar nicht erst laden.
        if (!InstallerIntegrity.IsVerifiable(asset.Digest))
        {
            _logger?.LogWarning("Setup-Download uebersprungen, Digest fehlt oder ist ungueltig: {Url}", asset.Url);
            return InstallerVerdict.NotVerifiable;
        }

        await HttpFileDownload.DownloadAsync(_httpClient, asset.Url, targetPath, progress, _logger, ct)
            .ConfigureAwait(false);

        // Der Download-Stream ist hier geschlossen; die Datei wird erst jetzt geprueft.
        using var check = await InstallerIntegrity.CheckAsync(targetPath, asset.Digest, _logger, ct)
            .ConfigureAwait(false);
        return check.Verdict;
    }

    /// <inheritdoc/>
    public Task<InstallerVerdict> LaunchVerifiedInstallerAsync(SetupAsset asset, string path, CancellationToken ct)
        => InstallerLauncher.RunVerifiedAsync(asset, path, InstallerLauncher.ShellStart, _logger, ct);

    /// <summary>
    /// Liest die Assembly-Version als "Major.Minor.Build"-String — inklusive
    /// Revision, wenn es eine gibt (vierstellige Patch-Releases wie 2.2.1.1).
    /// Einzige SSOT für Versions-Extraktion — wird auch von ServiceCollectionExtensions für den User-Agent genutzt.
    /// </summary>
    internal static string GetCurrentVersion()
    {
        var version = typeof(GitHubReleaseChecker).Assembly.GetName().Version;
        if (version is null)
            return "0.0.0";

        // Die Revision MUSS mit, sobald sie gesetzt ist: sonst meldet ein
        // Build der Version 2.2.1.1 sich als "2.2.1", der Release-Tag heisst
        // aber "v2.2.1.1" — und 2.2.1.1 > 2.2.1 waere dauerhaft wahr. Der
        // Nutzer bekaeme endlos ein Update auf die Version angeboten, die er
        // bereits installiert hat.
        return version.Revision > 0
            ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}

// --- JSON-Modelle mit Source Generator ---

[JsonSerializable(typeof(GitHubRelease))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal partial class GitHubReleaseContext : JsonSerializerContext { }

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; init; }
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; init; }

    /// <summary>Roher <c>digest</c>-Wert der API (<c>sha256:&lt;hex&gt;</c>); fehlt bei älteren Releases.</summary>
    [JsonPropertyName("digest")]
    public string? Digest { get; init; }
}
