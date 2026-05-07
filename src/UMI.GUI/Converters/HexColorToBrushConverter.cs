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
using System.Windows.Data;
using System.Windows.Media;

namespace UMI.GUI.Converters;

/// <summary>
/// Converts a hex color string (e.g. "#f97316") to a SolidColorBrush.
/// Falls back to a muted gray when the value is null, empty, or not a valid color.
/// </summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public class HexColorToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush FallbackBrush =
        new(Color.FromRgb(0x6b, 0x72, 0x80));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {

            }
        }

        return FallbackBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a hex color string to a muted SolidColorBrush (very low alpha / darkened).
/// Used for badge backgrounds, separators, etc. where the full color would be too vivid.
/// The muted version is the full color blended with the dark card background at ~15% opacity.
/// </summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public class HexColorToMutedBrushConverter : IValueConverter
{

    private static readonly Color BackgroundColor = Color.FromRgb(0x31, 0x32, 0x44);
    private const double MutedAlpha = 0.18;

    private static readonly SolidColorBrush FallbackBrush =
        new(Color.FromRgb(0x1f, 0x22, 0x28));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var accentColor = (Color)ColorConverter.ConvertFromString(hex);

                var r = (byte)(BackgroundColor.R * (1 - MutedAlpha) + accentColor.R * MutedAlpha);
                var g = (byte)(BackgroundColor.G * (1 - MutedAlpha) + accentColor.G * MutedAlpha);
                var b = (byte)(BackgroundColor.B * (1 - MutedAlpha) + accentColor.B * MutedAlpha);

                var muted = new SolidColorBrush(Color.FromRgb(r, g, b));
                muted.Freeze();
                return muted;
            }
            catch
            {

            }
        }

        return FallbackBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
