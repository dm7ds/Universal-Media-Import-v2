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

using System.IO;
using System.Windows.Media.Imaging;
using UMI.Core.Models;
using UMI.GUI.Converters;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Represents a single photo in the Sequence Reviewer.
/// Holds tag, rating, thumbnail, and selection state for filmstrip display.
/// </summary>
public class ReviewPhotoViewModel : ViewModelBase
{
    public string FileName { get; }
    public string FullPath { get; }

    private byte[]? _thumbnailData;
    public byte[]? ThumbnailData => _thumbnailData;

    /// <summary>
    /// UTC capture time from EXIF (QuickTime CreateDate is UTC without suffix).
    /// Null when not available.
    /// </summary>
    public DateTime? CaptureTime { get; }

    private BitmapSource? _thumbnail;
    /// <summary>Lazy-decoded thumbnail, frozen for cross-thread use.</summary>
    public BitmapSource? Thumbnail
    {
        get
        {
            if (_thumbnail is null && _thumbnailData is { Length: > 0 })
                _thumbnail = BytesToBitmapConverter.DecodeThumbnail(_thumbnailData);
            return _thumbnail;
        }
    }

    /// <summary>
    /// Updates the thumbnail data and forces re-decode on next access.
    /// Used by post-load to fill in thumbnails that failed during parallel scan.
    /// </summary>
    public void SetThumbnailData(byte[] data)
    {
        _thumbnailData = data;
        _thumbnail = null;
        OnPropertyChanged(nameof(ThumbnailData));
        OnPropertyChanged(nameof(Thumbnail));
    }

    private ReviewTag _tag;
    public ReviewTag Tag
    {
        get => _tag;
        set
        {
            if (SetProperty(ref _tag, value))
            {
                OnPropertyChanged(nameof(IsFavorite));
                OnPropertyChanged(nameof(IsTrash));
            }
        }
    }

    private int _rating;
    /// <summary>Star rating 0–5 (0 = unrated).</summary>
    public int Rating
    {
        get => _rating;
        set
        {
            if (SetProperty(ref _rating, Math.Clamp(value, 0, 5)))
            {
                OnPropertyChanged(nameof(HasRating));
                OnPropertyChanged(nameof(RatingStars));
            }
        }
    }

    private bool _isCurrent;
    /// <summary>True when this is the currently displayed photo in the reviewer.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    public bool IsFavorite => Tag == ReviewTag.Favorite;
    public bool IsTrash    => Tag == ReviewTag.Trash;
    public bool HasRating  => Rating > 0;

    /// <summary>
    /// Returns a list with <see cref="Rating"/> items so XAML ItemsControls can
    /// render one star-icon per element without needing a value converter.
    /// </summary>
    public IReadOnlyList<int> RatingStars => Rating > 0
        ? Enumerable.Range(1, Rating).ToList()
        : Array.Empty<int>();

    /// <summary>
    /// Camera name derived from the UMI relative path structure:
    /// {yyyy-MM-dd}/{CameraName}/{Tag?}/{file}
    /// Null when the relative path does not have enough segments.
    /// </summary>
    public string? CameraName { get; }

    public ReviewPhotoViewModel(string fullPath, byte[]? thumbnailData, DateTime? captureTime = null, string? cameraName = null)
    {
        FullPath       = fullPath;
        FileName       = Path.GetFileName(fullPath);
        _thumbnailData = thumbnailData;
        CaptureTime    = captureTime;
        CameraName     = cameraName;
    }

    /// <summary>
    /// Extracts the camera name from a UMI relative path.
    /// Structure: {yyyy-MM-dd}/{CameraName}/{Tag?}/{file}
    /// Camera is always the 2nd segment (index 1) when at least 3 segments exist.
    /// </summary>
    public static string? ExtractCameraName(string? relativePath)
    {
        if (relativePath == null) return null;
        var parts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? parts[1] : null;
    }

}
