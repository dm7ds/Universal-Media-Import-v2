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

using System.Windows.Input;

namespace UMI.GUI.Helpers;

/// <summary>
/// Single source of truth for the Sequence Reviewer's keyboard shortcuts (I-013).
///
/// <c>SequenceReviewerWindow</c>'s <c>Window.InputBindings</c> AND the
/// inline KeyCap badges next to the Favorite/Trash marker buttons both read
/// these constants via <c>x:Static</c>. Never hardcode the key literal a
/// second time in XAML — that is exactly the drift Zampano's design decree
/// warned about (display text separating from the actual binding on the next
/// shortcut change).
/// </summary>
public static class ReviewerShortcuts
{
    public const Key FavoriteKey = Key.Space;
    public const Key TrashKey    = Key.X;
    public const Key OverviewKey = Key.G;
    public const Key Rating1Key  = Key.D1;
    public const Key Rating5Key  = Key.D5;

    /// <summary>Short glyph shown in a KeyCap badge for a given physical key.</summary>
    public static string DisplayText(Key key) => key switch
    {
        Key.Space => "␣", // ␣ — open box, common "space" glyph
        Key.D1    => "1",
        Key.D2    => "2",
        Key.D3    => "3",
        Key.D4    => "4",
        Key.D5    => "5",
        _         => key.ToString()
    };

    /// <summary>Precomputed KeyCap text for the Favorite button — derived from <see cref="FavoriteKey"/>.</summary>
    public static readonly string FavoriteKeyText = DisplayText(FavoriteKey);

    /// <summary>Precomputed KeyCap text for the Trash button — derived from <see cref="TrashKey"/>.</summary>
    public static readonly string TrashKeyText = DisplayText(TrashKey);

    /// <summary>Precomputed KeyCap text for the Overview toggle — derived from <see cref="OverviewKey"/>.</summary>
    public static readonly string OverviewKeyText = DisplayText(OverviewKey);

    /// <summary>Precomputed "1–5" range text for the rating shortcuts, derived from the actual bound keys.</summary>
    public static readonly string RatingKeyRangeText = $"{DisplayText(Rating1Key)}–{DisplayText(Rating5Key)}";
}
