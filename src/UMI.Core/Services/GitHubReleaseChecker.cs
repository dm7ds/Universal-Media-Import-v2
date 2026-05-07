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
            DownloadUrl: string.Empty,
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
            var downloadUrl = FindSetupAssetUrl(release.Assets);

            if (!IsUpdateAvailable(_currentVersion, latestVersion))
                return noUpdate with { LatestVersion = latestVersion, ReleaseNotesUrl = releaseNotesUrl };

            return new UpdateCheckResult(
                IsUpdateAvailable: true,
                CurrentVersion: _currentVersion,
                LatestVersion: latestVersion,
                DownloadUrl: downloadUrl,
                ReleaseNotesUrl: releaseNotesUrl);
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
    /// Findet die Download-URL des ersten Assets das auf .exe endet und "Setup" im Namen hat.
    /// </summary>
    internal static string FindSetupAssetUrl(IReadOnlyList<GitHubAsset>? assets)
    {
        if (assets is null || assets.Count == 0)
            return string.Empty;

        foreach (var asset in assets)
        {
            if (asset.Name is not null
                && asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return asset.BrowserDownloadUrl ?? string.Empty;
            }
        }

        return string.Empty;
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
    public async Task DownloadSetupAsync(string url, string targetPath, IProgress<double>? progress, CancellationToken ct)
    {
        using var response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            bytesRead += read;

            if (progress is not null && totalBytes > 0)
                progress.Report((double)bytesRead / totalBytes.Value);
        }

        // Ensure 100% is reported even when Content-Length was unknown
        progress?.Report(1.0);
    }

    /// <summary>
    /// Liest die Assembly-Version als "Major.Minor.Build"-String.
    /// Einzige SSOT für Versions-Extraktion — wird auch von ServiceCollectionExtensions für den User-Agent genutzt.
    /// </summary>
    internal static string GetCurrentVersion()
    {
        var version = typeof(GitHubReleaseChecker).Assembly.GetName().Version;
        if (version is null)
            return "0.0.0";

        // Major.Minor.Build (ohne Revision)
        return $"{version.Major}.{version.Minor}.{version.Build}";
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
}
