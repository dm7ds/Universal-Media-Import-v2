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

        var reported = System.Version.Parse(GitHubReleaseChecker.GetCurrentVersion());

        // Auf 4 Komponenten normalisieren, damit "2.2.1" als 2.2.1.0 verglichen wird.
        var actual = new System.Version(
            assemblyVersion!.Major,
            assemblyVersion.Minor,
            Math.Max(assemblyVersion.Build, 0),
            Math.Max(assemblyVersion.Revision, 0));

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

    // --- FindSetupAssetUrl ---

    [Fact]
    public void FindSetupAssetUrl_ReturnsFirstSetupExe()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "UMI_Setup_2.1.0.exe", BrowserDownloadUrl = "https://example.com/setup.exe" },
            new() { Name = "UMI_Sources.zip", BrowserDownloadUrl = "https://example.com/sources.zip" },
        };

        GitHubReleaseChecker.FindSetupAssetUrl(assets)
            .Should().Be("https://example.com/setup.exe");
    }

    [Fact]
    public void FindSetupAssetUrl_NoSetupAsset_ReturnsEmpty()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "UMI_Sources.zip", BrowserDownloadUrl = "https://example.com/sources.zip" },
        };

        GitHubReleaseChecker.FindSetupAssetUrl(assets)
            .Should().BeEmpty();
    }

    [Fact]
    public void FindSetupAssetUrl_NullAssets_ReturnsEmpty()
    {
        GitHubReleaseChecker.FindSetupAssetUrl(null)
            .Should().BeEmpty();
    }

    [Fact]
    public void FindSetupAssetUrl_EmptyAssets_ReturnsEmpty()
    {
        GitHubReleaseChecker.FindSetupAssetUrl(new List<GitHubAsset>())
            .Should().BeEmpty();
    }

    [Fact]
    public void FindSetupAssetUrl_CaseInsensitiveMatch()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "umi_setup_2.1.0.EXE", BrowserDownloadUrl = "https://example.com/setup.exe" },
        };

        GitHubReleaseChecker.FindSetupAssetUrl(assets)
            .Should().Be("https://example.com/setup.exe");
    }
}
