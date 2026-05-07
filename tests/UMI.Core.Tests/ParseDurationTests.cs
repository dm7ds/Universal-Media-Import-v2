// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.Core.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace UMI.Core.Tests;

/// <summary>
/// Unit Tests für GpsService.ParseDuration() - direktes Testen der Methode.
/// </summary>
public class ParseDurationTests
{
    private readonly GpsService _service;
    private readonly ITestOutputHelper _output;

    public ParseDurationTests(ITestOutputHelper output)
    {
        _output = output;

        var mockExifTool = new Mock<IExifToolWrapper>();
        var mockMp4Parser = new Mock<IMp4Parser>();
        var mockLogger = new Mock<ILogger<GpsService>>();
        var config = new GpsProcessingConfig
        {
            OptimizationEnabled = true,
            TimeBufferSeconds = 30,
            KeepOptimizedGpx = true,
            MinPointsThreshold = 2
        };

        var globalPaths = new GlobalPaths
        {
            Workbench = Path.GetTempPath(),
            Projects  = Path.GetTempPath(),
            GpxSource = Path.GetTempPath(),
            Tools     = new ToolPaths { ExifTool = string.Empty }
        };

        _service = new GpsService(mockExifTool.Object, mockMp4Parser.Object, config, globalPaths, mockLogger.Object);
    }

    [Fact]
    public void ParseDuration_60s_ReturnsCorrectTimeSpan()
    {

        var result = _service.ParseDuration("60 s");

        _output.WriteLine($"Input: '60 s'");
        _output.WriteLine($"Result: {result}");
        _output.WriteLine($"Expected: {TimeSpan.FromSeconds(60)}");

        result.Should().Be(TimeSpan.FromSeconds(60), "60 s sollte 60 Sekunden ergeben");
    }

    [Fact]
    public void ParseDuration_12345_ReturnsCorrectTimeSpan()
    {

        var result = _service.ParseDuration("123.45");

        _output.WriteLine($"Input: '123.45'");
        _output.WriteLine($"Result: {result}");
        _output.WriteLine($"Expected: {TimeSpan.FromSeconds(123.45)}");

        result.Should().Be(TimeSpan.FromSeconds(123.45), "123.45 sollte 123.45 Sekunden ergeben");
    }

    [Fact]
    public void ParseDuration_130_ReturnsCorrectTimeSpan()
    {

        var result = _service.ParseDuration("1:30");

        _output.WriteLine($"Input: '1:30'");
        _output.WriteLine($"Result: {result}");
        _output.WriteLine($"Expected: {TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(30))}");

        result.Should().Be(TimeSpan.FromSeconds(90), "1:30 sollte 90 Sekunden ergeben");
    }

    [Fact]
    public void ParseDuration_01523_ReturnsCorrectTimeSpan()
    {

        var result = _service.ParseDuration("0:15:23");

        _output.WriteLine($"Input: '0:15:23'");
        _output.WriteLine($"Result: {result}");

        var expected = TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(23));
        _output.WriteLine($"Expected: {expected}");

        result.Should().Be(expected, "0:15:23 sollte 15 Minuten 23 Sekunden ergeben");
    }

    [Fact]
    public void ParseDuration_55min_ReturnsCorrectTimeSpan()
    {

        var result = _service.ParseDuration("5.5 min");

        _output.WriteLine($"Input: '5.5 min'");
        _output.WriteLine($"Result: {result}");
        _output.WriteLine($"Expected: {TimeSpan.FromMinutes(5.5)}");

        result.Should().Be(TimeSpan.FromMinutes(5.5), "5.5 min sollte 5.5 Minuten ergeben");
    }

    [Theory]
    [InlineData("60 s", 60.0)]
    [InlineData("123.45", 123.45)]
    [InlineData("1:30", 90.0)]
    [InlineData("0:15:23", 923.0)]
    [InlineData("5.5 min", 330.0)]
    public void ParseDuration_AllFormats_ReturnCorrectSeconds(string input, double expectedSeconds)
    {

        var result = _service.ParseDuration(input);

        _output.WriteLine($"Input: '{input}'");
        _output.WriteLine($"Result: {result.TotalSeconds}s");
        _output.WriteLine($"Expected: {expectedSeconds}s");

        result.TotalSeconds.Should().BeApproximately(expectedSeconds, 0.1,
            $"'{input}' sollte {expectedSeconds} Sekunden ergeben");
    }
}
