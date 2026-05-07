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

namespace UMI.Core.Models;

/// <summary>
/// Geladene EXIF-Daten eines Ordners (in-memory).
/// </summary>
public record VisualizerData
{
    public required string FolderPath { get; init; }
    public required List<VisualizerPhoto> Photos { get; init; }
    public int TotalCount => Photos.Count;
}

/// <summary>
/// Ein Foto mit seinen EXIF-Werten (für Visualizer).
/// </summary>
public record VisualizerPhoto
{
    public required string FileName { get; init; }

    /// <summary>
    /// Relative path from the scan root folder (e.g. "2024-09-22/R10/Sport_154111/IMG_1208.CR3").
    /// Falls back to FileName when not set (single-folder scan).
    /// </summary>
    public string? RelativePath { get; init; }

    public required DateTime? CaptureTime { get; init; }
    public required Dictionary<string, double> ExifValues { get; init; }
    public required Dictionary<string, string> ExifStringValues { get; init; }

    /// <summary>
    /// Rohe JPEG-Thumbnail-Daten aus EXIF IFD1. Nur befüllt wenn
    /// LoadFolderAsync mit loadThumbnails=true aufgerufen wurde.
    /// </summary>
    public byte[]? ThumbnailData { get; init; }
}

/// <summary>
/// Ergebnis des AutoPresetGenerators: automatisch generierte Match-Conditions
/// und Gruppierungs-Konfiguration aus einer Selektion von Fotos.
/// </summary>
public record GeneratedPresetResult
{
    public required List<MatchCondition> SuggestedConditions { get; init; }
    public required GroupingConfig SuggestedGrouping { get; init; }
    public required List<string> ExplanationLines { get; init; }

    /// <summary>
    /// EXIF fields that are stable within each series but differ between series.
    /// Suggested as StableFields for the GroupingConfig so sequences are split
    /// when these field values change.
    /// </summary>
    public List<string>? SuggestedStableFields { get; init; }
}

/// <summary>
/// Ergebnis der Burst-Profil-Evaluation.
/// </summary>
public record VisualizerResult
{
    public int MatchedCount { get; init; }
    public int UnmatchedCount { get; init; }
    public required List<VisualizerSequence> Sequences { get; init; }
    public required List<VisualizerPhotoResult> OrphanedMatches { get; init; }
    public required List<VisualizerPhotoResult> Unmatched { get; init; }
    public int? LargestSubMinGroup { get; init; }
    public double EffectiveThreshold { get; init; }
}

/// <summary>
/// Eine erkannte Sequenz.
/// </summary>
public record VisualizerSequence
{
    public required string SequenceName { get; init; }
    public required List<VisualizerPhotoResult> Photos { get; init; }
    public int Count => Photos.Count;
    public double AverageGap { get; init; }
    public string TimeRange { get; init; } = "";
}

/// <summary>
/// Ein Foto im Visualizer-Ergebnis mit Matching-Status.
/// </summary>
public record VisualizerPhotoResult
{
    public required string FileName { get; init; }
    /// <summary>
    /// Relative path from scan root. Unique key for recursive scans.
    /// Falls back to FileName when not set.
    /// </summary>
    public string? RelativePath { get; init; }
    public DateTime? CaptureTime { get; init; }
    public double? GapSeconds { get; init; }
    public bool RuleMatched { get; init; }
    public string? FailedCondition { get; init; }
    public required Dictionary<string, string> DisplayValues { get; init; }
}
