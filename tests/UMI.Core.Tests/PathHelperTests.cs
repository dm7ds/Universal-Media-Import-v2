// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using FluentAssertions;
using UMI.Core.Utilities;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Tests für PathHelper (.metadata/ Pfad-Berechnung).
/// </summary>
public class PathHelperTests
{
    [Theory]
    [InlineData(@"E:\Workbench\2026-02-12\OA5\Video\DJI_001.mp4",
                @"E:\Workbench\2026-02-12\.metadata\OA5\Video\DJI_001.meta.json",
                ".meta.json")]
    [InlineData(@"E:\Workbench\2026-02-12\OA5\DJI_001.mp4",
                @"E:\Workbench\2026-02-12\.metadata\OA5\DJI_001.meta.json",
                ".meta.json")]
    [InlineData(@"E:\Workbench\2026-02-12\DJI_001.mp4",
                @"E:\Workbench\2026-02-12\.metadata\DJI_001.meta.json",
                ".meta.json")]
    [InlineData(@"E:\Workbench\2026-02-12\OA5\Video\Gyroflow\DJI_001.mp4",
                @"E:\Workbench\2026-02-12\.metadata\OA5\Video\Gyroflow\DJI_001_optimized.gpx",
                "_optimized.gpx")]
    [InlineData(@"E:\Workbench\2024-09-22\R10\Photo\Sport_154111\IMG_1208.CR3",
                @"E:\Workbench\2024-09-22\.metadata\R10\Photo\Sport_154111\IMG_1208.meta.json",
                ".meta.json")]
    public void GetMetadataPath_WithValidTagRoot_ReturnsCorrectPath(
        string mediaPath, string expectedMetaPath, string extension)
    {

        var result = PathHelper.GetMetadataPath(mediaPath, extension);

        result.Should().NotBeNull();
        result.Should().Be(expectedMetaPath);
    }

    [Theory]
    [InlineData(@"E:\Workbench\OA5\Video\DJI_001.mp4")]
    [InlineData(@"C:\DJI_001.mp4")]
    [InlineData("")]
    [InlineData(null)]
    public void GetMetadataPath_WithoutTagRoot_ReturnsNull(string? mediaPath)
    {

        var result = PathHelper.GetMetadataPath(mediaPath, ".meta.json");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(@"E:\Workbench\2026-02-12\OA5\Video", @"E:\Workbench\2026-02-12")]
    [InlineData(@"E:\Workbench\2026-02-12\OA5", @"E:\Workbench\2026-02-12")]
    [InlineData(@"E:\Workbench\2026-02-12", @"E:\Workbench\2026-02-12")]
    [InlineData(@"E:\Workbench\2025-01-01\R10\Photo", @"E:\Workbench\2025-01-01")]
    [InlineData(@"E:\Workbench\2024-09-22\R10", @"E:\Workbench\2024-09-22")]
    public void FindTagRoot_WithValidDateFolder_ReturnsTagRoot(string directory, string expected)
    {

        var result = PathHelper.FindTagRoot(directory);

        result.Should().NotBeNull();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(@"E:\Workbench\OA5\Video")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"E:\")]
    public void FindTagRoot_WithoutDateFolder_ReturnsNull(string directory)
    {

        var result = PathHelper.FindTagRoot(directory);

        result.Should().BeNull();
    }

    [Fact]
    public void GetMetadataPath_NormalizesWindowsBackslashes()
    {

        var mixedPath = @"E:\Workbench\2026-02-12/OA5/Video/DJI_001.mp4";

        var result = PathHelper.GetMetadataPath(mixedPath, ".meta.json");

        result.Should().NotBeNull();
        result.Should().Contain(@"\.metadata\");
        result.Should().NotContain("/");
    }

    [Fact]
    public void GetMetadataPath_WithTempPath_FindsTagRoot()
    {

        var tempBase = Path.Combine(Path.GetTempPath(), $"PathHelper_Test_{Guid.NewGuid()}");
        var videoPath = Path.Combine(tempBase, "2025-02-09", "OA5", "Video", "test_video.mp4");
        var expected = Path.Combine(tempBase, "2025-02-09", ".metadata", "OA5", "Video", "test_video_optimized.gpx");

        var result = PathHelper.GetMetadataPath(videoPath, "_optimized.gpx");

        result.Should().NotBeNull();
        result.Should().Be(expected);
    }
}
