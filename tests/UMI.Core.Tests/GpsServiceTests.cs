// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.Core.Utilities;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Tests für GPS Smart-Matching mit Timezone-Normalisierung.
/// </summary>
public class GpsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IExifToolWrapper> _mockExifTool;
    private readonly Mock<IMp4Parser> _mockMp4Parser;
    private readonly Mock<ILogger<GpsService>> _mockLogger;
    private readonly GpsProcessingConfig _config;
    private readonly GpsService _service;

    private static readonly XNamespace GpxNs = "http://www.topografix.com/GPX/1/1";

    public GpsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"UMI_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        _mockExifTool = new Mock<IExifToolWrapper>();
        _mockMp4Parser = new Mock<IMp4Parser>();
        _mockLogger = new Mock<ILogger<GpsService>>();

        _config = new GpsProcessingConfig
        {
            OptimizationEnabled = true,
            TimeBufferSeconds = 30,
            KeepOptimizedGpx = true,
            MinPointsThreshold = 2
        };

        _mockMp4Parser
            .Setup(x => x.ReadMetadataAsync(It.IsAny<string>()))
            .ReturnsAsync(new Mp4Metadata { IsValid = false });

        var globalPaths = new GlobalPaths
        {
            Workbench = _tempDir,
            Projects  = _tempDir,
            GpxSource = _tempDir,
            Tools     = new ToolPaths { ExifTool = string.Empty }
        };

        _service = new GpsService(_mockExifTool.Object, _mockMp4Parser.Object, _config, globalPaths, _mockLogger.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task OptimizeGpsForVideoAsync_UtcGpxMatchesLocalCreateDate_CET()
    {

        var videoDir = Path.Combine(_tempDir, "2025-02-09", "OA5", "Video");
        Directory.CreateDirectory(videoDir);

        var videoPath = Path.Combine(videoDir, "DJI_20250209151832_0001.mp4");
        File.WriteAllText(videoPath, "dummy video");

        _mockExifTool
            .Setup(x => x.ReadMetadataAsync(videoPath, It.IsAny<string[]>()))
            .ReturnsAsync(new Dictionary<string, object?>
            {
                ["CreateDate"] = "2025:02:09 15:18:32",
                ["Duration"] = "60 s"
            });

        _mockExifTool
            .Setup(x => x.InjectGpsFromGpxAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var gpxDir = Path.Combine(_tempDir, "gpx");
        Directory.CreateDirectory(gpxDir);

        CreateTestGpxFile(
            Path.Combine(gpxDir, "Tour_2025-02-09.gpx"),
            new DateTime(2025, 2, 9, 14, 18, 32, DateTimeKind.Utc),
            new DateTime(2025, 2, 9, 14, 19, 32, DateTimeKind.Utc),
            new[] {
                (51.5074, -0.1278, 100.0),
                (51.5075, -0.1279, 100.5),
                (51.5076, -0.1280, 101.0)
            }
        );

        var result = await _service.OptimizeGpsForVideoAsync(videoPath, gpxDir);

        result.Should().NotBeNull("GPX sollte mit Video-Zeitfenster matchen (CET → UTC Konvertierung)");
        result.Should().EndWith("_optimized.gpx");
        File.Exists(result!).Should().BeTrue();
    }

    [Fact]
    public async Task OptimizeGpsForVideoAsync_UtcGpxMatchesUtcGpsDateTime()
    {

        var videoDir = Path.Combine(_tempDir, "2025-02-09", "OA5", "Video");
        Directory.CreateDirectory(videoDir);

        var videoPath = Path.Combine(videoDir, "test_video_gps.mp4");
        File.WriteAllText(videoPath, "dummy video");

        _mockExifTool
            .Setup(x => x.ReadMetadataAsync(videoPath, It.IsAny<string[]>()))
            .ReturnsAsync(new Dictionary<string, object?>
            {
                ["GPSDateTime"] = "2025:02:09 14:18:32",
                ["Duration"] = "60 s"
            });

        _mockExifTool
            .Setup(x => x.InjectGpsFromGpxAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var gpxDir = Path.Combine(_tempDir, "gpx");
        Directory.CreateDirectory(gpxDir);

        CreateTestGpxFile(
            Path.Combine(gpxDir, "Tour_2025-02-09.gpx"),
            new DateTime(2025, 2, 9, 14, 18, 32, DateTimeKind.Utc),
            new DateTime(2025, 2, 9, 14, 19, 32, DateTimeKind.Utc),
            new[] {
                (51.5074, -0.1278, 100.0),
                (51.5075, -0.1279, 100.5),
                (51.5076, -0.1280, 101.0)
            }
        );

        var result = await _service.OptimizeGpsForVideoAsync(videoPath, gpxDir);

        result.Should().NotBeNull("GPX sollte direkt mit GPSDateTime (UTC) matchen");
        result.Should().EndWith("_optimized.gpx");
    }

    [Fact]
    public async Task OptimizeGpsForVideoAsync_NoMatch_WhenTimesDoNotOverlap()
    {

        var videoDir = Path.Combine(_tempDir, "2025-02-09", "OA5", "Video");
        Directory.CreateDirectory(videoDir);

        var videoPath = Path.Combine(videoDir, "test_video_nomatch.mp4");
        File.WriteAllText(videoPath, "dummy video");

        _mockExifTool
            .Setup(x => x.ReadMetadataAsync(videoPath, It.IsAny<string[]>()))
            .ReturnsAsync(new Dictionary<string, object?>
            {
                ["GPSDateTime"] = "2025:02:09 10:00:00",
                ["Duration"] = "60 s"
            });

        var gpxDir = Path.Combine(_tempDir, "gpx");
        Directory.CreateDirectory(gpxDir);

        CreateTestGpxFile(
            Path.Combine(gpxDir, "Tour_2025-02-09.gpx"),
            new DateTime(2025, 2, 9, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 2, 9, 14, 1, 0, DateTimeKind.Utc),
            new[] {
                (51.5074, -0.1278, 100.0),
                (51.5075, -0.1279, 100.5),
                (51.5076, -0.1280, 101.0)
            }
        );

        var result = await _service.OptimizeGpsForVideoAsync(videoPath, gpxDir);

        result.Should().BeNull("Keine Überlappung zwischen Video und GPX (zu großer Zeitabstand)");
    }

    [Fact]
    public async Task OptimizeGpsForVideoAsync_CultureInfoIndependent_GermanDecimalSeparator()
    {

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var videoDir = Path.Combine(_tempDir, "2025-02-09", "OA5", "Video");
            Directory.CreateDirectory(videoDir);

            var videoPath = Path.Combine(videoDir, "test_video_culture.mp4");
            File.WriteAllText(videoPath, "dummy video");

            _mockExifTool
                .Setup(x => x.ReadMetadataAsync(videoPath, It.IsAny<string[]>()))
                .ReturnsAsync(new Dictionary<string, object?>
                {
                    ["GPSDateTime"] = "2025:02:09 14:18:32",
                    ["Duration"] = "60 s"
                });

            _mockExifTool
                .Setup(x => x.InjectGpsFromGpxAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var gpxDir = Path.Combine(_tempDir, "gpx");
            Directory.CreateDirectory(gpxDir);

            CreateTestGpxFile(
                Path.Combine(gpxDir, "Tour_2025-02-09.gpx"),
                new DateTime(2025, 2, 9, 14, 18, 32, DateTimeKind.Utc),
                new DateTime(2025, 2, 9, 14, 19, 32, DateTimeKind.Utc),
                new[] {
                    (51.2277, 6.7735, 100.5),
                    (51.2278, 6.7736, 100.6),
                    (51.2279, 6.7737, 100.7)
                }
            );

            var result = await _service.OptimizeGpsForVideoAsync(videoPath, gpxDir);

            result.Should().NotBeNull("CultureInfo sollte Koordinaten-Parsing nicht beeinflussen");

            var gpxContent = File.ReadAllText(result!);
            gpxContent.Should().Contain("lat=\"51.2277\"", "Latitude sollte korrekt mit Punkt geparst werden");
            gpxContent.Should().Contain("lon=\"6.7735\"", "Longitude sollte korrekt mit Punkt geparst werden");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task OptimizeGpsForVideoAsync_ParsesDurationFormat_Seconds()
    {
        await TestDurationFormat("60 s");
    }

    [Fact]
    public async Task OptimizeGpsForVideoAsync_ParsesDurationFormat_TimeSpan()
    {
        await TestDurationFormat("1:30");
    }

    [Fact]
    public async Task OptimizeGpsForVideoAsync_ParsesDurationFormat_FullTime()
    {
        await TestDurationFormat("0:15:23");
    }

    [Fact]
    public async Task OptimizeGpsForVideoAsync_ParsesDurationFormat_Numeric()
    {
        await TestDurationFormat("123.45");
    }

    [Fact]
    public async Task OptimizeGpsForVideoAsync_ParsesDurationFormat_Minutes()
    {
        await TestDurationFormat("5.5 min");
    }

    private async Task TestDurationFormat(string durationStr)
    {

        var testId = durationStr.Replace(":", "_").Replace(" ", "_").Replace(".", "_");
        var testDir = Path.Combine(_tempDir, $"duration_{testId}");
        var videoDir = Path.Combine(testDir, "2025-02-09", "OA5", "Video");
        Directory.CreateDirectory(videoDir);

        var videoPath = Path.Combine(videoDir, $"test_video_{testId}.mp4");
        File.WriteAllText(videoPath, "dummy video");

        var mockExifTool = new Mock<IExifToolWrapper>();
        var mockCalls = 0;

        mockExifTool
            .Setup(x => x.ReadMetadataAsync(It.IsAny<string>(), It.IsAny<string[]>()))
            .ReturnsAsync(() =>
            {
                mockCalls++;
                return new Dictionary<string, object?>
                {
                    ["GPSDateTime"] = "2025:02:09 14:18:32",
                    ["Duration"] = durationStr
                };
            });

        mockExifTool
            .Setup(x => x.InjectGpsFromGpxAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var testGlobalPaths = new GlobalPaths
        {
            Workbench = testDir,
            Projects  = testDir,
            GpxSource = testDir,
            Tools     = new ToolPaths { ExifTool = string.Empty }
        };

        var service = new GpsService(mockExifTool.Object, _mockMp4Parser.Object, _config, testGlobalPaths, _mockLogger.Object);

        var gpxDir = Path.Combine(testDir, "gpx");
        Directory.CreateDirectory(gpxDir);

        CreateTestGpxFile(
            Path.Combine(gpxDir, "Tour.gpx"),
            new DateTime(2025, 2, 9, 14, 18, 0, DateTimeKind.Utc),
            new DateTime(2025, 2, 9, 14, 25, 0, DateTimeKind.Utc),
            new[] {
                (51.5074, -0.1278, 100.0),
                (51.50745, -0.12785, 100.1),
                (51.5075, -0.1279, 100.2),
                (51.50755, -0.12795, 100.3),
                (51.5076, -0.1280, 100.4),
                (51.50765, -0.12805, 100.5),
                (51.5077, -0.1281, 100.6),
                (51.50775, -0.12815, 100.7),
                (51.5078, -0.1282, 100.8),
                (51.50785, -0.12825, 100.9),
                (51.5079, -0.1283, 101.0)
            }
        );

        var result = await service.OptimizeGpsForVideoAsync(videoPath, gpxDir);

        mockCalls.Should().BeGreaterThan(0, "ExifTool Mock sollte aufgerufen worden sein");
        result.Should().NotBeNull($"Duration-Format '{durationStr}' sollte korrekt geparst werden. Mock wurde {mockCalls}x aufgerufen.");
    }

    /// <summary>
    /// Erstellt eine Test-GPX-Datei mit UTC-Timestamps.
    /// </summary>
    private void CreateTestGpxFile(
        string filePath,
        DateTime startUtc,
        DateTime endUtc,
        (double lat, double lon, double ele)[] points)
    {
        var trackPoints = new List<XElement>();
        var pointCount = points.Length;
        var timeIncrement = (endUtc - startUtc).TotalSeconds / pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            var (lat, lon, ele) = points[i];
            var time = startUtc.AddSeconds(i * timeIncrement);

            trackPoints.Add(new XElement(GpxNs + "trkpt",
                new XAttribute("lat", lat.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("lon", lon.ToString(CultureInfo.InvariantCulture)),
                new XElement(GpxNs + "ele", ele.ToString(CultureInfo.InvariantCulture)),
                new XElement(GpxNs + "time", time.ToString("yyyy-MM-ddTHH:mm:ssZ"))
            ));
        }

        var gpx = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(GpxNs + "gpx",
                new XAttribute("version", "1.1"),
                new XAttribute("creator", "UMI Tests"),
                new XElement(GpxNs + "trk",
                    new XElement(GpxNs + "name", "Test Track"),
                    new XElement(GpxNs + "trkseg", trackPoints)
                )
            )
        );

        gpx.Save(filePath);
    }
}
