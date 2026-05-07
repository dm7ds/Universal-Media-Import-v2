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
using System.Globalization;
using System.Windows;
using UMI.Core.Services;

namespace UMI.GUI.Views;

/// <summary>
/// Modal dialog for managing SD card and MTP device assignments for a camera.
/// Shows assigned devices, allows removal and manual addition of SD cards.
/// </summary>
public partial class CardManagementDialog : Window
{
    private readonly IConfigWriterService _configWriter;
    private readonly string _cameraId;
    private bool _hasChanges;

    /// <summary>Display model for an SD card assignment.</summary>
    public record SdCardDisplay(string Vsn, string Label);

    /// <summary>Display model for an MTP device assignment.</summary>
    public record MtpDeviceDisplay(string SerialNumber, string Label);

    public ObservableCollection<SdCardDisplay> SdCards { get; } = new();
    public ObservableCollection<MtpDeviceDisplay> MtpDevices { get; } = new();

    public CardManagementDialog(IConfigWriterService configWriter, string cameraId, string cameraName)
    {
        _configWriter = configWriter;
        _cameraId = cameraId;

        InitializeComponent();

        CameraNameText.Text = cameraName;
        LoadDevices();
    }

    private void LoadDevices()
    {
        SdCards.Clear();
        MtpDevices.Clear();

        var config = _configWriter.Config;

        foreach (var (vsn, reg) in config.SdCards)
        {
            if (string.Equals(reg.CameraId, _cameraId, StringComparison.OrdinalIgnoreCase))
            {
                SdCards.Add(new SdCardDisplay(vsn, reg.Label ?? string.Empty));
            }
        }

        foreach (var (serial, reg) in config.MtpDevices)
        {
            if (string.Equals(reg.CameraId, _cameraId, StringComparison.OrdinalIgnoreCase))
            {
                MtpDevices.Add(new MtpDeviceDisplay(serial, reg.Label ?? string.Empty));
            }
        }

        SdCardsList.ItemsSource = SdCards;
        MtpDevicesList.ItemsSource = MtpDevices;

        NoSdCardsText.Visibility = SdCards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoMtpDevicesText.Visibility = MtpDevices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RemoveSdCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string vsn)
        {
            _configWriter.UnregisterSdCard(vsn);
            await _configWriter.SaveAsync();
            _hasChanges = true;
            LoadDevices();
        }
    }

    private void AddSdCard_Click(object sender, RoutedEventArgs e)
    {
        AddSdCardPanel.Visibility = Visibility.Visible;
        AddSdCardButton.Visibility = Visibility.Collapsed;
        NewVsnTextBox.Text = string.Empty;
        NewLabelTextBox.Text = string.Empty;
        NewVsnTextBox.Focus();
    }

    private void CancelAddSdCard_Click(object sender, RoutedEventArgs e)
    {
        AddSdCardPanel.Visibility = Visibility.Collapsed;
        AddSdCardButton.Visibility = Visibility.Visible;
    }

    private async void ConfirmAddSdCard_Click(object sender, RoutedEventArgs e)
    {
        var vsn = NewVsnTextBox.Text.Trim().ToUpper(CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(vsn)) return;

        var label = NewLabelTextBox.Text.Trim();

        var registration = SdCardRegistrationHelper.Create(
            cameraId: _cameraId,
            label: string.IsNullOrEmpty(label) ? null : label);

        _configWriter.RegisterSdCard(vsn, registration);
        await _configWriter.SaveAsync();
        _hasChanges = true;

        AddSdCardPanel.Visibility = Visibility.Collapsed;
        AddSdCardButton.Visibility = Visibility.Visible;
        LoadDevices();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hasChanges)
        {
            DialogResult = true;
        }
        Close();
    }
}
