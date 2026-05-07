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

using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace UMI.GUI.Converters;

/// <summary>
/// Converts a raw JPEG byte array (EXIF thumbnail) to a <see cref="BitmapSource"/>
/// suitable for WPF Image controls.
///
/// null / empty array → null (Image shows nothing)
/// byte[]             → BitmapImage loaded from MemoryStream, then Frozen.
///
/// IMPORTANT:
///   • CacheOption = OnLoad so the MemoryStream can be disposed immediately.
///   • DecodePixelWidth = 256 caps memory usage; WPF scales up as needed.
///   • Freeze() is MANDATORY — thumbnails are loaded on a background thread
///     and the BitmapImage must be freezable before crossing thread boundaries.
/// </summary>
[ValueConversion(typeof(byte[]), typeof(BitmapSource))]
public class BytesToBitmapConverter : IValueConverter
{
    /// <summary>
    /// SSOT for byte[]→BitmapSource decoding.
    /// Used by this converter, ReviewPhotoViewModel, and ThumbnailItemViewModel.
    /// </summary>
    /// <param name="data">Raw JPEG bytes. Returns null if null or empty.</param>
    /// <param name="decodePixelWidth">Caps decoded resolution to save memory. WPF scales up as needed.</param>
    public static BitmapSource? DecodeThumbnail(byte[]? data, int decodePixelWidth = 256)
    {
        if (data == null || data.Length == 0) return null;

        try
        {
            var image = new BitmapImage();
            using var ms = new MemoryStream(data);

            image.BeginInit();
            image.CacheOption     = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodePixelWidth;
            image.StreamSource    = ms;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            return null;
        }
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DecodeThumbnail(value as byte[]);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
