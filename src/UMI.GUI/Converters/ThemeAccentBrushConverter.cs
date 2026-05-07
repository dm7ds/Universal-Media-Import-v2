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
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace UMI.GUI.Converters;

/// <summary>
/// Resolves a themed accent brush from a GroupAccentTag string (e.g. "Drone", "Mirrorless", "Action").
///
/// ConverterParameter controls which resource is resolved:
///   null / ""  → Brush{Tag}           (e.g. BrushDrone — full accent brush)
///   "Muted"    → Color{Tag}Muted      (e.g. ColorDroneMuted — muted background color, wrapped in SolidColorBrush)
///
/// Falls back to BrushTextMuted / ColorBgCard when the resource key is not found.
/// No hex literals — all values come from Default.xaml theme tokens.
/// </summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public class ThemeAccentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var tag = value as string;
        if (string.IsNullOrWhiteSpace(tag))
            return Fallback(parameter);

        var suffix = parameter as string ?? string.Empty;

        string resourceKey = suffix == "Muted"
            ? $"Color{tag}Muted"
            : $"Brush{tag}";

        var resources = Application.Current.Resources;

        if (suffix == "Muted")
        {

            if (resources.Contains(resourceKey) && resources[resourceKey] is Color color)
                return new SolidColorBrush(color);
        }
        else
        {

            if (resources.Contains(resourceKey))
                return resources[resourceKey];
        }

        return Fallback(parameter);
    }

    private static object Fallback(object? parameter)
    {
        var resources = Application.Current.Resources;
        var suffix = parameter as string ?? string.Empty;

        if (suffix == "Muted")
        {
            return resources.Contains("BrushBubbleInactive")
                ? resources["BrushBubbleInactive"]
                : new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44));
        }

        return resources.Contains("BrushTextMuted")
            ? resources["BrushTextMuted"]
            : new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
