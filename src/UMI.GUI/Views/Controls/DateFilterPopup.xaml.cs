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

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace UMI.GUI.Views.Controls;

/// <summary>
/// DateFilterPopup UserControl.
/// Encapsulates the date-range filter Popup (shadow Border + quick-filter buttons +
/// From/To DatePickers + Clear button). The ToggleButton and the active-filter pill
/// remain in the consuming tab.
///
/// Usage:
///   1. Set DataContext to a DateFilterViewModel instance (directly or via Binding).
///   2. Set HeaderText to the context-specific label ("Import Date Range" etc.).
///   3. Set IsPopupOpen (TwoWay) to the DateFilterViewModel.IsPopupOpen property.
///   4. Set PlacementTargetRef to the ToggleButton the popup should appear below.
/// </summary>
public partial class DateFilterPopup : UserControl
{

    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(
            nameof(HeaderText),
            typeof(string),
            typeof(DateFilterPopup),
            new PropertyMetadata(string.Empty));

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public static readonly DependencyProperty IsPopupOpenProperty =
        DependencyProperty.Register(
            nameof(IsPopupOpen),
            typeof(bool),
            typeof(DateFilterPopup),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsPopupOpenChanged));

    public bool IsPopupOpen
    {
        get => (bool)GetValue(IsPopupOpenProperty);
        set => SetValue(IsPopupOpenProperty, value);
    }

    private static void OnIsPopupOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateFilterPopup ctrl)
            ctrl.SyncPopupOpen((bool)e.NewValue);
    }

    /// <summary>
    /// The UIElement the popup is anchored to (typically the ToggleButton in the tab).
    /// Set this to the x:Name-referenced ToggleButton from the consuming tab.
    /// </summary>
    public static readonly DependencyProperty PlacementTargetRefProperty =
        DependencyProperty.Register(
            nameof(PlacementTargetRef),
            typeof(UIElement),
            typeof(DateFilterPopup),
            new PropertyMetadata(null, OnPlacementTargetRefChanged));

    public UIElement? PlacementTargetRef
    {
        get => (UIElement?)GetValue(PlacementTargetRefProperty);
        set => SetValue(PlacementTargetRefProperty, value);
    }

    private static void OnPlacementTargetRefChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateFilterPopup ctrl)
            ctrl.ApplyPlacementTarget(e.NewValue as UIElement);
    }

    public DateFilterPopup()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private bool _calendarEventsWired;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {

        if (FindPopup() is Popup popup)
        {
            popup.PlacementTarget = PlacementTargetRef;
            popup.IsOpen = IsPopupOpen;

            popup.Closed += (_, _) => IsPopupOpen = false;

            popup.Opened += OnPopupOpened;
        }
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (_calendarEventsWired) return;
        if (FindPopup() is Popup popup)
        {
            WireDatePickerCalendarEvents(popup);
            _calendarEventsWired = true;
        }
    }

    private void WireDatePickerCalendarEvents(Popup popup)
    {
        if (popup.Child is not DependencyObject root)
            return;

        foreach (var dp in FindLogicalChildren<DatePicker>(root))
        {
            dp.CalendarOpened += (_, _) => popup.StaysOpen = true;
            dp.CalendarClosed += (_, _) => popup.StaysOpen = false;
        }
    }

    /// <summary>
    /// Uses LogicalTreeHelper instead of VisualTreeHelper because the logical tree
    /// is populated immediately, while the visual tree may not be ready on first
    /// Popup.Opened (especially for lazily-loaded tabs like ProcessTab).
    /// </summary>
    private static IEnumerable<T> FindLogicalChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match)
                yield return match;

            foreach (var descendant in FindLogicalChildren<T>(child))
                yield return descendant;
        }
    }

    private Popup? FindPopup() => Content as Popup;

    private void SyncPopupOpen(bool value)
    {
        if (FindPopup() is Popup popup && popup.IsOpen != value)
            popup.IsOpen = value;
    }

    private void ApplyPlacementTarget(UIElement? target)
    {
        if (FindPopup() is Popup popup)
            popup.PlacementTarget = target;
    }
}
