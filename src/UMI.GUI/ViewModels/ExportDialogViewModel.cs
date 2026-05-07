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

using System.Collections.ObjectModel;
using System.Windows.Input;
using UMI.Core.Models;
using UMI.GUI.Helpers;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Filter mode for the Export Dialog tag filter.
/// Controls which photos are included in the export based on their review tag.
/// </summary>
public enum ExportTagFilter { FavoritesOnly, UntaggedOnly, AllExclTrash }

/// <summary>
/// ViewModel for the Export Dialog.
/// Provides filter properties, live photo count preview, and export/cancel commands.
/// Receives all sequences from the parent SequenceReviewerViewModel as input so it
/// can calculate the live count across ALL sequences (not just the current one).
/// </summary>
public class ExportDialogViewModel : ViewModelBase
{
    private readonly IReadOnlyList<ReviewSequenceViewModel> _allSequences;

    /// <summary>Raised when the user confirms the export (Export button clicked).</summary>
    public event EventHandler? ExportConfirmed;

    /// <summary>Raised when the user cancels the dialog.</summary>
    public event EventHandler? CancelRequested;

    private bool _isCopy;
    /// <summary>True when the export action is Copy; false when Move.</summary>
    public bool IsCopy
    {
        get => _isCopy;
        set
        {
            if (SetProperty(ref _isCopy, value))
                OnPropertyChanged(nameof(IsMove));
        }
    }

    /// <summary>True when the export action is Move (inverse of <see cref="IsCopy"/>).</summary>
    public bool IsMove
    {
        get => !_isCopy;
        set => IsCopy = !value;
    }

    private bool _keepSequences = true;
    /// <summary>
    /// When true, a subfolder per sequence name is created under the date (and optional camera) folder.
    /// When false, photos are placed flat under date/[camera]/.
    /// </summary>
    public bool KeepSequences
    {
        get => _keepSequences;
        set
        {
            if (SetProperty(ref _keepSequences, value))
                RefreshPhotoCount();
        }
    }

    private ExportTagFilter _selectedTagFilter = ExportTagFilter.FavoritesOnly;
    /// <summary>Tag filter applied to select photos for export.</summary>
    public ExportTagFilter SelectedTagFilter
    {
        get => _selectedTagFilter;
        set
        {
            if (SetProperty(ref _selectedTagFilter, value))
                RefreshPhotoCount();
        }
    }

    private int _minRating;
    /// <summary>Minimum star rating (0–5). Photos below this threshold are excluded. 0 = all.</summary>
    public int MinRating
    {
        get => _minRating;
        set
        {
            if (SetProperty(ref _minRating, Math.Clamp(value, 0, 5)))
                RefreshPhotoCount();
        }
    }

    /// <summary>Available tag filter options for binding to ComboBox or RadioButtons.</summary>
    public static IReadOnlyList<ExportTagFilter> AvailableTagFilters { get; } =
        Enum.GetValues<ExportTagFilter>().ToList();

    private string _exportPath = string.Empty;
    /// <summary>Target folder path for the export. Must be non-empty to allow export.</summary>
    public string ExportPath
    {
        get => _exportPath;
        set
        {
            if (SetProperty(ref _exportPath, value))
                OnPropertyChanged(nameof(CanExport));
        }
    }

    private int _photoCount;
    /// <summary>
    /// Number of photos that will be exported given the current filter settings.
    /// Updated live whenever any filter property changes.
    /// </summary>
    public int PhotoCount
    {
        get => _photoCount;
        private set => SetProperty(ref _photoCount, value);
    }

    /// <summary>Preview label shown in the dialog: "{N} photos selected".</summary>
    public string PhotoCountPreview =>
        string.Format(Strings.SequenceReviewer_ExportPreview, PhotoCount);

    /// <summary>True when ExportPath is set and at least one photo is selected.</summary>
    public bool CanExport => !string.IsNullOrWhiteSpace(ExportPath) && PhotoCount > 0;

    /// <summary>Command that opens a folder browser to select the export target directory.</summary>
    public ICommand BrowseCommand { get; }

    /// <summary>Command that closes the dialog with a confirmed export result.</summary>
    public ICommand ExportCommand { get; }

    /// <summary>Command that cancels the dialog without exporting.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Initialises the ViewModel with all sequences from the Sequence Reviewer.
    /// </summary>
    /// <param name="allSequences">All sequences to apply filters against.</param>
    /// <param name="initialExportPath">Pre-fill the target path (from AppSettings.LastExportPath).</param>
    public ExportDialogViewModel(
        IReadOnlyList<ReviewSequenceViewModel> allSequences,
        string? initialExportPath = null)
    {
        _allSequences = allSequences;
        _exportPath   = initialExportPath ?? string.Empty;

        BrowseCommand = new RelayCommand(ExecuteBrowse);
        ExportCommand = new RelayCommand(ExecuteExport, () => CanExport);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));

        RefreshPhotoCount();
    }

    private void ExecuteBrowse()
    {
        var chosen = DialogHelper.BrowseFolder(
            Strings.SequenceReviewer_ExportTitle,
            string.IsNullOrWhiteSpace(ExportPath) ? null : ExportPath);

        if (chosen is not null)
            ExportPath = chosen;
    }

    private void ExecuteExport()
    {
        ExportConfirmed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Recalculates <see cref="PhotoCount"/> from the current filter settings
    /// across all sequences. Called whenever any filter property changes.
    /// </summary>
    private void RefreshPhotoCount()
    {
        var count = 0;
        foreach (var seq in _allSequences)
        {
            foreach (var photo in seq.Photos)
            {
                if (PassesTagFilter(photo) && PassesRatingFilter(photo))
                    count++;
            }
        }

        PhotoCount = count;
        OnPropertyChanged(nameof(PhotoCountPreview));
        OnPropertyChanged(nameof(CanExport));
    }

    private bool PassesTagFilter(ReviewPhotoViewModel photo) => SelectedTagFilter switch
    {
        ExportTagFilter.FavoritesOnly  => photo.Tag == ReviewTag.Favorite,
        ExportTagFilter.UntaggedOnly   => photo.Tag == ReviewTag.None,
        ExportTagFilter.AllExclTrash   => photo.Tag != ReviewTag.Trash,
        _                              => false
    };

    private bool PassesRatingFilter(ReviewPhotoViewModel photo) =>
        MinRating == 0 || photo.Rating >= MinRating;
}
