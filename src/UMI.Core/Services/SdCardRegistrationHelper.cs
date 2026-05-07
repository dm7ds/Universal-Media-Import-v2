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

using UMI.Core.Models;

namespace UMI.Core.Services;

/// <summary>
/// Zentrale Erstellung von SdCardRegistration mit konsistenten Defaults.
/// IMMER DateTime.UtcNow, IMMER ISO 8601 "o" Format.
/// </summary>
public static class SdCardRegistrationHelper
{
    public static SdCardRegistration Create(
        string cameraId,
        string? label = null,
        string? diskSerial = null,
        long sizeBytes = 0,
        string? model = null,
        SdCardRegistration? existing = null)
    {
        return new SdCardRegistration
        {
            CameraId     = cameraId,
            Label        = label,
            DiskSerial   = diskSerial ?? existing?.DiskSerial,
            SizeBytes    = sizeBytes > 0 ? sizeBytes : existing?.SizeBytes ?? 0,
            Model        = model ?? existing?.Model,
            FirstSeen    = existing?.FirstSeen is { Length: > 0 } fs ? fs : DateTime.UtcNow.ToString("o"),
            LastSeen     = DateTime.UtcNow.ToString("o"),
            UsageHistory = existing?.UsageHistory ?? new(),
            LastUsedWith = existing?.LastUsedWith ?? new()
        };
    }
}
