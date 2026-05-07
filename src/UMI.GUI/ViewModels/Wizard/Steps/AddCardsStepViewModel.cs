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
using System.Windows.Threading;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard.Steps;

/// <summary>
/// Represents a card that has been assigned to the current camera.
/// Shown in the assigned-cards list in the AddCards step.
/// </summary>
public class CardAssignment : ViewModelBase
{
    /// <summary>Drive letter of the assigned card (e.g. "F:").</summary>
    public string DriveLetter { get; init; } = string.Empty;

    /// <summary>Volume label (e.g. "DJI_ACTION"). Null when not available.</summary>
    public string? VolumeLabel { get; init; }

    /// <summary>Volume serial number (e.g. "A4F2-8B31"). Null when read fails.</summary>
    public string? VolumeSerial { get; init; }

    /// <summary>Disk size in bytes.</summary>
    public long DiskSizeBytes { get; init; }

    /// <summary>Human-readable size (e.g. "32 GB").</summary>
    public string SizeInfo { get; init; } = string.Empty;

    /// <summary>Display string shown in the UI.</summary>
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(VolumeLabel)
            ? DriveLetter
            : $"{DriveLetter} — {VolumeLabel}";
}

/// <summary>
/// Step 4b — Register Additional SD Cards.
/// Appears after CameraConfirm. Lets the user assign additional SD cards to the camera they just configured.
/// Adding cards is optional — IsValid is always true (user can skip with Next).
///
/// DriveWatcher events come on WMI thread — always dispatch to UI thread!
/// </summary>
public class AddCardsStepViewModel : WizardStepViewModelBase, IDisposable
{
    private readonly WizardSession _session;
    private readonly IDriveWatcherService _driveWatcher;
    private readonly Dispatcher _dispatcher;

    public override string StepTitle => Strings.Wizard_AddCardsTitle;
    public override string StepDescription => Strings.Wizard_AddCardsDescription;

    private bool _driveEventsSubscribed;

    private string _cameraName = string.Empty;
    /// <summary>Display name of the camera being configured (from last session entry).</summary>
    public string CameraName
    {
        get => _cameraName;
        private set => SetProperty(ref _cameraName, value);
    }

    /// <summary>SD cards already assigned to this camera.</summary>
    public ObservableCollection<CardAssignment> AssignedCards { get; } = new();

    /// <summary>Drives currently connected but not yet assigned to this camera.</summary>
    public ObservableCollection<SourceItem> AvailableDrives { get; } = new();

    private bool _hasAvailableDrives;
    public bool HasAvailableDrives
    {
        get => _hasAvailableDrives;
        private set => SetProperty(ref _hasAvailableDrives, value);
    }

    private bool _hasAssignedCards;
    public bool HasAssignedCards
    {
        get => _hasAssignedCards;
        private set => SetProperty(ref _hasAssignedCards, value);
    }

    /// <summary>Assign an available drive to the current camera.</summary>
    public ICommand AddCardCommand { get; }

    /// <summary>Remove a previously assigned card from the current camera.</summary>
    public ICommand RemoveCardCommand { get; }

    public AddCardsStepViewModel(
        WizardSession session,
        IDriveWatcherService driveWatcher,
        Dispatcher? dispatcher = null)
    {
        _session = session;
        _driveWatcher = driveWatcher;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

        AddCardCommand    = new RelayCommand<SourceItem>(source => _ = ExecuteAddCardAsync(source));
        RemoveCardCommand = new RelayCommand<CardAssignment>(ExecuteRemoveCard);

        IsValid = true;
    }

    public override Task OnEnterAsync(CancellationToken ct = default)
    {

        var lastCamera = _session.Cameras.LastOrDefault();
        CameraName = lastCamera?.DisplayName ?? Strings.Wizard_CameraDefaultName;

        AssignedCards.Clear();
        if (lastCamera != null)
        {
            foreach (var src in lastCamera.Sources)
            {
                AssignedCards.Add(new CardAssignment
                {
                    DriveLetter   = src.DriveLetter,
                    VolumeLabel   = src.VolumeLabel,
                    VolumeSerial  = src.VolumeSerial,
                    DiskSizeBytes = src.DiskSizeBytes,
                    SizeInfo      = WizardFormatHelpers.FormatSize(src.DiskSizeBytes)
                });
            }
        }

        if (!_driveEventsSubscribed)
        {
            _driveWatcher.DriveArrived += OnDriveArrived;
            _driveEventsSubscribed = true;
        }

        if (!_driveWatcher.IsWatching)
            _driveWatcher.StartWatching();

        RefreshAvailableDrives();

        StatusMessage = Strings.Wizard_AddCardsInsertAnother;
        HasAssignedCards = AssignedCards.Count > 0;
        IsValid = true;
        return Task.CompletedTask;
    }

