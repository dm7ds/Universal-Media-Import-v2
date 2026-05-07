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
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UMI.GUI.Behaviors;

/// <summary>
/// Attached Behavior that enables drag-and-drop reordering on any ItemsControl.
///
/// Usage:
///   beh:DragDropReorderBehavior.IsEnabled="True"
///   beh:DragDropReorderBehavior.DropCallback="{Binding ReorderCamerasCommand}"  (CameraGroupViewModel — per-group)
///
/// The DropCallback receives a (int OldIndex, int NewIndex) tuple as CommandParameter.
/// The behavior never mutates the ItemsSource — that is the ViewModel's responsibility.
///
/// Drag only starts from the grip handle (a child element with Tag="DragHandle").
/// Clicks on Buttons, ComboBoxes, CheckBoxes, and TextBoxes do not initiate a drag.
/// </summary>
public static class DragDropReorderBehavior
{

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DragDropReorderBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static readonly DependencyProperty DropCallbackProperty =
        DependencyProperty.RegisterAttached(
            "DropCallback",
            typeof(ICommand),
            typeof(DragDropReorderBehavior),
            new PropertyMetadata(null));

    public static ICommand? GetDropCallback(DependencyObject obj) => (ICommand?)obj.GetValue(DropCallbackProperty);
    public static void SetDropCallback(DependencyObject obj, ICommand? value) => obj.SetValue(DropCallbackProperty, value);

    private const string DragStateKey = "DragDropReorderBehavior.State";
    private const string DragHandleTag = "DragHandle";

    private sealed class DragState
    {
        public Point StartPoint { get; set; }
        public bool IsDragging { get; set; }
        public int DraggedIndex { get; set; } = -1;
        public InsertionAdorner? Adorner { get; set; }
        public GhostAdorner? Ghost { get; set; }
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl control) return;

