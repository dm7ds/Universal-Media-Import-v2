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

using System.Windows;
using System.Windows.Media;

namespace UMI.GUI.Helpers;

/// <summary>
/// SSOT for camera type → color mapping. Reads from Application.Current.Resources
/// at runtime so changes in Default.xaml are always reflected.
/// </summary>
public static class CameraTypeColors
{
    public static string GetHexColor(string? cameraType)
    {
        var resourceKey = cameraType switch
        {
            "Action"                => "ColorAction",
            "Drone"                 => "ColorDrone",
            "Mirrorless" or "DSLR"  => "ColorMirrorless",
            _                       => "ColorOther"
        };

        if (Application.Current?.Resources[resourceKey] is Color color)
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        return "#6b7280";
    }
}
