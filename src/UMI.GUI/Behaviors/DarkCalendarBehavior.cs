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
using System.Windows.Media;

namespace UMI.GUI.Behaviors;

/// <summary>
/// Attached behavior that dark-themes a DatePicker's Calendar popup.
/// WPF Calendar's internal CalendarItem template has hardcoded light colors
/// (white borders, dark text) that cannot be overridden via XAML styles alone.
/// This behavior walks the visual tree on CalendarOpened and forces dark theme colors
/// sourced from Application.Resources (ColorBgCard, ColorTextPrimary, ColorTextMuted, ColorBorder).
/// </summary>
public static class DarkCalendarBehavior
{

    private static Color BgCard => GetThemeColor("ColorBgCard", "#313244");
    private static Color TextPrimary => GetThemeColor("ColorTextPrimary", "#cdd6f4");
    private static Color TextMuted => GetThemeColor("ColorTextMuted", "#8890a8");
    private static Color BorderSubtle => GetThemeColor("ColorBorder", "#45475a");

    private static Color GetThemeColor(string key, string fallback)
    {
        var resources = Application.Current?.Resources;
        if (resources != null)
        {
            if (resources[key] is Color c) return c;
            if (resources[key] is SolidColorBrush b) return b.Color;
        }
        return (Color)ColorConverter.ConvertFromString(fallback);
    }

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DarkCalendarBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DatePicker picker)
        {
            if ((bool)e.NewValue)
                picker.CalendarOpened += OnCalendarOpened;
            else
                picker.CalendarOpened -= OnCalendarOpened;
        }
    }

    private static void OnCalendarOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is not DatePicker picker) return;

        var popup = FindChild<Popup>(picker);
        if (popup?.Child is not FrameworkElement popupContent) return;

        ApplyDarkTheme(popupContent);
    }

    private static void ApplyDarkTheme(DependencyObject root)
    {
        var bgBrush = new SolidColorBrush(BgCard);
        var fgBrush = new SolidColorBrush(TextPrimary);
        var mutedBrush = new SolidColorBrush(TextMuted);
        var borderBrush = new SolidColorBrush(BorderSubtle);

        bgBrush.Freeze();
        fgBrush.Freeze();
        mutedBrush.Freeze();
        borderBrush.Freeze();

        ApplyToTree(root, bgBrush, fgBrush, mutedBrush, borderBrush);
    }

    private static void ApplyToTree(
        DependencyObject obj,
        SolidColorBrush bg, SolidColorBrush fg, SolidColorBrush muted, SolidColorBrush border)
    {
        switch (obj)
        {
            case CalendarItem item:
                item.Background = bg;
                item.Foreground = fg;

                if (FindChild<System.Windows.Controls.Border>(item) is { } innerBorder)
                {
                    innerBorder.Background = bg;
                    innerBorder.BorderBrush = border;
                }
                break;

            case CalendarDayButton dayBtn:
                dayBtn.Foreground = fg;
                dayBtn.Background = Brushes.Transparent;
                break;

            case CalendarButton calBtn:
                calBtn.Foreground = fg;
                calBtn.Background = Brushes.Transparent;
                break;

            case System.Windows.Controls.Border b:

                if (b.Background is SolidColorBrush bgBrush && IsLight(bgBrush.Color))
                    b.Background = bg;
                if (b.BorderBrush is SolidColorBrush brBrush && IsLight(brBrush.Color))
                    b.BorderBrush = border;
                break;

            case TextBlock tb:

                if (tb.Foreground is SolidColorBrush tbBrush && IsDark(tbBrush.Color))
                    tb.Foreground = fg;
                break;

            case Button btn:
                if (btn.Foreground is SolidColorBrush btnBrush && IsDark(btnBrush.Color))
                    btn.Foreground = fg;
                break;
        }

        int count = VisualTreeHelper.GetChildrenCount(obj);
        for (int i = 0; i < count; i++)
            ApplyToTree(VisualTreeHelper.GetChild(obj, i), bg, fg, muted, border);
    }

    /// <summary>Color is "light" (likely a white/light-gray background that needs replacing).</summary>
    private static bool IsLight(Color c) => c.A > 0 && (c.R + c.G + c.B) > 500;

    /// <summary>Color is "dark" (likely hardcoded black/dark-gray text that needs replacing).</summary>
    private static bool IsDark(Color c) => c.A > 0 && (c.R + c.G + c.B) < 300;

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var result = FindChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
