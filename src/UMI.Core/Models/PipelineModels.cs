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

using UMI.Data.Models;

namespace UMI.Core.Models;

public class ScanResult
{
    public int TotalFiles { get; set; }
    public int Photos { get; set; }
    public int Videos { get; set; }
    public long TotalBytes { get; set; }
    public List<DetectedSequence> Sequences { get; set; } = new();
    public ImportStats Stats { get; set; } = new();
    public List<LayoutConflict> LayoutConflicts { get; set; } = new();

    /// <summary>Number of files skipped because their CreateDate is outside the date filter range.</summary>
    public int SkippedByDateFilter { get; set; }

    /// <summary>
    /// Für History-Update nach Copy (nur bei FixedPath, nicht DryRun).
    /// Format: RelativePath (Forward-Slashes) + FileSize.
    /// </summary>
    public List<(string RelativePath, long FileSize)> PendingHistoryEntries { get; set; } = new();
}

public class ScanProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentFile { get; set; } = "";
    public string Operation { get; set; } = "";
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}

public class CopyResult
{
    public int TotalFiles { get; set; }
    public int CopiedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int ErrorFiles { get; set; }
    public long TotalBytes { get; set; }
    public long CopiedBytes { get; set; }
}

public class CopyProgress
{

    public int CompletedFiles { get; set; }
    public int TotalFiles { get; set; }
    public long TotalCopiedBytes { get; set; }
    public long TotalBytes { get; set; }

    public string? CurrentFile { get; set; }
    public long CurrentFileSize { get; set; }
    public long CurrentFileCopiedBytes { get; set; }

    public double Percentage => TotalBytes > 0 ? (double)TotalCopiedBytes / TotalBytes * 100 : 0;
    public string ByteProgress => $"{Utilities.FormatHelper.FormatBytes(TotalCopiedBytes)} / {Utilities.FormatHelper.FormatBytes(TotalBytes)}";
}

/// <summary>
/// Internes Gruppierungsergebnis (nicht für extern).
/// </summary>
public class PhotoGroupResult
{
    public List<long> FileIds { get; set; } = new();
    public string Mode { get; set; } = "Single";
    public bool IsSequence { get; set; }
    public DateTime FirstPhotoTime { get; set; }
    public long PhotoCount { get; set; }
    public double ThresholdUsed { get; set; }
}