        if ((bool)e.NewValue)
            Attach(control);
        else
            Detach(control);
    }

    private static void Attach(ItemsControl control)
    {
        var state = new DragState();
        control.Tag = state;
        control.AllowDrop = true;

        control.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        control.PreviewMouseMove           += OnPreviewMouseMove;
        control.PreviewMouseLeftButtonUp   += OnPreviewMouseLeftButtonUp;
        control.DragOver                   += OnDragOver;
        control.Drop                       += OnDrop;
        control.DragLeave                  += OnDragLeave;
    }

    private static void Detach(ItemsControl control)
    {
        control.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        control.PreviewMouseMove           -= OnPreviewMouseMove;
        control.PreviewMouseLeftButtonUp   -= OnPreviewMouseLeftButtonUp;
        control.DragOver                   -= OnDragOver;
        control.Drop                       -= OnDrop;
        control.DragLeave                  -= OnDragLeave;
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ItemsControl control) return;
        if (control.Tag is not DragState state) return;

        var src = e.OriginalSource as DependencyObject;

        if (IsNestedDragDropControl(src, control))
            return;

        if (!IsOverDragHandle(src))
            return;

        if (IsInteractiveControl(src))
            return;

        state.StartPoint = e.GetPosition(control);
        state.IsDragging = false;
        state.DraggedIndex = -1;
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ItemsControl control) return;
        if (control.Tag is not DragState state) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (state.IsDragging) return;
        if (state.StartPoint == default) return;

        if (IsNestedDragDropControl(e.OriginalSource as DependencyObject, control))
        {
            state.StartPoint = default;
            return;
        }

        var currentPos = e.GetPosition(control);
        var diff = currentPos - state.StartPoint;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var container = GetItemContainerAt(control, state.StartPoint);
        if (container == null) return;

        var index = control.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0) return;

        state.IsDragging = true;
        state.DraggedIndex = index;

        var adornerLayer = AdornerLayer.GetAdornerLayer(control);
        if (adornerLayer != null)
        {
            state.Ghost = new GhostAdorner(control, container);
            adornerLayer.Add(state.Ghost);
        }

        try
        {
            DragDrop.DoDragDrop(control, new DragReorderData(index, control), DragDropEffects.Move);
        }
        finally
        {

            if (state.Ghost != null && adornerLayer != null)
            {
                adornerLayer.Remove(state.Ghost);
                state.Ghost = null;
            }

            RemoveInsertionAdorner(control, state);
            state.IsDragging = false;
            state.StartPoint = default;
        }
    }

    private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ItemsControl control) return;
        if (control.Tag is not DragState state) return;

        state.StartPoint = default;
        state.IsDragging = false;
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl control) return;
        if (control.Tag is not DragState state) return;
        if (!e.Data.GetDataPresent(typeof(DragReorderData))
            || e.Data.GetData(typeof(DragReorderData)) is not DragReorderData data
            || data.Source != control)
        {

            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var pos = e.GetPosition(control);
        var (targetIndex, insertBefore) = GetInsertionIndex(control, pos);

        state.Ghost?.UpdatePosition(pos);

        UpdateInsertionAdorner(control, state, targetIndex, insertBefore);

        AutoScrollIfNeeded(control, e);
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl control) return;
        if (control.Tag is not DragState state) return;
        if (!e.Data.GetDataPresent(typeof(DragReorderData))) return;

        var data = (DragReorderData)e.Data.GetData(typeof(DragReorderData));
        if (data.Source != control) return;
        var fromIndex = data.Index;

        var pos = e.GetPosition(control);
        var (targetIndex, insertBefore) = GetInsertionIndex(control, pos);

        var toIndex = insertBefore ? targetIndex : targetIndex + 1;

        if (toIndex > fromIndex) toIndex--;

        RemoveInsertionAdorner(control, state);

        if (toIndex == fromIndex) return;

        var command = GetDropCallback(control);
        if (command?.CanExecute((fromIndex, toIndex)) == true)
            command.Execute((fromIndex, toIndex));

        e.Handled = true;
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl control) return;
        if (control.Tag is not DragState state) return;
        RemoveInsertionAdorner(control, state);
    }

    /// <summary>
    /// Returns true if the original source element or any ancestor up to the
    /// ItemsControl is a DragHandle element (Tag="DragHandle").
    /// </summary>
    private static bool IsOverDragHandle(DependencyObject? element)
    {
        var current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.Tag is string tag && tag == DragHandleTag)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    /// <summary>Returns true when the click originated on an interactive control that should not trigger drag.</summary>
    private static bool IsInteractiveControl(DependencyObject? element)
    {
        var current = element;
        while (current != null)
        {
            if (current is Button or ComboBox or CheckBox or TextBox or RadioButton or Slider or ToggleButton)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    /// <summary>
    /// Returns true when there is a nested ItemsControl with DnD enabled between the
    /// original source element and the current (outer) control.
    /// When true, the outer control should NOT claim the drag — the nested control is
    /// closer to the source and should handle it.
    /// </summary>
    private static bool IsNestedDragDropControl(DependencyObject? element, ItemsControl outerControl)
    {
        var current = element;
        while (current != null && current != outerControl)
        {
            if (current is ItemsControl ic && ic != outerControl && GetIsEnabled(ic))
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    /// <summary>
    /// Auto-scrolls the nearest parent ScrollViewer when the cursor is within
    /// <see cref="AutoScrollEdgePx"/> of the top or bottom edge during a drag.
    /// Scroll speed increases linearly as the cursor gets closer to the edge.
    /// </summary>
    private const double AutoScrollEdgePx = 40;
    private const double AutoScrollMaxSpeed = 20;

    private static void AutoScrollIfNeeded(ItemsControl control, DragEventArgs e)
    {
        var scrollViewer = FindAncestor<ScrollViewer>(control);
        if (scrollViewer == null) return;

        var pos = e.GetPosition(scrollViewer);
        var viewHeight = scrollViewer.ActualHeight;

        if (pos.Y < AutoScrollEdgePx)
        {

            var speed = AutoScrollMaxSpeed * (1.0 - pos.Y / AutoScrollEdgePx);
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - speed);
        }
        else if (pos.Y > viewHeight - AutoScrollEdgePx)
        {

            var speed = AutoScrollMaxSpeed * (1.0 - (viewHeight - pos.Y) / AutoScrollEdgePx);
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + speed);
        }
    }

    /// <summary>Finds the first ancestor of type T in the visual tree.</summary>
    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(element);
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static FrameworkElement? GetItemContainerAt(ItemsControl control, Point position)
    {
        var hit = control.InputHitTest(position) as DependencyObject;
        while (hit != null && hit != control)
        {

            if (control.ItemContainerGenerator.IndexFromContainer(hit) >= 0)
                return hit as FrameworkElement;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    /// <summary>
    /// Returns (targetContainerIndex, insertBefore).
    /// insertBefore=true → line above item[targetIndex],
    /// insertBefore=false → line below item[targetIndex].
    /// </summary>
    private static (int index, bool insertBefore) GetInsertionIndex(ItemsControl control, Point pos)
    {
        var itemCount = control.Items.Count;
        if (itemCount == 0) return (0, true);

        for (var i = 0; i < itemCount; i++)
        {
            var container = control.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
            if (container == null) continue;

            var itemPos = container.TranslatePoint(new Point(0, 0), control);
            var itemHeight = container.ActualHeight;
            var midY = itemPos.Y + itemHeight / 2;

            if (pos.Y <= midY)
                return (i, true);
        }

        return (itemCount - 1, false);
    }

    private static void UpdateInsertionAdorner(ItemsControl control, DragState state, int index, bool insertBefore)
    {
        var adornerLayer = AdornerLayer.GetAdornerLayer(control);
        if (adornerLayer == null) return;

        if (state.Adorner != null)
        {
            adornerLayer.Remove(state.Adorner);
            state.Adorner = null;
        }

        var itemCount = control.Items.Count;
        if (itemCount == 0) return;

        var clampedIndex = Math.Max(0, Math.Min(index, itemCount - 1));
        var container = control.ItemContainerGenerator.ContainerFromIndex(clampedIndex) as FrameworkElement;
        if (container == null) return;

        state.Adorner = new InsertionAdorner(control, container, insertBefore);
        adornerLayer.Add(state.Adorner);
    }

    private static void RemoveInsertionAdorner(ItemsControl control, DragState state)
    {
        if (state.Adorner == null) return;
        var adornerLayer = AdornerLayer.GetAdornerLayer(control);
        adornerLayer?.Remove(state.Adorner);
        state.Adorner = null;
    }

    /// <summary>
    /// Looks up a typed resource from Application.Current.Resources by key.
    /// Returns default(T) when the key is missing or the value is the wrong type.
    /// </summary>
    internal static T? TryGetResource<T>(string key)
    {
        try
        {
            if (Application.Current.Resources[key] is T val) return val;
        }
        catch {  }
        return default;
    }
}

/// <summary>Data object passed via WPF DragDrop containing the source index and originating control.</summary>
internal sealed class DragReorderData(int index, ItemsControl source)
{
    public int Index { get; } = index;
    /// <summary>The ItemsControl that initiated this drag. Used to reject drags from other controls.</summary>
    public ItemsControl Source { get; } = source;
}

/// <summary>
/// Draws a horizontal insertion line above or below a target item container.
/// Line height and color come from Default.xaml tokens (DragInsertionLineHeight / BrushDragInsertionLine).
/// </summary>
internal sealed class InsertionAdorner : Adorner
{
    private readonly ItemsControl _itemsControl;
    private readonly FrameworkElement _targetContainer;
    private readonly bool _insertBefore;
    private readonly Brush _lineBrush;
    private readonly double _lineHeight;

    public InsertionAdorner(ItemsControl itemsControl, FrameworkElement targetContainer, bool insertBefore)
        : base(itemsControl)
    {
        _itemsControl = itemsControl;
        _targetContainer = targetContainer;
        _insertBefore = insertBefore;
        IsHitTestVisible = false;

        _lineBrush = DragDropReorderBehavior.TryGetResource<Brush>("BrushDragInsertionLine")
                     ?? new SolidColorBrush(Color.FromRgb(0xf9, 0x73, 0x16));
        _lineHeight = DragDropReorderBehavior.TryGetResource<double?>("DragInsertionLineHeight") ?? 2d;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var containerPos = _targetContainer.TranslatePoint(new Point(0, 0), _itemsControl);
        var y = _insertBefore
            ? containerPos.Y
            : containerPos.Y + _targetContainer.ActualHeight;

        var pen = new Pen(_lineBrush, _lineHeight);
        pen.Freeze();

        var capRadius = _lineHeight * 2;
        dc.DrawEllipse(_lineBrush, null, new Point(capRadius, y), capRadius, capRadius);
        dc.DrawEllipse(_lineBrush, null, new Point(_itemsControl.ActualWidth - capRadius, y), capRadius, capRadius);
        dc.DrawLine(pen, new Point(capRadius * 2, y), new Point(_itemsControl.ActualWidth - capRadius * 2, y));
    }
}

/// <summary>
/// Renders a semi-transparent ghost of the dragged item that follows the cursor.
/// Opacity from Default.xaml token DragGhostOpacity.
/// </summary>
internal sealed class GhostAdorner : Adorner
{
    private readonly FrameworkElement _draggedContainer;
    private Point _currentPosition;
    private readonly double _opacity;

    public GhostAdorner(ItemsControl itemsControl, FrameworkElement draggedContainer)
        : base(itemsControl)
    {
        _draggedContainer = draggedContainer;
        IsHitTestVisible = false;

        _opacity = DragDropReorderBehavior.TryGetResource<double?>("DragGhostOpacity") ?? 0.6d;
        _currentPosition = draggedContainer.TranslatePoint(new Point(0, 0), itemsControl);
    }

    public void UpdatePosition(Point pos)
    {

        _currentPosition = new Point(pos.X + 8, pos.Y - _draggedContainer.ActualHeight / 2);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var brush = new VisualBrush(_draggedContainer)
        {
            Opacity = _opacity,
            Stretch = Stretch.None
        };

        dc.DrawRectangle(
            brush,
            null,
            new Rect(_currentPosition, new Size(_draggedContainer.ActualWidth, _draggedContainer.ActualHeight)));
    }
}
