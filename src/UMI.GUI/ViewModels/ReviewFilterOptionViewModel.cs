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

using UMI.Core.Models;

namespace UMI.GUI.ViewModels;

/// <summary>
/// One segment of the Reviewer's "Anzeigen:" filter (I-013). Backs the
/// segmented-control ItemsControl in <c>SequenceReviewerWindow.xaml</c> —
/// exactly one class/template renders all three segments (All/Favorites/Trash),
/// no copy-pasted XAML blocks per segment (DRY, Zampano design decree).
/// </summary>
public class ReviewFilterOptionViewModel : ViewModelBase
{
    /// <summary>null = "All" segment (clears the filter); Favorite/Trash = tag filter.</summary>
    public ReviewTag? Tag { get; }

    /// <summary>
    /// Format string for the visible label. Segments with a Tag use a "{0}"
    /// placeholder for the live count (e.g. "Trash {0}"); the "All" segment
    /// has no placeholder and is shown verbatim.
    /// </summary>
    private readonly string _labelFormat;

    private int _count;
    /// <summary>Live count of photos carrying this segment's tag (0 for "All").</summary>
    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value))
            {
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(ShowDot));
            }
        }
    }

    /// <summary>Text shown in the segment, e.g. "Trash 12" or "All".</summary>
    public string DisplayText => Tag.HasValue ? string.Format(_labelFormat, Count) : _labelFormat;

    /// <summary>Status dot shown only for tagged segments once at least one photo carries the tag.</summary>
    public bool ShowDot => Tag.HasValue && Count > 0;

    /// <summary>Used by the dot-color DataTrigger — avoids binding a nullable enum directly (WPF trigger safety).</summary>
    public bool IsFavoriteSegment => Tag == ReviewTag.Favorite;

    private bool _isActive;
    /// <summary>True when this segment matches the ViewModel's current FilterTag.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public ReviewFilterOptionViewModel(ReviewTag? tag, string labelFormat)
    {
        Tag          = tag;
        _labelFormat = labelFormat;
    }
}
