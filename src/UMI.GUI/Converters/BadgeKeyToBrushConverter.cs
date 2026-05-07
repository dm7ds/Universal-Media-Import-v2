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
/// Resolves a badge foreground brush from a BadgeKey string.
/// Looks up "BrushBadge{BadgeKey}" in Application.Current.Resources.
///
/// Examples:
///   "Gps"   → BrushBadgeGps   (#22c55e)
///   "Gyro"  → BrushBadgeGyro  (#3b82f6)
///   "Burst" → BrushBadgeBurst (#f59e0b)
///   "Meta"  → BrushBadgeMeta  (#8b5cf6)
///   "Eis"   → BrushBadgeEis   (#ec4899)
///   "Lens"  → BrushBadgeLens  (#14b8a6)
///   "Post"  → BrushBadgePost  (#f97316)
///
/// Falls back to BrushTextMuted when the key is not found.
/// No hex literals — all values come from Default.xaml theme tokens.
/// </summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public class BadgeKeyToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string badgeKey && !string.IsNullOrWhiteSpace(badgeKey))
        {
            var resourceKey = $"BrushBadge{badgeKey}";
            if (Application.Current.Resources.Contains(resourceKey))
                return Application.Current.Resources[resourceKey];
        }

        return Application.Current.Resources.Contains("BrushTextMuted")
            ? Application.Current.Resources["BrushTextMuted"]
            : new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Resolves a badge background brush from a BadgeKey string.
/// Looks up "BrushBadge{BadgeKey}Bg" in Application.Current.Resources.
///
/// Falls back to BrushBubbleInactive when the key is not found.
/// No hex literals — all values come from Default.xaml theme tokens.
/// </summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public class BadgeKeyToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string badgeKey && !string.IsNullOrWhiteSpace(badgeKey))
        {
            var resourceKey = $"BrushBadge{badgeKey}Bg";
            if (Application.Current.Resources.Contains(resourceKey))
                return Application.Current.Resources[resourceKey];
        }

        return Application.Current.Resources.Contains("BrushBubbleInactive")
            ? Application.Current.Resources["BrushBubbleInactive"]
            : new SolidColorBrush(Colors.DarkSlateGray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
