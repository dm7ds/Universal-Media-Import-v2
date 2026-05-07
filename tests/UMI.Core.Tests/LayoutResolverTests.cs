// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using FluentAssertions;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Services;
using Xunit;

namespace UMI.Core.Tests;

public class LayoutResolverTests
{

    [Fact]
    public void CalculateDestPath_CameraFirst_WithCameraFolders_And_MediaFolders()
    {

        var config = new LayoutConfig
        {
            CameraFolders = true,
            MediaFolders = "true",
            SortOrder = SortOrder.CameraFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Video",
            useMediaFolders: true);

        result.Should().Be("C:\\Workbench\\2026-02-15\\OA5\\Video\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_TypeFirst_WithCameraFolders_And_MediaFolders()
    {

        var config = new LayoutConfig
        {
            CameraFolders = true,
            MediaFolders = "true",
            SortOrder = SortOrder.TypeFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Video",
            useMediaFolders: true);

        result.Should().Be("C:\\Workbench\\2026-02-15\\Video\\OA5\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_TypeFirst_NoCameraFolders()
    {

        var config = new LayoutConfig
        {
            CameraFolders = false,
            MediaFolders = "true",
            SortOrder = SortOrder.TypeFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Video",
            useMediaFolders: true);

        result.Should().Be("C:\\Workbench\\2026-02-15\\Video\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_TypeFirst_NoMediaFolders()
    {

        var config = new LayoutConfig
        {
            CameraFolders = true,
            MediaFolders = "false",
            SortOrder = SortOrder.TypeFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Video",
            useMediaFolders: false);

        result.Should().Be("C:\\Workbench\\2026-02-15\\OA5\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_Gyroflow_CameraFirst()
    {

        var config = new LayoutConfig
        {
            CameraFolders = true,
            MediaFolders = "true",
            SortOrder = SortOrder.CameraFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Gyroflow",
            useMediaFolders: true);

        result.Should().Be("C:\\Workbench\\2026-02-15\\OA5\\Gyroflow\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_Gyroflow_TypeFirst()
    {

        var config = new LayoutConfig
        {
            CameraFolders = true,
            MediaFolders = "true",
            SortOrder = SortOrder.TypeFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Gyroflow",
            useMediaFolders: true);

        result.Should().Be("C:\\Workbench\\2026-02-15\\Gyroflow\\OA5\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_Stabilized_CameraFirst()
    {

        var config = new LayoutConfig
        {
            CameraFolders = true,
            MediaFolders = "true",
            SortOrder = SortOrder.CameraFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Stabilized",
            useMediaFolders: true);

        result.Should().Be("C:\\Workbench\\2026-02-15\\OA5\\Stabilized\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_Stabilized_TypeFirst()
    {

        var config = new LayoutConfig
        {
            CameraFolders = true,
            MediaFolders = "true",
            SortOrder = SortOrder.TypeFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Stabilized",
            useMediaFolders: true);

        result.Should().Be("C:\\Workbench\\2026-02-15\\Stabilized\\OA5\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_CameraFirst_NoCameraFolders()
    {

        var config = new LayoutConfig
        {
            CameraFolders = false,
            MediaFolders = "true",
            SortOrder = SortOrder.CameraFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Video",
            useMediaFolders: true);

        result.Should().Be("C:\\Workbench\\2026-02-15\\Video\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_CameraFirst_NoMediaFolders()
    {

        var config = new LayoutConfig
        {
            CameraFolders = true,
            MediaFolders = "false",
            SortOrder = SortOrder.CameraFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Video",
            useMediaFolders: false);

        result.Should().Be("C:\\Workbench\\2026-02-15\\OA5\\DJI_0001.MP4");
    }

    [Fact]
    public void CalculateDestPath_TypeFirst_Stabilized_NoCameraFolders()
    {

        var config = new LayoutConfig
        {
            CameraFolders = false,
            MediaFolders = "true",
            SortOrder = SortOrder.TypeFirst
        };
        var resolver = new LayoutResolver(config);

        var result = resolver.CalculateDestPath(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            "DJI_0001.MP4",
            "video",
            "Stabilized",
            useMediaFolders: true);

        result.Should().Be("C:\\Workbench\\2026-02-15\\Stabilized\\DJI_0001.MP4");
    }

    [Fact]
    public void DetectConflicts_NoConflict_EmptyFolder()
    {

        var config = new LayoutConfig { CameraFolders = true };
        var resolver = new LayoutResolver(config);

        var conflicts = resolver.DetectConflicts(
            "C:\\NonExistent",
            "2026-02-15",
            "OA5",
            useMediaFolders: true);

        conflicts.Should().BeEmpty();
    }

    [Fact]
    public void DetectConflicts_NoConflict_MediaFoldersFalse()
    {

        var config = new LayoutConfig { CameraFolders = true };
        var resolver = new LayoutResolver(config);

        var conflicts = resolver.DetectConflicts(
            "C:\\Workbench",
            "2026-02-15",
            "OA5",
            useMediaFolders: false);

        conflicts.Should().BeEmpty();
    }

}
