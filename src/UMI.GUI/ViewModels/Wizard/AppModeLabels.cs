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

using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard;

/// <summary>
/// Single source of truth for human-readable AppMode display labels.
/// Used in WelcomeStep (mode cards), SummaryStep, and the main header dropdown.
/// NML rule: define once here, reference everywhere else.
/// Now backed by Strings.resx for i18n support.
/// </summary>
public static class AppModeLabels
{
    public static string Dau      => Strings.AppMode_Easy;
    public static string Simple   => Strings.AppMode_Standard;
    public static string Advanced => Strings.AppMode_Advanced;
}
