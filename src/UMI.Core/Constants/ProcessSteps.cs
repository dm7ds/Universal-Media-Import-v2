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

namespace UMI.Core.Constants;

/// <summary>
/// Constants for process history step names.
/// Used by IProcessHistoryService to track pipeline progress per video file.
/// </summary>
public static class ProcessSteps
{
    public const string Imported = "imported";
    public const string MetadataBackedUp = "metadata_backed_up";
    public const string EisDetected = "eis_detected";
    public const string GpxBuilt = "gpx_built";
    public const string GyroflowQueued = "gyroflow_queued";
    public const string GyroflowDone = "gyroflow_done";
    public const string MetadataRestored = "metadata_restored";
    public const string GpsInjected = "gps_injected";
    public const string Graded = "graded";
    public const string Finalized = "finalized";
}
