// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using FluentAssertions;
using UMI.Core.Services;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Tests für GitHubReleaseChecker — reine Funktions-Tests ohne HTTP.
/// </summary>
public class UpdateServiceTests
{
    // --- IsUpdateAvailable ---

    [Theory]
    [InlineData("2.0.0", "2.1.0", true)]
    [InlineData("2.1.0", "2.1.0", false)]
    [InlineData("2.2.0", "2.1.0", false)]
    [InlineData("1.9.9", "2.0.0", true)]
    [InlineData("0.0.0", "0.0.1", true)]
    [InlineData("10.0.0", "9.99.99", false)]
    // Vierstellige Patch-Releases (z.B. 2.2.1.1 — CI-Fix ohne Feature-Änderung):
    [InlineData("2.2.1",   "2.2.1.1", true)]   // Patch ist neuer als das Basis-Release
    [InlineData("2.2.1.1", "2.2.1.1", false)]  // schon installiert -> KEIN Update anbieten
    [InlineData("2.2.1.1", "2.2.1",   false)]  // Basis-Release ist nicht neuer
    [InlineData("2.2.1.1", "2.2.2",   true)]   // nächstes normales Release gewinnt
    public void IsUpdateAvailable_VersionComparison_ReturnsCorrectResult(
        string current, string latest, bool expected)
    {
        GitHubReleaseChecker.IsUpdateAvailable(current, latest)
            .Should().Be(expected);
    }

