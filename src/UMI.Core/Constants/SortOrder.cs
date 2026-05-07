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

namespace UMI.Core.Constants;

/// <summary>
/// Sort-order string constants for the layout configuration.
/// Defines the folder segment ordering in the workbench structure.
/// </summary>
public static class SortOrder
{
    /// <summary>
    /// workbench/date/camera/mediatype/file — default ordering.
    /// </summary>
    public const string CameraFirst = "camera_first";

    /// <summary>
    /// workbench/date/mediatype/camera/file
    /// </summary>
    public const string TypeFirst = "type_first";
}
