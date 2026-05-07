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
    public void IsUpdateAvailable_VersionComparison_ReturnsCorrectResult(
        string current, string latest, bool expected)
    {
        GitHubReleaseChecker.IsUpdateAvailable(current, latest)
            .Should().Be(expected);
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