    public override Task OnLeaveAsync(CancellationToken ct = default)
    {
        UnsubscribeEvents();
        return Task.CompletedTask;
    }

    private void OnDriveArrived(object? sender, DriveChangedEventArgs e)
    {

        _dispatcher.Invoke(() =>
        {
            RefreshAvailableDrives();
            StatusMessage = string.Format(Strings.Wizard_AddCardsNewDetected, e.DriveLetter);
        });
    }

    private void RefreshAvailableDrives()
    {
        AvailableDrives.Clear();

        var currentDrives = _driveWatcher.GetCurrentDrives();
        var assignedLetters = AssignedCards.Select(c => c.DriveLetter).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in currentDrives)
        {
            if (assignedLetters.Contains(drive.DriveLetter))
                continue;

            var sizeInfo = WizardFormatHelpers.FormatSize(drive.TotalSizeBytes);
            var displayName = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                ? $"{drive.DriveLetter} ({sizeInfo})"
                : $"{drive.DriveLetter} — {drive.VolumeLabel} ({sizeInfo})";

            AvailableDrives.Add(new SourceItem
            {
                DriveLetter  = drive.DriveLetter,
                RootPath     = drive.RootPath,
                DisplayName  = displayName,
                DetectionSource = "none"
            });
        }

        HasAvailableDrives = AvailableDrives.Count > 0;
    }

    private async Task ExecuteAddCardAsync(SourceItem? source)
    {
        if (source == null) return;

        var cardInfo = await Task.Run(() => VolumeInfoReader.ReadSdCardInfo(source.DriveLetter));

        var assignment = new CardAssignment
        {
            DriveLetter   = source.DriveLetter,
            VolumeLabel   = cardInfo.VolumeLabel ?? source.DisplayName,
            VolumeSerial  = cardInfo.VolumeSerial,
            DiskSizeBytes = cardInfo.DiskSizeBytes,
            SizeInfo      = WizardFormatHelpers.FormatSize(cardInfo.DiskSizeBytes)
        };

        AssignedCards.Add(assignment);
        HasAssignedCards = true;

        var lastCamera = _session.Cameras.LastOrDefault();
        if (lastCamera != null)
        {
            lastCamera.Sources.Add(new SourceAssignment
            {
                DriveLetter   = source.DriveLetter,
                VolumeSerial  = cardInfo.VolumeSerial,
                VolumeLabel   = cardInfo.VolumeLabel,
                DiskSerial    = VolumeInfoReader.IsFakeDiskSerial(cardInfo.DiskSerial) ? null : cardInfo.DiskSerial,
                DiskSizeBytes = cardInfo.DiskSizeBytes,
                SourceType    = "sd"
            });
        }

        var toRemove = AvailableDrives.FirstOrDefault(d =>
            string.Equals(d.DriveLetter, source.DriveLetter, StringComparison.OrdinalIgnoreCase));
        if (toRemove != null)
            AvailableDrives.Remove(toRemove);

        HasAvailableDrives = AvailableDrives.Count > 0;
        StatusMessage = string.Format(Strings.Wizard_AddCardsAssigned, source.DriveLetter, CameraName);
    }

    private void ExecuteRemoveCard(CardAssignment? card)
    {
        if (card == null) return;

        AssignedCards.Remove(card);
        HasAssignedCards = AssignedCards.Count > 0;

        var lastCamera = _session.Cameras.LastOrDefault();
        if (lastCamera != null)
        {
            var src = lastCamera.Sources.FirstOrDefault(s =>
                string.Equals(s.DriveLetter, card.DriveLetter, StringComparison.OrdinalIgnoreCase));
            if (src != null)
                lastCamera.Sources.Remove(src);
        }

        RefreshAvailableDrives();
        StatusMessage = string.Format(Strings.Wizard_AddCardsUnassigned, card.DriveLetter);
    }

    private void UnsubscribeEvents()
    {
        if (_driveEventsSubscribed)
        {
            _driveWatcher.DriveArrived -= OnDriveArrived;
            _driveEventsSubscribed = false;
        }
    }

    public void Dispose()
    {
        UnsubscribeEvents();
        GC.SuppressFinalize(this);
    }
}
