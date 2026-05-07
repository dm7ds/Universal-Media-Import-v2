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

using System.Drawing;
using System.Drawing.Imaging;
using LibHeifSharp;

// System.Drawing.Common ist Windows-only — UMI läuft ausschließlich auf Windows, daher OK.
#pragma warning disable CA1416

namespace UMI.Core.Utilities;

/// <summary>
/// Konvertiert HEIF-Bilddaten zu JPEG via LibHeifSharp.
/// Wird für CR3 HDR PQ Previews genutzt wo PRVW-Box HEIF statt JPEG enthält.
/// </summary>
public static class HeifJpegConverter
{
    /// <summary>
    /// Dekodiert HEIF-Bytes zu JPEG. Gibt null bei Fehler zurück.
    /// </summary>
    public static byte[]? ConvertToJpeg(byte[] heifData, int quality = 85)
    {
        try
        {
            using var context = new HeifContext(heifData);
            using var handle = context.GetPrimaryImageHandle();
            using var image = handle.Decode(HeifColorspace.Rgb, HeifChroma.InterleavedRgb24);

            var plane = image.GetPlane(HeifChannel.Interleaved);
            int width  = image.Width;
            int height = image.Height;
            int stride = plane.Stride;
            var srcRow = plane.Scan0;

            // Raw RGB → System.Drawing.Bitmap → JPEG
            using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                // LibHeifSharp liefert RGB, System.Drawing erwartet BGR → R/B tauschen
                unsafe
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* src = (byte*)(srcRow + y * stride);
                        byte* dst = (byte*)(bmpData.Scan0 + y * bmpData.Stride);
                        for (int x = 0; x < width; x++)
                        {
                            dst[x * 3 + 0] = src[x * 3 + 2]; // B ← R
                            dst[x * 3 + 1] = src[x * 3 + 1]; // G ← G
                            dst[x * 3 + 2] = src[x * 3 + 0]; // R ← B
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            // JPEG encode
            using var ms = new MemoryStream();
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
            var jpegCodec = ImageCodecInfo.GetImageEncoders()
                .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            bitmap.Save(ms, jpegCodec, encoderParams);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
