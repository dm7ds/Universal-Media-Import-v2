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

namespace UMI.GUI.ViewModels;

/// <summary>
/// Represents a burst sequence in the Sequence Reviewer.
/// Holds an ordered list of photos belonging to this sequence.
/// Inherits ViewModelBase so status properties can be refreshed via OnPropertyChanged.
/// </summary>
public class ReviewSequenceViewModel : ViewModelBase
{
    public string SequenceName { get; }
    public string? SourceProfileName { get; init; }
    /// <summary>Color palette index (0–7) assigned by the parent ViewModel based on the profile.</summary>
    public int? ProfileColorIndex { get; init; }
    public List<ReviewPhotoViewModel> Photos { get; }
    public int Count => Photos.Count;

    public ReviewPhotoViewModel? FirstPhoto => Photos.FirstOrDefault();

    /// <summary>
    /// When a filter is active, returns the first matching photo for the overview card thumbnail.
    /// Falls back to FirstPhoto when no filter predicate is set.
    /// </summary>
    private Func<ReviewPhotoViewModel, bool>? _filterPredicate;
    public ReviewPhotoViewModel? CoverPhoto =>
        _filterPredicate != null
            ? Photos.FirstOrDefault(_filterPredicate) ?? FirstPhoto
            : FirstPhoto;

    public string? CoverCaptureDate => CoverPhoto?.CaptureTime?.ToString("dd.MM.yyyy HH:mm");

    public void SetFilterPredicate(Func<ReviewPhotoViewModel, bool>? predicate)
    {
        _filterPredicate = predicate;
        OnPropertyChanged(nameof(CoverPhoto));
        OnPropertyChanged(nameof(CoverCaptureDate));
    }

    /// <summary>First photo's capture date formatted for display on overview cards.</summary>
    public string? FirstCaptureDate => FirstPhoto?.CaptureTime?.ToString("dd.MM.yyyy HH:mm");

    public bool HasRatedPhotos   => Photos.Any(p => p.HasRating || p.Tag != UMI.Core.Models.ReviewTag.None);
    public bool HasUnratedPhotos => !HasRatedPhotos;
    public int  RatedCount       => Photos.Count(p => p.HasRating || p.Tag != UMI.Core.Models.ReviewTag.None);

    public int FavoritesCount => Photos.Count(p => p.IsFavorite);
    public int TrashCount     => Photos.Count(p => p.IsTrash);
    public int StarredCount   => Photos.Count(p => p.HasRating);

    /// <summary>Localised "{n} photos" string for the Overview card.</summary>
    public string PhotoCountText => string.Format(UMI.GUI.Resources.Strings.SequenceReviewer_SeqPhotos, Count);

    /// <summary>
    /// True when this sequence is the currently selected sequence.
    /// Driven by SequenceReviewerViewModel.SelectSequence() so the Overview Grid
    /// can highlight the active card via a simple DataTrigger (WPF DataTrigger.Value
    /// does not accept Binding — IsSelected pattern is the correct workaround).
    /// </summary>
    private bool _isCurrentInOverview;
    public bool IsCurrentInOverview
    {
        get => _isCurrentInOverview;
        set => SetProperty(ref _isCurrentInOverview, value);
    }

    public ReviewSequenceViewModel(string sequenceName, List<ReviewPhotoViewModel> photos)
    {
        SequenceName = sequenceName;
        Photos       = photos;
    }

    /// <summary>
    /// Fires OnPropertyChanged for all computed status properties so the Overview
    /// Grid reflects any rating/tag changes made while in Single-Image mode.
    /// </summary>
    public void RefreshStatus()
    {
        OnPropertyChanged(nameof(FirstPhoto));
        OnPropertyChanged(nameof(HasRatedPhotos));
        OnPropertyChanged(nameof(HasUnratedPhotos));
        OnPropertyChanged(nameof(RatedCount));
        OnPropertyChanged(nameof(FavoritesCount));
        OnPropertyChanged(nameof(TrashCount));
        OnPropertyChanged(nameof(StarredCount));
        OnPropertyChanged(nameof(PhotoCountText));
    }
}
