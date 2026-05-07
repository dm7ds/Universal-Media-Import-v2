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
using System.Runtime.CompilerServices;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Base class for all ViewModels. Implements INotifyPropertyChanged
/// using the CallerMemberName pattern to avoid magic strings.
/// Also provides a shared <see cref="StatusMessage"/> property and
/// <see cref="ScheduleClearStatus"/> helper that auto-clears the message
/// after a configurable delay.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private CancellationTokenSource? _clearStatusCts;

    private string? _statusMessage;
    /// <summary>
    /// A transient status message shown in the UI.
    /// Set via <see cref="ScheduleClearStatus"/> to auto-clear after a delay.
    /// </summary>
    public virtual string? StatusMessage
    {
        get => _statusMessage;
        protected set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Sets <see cref="StatusMessage"/> to null after <paramref name="delayMs"/> milliseconds.
    /// Cancels any previously scheduled clear.
    /// </summary>
    protected void ScheduleClearStatus(int delayMs = 3000)
    {
        _clearStatusCts?.Cancel();
        _clearStatusCts?.Dispose();
        _clearStatusCts = new CancellationTokenSource();
        var token = _clearStatusCts.Token;
        _ = Task.Delay(delayMs, token).ContinueWith(
            _ => StatusMessage = null,
            token,
            TaskContinuationOptions.NotOnCanceled,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Cancels any pending auto-clear and disposes the token source.
    /// Call from Dispose() in derived classes that use <see cref="ScheduleClearStatus"/>.
    /// </summary>
    protected void CancelScheduledClear()
    {
        _clearStatusCts?.Cancel();
        _clearStatusCts?.Dispose();
        _clearStatusCts = null;
    }
}