    /// <summary>
    /// Regression: <see cref="GitHubReleaseChecker.GetCurrentVersion"/> verwarf früher
    /// die Revision. Ein Build der Version 2.2.1.1 meldete sich damit als "2.2.1",
    /// während der Release-Tag "v2.2.1.1" hieß — der Vergleich 2.2.1.1 &gt; 2.2.1 war
    /// dauerhaft wahr und der Nutzer bekam endlos ein Update auf die Version
    /// angeboten, die er schon installiert hatte.
    ///
    /// Der Test vergleicht gegen die ECHTE Assembly-Version als unabhängige
    /// Referenz — nicht gegen einen aus GetCurrentVersion() abgeleiteten Wert.
    /// Mit der alten Implementierung meldete diese Assembly (AssemblyVersion
    /// 2.2.1.1) "2.2.1" = 2.2.1.0, was kleiner als 2.2.1.1 ist → der Test schlägt
    /// dann fehl. Genau das ist die Schutzwirkung.
    /// </summary>
    [Fact]
    public void GetCurrentVersion_IsNotOlderThanAssemblyVersion_SoNoPhantomUpdateIsOffered()
    {
        var assemblyVersion = typeof(GitHubReleaseChecker).Assembly.GetName().Version;
        assemblyVersion.Should().NotBeNull();

        // BEIDE Seiten auf 4 Komponenten normalisieren. Version.Parse("2.2.2")
        // liefert Revision = -1 (nicht gesetzt), nicht 0 — normalisiert man nur
        // eine Seite, ist 2.2.2 kleiner als 2.2.2.0 und der Test schlägt bei
        // jeder dreistelligen Version fehl, ohne dass etwas kaputt wäre.
        var reported = Normalise(System.Version.Parse(GitHubReleaseChecker.GetCurrentVersion()));
        var actual   = Normalise(assemblyVersion!);

        static System.Version Normalise(System.Version v) => new(
            v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

        (reported >= actual).Should().BeTrue(
            "die gemeldete Version ({0}) darf nicht älter sein als die Assembly-Version ({1}) — " +
            "sonst erscheint der eigene Release dauerhaft als verfügbares Update",
            reported, actual);
    }

    [Theory]
    [InlineData("", "2.0.0", false)]
    [InlineData("2.0.0", "", false)]
    [InlineData("not-a-version", "2.0.0", false)]
    [InlineData("2.0.0", "not-a-version", false)]
    [InlineData("", "", false)]
    public void IsUpdateAvailable_InvalidVersionStrings_ReturnsFalse(
        string current, string latest, bool expected)
    {
        GitHubReleaseChecker.IsUpdateAvailable(current, latest)
            .Should().Be(expected);
    }

    // --- NormalizeVersion ---

    [Theory]
    [InlineData("v2.1.0", "2.1.0")]
    [InlineData("V2.1.0", "2.1.0")]
    [InlineData("2.1.0", "2.1.0")]
    [InlineData("v0.0.1", "0.0.1")]
    [InlineData(null, "0.0.0")]
    [InlineData("", "0.0.0")]
    [InlineData("   ", "0.0.0")]
    public void NormalizeVersion_StripsPrefixCorrectly(string? input, string expected)
    {
        GitHubReleaseChecker.NormalizeVersion(input)
            .Should().Be(expected);
    }

    // --- FindSetupAsset ---

    [Fact]
    public void FindSetupAsset_ReturnsFirstSetupExe()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "UMI_Setup_2.1.0.exe", BrowserDownloadUrl = "https://example.com/setup.exe" },
            new() { Name = "UMI_Sources.zip", BrowserDownloadUrl = "https://example.com/sources.zip" },
        };

        GitHubReleaseChecker.FindSetupAsset(assets)!.Url
            .Should().Be("https://example.com/setup.exe");
    }

    [Fact]
    public void FindSetupAsset_NoSetupAsset_ReturnsNull()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "UMI_Sources.zip", BrowserDownloadUrl = "https://example.com/sources.zip" },
        };

        GitHubReleaseChecker.FindSetupAsset(assets)
            .Should().BeNull();
    }

    [Fact]
    public void FindSetupAsset_NullAssets_ReturnsNull()
    {
        GitHubReleaseChecker.FindSetupAsset(null)
            .Should().BeNull();
    }

    [Fact]
    public void FindSetupAsset_EmptyAssets_ReturnsNull()
    {
        GitHubReleaseChecker.FindSetupAsset(new List<GitHubAsset>())
            .Should().BeNull();
    }

    [Fact]
    public void FindSetupAsset_CaseInsensitiveMatch()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "umi_setup_2.1.0.EXE", BrowserDownloadUrl = "https://example.com/setup.exe" },
        };

        GitHubReleaseChecker.FindSetupAsset(assets)!.Url
            .Should().Be("https://example.com/setup.exe");
    }

    [Fact]
    public void FindSetupAsset_SetupWithoutUrl_ReturnsNull()
    {
        var assets = new List<GitHubAsset> { new() { Name = "UMI_Setup_2.1.0.exe", BrowserDownloadUrl = null } };

        GitHubReleaseChecker.FindSetupAsset(assets)
            .Should().BeNull();
    }

    // --- digest-Feld der Release-API (I-021) ---

    /// <summary>
    /// Fixture in der Form der GitHub-Release-API (gekürzt). Der Digest steht NUR im
    /// JSON — der Erwartungswert ist die feste Literal-Zeichenkette, nicht aus dem
    /// Produktionscode abgeleitet. Das Setup-Asset ist bewusst NICHT das erste Asset:
    /// URL und Digest müssen vom selben Asset kommen.
    /// </summary>
    private const string ReleaseJson = """
        {
          "tag_name": "v9.9.9",
          "html_url": "https://github.com/dm7ds/Universal-Media-Import-v2/releases/tag/v9.9.9",
          "assets": [
            {
              "name": "UMI_Sources.zip",
              "browser_download_url": "https://example.com/sources.zip",
              "digest": "sha256:1111111111111111111111111111111111111111111111111111111111111111"
            },
            {
              "name": "UMI_Setup_9.9.9.exe",
              "browser_download_url": "https://example.com/UMI_Setup_9.9.9.exe",
              "digest": "sha256:31744aa8e0c4b1f2c3d4e5f60718293a4b5c6d7e8f9012233445566778bb8aa5"
            }
          ]
        }
        """;

    [Fact]
    public void ReleaseJson_DigestField_IsDeserializedOntoSetupAsset()
    {
        var release = System.Text.Json.JsonSerializer.Deserialize(
            ReleaseJson, GitHubReleaseContext.Default.GitHubRelease);

        var setup = GitHubReleaseChecker.FindSetupAsset(release!.Assets);

        setup.Should().NotBeNull();
        setup!.Url.Should().Be("https://example.com/UMI_Setup_9.9.9.exe");
        setup.Digest.Should().Be("sha256:31744aa8e0c4b1f2c3d4e5f60718293a4b5c6d7e8f9012233445566778bb8aa5");
    }

    [Fact]
    public void ReleaseJson_WithoutDigest_YieldsNullDigest()
    {
        const string json = """
            { "tag_name": "v1.0.0", "assets": [
              { "name": "UMI_Setup_1.0.0.exe", "browser_download_url": "https://example.com/s.exe" } ] }
            """;

        var release = System.Text.Json.JsonSerializer.Deserialize(json, GitHubReleaseContext.Default.GitHubRelease);

        GitHubReleaseChecker.FindSetupAsset(release!.Assets)!.Digest.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdates_CarriesDigestOfSetupAsset_IntoResult()
    {
        var handler = new JsonHandler(ReleaseJson);
        var sut = new GitHubReleaseChecker(new HttpClient(handler));

        var result = await sut.CheckForUpdatesAsync();

        result.IsUpdateAvailable.Should().BeTrue();
        result.Setup.Should().NotBeNull();
        result.Setup!.Url.Should().Be("https://example.com/UMI_Setup_9.9.9.exe");
        result.Setup.Digest.Should().Be("sha256:31744aa8e0c4b1f2c3d4e5f60718293a4b5c6d7e8f9012233445566778bb8aa5");
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
