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
using System.Windows.Media.Imaging;
using UMI.Core.Models;
using UMI.GUI.Converters;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for a single photo thumbnail in the BurstVisualizerV2 grid.
/// Exposes the photo's file info, EXIF data for AutoPresetGenerator, and
/// burst-assignment state (SequenceIndex, GapSeconds, IsOrphaned) that is
/// set by BurstVisualizerV2ViewModel.RebuildGridItems() after each evaluation.
/// </summary>
public class ThumbnailItemViewModel : ViewModelBase
{

    /// <summary>File name of the photo (without path).</summary>
    public string FileName { get; }

    /// <summary>Capture time read from EXIF.</summary>
    public DateTime? CaptureTime { get; }

    /// <summary>Raw JPEG thumbnail bytes from EXIF IFD1. May be null if not available.</summary>
    public byte[]? ThumbnailData { get; }

    /// <summary>
    /// Cached BitmapImage decoded from <see cref="ThumbnailData"/>.
    /// Lazy-initialized on first access, then reused for all subsequent bindings.
    /// Frozen for thread-safety.
    /// </summary>
    private BitmapSource? _thumbnailImage;
    public BitmapSource? ThumbnailImage => _thumbnailImage ??= BytesToBitmapConverter.DecodeThumbnail(ThumbnailData);

    /// <summary>
    /// Numeric EXIF values — forwarded to <see cref="AutoPresetGenerator.GenerateFromSelection"/>
    /// via the backing <see cref="VisualizerPhoto"/> when the user triggers preset generation.
    /// Exposed here so the ViewModel layer does not need to look up photos by file name.
    /// </summary>
    public Dictionary<string, double> ExifValues { get; }

    /// <summary>String EXIF values for display (FocusMode etc.).</summary>
    public Dictionary<string, string> ExifStringValues { get; }

    /// <summary>Capture time formatted as HH:mm:ss, or "—" if unknown.</summary>
    public string DisplayTime => CaptureTime?.ToString("HH:mm:ss") ?? "—";

    /// <summary>ISO value or "—".</summary>
    public string DisplayIso => ExifValues.TryGetValue("ISO", out var iso) ? $"ISO {iso:F0}" : "—";

    /// <summary>Exposure time: shows as fraction (1/250) or seconds (2.5s).</summary>
    public string DisplayExposure
    {
        get
        {
            if (!ExifValues.TryGetValue("ExposureTime", out var et)) return "—";
            return et >= 1.0 ? $"{et:G}s" : $"1/{1.0 / et:F0}";
        }
    }

    /// <summary>Aperture as f/X.X or "—".</summary>
    public string DisplayAperture => ExifValues.TryGetValue("Aperture", out var ap) ? $"f/{ap:F1}" : "—";

    /// <summary>Focus mode: "MF" or "AF" or "—".</summary>
    public string DisplayFocus
    {
        get
        {

            if (ExifStringValues.TryGetValue("FocusMode", out var fm))
            {
                if (fm.Contains("Manual", StringComparison.OrdinalIgnoreCase)) return "MF";
                if (fm.Contains("Auto", StringComparison.OrdinalIgnoreCase)) return "AF";
                return fm.Length > 4 ? fm[..4] : fm;
            }

            if (ExifValues.TryGetValue("FocusMode", out var fv))
                return fv == 1 ? "MF" : "AF";
            return "—";
        }
    }

    private int? _sequenceIndex;
    /// <summary>
    /// Index of the burst sequence this photo belongs to (0-based).
    /// Null means the photo is either unmatched or orphaned.
    /// </summary>
    public int? SequenceIndex
    {
        get => _sequenceIndex;
        set => SetProperty(ref _sequenceIndex, value);
    }

    private double? _gapSeconds;
    /// <summary>
    /// Time gap to the preceding photo in the same sequence (in seconds).
    /// Used by the grid for heatmap colouring. Null for the first photo of a sequence
    /// and for unmatched / orphaned photos.
    /// </summary>
    public double? GapSeconds
    {
        get => _gapSeconds;
        set => SetProperty(ref _gapSeconds, value);
    }

    private bool _isOrphaned;
    /// <summary>
    /// True when the photo's rule matched but its group was below MinCount.
    /// Displayed differently from fully unmatched photos in the grid.
    /// </summary>
    public bool IsOrphaned
    {
        get => _isOrphaned;
        set => SetProperty(ref _isOrphaned, value);
    }

    private bool _isSelected;
    /// <summary>
    /// True when the photo is selected in multi-select mode for preset generation.
    /// Toggled by <see cref="ClickCommand"/> (with Shift+Click range support via parent ViewModel).
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                OnSelectedChanged?.Invoke();
        }
    }

    /// <summary>
    /// Click handler that checks Shift state and delegates to the parent ViewModel
    /// for range selection support. Falls back to simple toggle if no parent is wired.
    /// </summary>
    public ICommand ClickCommand { get; }

    /// <summary>
    /// Called by the parent ViewModel to wire up click handling with Shift+Click range support.
    /// Parameters: (clickedItem, shiftHeld).
    /// </summary>
    internal Action<ThumbnailItemViewModel, bool>? OnClicked { get; set; }

    /// <summary>
    /// Invoked whenever <see cref="IsSelected"/> changes so the parent ViewModel
    /// can update its <c>SelectedCount</c> derived property without subscribing
    /// to PropertyChanged on every grid item individually.
    /// </summary>
    internal Action? OnSelectedChanged { get; set; }

    public ThumbnailItemViewModel(VisualizerPhoto photo)
    {
        FileName         = photo.FileName;
        CaptureTime      = photo.CaptureTime;
        ThumbnailData    = photo.ThumbnailData;
        ExifValues       = photo.ExifValues;
        ExifStringValues = photo.ExifStringValues;

        ClickCommand = new RelayCommand(() =>
        {
            bool shiftHeld = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
            if (OnClicked != null)
                OnClicked(this, shiftHeld);
            else
                IsSelected = !IsSelected;
        });
    }
}
