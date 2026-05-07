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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// An optional toggle option shown on an action card.
/// Subclasses of ProcessActionViewModel expose these via the Toggles property.
/// </summary>
public class ActionToggle : ViewModelBase
{
    /// <summary>Label displayed next to the toggle switch.</summary>
    public string Label { get; }

    /// <summary>Optional tooltip shown when hovering over the toggle row. Null = no tooltip.</summary>
    public string? Tooltip { get; }

    private bool _isChecked;
    /// <summary>Current toggle state.</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public ActionToggle(string label, bool defaultValue = false, string? tooltip = null)
    {
        Label      = label;
        _isChecked = defaultValue;
        Tooltip    = tooltip;
    }
}

/// <summary>
/// Abstract base for all Process tab action cards.
/// Each concrete action provides its own Title, Description, IconPathData,
/// and implements ExecuteRunAsync to do the actual work.
/// </summary>
public abstract class ProcessActionViewModel : ViewModelBase
{

    /// <summary>Display name shown on the action card.</summary>
    public abstract string Title { get; }

    /// <summary>Short description shown below the title.</summary>
    public abstract string Description { get; }

    /// <summary>
    /// WPF Path Data for the card icon. Use stroke-based geometry (24×24 viewbox).
    /// </summary>
    public abstract string IconPathData { get; }

    private bool _isRunning;
    /// <summary>True while ExecuteRunAsync is executing.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        protected set
        {
            if (SetProperty(ref _isRunning, value))
            {

                Application.Current?.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
            }
        }
    }

    private bool _isCancelling;
    /// <summary>True after CancelCommand is invoked and before the run finishes.</summary>
    public bool IsCancelling
    {
        get => _isCancelling;
        protected set
        {
            if (SetProperty(ref _isCancelling, value))
                Application.Current?.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    private double _progress;
    /// <summary>Progress in range [0, 1].</summary>
    public double Progress
    {
        get => _progress;
        protected set => SetProperty(ref _progress, Math.Clamp(value, 0.0, 1.0));
    }

    private string _progressText = string.Empty;
    /// <summary>Human-readable progress text, e.g. "3/12 videos".</summary>
    public string ProgressText
    {
        get => _progressText;
        protected set => SetProperty(ref _progressText, value);
    }

    private string _currentFile = string.Empty;
    /// <summary>Name of the file currently being processed.</summary>
    public string CurrentFile
    {
        get => _currentFile;
        protected set => SetProperty(ref _currentFile, value);
    }

    private bool _isStatusError;
    /// <summary>When true, StatusMessage should be styled as an error.</summary>
    public bool IsStatusError
    {
        get => _isStatusError;
        protected set => SetProperty(ref _isStatusError, value);
    }

    private bool _isRendering;
    /// <summary>True while Gyroflow is actively rendering a single video frame-by-frame.</summary>
    public bool IsRendering
    {
        get => _isRendering;
        protected set => SetProperty(ref _isRendering, value);
    }

    private double _renderProgress;
    /// <summary>Progress 0–1 for the current Gyroflow render.</summary>
    public double RenderProgress
    {
        get => _renderProgress;
        protected set => SetProperty(ref _renderProgress, Math.Clamp(value, 0.0, 1.0));
    }

    private string _renderProgressText = string.Empty;
    /// <summary>Human-readable render progress, e.g. "DJI_clip.mp4 — 57% ETA 9s".</summary>
    public string RenderProgressText
    {
        get => _renderProgressText;
        protected set => SetProperty(ref _renderProgressText, value);
    }

    /// <summary>
    /// Optional toggle options shown on the card.
    /// Override in subclass to add toggles. Empty by default (no toggles shown).
    /// </summary>
    public virtual IReadOnlyList<ActionToggle> Toggles => Array.Empty<ActionToggle>();

    /// <summary>True when this action card has at least one toggle option.</summary>
    public bool HasToggles => Toggles.Count > 0;

    /// <summary>
    /// True only for the StatisticsActionViewModel. Used by the XAML template
    /// to show the statistics table instead of the standard results list.
    /// </summary>
    public virtual bool IsStatisticsAction => false;

    private bool _isResultsVisible;
    /// <summary>True when the expandable results list is shown.</summary>
    public bool IsResultsVisible
    {
        get => _isResultsVisible;
        set => SetProperty(ref _isResultsVisible, value);
    }

    private bool _hasResults;
    /// <summary>True when there are result items to display.</summary>
    public bool HasResults
    {
        get => _hasResults;
        private set => SetProperty(ref _hasResults, value);
    }

    /// <summary>Per-file result items shown in the expandable list.</summary>
    public ObservableCollection<ActionResultItem> ResultItems { get; } = new();

    /// <summary>Toggles the results list visibility.</summary>
    public ICommand ToggleResultsCommand { get; }

    /// <summary>Starts the action. Disabled while IsRunning.</summary>
    public ICommand RunCommand { get; }

    /// <summary>Requests cancellation. Enabled only while IsRunning and not yet cancelling.</summary>
    public ICommand CancelCommand { get; }

    private CancellationTokenSource? _cts;

    protected ProcessActionViewModel()
    {
        RunCommand            = new RelayCommand(ExecuteRun,    () => !IsRunning);
        CancelCommand         = new RelayCommand(ExecuteCancel, () => IsRunning && !IsCancelling);
        ToggleResultsCommand  = new RelayCommand(() => IsResultsVisible = !IsResultsVisible);

        ResultItems.CollectionChanged += (_, _) => HasResults = ResultItems.Count > 0;
    }

    /// <summary>
    /// Implement the actual work here. Runs on a background thread via Task.Run.
    /// All property updates inside must be dispatched to the UI thread, or use
    /// the protected SetPropertyOnUiThread helper.
    /// </summary>
    protected abstract Task ExecuteRunAsync(CancellationToken ct);

    private async void ExecuteRun()
    {
        if (IsRunning) return;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        IsRunning        = true;
        IsCancelling     = false;
        Progress         = 0;
        ProgressText     = string.Empty;
        CurrentFile      = string.Empty;
        StatusMessage    = null;
        IsStatusError    = false;
        IsResultsVisible = false;
        ResultItems.Clear();

        try
        {
            await Task.Run(() => ExecuteRunAsync(_cts.Token), _cts.Token);
        }
        catch (OperationCanceledException)
        {
            SetOnUiThread(() =>
            {
                StatusMessage = Strings.Common_Cancelled;
                IsStatusError = false;
            });
        }
        catch (Exception ex)
        {
            SetOnUiThread(() =>
            {
                StatusMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
                IsStatusError = true;
            });
        }
        finally
        {
            SetOnUiThread(() =>
            {
                IsRunning    = false;
                IsCancelling = false;
            });

            _cts?.Dispose();
            _cts = null;
        }
    }

    private void ExecuteCancel()
    {
        if (!IsRunning || IsCancelling) return;
        IsCancelling = true;
        _cts?.Cancel();
    }

    /// <summary>
    /// Dispatches an action to the UI thread.
    /// Use this inside ExecuteRunAsync when updating bindable properties.
    /// </summary>
    protected void SetOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }
}
