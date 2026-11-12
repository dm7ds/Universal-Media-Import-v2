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

namespace UMI.GUI.Helpers;

/// <summary>
/// One row of the Sequence Reviewer's "?" shortcuts overlay (I-013): a key
/// glyph plus its description. Immutable, never changes after construction —
/// backs a single ItemsControl/DataTemplate in XAML so the seven rows are not
/// copy-pasted Grid blocks (DRY).
/// </summary>
public sealed class ShortcutOverlayEntry
{
    public string KeyText     { get; }
    public string Description { get; }

    public ShortcutOverlayEntry(string keyText, string description)
    {
        KeyText     = keyText;
        Description = description;
    }
}
