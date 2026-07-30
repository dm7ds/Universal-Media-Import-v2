// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using System.Text.Json;
using FluentAssertions;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;
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

    /// <summary>
    /// Creates a <see cref="FolderSortService"/> backed by a real
    /// <see cref="BurstProfileLoader"/> that reads from <paramref name="configRoot"/>.
    /// The caller is responsible for creating <c>{configRoot}/presets/burst/</c> with
    /// the desired profile files before calling <see cref="FolderSortService.SortAsync"/>.
    /// </summary>
    private static FolderSortService CreateServiceWithBurstLoader(string configRoot)
    {
        // ConfigPathResolver.BurstPresetsDir = configRoot + /presets/burst
        var resolver = new ConfigPathResolver(configRoot);
        var loader   = new BurstProfileLoader(resolver, logger: null);
        return new FolderSortService(
            metadataReader:     new MetadataReader(),
            burstMatching:      new BurstMatchingEngine(),
            sequenceGrouping:   new SequenceGroupingService(),
            burstProfileLoader: loader,
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

    // -----------------------------------------------------------------------
    // TASK-215: Burst-Detection with _unsorted CameraId and Default-Profiles
    // -----------------------------------------------------------------------

    /// <summary>
    /// Regression test for TASK-215.
    /// Verifies that when DetectBursts=true is requested for files in an
    /// _unsorted folder (no matching camera in config.Cameras), the service
    /// builds a fallback BurstDetectionConfig from all available profiles
    /// and runs burst grouping instead of silently skipping them.
    ///
    /// Test setup: one Sport profile on disk (stub .umi). Two files with
    /// identical last-write-time (= same "burst second") — enough for
    /// SequenceGroupingService to produce one sequence group when the
    /// Sport profile min_count <= 2. We verify DetectedSequences > 0.
    ///
    /// Note: The stub profile has no EXIF match conditions (empty ConditionGroup),
    /// so BurstMatchingEngine returns "Sport" for all photos via the default
    /// first-profile path, satisfying the grouping check.
    /// </summary>
    [Fact]
    public async Task SortAsync_DetectBursts_UnsortedFolder_UsesFallbackProfiles()
    {
        // --- Arrange: temp config root with one burst profile ---
        var configRoot = Path.Combine(_tempRoot, "cfg");
        var burstPresetsDir = Path.Combine(configRoot, "presets", "burst");
        Directory.CreateDirectory(burstPresetsDir);

        // Write a minimal Sport profile with low min_count so 2 photos qualify.
        var sportProfile = new
        {
            name             = "Sport",
            description      = "Stub sport profile for unit test",
            priority         = 10,
            match_conditions = new { @operator = "AND", conditions = Array.Empty<object>(), groups = Array.Empty<object>() },
            grouping         = new { max_gap_seconds = 60.0, min_count = 2, adaptive_gap = false },
        };
        var profileJson = JsonSerializer.Serialize(sportProfile);
        await File.WriteAllTextAsync(Path.Combine(burstPresetsDir, "Sport.umi"), profileJson);

        // Build service with the loader pointing at configRoot (BurstPresetsDir = configRoot/presets/burst).
        var service = CreateServiceWithBurstLoader(configRoot);

        // Config has NO cameras — files will resolve to _unsorted.
        var config = new UmiConfig
        {
            GlobalPaths = new GlobalPaths
            {
                Workbench = "",
                Projects  = "",
                GpxSource = "",
                Tools     = new ToolPaths { ExifTool = "" },
            },
        };

        // Three JPGs directly in _tempRoot (no camera sub-dir → _unsorted).
        // Three files satisfy both the Sport profile min_count=2 and the
        // BurstDetectionConfig.FallbackMinCount=3 pre-check.
        // Use times within the Sport profile's max_gap_seconds=60 so they form one group.
        var captureTime = new DateTime(2026, 6, 1, 10, 30, 0, DateTimeKind.Local);
        var file1 = Path.Combine(_tempRoot, "IMG_0001.jpg");
        var file2 = Path.Combine(_tempRoot, "IMG_0002.jpg");
        var file3 = Path.Combine(_tempRoot, "IMG_0003.jpg");
        await File.WriteAllBytesAsync(file1, new byte[] { 0xFF, 0xD8, 0xFF });
        await File.WriteAllBytesAsync(file2, new byte[] { 0xFF, 0xD8, 0xFF });
        await File.WriteAllBytesAsync(file3, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(file1, captureTime);
        File.SetLastWriteTime(file2, captureTime.AddSeconds(1));
        File.SetLastWriteTime(file3, captureTime.AddSeconds(2));

        // --- Act (DryRun so nothing actually moves) ---
        var result = await service.SortAsync(
            new FolderSortRequest(_tempRoot, FolderSortMode.Full, config, DetectBursts: true, DryRun: true));

        // --- Assert ---
        result.Errors.Should().Be(0);
        result.DetectedSequences.Should().BeGreaterThan(0,
            "fallback burst config should run Sport profile on _unsorted files and detect at least one sequence");
    }

    // -----------------------------------------------------------------------
    // TASK-217: FolderSortService must exclude .umi/ and .metadata/ internals
    // -----------------------------------------------------------------------

    /// <summary>
    /// Regression test for TASK-217.
    /// Thumbnails and preview files under {workbench}/.umi/thumbnails/ must NOT
    /// be sorted into a date folder. Before the fix, *.thumb.jpg / *.preview.jpg
    /// were picked up as photos (correct extension, no EXIF → LastWriteTime =
    /// today) and ended up in {workbench}/{today}/_unsorted/Photo/.
    ///
    /// Test setup: one real JPG + one .thumb.jpg inside .umi/thumbnails/.
    /// After SortAsync only the real JPG is moved; the thumbnail stays in place
    /// and no today-dated folder is created for it.
    /// </summary>
    [Fact]
    public async Task SortAsync_SkipsUmiInternalFiles_ThumbNotSorted()
    {
        var service = CreateService();
        var config  = MinimalConfig();

        // --- real photo in workbench root ---
        var realPhoto = Path.Combine(_tempRoot, "IMG_0001.jpg");
        await File.WriteAllBytesAsync(realPhoto, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(realPhoto, new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Local));

        // --- thumbnail inside .umi/thumbnails/ — must be excluded ---
        var umiThumbDir = Path.Combine(_tempRoot, FolderNameConstants.UmiDir, FolderNameConstants.UmiThumbnails);
        Directory.CreateDirectory(umiThumbDir);
        var thumbFile = Path.Combine(umiThumbDir, "IMG_0001.thumb.jpg");
        await File.WriteAllBytesAsync(thumbFile, new byte[] { 0xFF, 0xD8, 0xFF });
        // LastWriteTime = today (simulates the bug: no EXIF → today's date folder)
        File.SetLastWriteTime(thumbFile, DateTime.Now);

        // --- Act ---
        var result = await service.SortAsync(new FolderSortRequest(_tempRoot, FolderSortMode.Full, config));

        // --- Assert ---
        // Only the real photo is moved (1 move, 0 errors).
        result.Moved.Should().Be(1, "only the real photo should be sorted");
        result.Errors.Should().Be(0);

        // The thumbnail must remain exactly where it was placed.
        File.Exists(thumbFile).Should().BeTrue("thumbnail must not be touched by FolderSortService");

        // The real photo lands in its expected date folder.
        var expected = Path.Combine(
            _tempRoot, "2026-05-10", "_unsorted", FolderNameConstants.Photo, "IMG_0001.jpg");
        File.Exists(expected).Should().BeTrue("real photo must be sorted into the date tree");

        // No today-dated folder should exist (that was the bug).
        var todayFolder = Path.Combine(_tempRoot, DateTime.Now.ToString("yyyy-MM-dd"));
        Directory.Exists(todayFolder).Should().BeFalse(
            "no today-dated folder should be created; thumbnails must be excluded from sort");
    }

    /// <summary>
    /// Regression test for TASK-215.
    /// When a camera has BurstDetection enabled (Features.BurstDetection=true)
    /// but BurstDetectionConfig.ActiveProfiles is empty, the service's
    /// defensive LoadBurstConfig path should fall back to all available profiles.
    /// Verifies DetectedSequences > 0 (not silently skipped due to empty profiles).
    /// </summary>
    [Fact]
    public async Task SortAsync_DetectBursts_EmptyActiveProfiles_FallsBackToAvailableProfiles()
    {
        // --- Arrange: temp config root with one burst profile ---
        var configRoot = Path.Combine(_tempRoot, "cfg2");
        var burstPresetsDir = Path.Combine(configRoot, "presets", "burst");
        Directory.CreateDirectory(burstPresetsDir);

        var sportProfile = new
        {
            name             = "Sport",
            description      = "Stub sport profile for unit test",
            priority         = 10,
            match_conditions = new { @operator = "AND", conditions = Array.Empty<object>(), groups = Array.Empty<object>() },
            grouping         = new { max_gap_seconds = 60.0, min_count = 2, adaptive_gap = false },
        };
        await File.WriteAllTextAsync(
            Path.Combine(burstPresetsDir, "Sport.umi"),
            JsonSerializer.Serialize(sportProfile));

        var service = CreateServiceWithBurstLoader(configRoot);

        // Camera config with BurstDetectionConfig set but ActiveProfiles empty.
        var config = new UmiConfig
        {
            GlobalPaths = new GlobalPaths
            {
                Workbench = "",
                Projects  = "",
                GpxSource = "",
                Tools     = new ToolPaths { ExifTool = "" },
            },
        };
        config.Cameras["TestCam"] = new CameraConfig
        {
            Name       = "TestCam",
            FolderName = "TestCam",
            Enabled    = true,
            Features   = new CameraFeatures { BurstDetection = true },
            FileTypes  = new CameraFileTypes { Photo = new[] { ".jpg" }, Video = new[] { ".mp4" } },
            BurstDetectionConfig = new BurstDetectionConfig
            {
                Enabled        = true,
                ActiveProfiles = new List<string>(), // deliberately empty — the bug state
            },
        };

        // Two JPGs in a TestCam sub-folder (ExtractCameraIdFromPath will find "TestCam").
        var camDir = Path.Combine(_tempRoot, "TestCam");
        Directory.CreateDirectory(camDir);
        var captureTime = new DateTime(2026, 6, 1, 10, 30, 0, DateTimeKind.Local);
        var file1 = Path.Combine(camDir, "IMG_0001.jpg");
        var file2 = Path.Combine(camDir, "IMG_0002.jpg");
        var file3 = Path.Combine(camDir, "IMG_0003.jpg");
        await File.WriteAllBytesAsync(file1, new byte[] { 0xFF, 0xD8, 0xFF });
        await File.WriteAllBytesAsync(file2, new byte[] { 0xFF, 0xD8, 0xFF });
        await File.WriteAllBytesAsync(file3, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(file1, captureTime);
        File.SetLastWriteTime(file2, captureTime.AddSeconds(1));
        File.SetLastWriteTime(file3, captureTime.AddSeconds(2));

        // --- Act (DryRun) ---
        var result = await service.SortAsync(
            new FolderSortRequest(_tempRoot, FolderSortMode.Full, config, DetectBursts: true, DryRun: true));

        // --- Assert ---
        result.Errors.Should().Be(0);
        result.DetectedSequences.Should().BeGreaterThan(0,
            "defensive fallback in LoadBurstConfig should use available profiles when ActiveProfiles is empty");
    }
}
