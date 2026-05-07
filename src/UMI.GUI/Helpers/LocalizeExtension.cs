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

using System.Windows.Markup;
using UMI.GUI.Resources;

namespace UMI.GUI.Helpers;

/// <summary>
/// XAML MarkupExtension for localized strings from <see cref="Strings"/> resources.
/// Usage: <c>&lt;TextBlock Text="{helpers:Localize ImportTab_SmartWatch}" /&gt;</c>
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public class LocalizeExtension : MarkupExtension
{
    /// <summary>Resource key to look up in <see cref="Strings.ResourceManager"/>.</summary>
    public string Key { get; set; }

    public LocalizeExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
        => Strings.ResourceManager.GetString(Key, Strings.Culture) ?? $"[{Key}]";
}
