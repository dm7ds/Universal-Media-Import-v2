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

using System.Buffers.Binary;

namespace UMI.Core.Utilities;

/// <summary>
/// Extrahiert eingebettete JPEG-Thumbnails und Previews direkt aus CR3-Dateien (ISOBMFF).
/// Kein ExifTool nötig — reine Binärextraktion, &lt;5ms pro Datei.
/// </summary>
/// <remarks>
/// CR3-Dateien sind ISOBMFF-Container (wie MP4). Canon bettet JPEGs in UUID-Boxen ein:
/// - THMB: Thumbnail (~160-320px) in Canon Metadata UUID
/// - PRVW: Preview (~1620px) in Canon Preview UUID
///
/// ISOBMFF Box-Header:
///   4 Bytes: Size (uint32 BE) — wenn 1: extended size (8 Bytes uint64 BE) folgt
///   4 Bytes: Type (ASCII, z.B. "moov", "THMB", "uuid")
///   UUID-Box: nach Type weitere 16 Bytes UUID, dann Child-Boxen
/// </remarks>
public static class Cr3JpegExtractor
{
    private const byte JpegSoi0 = 0xFF;
    private const byte JpegSoi1 = 0xD8;

    // Canon Metadata UUID: {85c0b687-820f-11e0-8111-f4ce462b6a48}
    // Identisch über alle Canon-Kameramodelle.
    private static readonly byte[] CanonMetadataUuid =
    [
        0x85, 0xc0, 0xb6, 0x87, 0x82, 0x0f, 0x11, 0xe0,
        0x81, 0x11, 0xf4, 0xce, 0x46, 0x2b, 0x6a, 0x48
    ];

    // Canon Preview UUID Prefix: Die ersten 6 Bytes sind über alle Canon-Modelle gleich,
    // die restlichen 10 Bytes variieren je nach Kamera (R5, R6, R10, M50, etc.).
    // Bekannte Varianten:
    //   R5/R6:  eaf42b5e-1c98-11e2-b4fa-ac54aab1e3f0
    //   R10:    eaf42b5e-1c98-4b88-b9fb-b7dc406e4d16
    private static readonly byte[] CanonPreviewUuidPrefix = [0xea, 0xf4, 0x2b, 0x5e, 0x1c, 0x98];

