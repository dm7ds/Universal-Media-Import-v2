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
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using UMI.Core.Services;
using UMI.GUI.Services;
using UMI.GUI.ViewModels;

namespace UMI.GUI;

/// <summary>
/// Code-behind for MainWindow.
/// Receives the ViewModel from DI and assigns it to DataContext; additionally guards closing
/// while an import runs (I-028). All other logic lives in MainViewModel.
/// </summary>
public partial class MainWindow : Window
{
    private readonly CloseGuard _closeGuard;
    private readonly ILogger<MainWindow>? _logger;

    // Closing is cancelled while the question/cancel runs; the second Close() must pass through.
    private bool _closeApproved;
    private bool _closeRequestPending;

    // After the session-ending notification the close can no longer be vetoed (WPF ignores Cancel).
    private bool _sessionEnding;

    public MainWindow(MainViewModel viewModel, ILoggerFactory? loggerFactory = null)
    {
        InitializeComponent();
        DataContext = viewModel;

        _logger = loggerFactory?.CreateLogger<MainWindow>();
        _closeGuard = new CloseGuard(
            viewModel.ImportViewModel,
            new WpfCloseConfirmation(this),
            loggerFactory?.CreateLogger<CloseGuard>());
        // After "yes" the window waits up to 10 s for the import to stop: show that something is happening.
        _closeGuard.StoppingImports += (_, _) => Mouse.OverrideCursor = Cursors.Wait;

        Closing += MainWindow_Closing;
        if (Application.Current is { } app)
        {
            app.SessionEnding += Application_SessionEnding;
            Closed += (_, _) => app.SessionEnding -= Application_SessionEnding;
        }
    }

    /// <summary>
    /// X, Alt+F4 and the installer's WM_CLOSE end up here and can be vetoed: with a running import
    /// the close is cancelled, the user is asked, and only on "yes" are the imports cancelled and
    /// the window closed. The updater uses this same path (MainViewModel.CloseMainWindowForUpdate), so
    /// the one guard also covers an import that starts while the UAC prompt is open. Only the session
    /// end still shuts down without a veto: WPF raises Closing there but ignores Cancel, so it must not
    /// ask (see <see cref="_sessionEnding"/>). The remaining Application.Shutdown() calls (App.xaml.cs:
    /// startup failure, cancelled first-run wizard) happen before any import can exist.
    /// </summary>
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_closeApproved || _sessionEnding) return;

        if (_closeRequestPending)
        {
            e.Cancel = true;
            return;
        }

        if (!_closeGuard.ImportsRunning) return;

        e.Cancel = true;
        _closeRequestPending = true;

        // The question must be visible: a minimized owner would hide its dialog (installer-initiated close).
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();

        // Show the dialog after the WM_CLOSE handler has returned, not as a nested loop inside it.
        _ = Dispatcher.InvokeAsync(ConfirmAndCloseAsync);
    }

    private async Task ConfirmAndCloseAsync()
    {
        try
        {
            var decision = await _closeGuard.RequestCloseAsync();
            if (decision == CloseDecision.Declined) return;

            _closeApproved = true;
            Close();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Window stays open and the import keeps its state; the user can retry closing.
            _closeApproved = false;
            _logger?.LogError(ex, "Closing while an import runs failed — window stays open");
        }
        finally
        {
            Mouse.OverrideCursor = null;
            _closeRequestPending = false;
        }
    }

    private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        _sessionEnding = true;
        _closeGuard.StopImportsForSessionEnd();
    }

    /// <summary>
    /// Commits the workbench path binding when the user presses Enter and clears keyboard focus.
    /// </summary>
    private void WorkbenchPath_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var tb = (TextBox)sender;
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Keyboard.ClearFocus();
        }
    }

    /// <summary>
    /// Commits the workbench path binding when the TextBox loses focus.
    /// </summary>
    private void WorkbenchPath_LostFocus(object sender, RoutedEventArgs e)
    {
        var tb = (TextBox)sender;
        tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    // ── Window chrome button handlers ──────────────────────────────────────

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    /// <summary>
    /// Double-click on the title bar toggles maximize/restore.
    /// Border has no MouseDoubleClick event; ClickCount==2 on MouseLeftButtonDown is the correct WPF pattern.
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}
