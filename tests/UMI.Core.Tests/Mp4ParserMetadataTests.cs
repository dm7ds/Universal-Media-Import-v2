// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UMI.Core.Utilities;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Tests für Mp4Parser.ReadMetadataAsync() - natives CreateDate/Duration Lesen.
/// </summary>
public class Mp4ParserMetadataTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ILogger<Mp4Parser>> _mockLogger;
    private readonly Mp4Parser _parser;

    public Mp4ParserMetadataTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"UMI_Mp4Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        _mockLogger = new Mock<ILogger<Mp4Parser>>();
        _parser = new Mp4Parser(_mockLogger.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task ReadMetadataAsync_ValidMp4_ReturnsUtcCreateDate()
    {

        var mp4Path = Path.Combine(_tempDir, "test_video.mp4");

        var mp4Epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var targetDate = new DateTime(2025, 2, 9, 14, 18, 32, DateTimeKind.Utc);
        uint creation_time = (uint)(targetDate - mp4Epoch).TotalSeconds;

        uint timescale = 1000;
        uint duration = 60000;

        CreateMinimalMp4WithMvhd(mp4Path, creation_time, timescale, duration);

        var result = await _parser.ReadMetadataAsync(mp4Path);

        result.IsValid.Should().BeTrue("MP4 sollte gültige mvhd Box haben");
        result.CreateDate.Should().NotBeNull();
        result.CreateDate!.Value.Kind.Should().Be(DateTimeKind.Utc, "CreateDate MUSS UTC sein");

        var expectedDate = new DateTime(2025, 2, 9, 14, 18, 32, DateTimeKind.Utc);
        result.CreateDate.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ReadMetadataAsync_ValidMp4_CalculatesDurationCorrectly()
    {

        var mp4Path = Path.Combine(_tempDir, "test_duration.mp4");

        var mp4Epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var testDate = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        uint creation_time = (uint)(testDate - mp4Epoch).TotalSeconds;

        uint timescale = 1000;
        uint duration = 90000;

        CreateMinimalMp4WithMvhd(mp4Path, creation_time, timescale, duration);

        var result = await _parser.ReadMetadataAsync(mp4Path);

        result.IsValid.Should().BeTrue();
        result.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(90), TimeSpan.FromMilliseconds(10));
        result.Timescale.Should().Be(1000);
    }

    [Fact]
    public async Task ReadMetadataAsync_MvhdVersion1_Handles64BitFields()
    {

        var mp4Path = Path.Combine(_tempDir, "test_v1.mp4");

        var mp4Epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var testDate = new DateTime(2023, 8, 20, 9, 30, 0, DateTimeKind.Utc);
        ulong creation_time = (ulong)(testDate - mp4Epoch).TotalSeconds;

        uint timescale = 30000;
        ulong duration = 1800000;

        CreateMinimalMp4WithMvhdV1(mp4Path, creation_time, timescale, duration);

        var result = await _parser.ReadMetadataAsync(mp4Path);

        result.IsValid.Should().BeTrue("mvhd Version 1 sollte unterstützt werden");
        result.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task ReadMetadataAsync_InvalidFile_ReturnsIsValidFalse()
    {

        var txtPath = Path.Combine(_tempDir, "not_mp4.txt");
        File.WriteAllText(txtPath, "This is not an MP4 file");

        var result = await _parser.ReadMetadataAsync(txtPath);

        result.IsValid.Should().BeFalse("Ungültige MP4-Datei sollte IsValid=false zurückgeben");
        result.CreateDate.Should().BeNull();
    }

    [Fact]
    public async Task ReadMetadataAsync_NonExistentFile_ReturnsIsValidFalse()
    {

        var nonExistentPath = Path.Combine(_tempDir, "does_not_exist.mp4");

        var result = await _parser.ReadMetadataAsync(nonExistentPath);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReadMetadataAsync_CorruptedMvhd_ReturnsIsValidFalse()
    {

        var mp4Path = Path.Combine(_tempDir, "corrupted.mp4");

        using (var fs = File.Create(mp4Path))
        using (var writer = new BinaryWriter(fs))
        {

            WriteBox(writer, "ftyp", new byte[] { 0x69, 0x73, 0x6F, 0x6D });

            var moovData = new MemoryStream();
            using (var moovWriter = new BinaryWriter(moovData))
            {

                WriteBigEndianUInt32(moovWriter, 8);
                moovWriter.Write(System.Text.Encoding.ASCII.GetBytes("mvhd"));
            }

            WriteBox(writer, "moov", moovData.ToArray());
        }

        var result = await _parser.ReadMetadataAsync(mp4Path);

        result.IsValid.Should().BeFalse("Korrupte mvhd sollte IsValid=false zurückgeben");
    }

    [Fact]
    public async Task ReadMetadataAsync_CreateDateOutOfRange_ReturnsIsValidFalse()
    {

        var mp4Path = Path.Combine(_tempDir, "old_date.mp4");

        var mp4Epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var oldDate = new DateTime(1990, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        uint creation_time = (uint)(oldDate - mp4Epoch).TotalSeconds;

        CreateMinimalMp4WithMvhd(mp4Path, creation_time, 1000, 60000);

        var result = await _parser.ReadMetadataAsync(mp4Path);

        result.IsValid.Should().BeFalse("CreateDate < 2000 sollte IsValid=false setzen");
        result.CreateDate.Should().BeNull("CreateDate sollte null sein bei ungültigem Jahr");
    }

    /// <summary>
    /// Erstellt minimales MP4 mit mvhd Version 0 (32-bit Felder).
    /// </summary>
    private void CreateMinimalMp4WithMvhd(string filePath, uint creationTime, uint timescale, uint duration)
    {
        using var fs = File.Create(filePath);
        using var writer = new BinaryWriter(fs);

        WriteBox(writer, "ftyp", new byte[]
        {
            0x69, 0x73, 0x6F, 0x6D,
            0x00, 0x00, 0x02, 0x00,
            0x69, 0x73, 0x6F, 0x6D
        });

        var moovData = new MemoryStream();
        using (var moovWriter = new BinaryWriter(moovData))
        {

            var mvhdData = new MemoryStream();
            using (var mvhdWriter = new BinaryWriter(mvhdData))
            {
                mvhdWriter.Write((byte)0);
                mvhdWriter.Write(new byte[3]);

                WriteBigEndianUInt32(mvhdWriter, creationTime);
                WriteBigEndianUInt32(mvhdWriter, creationTime);
                WriteBigEndianUInt32(mvhdWriter, timescale);
                WriteBigEndianUInt32(mvhdWriter, duration);

                WriteBigEndianUInt32(mvhdWriter, 0x00010000);
                WriteBigEndianUInt16(mvhdWriter, 0x0100);
                mvhdWriter.Write(new byte[10]);
                mvhdWriter.Write(new byte[36]);
                mvhdWriter.Write(new byte[24]);
                WriteBigEndianUInt32(mvhdWriter, 1);
            }

            WriteBox(moovWriter, "mvhd", mvhdData.ToArray());
        }

        WriteBox(writer, "moov", moovData.ToArray());

        WriteBox(writer, "mdat", Array.Empty<byte>());
    }

    /// <summary>
    /// Erstellt minimales MP4 mit mvhd Version 1 (64-bit Felder).
    /// </summary>
    private void CreateMinimalMp4WithMvhdV1(string filePath, ulong creationTime, uint timescale, ulong duration)
    {
        using var fs = File.Create(filePath);
        using var writer = new BinaryWriter(fs);

        WriteBox(writer, "ftyp", new byte[] { 0x69, 0x73, 0x6F, 0x6D, 0, 0, 2, 0, 0x69, 0x73, 0x6F, 0x6D });

        var moovData = new MemoryStream();
        using (var moovWriter = new BinaryWriter(moovData))
        {
            var mvhdData = new MemoryStream();
            using (var mvhdWriter = new BinaryWriter(mvhdData))
            {
                mvhdWriter.Write((byte)1);
                mvhdWriter.Write(new byte[3]);

                WriteBigEndianUInt64(mvhdWriter, creationTime);
                WriteBigEndianUInt64(mvhdWriter, creationTime);
                WriteBigEndianUInt32(mvhdWriter, timescale);
                WriteBigEndianUInt64(mvhdWriter, duration);

                WriteBigEndianUInt32(mvhdWriter, 0x00010000);
                WriteBigEndianUInt16(mvhdWriter, 0x0100);
                mvhdWriter.Write(new byte[10]);
                mvhdWriter.Write(new byte[36]);
                mvhdWriter.Write(new byte[24]);
                WriteBigEndianUInt32(mvhdWriter, 1);
            }

            WriteBox(moovWriter, "mvhd", mvhdData.ToArray());
        }

        WriteBox(writer, "moov", moovData.ToArray());
        WriteBox(writer, "mdat", Array.Empty<byte>());
    }

    /// <summary>
    /// Schreibt MP4 Box mit Big Endian Size + Type + Data.
    /// </summary>
    private void WriteBox(BinaryWriter writer, string type, byte[] data)
    {
        uint size = (uint)(8 + data.Length);
        WriteBigEndianUInt32(writer, size);
        writer.Write(System.Text.Encoding.ASCII.GetBytes(type));
        writer.Write(data);
    }

    private void WriteBigEndianUInt32(BinaryWriter writer, uint value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    private void WriteBigEndianUInt64(BinaryWriter writer, ulong value)
    {
        writer.Write((byte)(value >> 56));
        writer.Write((byte)(value >> 48));
        writer.Write((byte)(value >> 40));
        writer.Write((byte)(value >> 32));
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    private void WriteBigEndianUInt16(BinaryWriter writer, ushort value)
    {
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }
}
