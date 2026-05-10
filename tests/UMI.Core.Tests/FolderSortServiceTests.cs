// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using FluentAssertions;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.Core.Utilities;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Filesystem-level smoke tests for <see cref="FolderSortService"/>. We don't
/// stub MetadataReader / SequenceGroupingService — they're cheap, deterministic
/// and the whole point is that the service uses them end-to-end. Each test
/// creates a temp workbench, drops a couple of dummy files in, runs the
/// service, and asserts the resulting tree.
/// </summary>
public class FolderSortServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public FolderSortServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "umi-foldersort-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private static FolderSortService CreateService()
    {
        return new FolderSortService(
            metadataReader:     new MetadataReader(),
            burstMatching:      new BurstMatchingEngine(),
            sequenceGrouping:   new SequenceGroupingService(),
            burstProfileLoader: null,
            logger:             null);
    }

    private static UmiConfig MinimalConfig()
    {
        var cfg = new UmiConfig
        {
            GlobalPaths = new GlobalPaths
            {
                Workbench = "",
                Projects  = "",
                GpxSource = "",
                Tools     = new ToolPaths { ExifTool = "" },
            },
        };
        cfg.Cameras["TestCam"] = new CameraConfig
        {
            Name       = "TestCam",
            FolderName = "TestCam",
            Enabled    = true,
            FileTypes  = new CameraFileTypes
            {
                Photo = new[] { ".jpg" },
                Video = new[] { ".mp4" },
            },
        };
        return cfg;
    }

    [Fact]
    public async Task SortAsync_NoFiles_ReturnsZeroResult()
    {
        var service = CreateService();
        var config  = MinimalConfig();

        var result = await service.SortAsync(new FolderSortRequest(_tempRoot, FolderSortMode.Full, config));

        result.Moved.Should().Be(0);
        result.Skipped.Should().Be(0);
        result.Errors.Should().Be(0);
        result.DateFolders.Should().Be(0);
    }

    [Fact]
    public async Task SortAsync_FullMode_MovesFilesIntoDateCameraTypeTree()
    {
        var service = CreateService();
        var config  = MinimalConfig();

        // Drop a fake JPG into a sub-folder named after the camera so the
        // path-segment based ExtractCameraIdFromPath finds it. Set
        // LastWriteTime as the capture-date proxy (no EXIF in dummy bytes).
        var camDir = Path.Combine(_tempRoot, "TestCam");
        Directory.CreateDirectory(camDir);
        var src = Path.Combine(camDir, "IMG_0001.jpg");
        await File.WriteAllBytesAsync(src, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(src, new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Local));

        var result = await service.SortAsync(new FolderSortRequest(_tempRoot, FolderSortMode.Full, config));

        result.Moved.Should().Be(1);
        result.Errors.Should().Be(0);
        result.DateFolders.Should().Be(1);

        var expected = Path.Combine(_tempRoot, "2026-05-01", "TestCam", FolderNameConstants.Photo, "IMG_0001.jpg");
        File.Exists(expected).Should().BeTrue();
        File.Exists(src).Should().BeFalse();
    }

    [Fact]
    public async Task SortAsync_DryRun_DoesNotMoveFiles()
    {
        var service = CreateService();
        var config  = MinimalConfig();

        var src = Path.Combine(_tempRoot, "IMG_0002.jpg");
        await File.WriteAllBytesAsync(src, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(src, new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Local));

        var result = await service.SortAsync(new FolderSortRequest(
            _tempRoot, FolderSortMode.Full, config, DetectBursts: false, DryRun: true));

        result.Moved.Should().Be(1);
        File.Exists(src).Should().BeTrue("DryRun must not move files");
    }
}
