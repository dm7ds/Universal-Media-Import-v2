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
/// Converts a nullable int (sequence index) to a SolidColorBrush from the
/// Burst Visualizer V2 sequence palette (BrushVizSeq0 .. BrushVizSeq7).
///
/// null  -> BrushVizUnmatched (border token, dim)
/// n     -> BrushVizSeq{n % 8}  — cycles through 8 palette entries
///
/// All brush values come from Default.xaml theme tokens — no hex literals.
/// Brushes are cached once in the static constructor to avoid repeated resource lookups.
/// </summary>
[ValueConversion(typeof(int?), typeof(SolidColorBrush))]
public class SequenceIndexToBrushConverter : IValueConverter
{
    private const int PaletteSize = 8;
    private static readonly Brush?[] _seqBrushes = new Brush?[PaletteSize];
    private static readonly Brush? _unmatchedBrush;
    private static readonly Brush _fallbackBrush = new SolidColorBrush(Colors.Gray);

    static SequenceIndexToBrushConverter()
    {
        _fallbackBrush.Freeze();

        _unmatchedBrush = Application.Current?.Resources["BrushVizUnmatched"] as Brush;
        for (int i = 0; i < PaletteSize; i++)
            _seqBrushes[i] = Application.Current?.Resources[$"BrushVizSeq{i}"] as Brush;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int index)
        {

            return _unmatchedBrush ?? _fallbackBrush;
        }

        return _seqBrushes[index % PaletteSize] ?? _unmatchedBrush ?? _fallbackBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
