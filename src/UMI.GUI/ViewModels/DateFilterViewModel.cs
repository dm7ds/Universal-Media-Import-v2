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

using System.Windows.Input;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// Reusable date range filter. Used by Import and Process tabs.
/// Session-only (not persisted to config).
/// </summary>
public class DateFilterViewModel : ViewModelBase
{
    private bool _isPopupOpen;
    public bool IsPopupOpen
    {
        get => _isPopupOpen;
        set => SetProperty(ref _isPopupOpen, value);
    }

    private DateTime? _dateFrom;
    public DateTime? DateFrom
    {
        get => _dateFrom;
        set
        {
            if (SetProperty(ref _dateFrom, value))
            {
                OnPropertyChanged(nameof(HasDateFilter));
                OnPropertyChanged(nameof(DateFilterText));
                OnPropertyChanged(nameof(DateFromTime));
            }
        }
    }

    private DateTime? _dateTo;
    public DateTime? DateTo
    {
        get => _dateTo;
        set
        {
            if (value.HasValue && value.Value.TimeOfDay == TimeSpan.Zero)
                value = value.Value.Date.AddDays(1).AddSeconds(-1);

            if (SetProperty(ref _dateTo, value))
            {
                OnPropertyChanged(nameof(HasDateFilter));
                OnPropertyChanged(nameof(DateFilterText));
                OnPropertyChanged(nameof(DateToTime));
            }
        }
    }

    public bool HasDateFilter => DateFrom.HasValue || DateTo.HasValue;

    public string DateFilterText
    {
        get
        {
            if (DateFrom.HasValue && DateTo.HasValue)
                return string.Format(Strings.DateFilter_RangeFormat,
                    DateFrom.Value.ToString("g"),
                    DateTo.Value.ToString("g"));
            if (DateFrom.HasValue)
                return string.Format(Strings.DateFilter_FromFormat, DateFrom.Value.ToString("g"));
            if (DateTo.HasValue)
                return string.Format(Strings.DateFilter_ToFormat, DateTo.Value.ToString("g"));
            return string.Empty;
        }
    }

    public string DateFromTime
    {
        get => DateFrom?.ToString("HH:mm") ?? "00:00";
        set
        {
            if (TimeSpan.TryParse(value, out var time))
                DateFrom = (DateFrom?.Date ?? DateTime.Today) + time;
        }
    }

    public string DateToTime
    {
        get => DateTo?.ToString("HH:mm") ?? "23:59";
        set
        {
            if (TimeSpan.TryParse(value, out var time))
                DateTo = (DateTo?.Date ?? DateTime.Today) + time;
        }
    }

    public ICommand ClearDateFilterCommand { get; }
    public ICommand SetTodayFilterCommand { get; }
    public ICommand SetYesterdayFilterCommand { get; }

    public DateFilterViewModel()
    {
        ClearDateFilterCommand = new RelayCommand(() =>
        {
            DateFrom = null;
            DateTo = null;
        });

        SetTodayFilterCommand = new RelayCommand(() =>
        {
            DateFrom = DateTime.Today;
            DateTo = DateTime.Today.AddDays(1).AddSeconds(-1);
        });

        SetYesterdayFilterCommand = new RelayCommand(() =>
        {
            DateFrom = DateTime.Today.AddDays(-1);
            DateTo = DateTime.Today.AddSeconds(-1);
        });
    }

    /// <summary>
    /// Prüft ob ein Ordnername (yyyy-MM-dd) im aktiven Date-Range liegt.
    /// Wird im Process-Tab für Folder-basierte Filterung genutzt.
    /// </summary>
    public bool MatchesDateFolder(string folderName)
    {
        if (!HasDateFilter) return true;
        if (!DateOnly.TryParseExact(folderName, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var folderDate))
            return true;

        if (DateFrom.HasValue && folderDate < DateOnly.FromDateTime(DateFrom.Value))
            return false;
        if (DateTo.HasValue && folderDate > DateOnly.FromDateTime(DateTo.Value))
            return false;
        return true;
    }
}