    /// <summary>
    /// Extrahiert den eingebetteten JPEG-Thumbnail aus einer CR3-Datei (THMB-Box, ~160-320px).
    /// Gibt null zurück wenn kein Thumbnail gefunden oder ein Fehler auftritt.
    /// </summary>
    public static byte[]? ExtractThumbnail(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bounds = FindNamedBoxDataBounds(stream, "THMB");
            return bounds.HasValue
                ? ExtractJpegFromBoxData(stream, bounds.Value.DataStart, bounds.Value.BoxEnd)
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extrahiert das eingebettete JPEG-Preview aus einer CR3-Datei (PRVW-Box, ~1620px).
    /// Gibt null zurück wenn kein Preview gefunden oder ein Fehler auftritt.
    /// </summary>
    public static byte[]? ExtractPreview(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bounds = FindNamedBoxDataBounds(stream, "PRVW");
            return bounds.HasValue
                ? ExtractJpegFromBoxData(stream, bounds.Value.DataStart, bounds.Value.BoxEnd)
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extrahiert HEIF-Preview-Daten aus CR3 PRVW-Box (für HDR PQ Modus).
    /// Sucht nach ISOBMFF/HEIF ftyp-Signatur statt JPEG SOI.
    /// Gibt null zurück wenn keine HEIF-Daten gefunden.
    /// </summary>
    public static byte[]? ExtractPreviewHeif(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bounds = FindNamedBoxDataBounds(stream, "PRVW");
            return bounds.HasValue
                ? ExtractHeifFromBoxData(stream, bounds.Value.DataStart, bounds.Value.BoxEnd)
                : null;
        }
        catch
        {
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigiert moov → Canon UUID-Boxen → targetBoxName und gibt den Datenbereich der
    /// gefundenen Box zurück (DataStart = nach Box-Header, BoxEnd = absolutes Ende).
    /// Wird sowohl von JPEG- als auch HEIF-Extraktion genutzt (DRY!).
    /// </summary>
    private readonly record struct BoxDataBounds(long DataStart, long BoxEnd);

    private static BoxDataBounds? FindNamedBoxDataBounds(FileStream stream, string targetBoxName)
    {
        var streamLength = stream.Length;

        // Top-Level UUID-Fallback: letzte matching UUID ohne Child-Box-Header
        BoxDataBounds? topLevelFallback = null;

        // Ebene 1: Top-Level-Boxen durchsuchen — wir brauchen "moov"
        long pos = 0;
        while (pos + 8 <= streamLength)
        {
            stream.Position = pos;
            if (!TryReadBoxHeader(stream, out var boxSize, out var boxType))
                break;

            if (boxType == "moov")
            {
                long moovDataStart = stream.Position; // nach dem Header
                long moovEnd = pos + boxSize;

                // Primär: In Canon UUID-Boxen suchen (Standard CR3 Layout)
                var result = SearchUuidBoxesForTarget(stream, moovDataStart, moovEnd, targetBoxName);
                if (result.HasValue)
                    return result;

                // Fallback: Direkt als moov-Child suchen (manche Canon-Modelle/Firmware)
                var directChild = SearchChildBoxBounds(stream, moovDataStart, moovEnd, targetBoxName);
                if (directChild.HasValue)
                    return directChild;
            }

            // Top-Level UUID-Box: Canon Preview UUID kann direkt auf Top-Level liegen (z.B. R10)
            if (boxType == "uuid")
            {
                long uuidDataStart = stream.Position;
                if (uuidDataStart + 16 <= streamLength)
                {
                    var uuidBytes = new byte[16];
                    stream.Read(uuidBytes, 0, 16);
                    long childStart = stream.Position;
                    long topBoxEnd = pos + boxSize;

                    if (UuidMatches(uuidBytes, CanonMetadataUuid) || IsCanonPreviewUuid(uuidBytes))
                    {
                        var bounds = SearchChildBoxBounds(stream, childStart, topBoxEnd, targetBoxName);
                        if (bounds.HasValue)
                            return bounds;
                    }

                    // Fallback NUR für CanonPreviewUuid — dort liegen PRVW-Daten direkt
                    // ohne Child-Box-Header. CanonMetadataUuid-Fallback würde THMB-JPEG liefern!
                    if (IsCanonPreviewUuid(uuidBytes) && childStart < topBoxEnd)
                        topLevelFallback = new BoxDataBounds(childStart, topBoxEnd);
                }
            }

            if (boxSize == 0) break; // "bis Dateiende" — nicht weiter iterieren
            pos += boxSize;
        }

        return topLevelFallback;
    }

    /// <summary>
    /// Durchsucht alle UUID-Boxen in einem Bereich nach einer Child-Box mit dem Zielnamen.
    /// Gibt den Datenbereich der gefundenen Child-Box zurück.
    /// </summary>
    private static BoxDataBounds? SearchUuidBoxesForTarget(
        FileStream stream, long searchStart, long searchEnd, string targetBoxName)
    {
        var streamLength = stream.Length;
        long pos = searchStart;

        while (pos + 8 <= searchEnd && pos + 8 <= streamLength)
        {
            stream.Position = pos;
            if (!TryReadBoxHeader(stream, out var boxSize, out var boxType))
                break;

            long boxEnd = pos + boxSize;

            if (boxType == "uuid")
            {
                // UUID-Box: 16 Bytes UUID nach dem Header lesen
                long uuidDataStart = stream.Position;
                if (uuidDataStart + 16 > boxEnd || uuidDataStart + 16 > streamLength)
                {
                    if (boxSize == 0) break;
                    pos += boxSize;
                    continue;
                }

                var uuidBytes = new byte[16];
                stream.Read(uuidBytes, 0, 16);
                long childStart = stream.Position;

                if (UuidMatches(uuidBytes, CanonMetadataUuid) || IsCanonPreviewUuid(uuidBytes))
                {
                    var bounds = SearchChildBoxBounds(stream, childStart, boxEnd, targetBoxName);
                    if (bounds.HasValue)
                        return bounds;

                    // KEIN Fallback hier! Bei R10 liegt CanonPreviewUuid auf Top-Level,
                    // nicht in moov. Ein Fallback auf CanonMetadataUuid-Daten würde das
                    // Thumbnail (THMB) statt das Preview (PRVW) liefern.
                    // Fallback passiert in FindNamedBoxDataBounds auf Top-Level-Ebene.
                }
            }

            if (boxSize == 0) break;
            pos += boxSize;
        }

        return null;
    }

    /// <summary>
    /// Sucht innerhalb eines Bereichs nach einer Child-Box mit dem Zielnamen und
    /// gibt deren Datenbereich (DataStart, BoxEnd) zurück.
    /// </summary>
    private static BoxDataBounds? SearchChildBoxBounds(
        FileStream stream, long searchStart, long searchEnd, string targetBoxName)
    {
        var streamLength = stream.Length;
        long pos = searchStart;

        while (pos + 8 <= searchEnd && pos + 8 <= streamLength)
        {
            stream.Position = pos;
            if (!TryReadBoxHeader(stream, out var boxSize, out var boxType))
                break;

            long boxDataStart = stream.Position;
            long boxEnd = pos + boxSize;

            if (boxType == targetBoxName)
            {
                return new BoxDataBounds(boxDataStart, boxEnd);
            }

            if (boxSize == 0) break;
            pos += boxSize;
        }

        return null;
    }

    /// <summary>
    /// Scannt Box-Daten nach HEIF/ISOBMFF ftyp-Signatur (0x66 0x74 0x79 0x70 = "ftyp").
    /// Die ftyp-Box beginnt 4 Bytes vor "ftyp" (mit dem Size-Feld).
    /// Gibt null zurück wenn keine HEIF-Daten in den ersten 256 Bytes gefunden.
    /// </summary>
    private static byte[]? ExtractHeifFromBoxData(FileStream stream, long dataStart, long boxEnd)
    {
        var streamLength = stream.Length;
        var scanEnd = Math.Min(boxEnd, streamLength);

        var scanBufLen = (int)Math.Min(256L, scanEnd - dataStart);
        if (scanBufLen < 8) return null;

        stream.Position = dataStart;
        var scanBuf = new byte[scanBufLen];
        var bytesRead = stream.Read(scanBuf, 0, scanBufLen);

        // Suche nach "ftyp" (0x66 0x74 0x79 0x70)
        for (int i = 0; i <= bytesRead - 4; i++)
        {
            if (scanBuf[i] == 0x66 && scanBuf[i + 1] == 0x74 &&
                scanBuf[i + 2] == 0x79 && scanBuf[i + 3] == 0x70)
            {
                // ftyp gefunden — gehe 4 Bytes zurück zum Size-Feld
                long heifStart = dataStart + i - 4;
                if (heifStart < dataStart) heifStart = dataStart;

                var heifLength = (int)(scanEnd - heifStart);
                if (heifLength < 12) return null;

                var heifData = new byte[heifLength];
                stream.Position = heifStart;
                var read = stream.Read(heifData, 0, heifLength);
                return read > 0 ? heifData[..read] : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Scannt Box-Daten ab dataStart nach JPEG SOI-Marker (FF D8).
    /// Liest dann bis zum EOI-Marker (FF D9) oder Box-Ende.
    /// Scannt maximal 256 Bytes Header vor dem ersten JPEG SOI.
    /// </summary>
    private static byte[]? ExtractJpegFromBoxData(FileStream stream, long dataStart, long boxEnd)
    {
        var streamLength = stream.Length;
        var scanEnd = Math.Min(boxEnd, streamLength);

        // Maximal 256 Bytes Header vor dem JPEG scannen
        var scanLimit = Math.Min(dataStart + 256, scanEnd - 1);

        stream.Position = dataStart;

        long jpegStart = -1;
        var prevByte = -1;

        for (long scanPos = dataStart; scanPos < scanLimit; scanPos++)
        {
            int b = stream.ReadByte();
            if (b < 0) break;

            if (prevByte == JpegSoi0 && b == JpegSoi1)
            {
                jpegStart = scanPos - 1;
                break;
            }

            prevByte = b;
        }

        if (jpegStart < 0)
            return null;

        // JPEG-Länge = von SOI bis Box-Ende (oder Dateiende)
        var jpegLength = (int)(scanEnd - jpegStart);
        if (jpegLength < 2)
            return null;

        var jpegData = new byte[jpegLength];
        stream.Position = jpegStart;
        var bytesRead = stream.Read(jpegData, 0, jpegLength);

        if (bytesRead < 2 || jpegData[0] != JpegSoi0 || jpegData[1] != JpegSoi1)
            return null;

        // KEIN EOI-Trimming! Ein naiver FF D9 Scan findet die EOI des EXIF-Thumbnails
        // innerhalb APP1 (raw binary, kein Byte-Stuffing), nicht die echte äußere EOI.
        // JPEG-Decoder ignorieren Daten nach der echten EOI — Box-Ende als Grenze reicht.
        return bytesRead == jpegLength ? jpegData : jpegData[..bytesRead];
    }

    /// <summary>
    /// Liest einen ISOBMFF Box-Header ab der aktuellen Stream-Position.
    /// Setzt die Stream-Position auf den Beginn der Box-Daten (nach dem Header).
    /// Gibt false zurück wenn nicht genug Bytes vorhanden sind.
    /// </summary>
    private static bool TryReadBoxHeader(FileStream stream, out long boxSize, out string boxType)
    {
        boxSize = 0;
        boxType = string.Empty;

        Span<byte> headerBuf = stackalloc byte[8];
        var read = stream.Read(headerBuf);
        if (read < 8)
            return false;

        var size32 = BinaryPrimitives.ReadUInt32BigEndian(headerBuf[..4]);
        boxType = System.Text.Encoding.ASCII.GetString(headerBuf[4..8]);

        if (size32 == 1)
        {
            // Extended size: 8 Bytes uint64 BE folgen nach dem Standard-Header
            Span<byte> extBuf = stackalloc byte[8];
            var extRead = stream.Read(extBuf);
            if (extRead < 8)
                return false;

            boxSize = (long)BinaryPrimitives.ReadUInt64BigEndian(extBuf);
        }
        else if (size32 == 0)
        {
            // Box reicht bis Dateiende
            boxSize = stream.Length - (stream.Position - 8);
        }
        else
        {
            boxSize = size32;
        }

        return boxSize >= 8;
    }

    /// <summary>
    /// Vergleicht zwei UUID-Byte-Arrays auf Gleichheit.
    /// </summary>
    private static bool UuidMatches(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a.SequenceEqual(b);

    /// <summary>
    /// Prüft ob eine UUID mit dem Canon Preview UUID-Prefix beginnt (erste 6 Bytes).
    /// Canon verwendet pro Kameramodell unterschiedliche Suffixe, aber der Prefix ist stabil.
    /// </summary>
    private static bool IsCanonPreviewUuid(ReadOnlySpan<byte> uuid)
        => uuid.Length >= 6 && uuid[..6].SequenceEqual(CanonPreviewUuidPrefix);

    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Diagnostik: Gibt die ISOBMFF-Box-Struktur einer CR3-Datei als formatierten String zurück.
    /// Maximal 3 Ebenen tief. Für UUID-Boxen wird die UUID als Hex ausgegeben.
    /// </summary>
    public static string DumpBoxStructure(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sb = new System.Text.StringBuilder();
            var streamLength = stream.Length;

            long pos = 0;
            while (pos + 8 <= streamLength)
            {
                stream.Position = pos;
                if (!TryReadBoxHeader(stream, out var boxSize, out var boxType))
                    break;

                long headerEnd = stream.Position;
                long boxEnd = pos + boxSize;

                // UUID: lese 16-Byte UUID-Payload
                string uuidSuffix = string.Empty;
                long childStart = headerEnd;
                if (boxType == "uuid" && headerEnd + 16 <= streamLength)
                {
                    var uuidBytes = new byte[16];
                    stream.Read(uuidBytes, 0, 16);
                    childStart = stream.Position;
                    var hex = Convert.ToHexString(uuidBytes).ToLowerInvariant();
                    string label = UuidMatches(uuidBytes, CanonMetadataUuid) ? " [CanonMetadata]"
                                 : IsCanonPreviewUuid(uuidBytes)  ? " [CanonPreview]"
                                 : string.Empty;
                    uuidSuffix = $" {hex}{label}";
                }

                sb.AppendLine($"[{pos}] {boxType}{uuidSuffix} ({boxSize} bytes)");

                // Level-2: Children von moov und uuid ausgeben
                bool isContainer = boxType == "moov" || boxType == "uuid";
                if (isContainer && childStart < boxEnd)
                {
                    long l2pos = childStart;
                    while (l2pos + 8 <= boxEnd && l2pos + 8 <= streamLength)
                    {
                        stream.Position = l2pos;
                        if (!TryReadBoxHeader(stream, out var l2Size, out var l2Type))
                            break;

                        long l2HeaderEnd = stream.Position;
                        long l2BoxEnd = l2pos + l2Size;

                        string l2UuidSuffix = string.Empty;
                        long l2ChildStart = l2HeaderEnd;
                        if (l2Type == "uuid" && l2HeaderEnd + 16 <= streamLength)
                        {
                            var uuidBytes2 = new byte[16];
                            stream.Read(uuidBytes2, 0, 16);
                            l2ChildStart = stream.Position;
                            var hex2 = Convert.ToHexString(uuidBytes2).ToLowerInvariant();
                            string label2 = UuidMatches(uuidBytes2, CanonMetadataUuid) ? " [CanonMetadata]"
                                          : IsCanonPreviewUuid(uuidBytes2)  ? " [CanonPreview]"
                                          : string.Empty;
                            l2UuidSuffix = $" {hex2}{label2}";
                        }

                        sb.AppendLine($"  [{l2pos}] {l2Type}{l2UuidSuffix} ({l2Size} bytes)");

                        // Level-3: Children von Level-2 uuid/moov ausgeben
                        bool l2IsContainer = l2Type == "moov" || l2Type == "uuid";
                        if (l2IsContainer && l2ChildStart < l2BoxEnd)
                        {
                            long l3pos = l2ChildStart;
                            while (l3pos + 8 <= l2BoxEnd && l3pos + 8 <= streamLength)
                            {
                                stream.Position = l3pos;
                                if (!TryReadBoxHeader(stream, out var l3Size, out var l3Type))
                                    break;

                                sb.AppendLine($"    [{l3pos}] {l3Type} ({l3Size} bytes)");

                                if (l3Size == 0) break;
                                l3pos += l3Size;
                            }
                        }

                        if (l2Size == 0) break;
                        l2pos += l2Size;
                    }
                }

                if (boxSize == 0) break;
                pos += boxSize;
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"DumpBoxStructure error: {ex.Message}";
        }
    }
}
