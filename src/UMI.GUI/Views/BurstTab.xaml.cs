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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UMI.GUI.ViewModels;

namespace UMI.GUI.Views;

/// <summary>
/// Code-behind for BurstTab.xaml.
/// Contains only Drag-and-Drop logic for profile card reordering —
/// this is pure UI behaviour with no business logic (acceptable in code-behind per architecture guidelines).
///
/// DnD pattern:
///   - DragHandle_PreviewMouseLeftButtonDown: records drag source + start position
///   - DragHandle_PreviewMouseMove: starts DragDrop once vertical distance exceeds system threshold
///   - Card_DragOver: determines drop position (upper/lower half) and sets ViewModel indicator
///   - Card_DragLeave: clears all drop indicators
///   - Card_Drop: determines source/target indices, calls BurstTabViewModel.MoveProfile(), clears indicators
///
/// IMPORTANT — WPF Falle #10:
///   InputBindings (MouseBinding on the header grid) work independently from the Routed Event system.
///   DnD handlers are attached ONLY to the drag-handle element, NOT the entire header.
///   This ensures clicking the name area still triggers Expand/Collapse via InputBindings.
/// </summary>
public partial class BurstTab : UserControl
{

    private BurstProfileCardViewModel? _dragItem;
    private Point _dragStart;

    public BurstTab()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Called when the user presses the left mouse button on the drag handle.
    /// Records the drag source ViewModel and start position for threshold detection.
    /// </summary>
    private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        if (sender is FrameworkElement fe && fe.DataContext is BurstProfileCardViewModel vm)
            _dragItem = vm;
    }

    /// <summary>
    /// Called while the mouse moves over the drag handle with left button held.
    /// Initiates DragDrop.DoDragDrop once the vertical distance exceeds the system threshold.
    /// Clears _dragItem after initiating so a subsequent drop on the same card is a no-op.
    /// </summary>
    private void DragHandle_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem == null || e.LeftButton != MouseButtonState.Pressed) return;

        var diff = _dragStart - e.GetPosition(null);
        if (Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var dragSource = _dragItem;
            _dragItem = null;

            DragDrop.DoDragDrop((DependencyObject)sender, dragSource, DragDropEffects.Move);
        }
    }

    /// <summary>
    /// Clears IsDropTargetAbove and IsDropTargetBelow on all profile cards.
    /// Delegates to BurstTabViewModel.ClearAllDropIndicators().
    /// </summary>
    private void ClearAllDropIndicators()
    {
        if (DataContext is BurstTabViewModel tabVm)
            tabVm.ClearAllDropIndicators();
    }

    /// <summary>
    /// Called while a card is dragged over another card.
    /// Determines whether the drop position is in the upper or lower half
    /// and sets the corresponding indicator on the target card's ViewModel.
    /// </summary>
    private void Card_DragOver(object sender, DragEventArgs e)
    {
        ClearAllDropIndicators();

        if (sender is FrameworkElement fe && fe.DataContext is BurstProfileCardViewModel target)
        {
            var pos = e.GetPosition(fe);
            if (pos.Y < fe.ActualHeight / 2)
                target.IsDropTargetAbove = true;
            else
                target.IsDropTargetBelow = true;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    /// <summary>
    /// Called when the drag leaves a card without dropping.
    /// Clears all drop indicators.
    /// </summary>
    private void Card_DragLeave(object sender, DragEventArgs e)
    {
        ClearAllDropIndicators();
        e.Handled = true;
    }

    /// <summary>
    /// Called when the user drops a dragged card onto another card's Border.
    /// Resolves source/target indices from the tab ViewModel and calls MoveProfile.
    /// Clears all drop indicators after the drop.
    /// </summary>
    /// <summary>
    /// Toggles expand/collapse when clicking anywhere on the card header,
    /// EXCEPT on Buttons (edit/save/cancel/delete) or the drag handle.
    /// Uses MouseLeftButtonUp (routed event) instead of InputBindings to avoid
    /// WPF Falle #12 (InputBindings fire independently, ignoring e.Handled).
    /// </summary>
    private void CardHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source != sender)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
                return;
            if (source is FrameworkElement fe && fe.Cursor == Cursors.SizeAll)
                return;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        if (sender is FrameworkElement el && el.DataContext is BurstProfileCardViewModel vm)
        {
            vm.IsExpanded = !vm.IsExpanded;
            e.Handled = true;
        }
    }

    private void Card_Drop(object sender, DragEventArgs e)
    {
        ClearAllDropIndicators();

        if (e.Data.GetData(typeof(BurstProfileCardViewModel)) is BurstProfileCardViewModel source
            && sender is FrameworkElement fe
            && fe.DataContext is BurstProfileCardViewModel target
            && DataContext is BurstTabViewModel tabVm)
        {
            var fromIndex = tabVm.Profiles.IndexOf(source);
            var toIndex   = tabVm.Profiles.IndexOf(target);

            if (fromIndex >= 0 && toIndex >= 0 && fromIndex != toIndex)
                tabVm.MoveProfile(fromIndex, toIndex);
        }

        e.Handled = true;
    }
}
