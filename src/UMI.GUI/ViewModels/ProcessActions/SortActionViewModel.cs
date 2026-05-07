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
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.ProcessActions;

/// <summary>
/// Process action stub for sorting files by EXIF date into date-named folders.
/// Not yet implemented in the GUI — shows a placeholder status message.
/// The DetectBursts toggle is prepared for future burst-detection integration.
/// </summary>
public sealed class SortActionViewModel : ProcessActionViewModel
{

    public override string Title => Strings.Sort_Title;

    public override string Description => Strings.Sort_Description;

    /// <summary>
    /// Calendar icon. 24×24 stroke-based WPF Path Data.
    /// Calendar outline with header bar and date grid rows.
    /// </summary>
    public override string IconPathData =>
        "M6,2 V4 H4 A2,2 0 0,0 2,6 V20 A2,2 0 0,0 4,22 H20 A2,2 0 0,0 22,20 V6 A2,2 0 0,0 20,4 H18 V2 H16 V4 H8 V2 Z " +
        "M2,10 H22 " +
        "M7,15 H11 M13,15 H17";

    private readonly ActionToggle _burstToggle = new(Strings.Sort_ToggleBurst);

    public override IReadOnlyList<ActionToggle> Toggles => [_burstToggle];

    protected override Task ExecuteRunAsync(CancellationToken ct)
    {
        SetOnUiThread(() =>
        {
            StatusMessage = Strings.Sort_NotAvailable;
            IsStatusError = false;
            Progress      = 1;
            ProgressText  = string.Empty;
        });

        return Task.CompletedTask;
    }
}
