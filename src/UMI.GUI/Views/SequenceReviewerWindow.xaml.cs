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

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using UMI.Core.Models;
using UMI.GUI.Resources;
using UMI.GUI.ViewModels;

namespace UMI.GUI.Views;

/// <summary>
/// Sequence Reviewer window with resizable frame, filmstrip auto-scroll and drag-scroll.
/// Handles drag-move, filmstrip click-navigation, and filter shortcut buttons.
/// All business logic lives in <see cref="SequenceReviewerViewModel"/>.
/// </summary>
public partial class SequenceReviewerWindow : Window
{
    private readonly SequenceReviewerViewModel _vm;
    private readonly string _workbenchPath;

    private bool   _isDraggingFilmstrip;
    private Point  _filmstripDragStart;
    private double _filmstripScrollStart;

    public SequenceReviewerWindow(SequenceReviewerViewModel viewModel, string workbenchPath)
    {
        InitializeComponent();

        _vm             = viewModel;
        _workbenchPath  = workbenchPath;
        DataContext     = _vm;

        _vm.CloseRequested += (_, _) => Close();
        _vm.PropertyChanged += OnVmPropertyChanged;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateMaxRestoreIcon();

        if (!string.IsNullOrWhiteSpace(_workbenchPath))
            _ = _vm.LoadFolderAsync(_workbenchPath);

        PreviewKeyDown += (s, args) =>
        {
            if (args.Key == Key.F1)
            {
                HelpButton_Click(s, args);
                args.Handled = true;
                return;
            }

            // ComboBox eats arrow keys — redirect to ViewModel commands
            if (Keyboard.FocusedElement is DependencyObject focused
                && IsInsideComboBox(focused))
            {
                ICommand? cmd = args.Key switch
                {
                    Key.Left  => _vm.NavigateLeftCommand,
                    Key.Right => _vm.NavigateRightCommand,
                    Key.Up    => _vm.NavigateUpCommand,
                    Key.Down  => _vm.NavigateDownCommand,
                    _         => null
                };
                if (cmd is { } c && c.CanExecute(null))
                {
                    c.Execute(null);
                    Keyboard.Focus(this);
                    args.Handled = true;
                }
            }
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SequenceReviewerViewModel.CurrentPhotoIndex))
            ScrollFilmstripToCurrentPhoto();
    }

    private void ScrollFilmstripToCurrentPhoto()
    {
        var index = _vm.CurrentPhotoIndex;
        if (index < 0) return;

        var container = FilmstripItems.ItemContainerGenerator.ContainerFromIndex(index);
        if (container is FrameworkElement el)
        {
            el.BringIntoView();
            return;
        }

        FilmstripScroll.ScrollToHorizontalOffset(index * 86.0);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && WindowState == WindowState.Normal)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Window_StateChanged(object? sender, EventArgs e)
        => UpdateMaxRestoreIcon();

    /// <summary>
    /// Swaps the Maximize/Restore icon based on current <see cref="WindowState"/>.
    /// Maximized → show Restore icon; Normal → show Maximize icon.
    /// </summary>
    private void UpdateMaxRestoreIcon()
    {
        if (MaxRestoreIcon is null) return;

        var geo = WindowState == WindowState.Maximized
            ? (Geometry)FindResource("GeoRestore")
            : (Geometry)FindResource("GeoMaximize");

        MaxRestoreIcon.Data = geo;
    }

    private void FilmstripThumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ReviewPhotoViewModel photo)
        {
            _vm.CurrentPhoto = photo;
            _vm.CurrentPhotoIndex = _vm.FilmstripPhotos.IndexOf(photo);
        }
    }

    private void FilmstripScroll_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingFilmstrip = false;
        _filmstripDragStart  = e.GetPosition(FilmstripScroll);
        _filmstripScrollStart = FilmstripScroll.HorizontalOffset;
    }

    private void FilmstripScroll_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos  = e.GetPosition(FilmstripScroll);
        var diff = _filmstripDragStart.X - pos.X;

        if (!_isDraggingFilmstrip)
        {
            if (Math.Abs(diff) < 5.0) return;

            _isDraggingFilmstrip = true;
            FilmstripScroll.CaptureMouse();
        }

        FilmstripScroll.ScrollToHorizontalOffset(_filmstripScrollStart + diff);
        e.Handled = true;
    }

    private void FilmstripScroll_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingFilmstrip)
        {
            _isDraggingFilmstrip = false;
            FilmstripScroll.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void ComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => Keyboard.Focus(this)),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private static bool IsInsideComboBox(DependencyObject element)
    {
        for (var d = element; d != null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ComboBox) return true;
        }
        return false;
    }

    private void FilterFavorites_Click(object sender, RoutedEventArgs e)
    {
        _vm.FilterTag = ReviewTag.Favorite;
        _vm.ApplyFilterCommand.Execute(null);
    }

    private void FilterTrash_Click(object sender, RoutedEventArgs e)
    {
        _vm.FilterTag = ReviewTag.Trash;
        _vm.ApplyFilterCommand.Execute(null);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        string anchor = _vm.IsOverviewMode
            ? Strings.Help_AnchorReviewerOverview
            : Strings.Help_AnchorSequenceReviewer;

        var helpWindow = new HelpWindow(language);
        helpWindow.Owner = this;
        helpWindow.Show();
        helpWindow.ScrollToSection(anchor);
    }
}
