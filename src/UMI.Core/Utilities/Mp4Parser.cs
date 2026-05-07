// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This file is part of UMI - Universal Media Import.
//
//     UMI - Universal Media Import is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     UMI - Universal Media Import is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with UMI - Universal Media Import.  If not, see <http://www.gnu.org/licenses/>.

using Microsoft.Extensions.Logging;
using System.Text;

namespace UMI.Core.Utilities;

/// <summary>
/// MP4-Parser für EIS-Erkennung (Electronic Image Stabilization) und native Metadata-Extraktion.
/// Erkennt kameraseitige Stabilisierung (DJI Rocksteady, GoPro HyperSmooth, etc.).
/// Liest mvhd für CreateDate/Duration.
/// </summary>
public class Mp4Parser : IMp4Parser
{
    private readonly ILogger<Mp4Parser>? _logger;

    public Mp4Parser(ILogger<Mp4Parser>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Erkennt Kamera-Modell aus MP4-Header.
    /// Durchsucht ftyp, udta und andere Metadaten-Bereiche.
    /// </summary>
    public async Task<CameraInfo> DetectCameraModelAsync(string videoPath, CancellationToken ct = default)
    {
        if (!File.Exists(videoPath))
        {
            return new CameraInfo { Model = "Unknown", Manufacturer = "Unknown" };
        }

        try
        {
            using var stream = File.OpenRead(videoPath);
            using var reader = new BigEndianBinaryReader(stream);

            var info = new CameraInfo();

            int headerSize = (int)Math.Min(stream.Length, 1024 * 1024);
            byte[] headerData = new byte[headerSize];
            stream.Position = 0;
            stream.Read(headerData, 0, headerSize);

            string headerText = Encoding.ASCII.GetString(
                headerData.Select(b => b >= 32 && b <= 126 ? b : (byte)32).ToArray());

            _logger?.LogDebug("Header-Preview (erste 500 Zeichen): {Preview}",
                headerText.Substring(0, Math.Min(500, headerText.Length)));

            info.Manufacturer = "DJI";

            var cameraPatterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {

                { "DJI OsmoAction5 Pro", "Osmo Action 5 Pro" },
                { "OsmoAction5 Pro", "Osmo Action 5 Pro" },
                { "OsmoAction5Pro", "Osmo Action 5 Pro" },
                { "Osmo Action 5 Pro", "Osmo Action 5 Pro" },
                { "OA5PRO", "Osmo Action 5 Pro" },
                { "FC3582", "Osmo Action 5 Pro" },
                { "FC3582A", "Osmo Action 5 Pro" },

                { "OsmoAction5", "Osmo Action 5" },
                { "FC3582C", "Osmo Action 5" },
                { "OA5", "Osmo Action 5" },

                { "OsmoAction4", "Osmo Action 4" },
                { "Osmo Action 4", "Osmo Action 4" },
                { "FC2403", "Osmo Action 4" },
                { "OA4", "Osmo Action 4" },

                { "OsmoAction3", "Osmo Action 3" },
                { "Osmo Action 3", "Osmo Action 3" },
                { "FC330", "Osmo Action 3" },
                { "OA3", "Osmo Action 3" },

                { "FC3411", "Osmo Action" },
                { "OSMO ACTION", "Osmo Action" },

                { "FC220", "Mavic Pro" },
                { "FC6310", "Mavic 2 Pro" },
                { "FC3170", "Mavic Air 2" },
                { "FC7303", "Mavic 3" }
            };

            var matches = new List<(string pattern, string model, int length)>();
            foreach (var pattern in cameraPatterns)
            {
                if (headerText.Contains(pattern.Key, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add((pattern.Key, pattern.Value, pattern.Key.Length));
                    _logger?.LogDebug("Pattern gefunden: '{Pattern}' -> {Model}",
                        pattern.Key, pattern.Value);
                }
            }

            if (matches.Count > 0)
            {
                var bestMatch = matches.OrderByDescending(m => m.length).First();
                info.Model = bestMatch.model;
                info.ModelCode = bestMatch.pattern;
                _logger?.LogDebug("Beste Match gewählt: '{Pattern}' -> {Model}",
                    bestMatch.pattern, bestMatch.model);
            }

            int djiIndex = headerText.IndexOf("DJI", StringComparison.OrdinalIgnoreCase);
            if (djiIndex > 0 && string.IsNullOrEmpty(info.Model))
            {

                int searchStart = Math.Max(0, djiIndex - 50);
                int searchEnd = Math.Min(headerText.Length, djiIndex + 100);
                string djiContext = headerText.Substring(searchStart, searchEnd - searchStart);

                var fcMatch = System.Text.RegularExpressions.Regex.Match(djiContext, @"FC\d{3,4}[A-Z]?");
                if (fcMatch.Success)
                {
                    string fcCode = fcMatch.Value;
                    if (cameraPatterns.TryGetValue(fcCode, out string? modelName))
                    {
                        info.Model = modelName;
                        info.ModelCode = fcCode;
                    }
                    else
                    {
                        info.Model = $"Unknown DJI Camera ({fcCode})";
                        info.ModelCode = fcCode;
                    }
                }
            }

            if (string.IsNullOrEmpty(info.Model))
            {
                info.Model = "Unknown DJI Camera";
            }

            _logger?.LogInformation("Kamera-Erkennung: {Manufacturer} {Model} ({Code})",
                info.Manufacturer, info.Model, info.ModelCode ?? "N/A");

            return info;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler bei Kamera-Erkennung: {Video}", videoPath);
            return new CameraInfo { Model = "Unknown", Manufacturer = "Unknown" };
        }
    }

    /// <summary>
    /// Liest CreateDate und Duration direkt aus MP4-Header (mvhd Box).
    /// Kein ExifTool nötig – pure C# Binary-Parsing.
    /// ~100x schneller als ExifTool-Aufruf.
    /// </summary>
    /// <param name="videoPath">Pfad zur MP4-Datei</param>
    /// <returns>Mp4Metadata mit CreateDate (UTC), Duration, Timescale. IsValid=false bei Fehler.</returns>
    public async Task<Mp4Metadata> ReadMetadataAsync(string videoPath, CancellationToken ct = default)
    {
        var result = new Mp4Metadata { IsValid = false };

        if (!File.Exists(videoPath))
        {
            _logger?.LogWarning("MP4-Datei nicht gefunden: {Path}", videoPath);
            return result;
        }

        try
        {
            using var stream = File.OpenRead(videoPath);
            var reader = new BigEndianBinaryReader(stream, leaveOpen: true);

            var moov = FindTopLevelBox(stream, "moov");
            if (moov == null)
            {
                _logger?.LogWarning("Keine 'moov' Box gefunden in {File}", Path.GetFileName(videoPath));
                return result;
            }

            var mvhd = FindChildBox(stream, moov.Value.DataStart, moov.Value.DataSize, "mvhd");
            if (mvhd == null)
            {
                _logger?.LogWarning("Keine 'mvhd' Box gefunden in {File}", Path.GetFileName(videoPath));
                return result;
            }

            stream.Position = mvhd.Value.DataStart;

            byte version = reader.ReadByte();
            byte[] flags = reader.ReadBytes(3);

            uint creationTime32 = 0;
            uint modificationTime32 = 0;
            uint timescale = 0;
            uint duration32 = 0;

            ulong creationTime64 = 0;
            ulong modificationTime64 = 0;
            ulong duration64 = 0;

            if (version == 0)
            {

                creationTime32 = reader.ReadUInt32();
                modificationTime32 = reader.ReadUInt32();
                timescale = reader.ReadUInt32();
                duration32 = reader.ReadUInt32();

                _logger?.LogDebug("mvhd v0: creation={Creation}, mod={Mod}, timescale={Timescale}, dur={Dur}",
                    creationTime32, modificationTime32, timescale, duration32);
            }
            else if (version == 1)
            {

                creationTime64 = reader.ReadUInt64();
                modificationTime64 = reader.ReadUInt64();
                timescale = reader.ReadUInt32();
                duration64 = reader.ReadUInt64();

                _logger?.LogDebug("mvhd v1: creation={Creation}, mod={Mod}, timescale={Timescale}, dur={Dur}",
                    creationTime64, modificationTime64, timescale, duration64);
            }
            else
            {
                _logger?.LogWarning("Unbekannte mvhd Version: {Version} in {File}", version, Path.GetFileName(videoPath));
                return result;
            }

            var mp4Epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            ulong creationTime = version == 0 ? creationTime32 : creationTime64;
            ulong modificationTime = version == 0 ? modificationTime32 : modificationTime64;
            ulong duration = version == 0 ? duration32 : duration64;

            if (creationTime > 0 && creationTime < 10_000_000_000)
            {
                result.CreateDate = mp4Epoch.AddSeconds(creationTime);
                result.ModifyDate = mp4Epoch.AddSeconds(modificationTime);

                if (result.CreateDate.Value.Year < 2000 || result.CreateDate.Value.Year > 2100)
                {
                    _logger?.LogWarning("Unplausibles CreateDate: {Date} (Jahr {Year}) in {File}",
                        result.CreateDate, result.CreateDate.Value.Year, Path.GetFileName(videoPath));
                    result.CreateDate = null;
                    result.ModifyDate = null;
                }
            }

            result.Timescale = timescale;

            if (timescale > 0 && duration > 0)
            {
                result.Duration = TimeSpan.FromSeconds((double)duration / timescale);

                if (result.Duration.TotalHours > 24)
                {
                    _logger?.LogWarning("Unplausible Duration: {Duration}h in {File}",
                        result.Duration.TotalHours, Path.GetFileName(videoPath));
                    result.Duration = TimeSpan.Zero;
                }
            }

            result.IsValid = result.CreateDate.HasValue && timescale > 0;

            if (result.IsValid)
            {
                _logger?.LogDebug("MP4 nativ: CreateDate={CreateDate} UTC, Duration={Duration:F1}s, Timescale={Timescale}",
                    result.CreateDate?.ToString("yyyy-MM-dd HH:mm:ss"), result.Duration.TotalSeconds, result.Timescale);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim MP4-Metadata-Parsing: {File}", Path.GetFileName(videoPath));
            return result;
        }
    }

    /// <summary>
    /// Erkennt kameraseitige elektronische Bildstabilisierung (EIS).
    /// Analysiert Gyro-Metadata-Track (DJI, GoPro, etc.) auf Nullen/Identity-Quaternions.
    /// </summary>
    public async Task<EisDetectionResult> DetectEisStatusAsync(string videoPath, CancellationToken ct = default)
    {
        if (!File.Exists(videoPath))
        {
            return new EisDetectionResult
            {
                Status = EisStatus.Unknown,
                Message = "Datei nicht gefunden"
            };
        }

        try
        {
            using var stream = File.OpenRead(videoPath);

            _logger?.LogDebug("Starte MP4-Analyse: {File} ({Size} bytes)",
                Path.GetFileName(videoPath), stream.Length);

            long trackOffset = FindDjmdTrackOffset(stream);

            if (trackOffset <= 0)
            {
                _logger?.LogWarning("Kein DJI-Metadaten-Track gefunden in {File}", Path.GetFileName(videoPath));
                return new EisDetectionResult
                {
                    Status = EisStatus.NoMetadataTrack,
                    Message = "Kein DJI-Metadaten-Track gefunden"
                };
            }

            _logger?.LogDebug("DJI Track gefunden bei Offset: {Offset:X}", trackOffset);

            const int sampleSizeSchemaThreshold = 500;
            const int eisSampleSizeThreshold = 950;

            int sampleSize = FindDjmdSampleSize(stream);
            _logger?.LogDebug("DJI Metadata Sample-Size (2. Frame): {Size} Bytes", sampleSize);

            EisStatus status;
            string message;

            if (sampleSize > sampleSizeSchemaThreshold)
            {

                bool eisActive = sampleSize >= eisSampleSizeThreshold;
                status = eisActive ? EisStatus.StabilizationOn : EisStatus.StabilizationOff;
                message = eisActive
                    ? $"Sample-Size {sampleSize} >= {eisSampleSizeThreshold} (EIS aktiv, Stabilisierungsdaten im Track)"
                    : $"Sample-Size {sampleSize} < {eisSampleSizeThreshold} (Raw Gyro Data, kein EIS)";
            }
            else
            {

                stream.Seek(trackOffset, SeekOrigin.Begin);
                byte[] buffer = new byte[256];
                int read = stream.Read(buffer, 0, buffer.Length);
                bool eisFlagFound = read >= 20 && IndexOfBytes(buffer, new byte[] { 0x08, 0x01, 0x10, 0x01 }) != -1;
                status = eisFlagFound ? EisStatus.StabilizationOn : EisStatus.StabilizationOff;
                message = eisFlagFound
                    ? $"Byte-Pattern '08 01 10 01' gefunden (EIS aktiv, Sample-Size={sampleSize})"
                    : $"Byte-Pattern NICHT gefunden (Raw Gyro Data, Sample-Size={sampleSize})";
            }

            _logger?.LogInformation("EIS-Erkennung: {Status} - {Message}",
                status, message);

            return new EisDetectionResult
            {
                Status = status,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim MP4-Parsing: {Video}", videoPath);
            return new EisDetectionResult
            {
                Status = EisStatus.Unknown,
                Message = $"Fehler: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Liest die Sample-Größe des 2. Frames im DJI-Metadata-Track aus der stsz-Box.
    /// Der 2. Frame ist repräsentativ für Frame-Daten (1. Frame = Header, oft größer).
    /// Returns: Sample-Size in Bytes, oder -1 wenn nicht ermittelbar.
    /// </summary>
    private int FindDjmdSampleSize(Stream stream)
    {
        var moov = FindTopLevelBox(stream, "moov");
        if (moov == null) return -1;

        stream.Seek(moov.Value.DataStart, SeekOrigin.Begin);
        byte[] moovData = new byte[(int)Math.Min(moov.Value.DataSize, 5 * 1024 * 1024)];
        stream.Read(moovData, 0, moovData.Length);

        string moovString = Encoding.ASCII.GetString(
            moovData.Select(b => b < 32 ? (byte)32 : b).ToArray());

        int hdlrIndex = -1;
        if (moovString.Contains("djmd"))
            hdlrIndex = moovString.IndexOf("djmd");
        else if (moovString.Contains("DJI meta"))
            hdlrIndex = moovString.IndexOf("DJI meta");

        if (hdlrIndex == -1) return -1;

        for (int i = hdlrIndex; i < moovData.Length - 20; i++)
        {
            if (moovData[i] == 's' && moovData[i + 1] == 't' &&
                moovData[i + 2] == 's' && moovData[i + 3] == 'z')
            {
                int uniformSize = (moovData[i + 8] << 24) | (moovData[i + 9] << 16) |
                                  (moovData[i + 10] << 8) | moovData[i + 11];
                if (uniformSize > 0) return uniformSize;

                int sampleCount = (moovData[i + 12] << 24) | (moovData[i + 13] << 16) |
                                  (moovData[i + 14] << 8) | moovData[i + 15];
                if (sampleCount < 2) return -1;

                int offset = i + 16 + 4;
                if (offset + 4 > moovData.Length) return -1;

                return (moovData[offset] << 24) | (moovData[offset + 1] << 16) |
                       (moovData[offset + 2] << 8) | moovData[offset + 3];
            }
        }

        return -1;
    }

    /// <summary>
    /// Findet DJI Metadata Track Offset (optimiert für OA5).
    /// Liest moov-Atom in Speicher und sucht nach Handler + Chunk-Offset.
    /// </summary>
    private long FindDjmdTrackOffset(Stream stream)
    {

        var moov = FindTopLevelBox(stream, "moov");
        if (moov == null) return -1;

        long moovStart = moov.Value.DataStart;
        long moovSize = moov.Value.DataSize;

        stream.Seek(moovStart, SeekOrigin.Begin);
        byte[] moovData = new byte[(int)Math.Min(moovSize, 5 * 1024 * 1024)];
        stream.Read(moovData, 0, moovData.Length);

        string moovString = Encoding.ASCII.GetString(
            moovData.Select(b => b < 32 ? (byte)32 : b).ToArray());

        int hdlrIndex = -1;
        if (moovString.Contains("djmd"))
            hdlrIndex = moovString.IndexOf("djmd");
        else if (moovString.Contains("DJI meta"))
            hdlrIndex = moovString.IndexOf("DJI meta");

        if (hdlrIndex == -1) return -1;

        for (int i = hdlrIndex; i < moovData.Length - 8; i++)
        {

            if (moovData[i] == 's' && moovData[i + 1] == 't' &&
                moovData[i + 2] == 'c' && moovData[i + 3] == 'o')
            {
                int firstOffsetPos = i + 12;
                if (firstOffsetPos + 4 > moovData.Length) continue;

                byte[] offsetBytes = new byte[4];
                Array.Copy(moovData, firstOffsetPos, offsetBytes, 0, 4);
                Array.Reverse(offsetBytes);
                return BitConverter.ToUInt32(offsetBytes, 0);
            }

            if (moovData[i] == 'c' && moovData[i + 1] == 'o' &&
                moovData[i + 2] == '6' && moovData[i + 3] == '4')
            {
                int firstOffsetPos = i + 12;
                if (firstOffsetPos + 8 > moovData.Length) continue;

                byte[] offsetBytes = new byte[8];
                Array.Copy(moovData, firstOffsetPos, offsetBytes, 0, 8);
                Array.Reverse(offsetBytes);
                return (long)BitConverter.ToUInt64(offsetBytes, 0);
            }
        }

        return -1;
    }

    /// <summary>
    /// Sucht Byte-Pattern in Byte-Array.
    /// </summary>
    private static int IndexOfBytes(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Findet eine Top-Level Box im MP4-File und gibt Start-Position + Größe zurück.
    /// DRY: Wiederverwendbar für moov, ftyp, mdat, etc.
    /// </summary>
    /// <param name="stream">Stream (Position wird verändert, bleibt offen)</param>
    /// <param name="boxType">4-Zeichen Box-Typ (z.B. "moov")</param>
    /// <returns>DataStart (nach Box-Header) und DataSize, oder null wenn nicht gefunden</returns>
    private (long DataStart, long DataSize)? FindTopLevelBox(Stream stream, string boxType)
    {
        var reader = new BigEndianBinaryReader(stream, leaveOpen: true);
        long fileSize = stream.Length;
        stream.Position = 0;

        while (stream.Position < fileSize)
        {
            long boxStart = stream.Position;
            if (boxStart + 8 > fileSize) break;

            uint size32 = reader.ReadUInt32();
            string type = Encoding.ASCII.GetString(reader.ReadBytes(4));

            long boxSize;

            if (size32 == 1)
            {
                if (boxStart + 16 > fileSize) break;
                boxSize = (long)reader.ReadUInt64();
            }

            else if (size32 == 0)
            {
                boxSize = fileSize - boxStart;
            }
            else
            {
                boxSize = size32;
            }

            _logger?.LogDebug("MP4 Box: '{Type}' bei Offset {Offset}, Size {Size} bytes",
                type, boxStart, boxSize);

            if (type == boxType)
            {
                long dataStart = stream.Position;
                long dataSize = boxSize - (dataStart - boxStart);

                _logger?.LogDebug("Target Box gefunden: {Type} @ {Start}, DataSize: {Size} bytes",
                    type, boxStart, dataSize);

                return (dataStart, dataSize);
            }

            if (boxSize < 8 || boxStart + boxSize > fileSize)
            {
                _logger?.LogWarning("Ungültige Box-Größe: {Type} @ {Offset}, Size {Size} (Datei: {FileSize})",
                    type, boxStart, boxSize, fileSize);
                break;
            }

            stream.Seek(boxStart + boxSize, SeekOrigin.Begin);
        }

        return null;
    }

    /// <summary>
    /// Sucht eine Child-Box innerhalb eines gegebenen Bereichs.
    /// DRY: Wiederverwendbar für mvhd, trak, udta, etc.
    /// </summary>
    /// <param name="stream">Stream (bleibt offen)</param>
    /// <param name="parentStart">Start der Parent-Box Data</param>
    /// <param name="parentSize">Größe der Parent-Box Data</param>
    /// <param name="boxType">Gesuchter 4-Zeichen Box-Typ</param>
    /// <returns>DataStart (nach Box-Header) und DataSize, oder null</returns>
    private (long DataStart, long DataSize)? FindChildBox(Stream stream, long parentStart, long parentSize, string boxType)
    {
        var reader = new BigEndianBinaryReader(stream, leaveOpen: true);
        long parentEnd = parentStart + parentSize;
        stream.Position = parentStart;

        while (stream.Position < parentEnd)
        {
            long boxStart = stream.Position;
            if (boxStart + 8 > parentEnd) break;

            uint size = reader.ReadUInt32();
            string type = Encoding.ASCII.GetString(reader.ReadBytes(4));

            if (size == 1 && boxStart + 16 <= parentEnd)
            {
                size = (uint)reader.ReadUInt64();
            }

            if (type == boxType)
            {
                long dataStart = stream.Position;
                long dataSize = size - (dataStart - boxStart);

                _logger?.LogDebug("Child Box gefunden: {Type} @ {Start} (innerhalb Parent), Size: {Size} bytes",
                    type, boxStart, dataSize);

                return (dataStart, dataSize);
            }

            if (size == 0 || size < 8) break;
            stream.Seek(boxStart + size, SeekOrigin.Begin);
        }

        return null;
    }

    /// <summary>
    /// BinaryReader mit Big Endian Support für MP4-Parsing.
    /// </summary>
    private class BigEndianBinaryReader : BinaryReader
    {
        public BigEndianBinaryReader(Stream stream, bool leaveOpen = false)
            : base(stream, System.Text.Encoding.UTF8, leaveOpen) { }

        public override uint ReadUInt32()
        {
            var data = base.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        public override ulong ReadUInt64()
        {
            var data = base.ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToUInt64(data, 0);
        }
    }
}

/// <summary>
/// Status der kameraseitigen elektronischen Bildstabilisierung (EIS).
/// </summary>
public enum EisStatus
{
    /// <summary>Stabilisierung ist aktiviert (z.B. DJI Rocksteady ON, GoPro HyperSmooth ON).</summary>
    StabilizationOn,

    /// <summary>Stabilisierung ist deaktiviert (z.B. DJI Rocksteady OFF, GoPro HyperSmooth OFF).</summary>
    StabilizationOff,

    /// <summary>Kein Gyro-Metadata-Track gefunden.</summary>
    NoMetadataTrack,

    /// <summary>Status konnte nicht bestimmt werden.</summary>
    Unknown
}

/// <summary>
/// Ergebnis der EIS-Erkennung.
/// </summary>
public class EisDetectionResult
{
    public EisStatus Status { get; set; }
    public double ZeroDensity { get; set; }
    public double QuaternionDensity { get; set; }
    public double Variance { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CameraInfo
{
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? ModelCode { get; set; }
    public string? FirmwareVersion { get; set; }
}

/// <summary>
/// Basis-Metadaten direkt aus MP4-Header gelesen (ohne ExifTool).
/// </summary>
public class Mp4Metadata
{
    /// <summary>CreateDate aus mvhd (immer UTC).</summary>
    public DateTime? CreateDate { get; set; }

    /// <summary>ModifyDate aus mvhd (immer UTC).</summary>
    public DateTime? ModifyDate { get; set; }

    /// <summary>Duration berechnet aus mvhd (duration / timescale).</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Timescale aus mvhd (z.B. 1000 für Millisekunden).</summary>
    public uint Timescale { get; set; }

    /// <summary>True wenn erfolgreich geparsed.</summary>
    public bool IsValid { get; set; }
}
