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
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Entry for the camera selection list in FloatingCardDialog.
/// </summary>
public record CameraSelectionEntry(
    string CameraId,
    string DisplayName,
    int UsageCount,
    DateTime? LastUsed,
    bool IsExifMatch)
{
    /// <summary>True when this entry has usage statistics worth displaying.</summary>
    public bool HasUsageStats => UsageCount > 0;

    /// <summary>Formatted last-used date for display, or empty string.</summary>
    public string LastUsedDisplay => LastUsed.HasValue
        ? LastUsed.Value.ToLocalTime().ToString("yyyy-MM-dd")
        : string.Empty;
};

/// <summary>
/// ViewModel for FloatingCardDialog.
/// Handles both Floating (known card, assignment=Floating) and Unknown (Tier 3) modes.
/// Instantiated per-call via new — no DI singleton.
/// </summary>
public sealed class FloatingCardDialogViewModel : ViewModelBase
{

    private readonly long _cardSizeBytes;

    /// <summary>True = Floating-Karte (bekannt, wechselt Kameras). False = Unknown (Tier 3, unbekannte Karte).</summary>
    public bool IsFloating { get; }

    public string CardLabel { get; }
    public string CardVsn { get; }

    /// <summary>Human-readable card size via FormatHelper.</summary>
    public string CardSizeDisplay { get; }

    public string? ExifModel { get; }

    public bool HasExifModel => !string.IsNullOrEmpty(ExifModel);

    public ObservableCollection<CameraSelectionEntry> Cameras { get; }

    private CameraSelectionEntry? _selectedCamera;
    public CameraSelectionEntry? SelectedCamera
    {
        get => _selectedCamera;
        set => SetProperty(ref _selectedCamera, value);
    }

    private bool _alwaysUseThisCamera;
    /// <summary>Only relevant in Floating mode: remember this selection permanently.</summary>
    public bool AlwaysUseThisCamera
    {
        get => _alwaysUseThisCamera;
        set => SetProperty(ref _alwaysUseThisCamera, value);
    }

    private bool _registerAsFloating;
    /// <summary>Only relevant in Unknown mode: when true, the new registration gets CameraId="" (Floating). Default: false (Fixed).</summary>
    public bool RegisterAsFloating
    {
        get => _registerAsFloating;
        set => SetProperty(ref _registerAsFloating, value);
    }

    public bool DialogResult { get; private set; }
    public string? ChosenCameraId { get; private set; }

    public ICommand ImportCommand { get; }
    public ICommand SkipCommand { get; }

    public event EventHandler? CloseRequested;

    /// <summary>
    /// Creates a FloatingCardDialogViewModel.
    /// </summary>
    /// <param name="isFloating">True = Floating mode (known card, Floating assignment). False = Unknown mode (Tier 3).</param>
    /// <param name="cardLabel">Volume label of the SD card.</param>
    /// <param name="cardVsn">Volume serial number.</param>
    /// <param name="cardSizeBytes">Card size in bytes (displayed via FormatHelper).</param>
    /// <param name="exifModel">EXIF camera model string (nullable).</param>
    /// <param name="registration">SD card registration (for Floating: has UsageHistory/LastUsedWith).</param>
    /// <param name="cameras">All configured cameras from config.</param>
    /// <param name="exifMatchedCameraId">EXIF-matched camera ID for Unknown mode pre-selection.</param>
    public FloatingCardDialogViewModel(
        bool isFloating,
        string cardLabel,
        string cardVsn,
        long cardSizeBytes,
        string? exifModel,
        SdCardRegistration? registration,
        Dictionary<string, CameraConfig> cameras,
        string? exifMatchedCameraId)
    {
        IsFloating = isFloating;
        CardLabel = cardLabel;
        CardVsn = cardVsn;
        _cardSizeBytes = cardSizeBytes;
        CardSizeDisplay = FormatHelper.FormatBytes(cardSizeBytes);
        ExifModel = exifModel;

        Cameras = BuildCameraList(isFloating, registration, cameras, exifMatchedCameraId);

        _selectedCamera = DeterminePreselection(isFloating, registration, cameras, exifMatchedCameraId);

        ImportCommand = new RelayCommand(ExecuteImport);
        SkipCommand = new RelayCommand(ExecuteSkip);
    }

    /// <summary>
    /// Builds the sorted camera list.
    /// Floating: sorted by usage count desc, then last used desc.
    /// Unknown: EXIF match first, then alphabetically.
    /// </summary>
    private static ObservableCollection<CameraSelectionEntry> BuildCameraList(
        bool isFloating,
        SdCardRegistration? registration,
        Dictionary<string, CameraConfig> cameras,
        string? exifMatchedCameraId)
    {
        IEnumerable<CameraSelectionEntry> entries;

        if (isFloating && registration != null)
        {

            entries = cameras
                .Select(kvp => new CameraSelectionEntry(
                    CameraId: kvp.Key,
                    DisplayName: kvp.Value.Name,
                    UsageCount: registration.UsageHistory.TryGetValue(kvp.Key, out var count) ? count : 0,
                    LastUsed: registration.LastUsedWith.TryGetValue(kvp.Key, out var dt) ? dt : null,
                    IsExifMatch: false))
                .OrderByDescending(e => e.UsageCount)
                .ThenByDescending(e => e.LastUsed ?? DateTime.MinValue);
        }
        else
        {

            entries = cameras
                .Select(kvp => new CameraSelectionEntry(
                    CameraId: kvp.Key,
                    DisplayName: kvp.Value.Name,
                    UsageCount: 0,
                    LastUsed: null,
                    IsExifMatch: kvp.Key == exifMatchedCameraId))
                .OrderByDescending(e => e.IsExifMatch)
                .ThenBy(e => e.DisplayName);
        }

        return new ObservableCollection<CameraSelectionEntry>(entries);
    }

    /// <summary>
    /// Determines the pre-selected entry.
    /// Floating: camera with highest usage count (first in list).
    /// Unknown: EXIF-matched camera if any, else first entry.
    /// </summary>
    private CameraSelectionEntry? DeterminePreselection(
        bool isFloating,
        SdCardRegistration? registration,
        Dictionary<string, CameraConfig> cameras,
        string? exifMatchedCameraId)
    {
        if (Cameras.Count == 0)
            return null;

        if (isFloating)
        {

            return Cameras[0];
        }
        else
        {

            if (exifMatchedCameraId != null)
                return Cameras.FirstOrDefault(e => e.IsExifMatch) ?? Cameras[0];

            return Cameras[0];
        }
    }

    private void ExecuteImport()
    {
        DialogResult = true;
        ChosenCameraId = SelectedCamera?.CameraId;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteSkip()
    {
        DialogResult = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

}
