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

using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Data.Models;

namespace UMI.Core.Services;

/// <summary>
/// Service für Zeit-basierte Foto-Gruppierung (Burst-Sequenzen).
/// Gruppiert Fotos nach Zeitlücken und ermittelt adaptive Thresholds.
/// </summary>
public class SequenceGroupingService
{
    private readonly ILogger<SequenceGroupingService>? _logger;

    public SequenceGroupingService(ILogger<SequenceGroupingService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gruppiert Fotos nach Zeitlücken zwischen aufeinanderfolgenden Aufnahmen.
    /// </summary>
    public List<PhotoGroupResult> GroupPhotosByTimeGaps(
        List<ImportedFile> photos, BurstDetectionConfig config)
    {
        var groups = new List<PhotoGroupResult>();
        if (photos.Count == 0) return groups;

        _logger?.LogDebug("=== Burst-Gruppierung START ===");
        _logger?.LogDebug("Fotos: {Count}, Logger: {HasLogger}", photos.Count, _logger != null);

        var currentGroup = new List<ImportedFile> { photos[0] };
        var currentMode = photos[0].ShootingMode ?? "Single";
        _logger?.LogDebug("Gruppierung Start: {File} (Mode: {Mode})",
            Path.GetFileName(photos[0].SourcePath), currentMode);

        for (int i = 1; i < photos.Count; i++)
        {
            var prev = photos[i - 1];
            var current = photos[i];

            var prevTime = ParseCaptureTime(prev);
            var currTime = ParseCaptureTime(current);
            var gap = (currTime - prevTime).TotalSeconds;

            var threshold = GetThresholdForProfile(currentMode, currentGroup, config);

            var sameMode = (current.ShootingMode ?? "Single") == currentMode;

            if (gap <= threshold && sameMode)
            {

                _logger?.LogDebug("Gap {File1}→{File2}: {Gap:F2}s <= {Threshold}s (mode: {Mode}) → ZUSAMMEN",
                    Path.GetFileName(prev.SourcePath), Path.GetFileName(current.SourcePath),
                    gap, threshold, currentMode);
                currentGroup.Add(current);
            }
            else
            {

                var reason = !sameMode ? $"Mode-Wechsel ({currentMode}→{current.ShootingMode})" : $"Gap > Threshold";
                _logger?.LogDebug("Gap {File1}→{File2}: {Gap:F2}s (threshold: {Threshold}s, mode: {Mode}) → SPLIT ({Reason})",
                    Path.GetFileName(prev.SourcePath), Path.GetFileName(current.SourcePath),
                    gap, threshold, currentMode, reason);
                groups.Add(FinalizeGroup(currentGroup, currentMode, config));
                currentGroup = new List<ImportedFile> { current };
                currentMode = current.ShootingMode ?? "Single";
            }
        }

        if (currentGroup.Count > 0)
        {
            groups.Add(FinalizeGroup(currentGroup, currentMode, config));
        }

        return groups;
    }

    private PhotoGroupResult FinalizeGroup(
        List<ImportedFile> photos, string mode, BurstDetectionConfig config)
    {

        var profile = config.LoadedProfiles.FirstOrDefault(p => p.Name == mode);
        var minCount = profile?.Grouping.MinCount ?? config.FallbackMinCount;

        var isSequence = (profile != null) && photos.Count >= minCount;
        var firstTime = ParseCaptureTime(photos[0]);

        return new PhotoGroupResult
        {
            FileIds = photos.Select(p => p.Id).ToList(),
            Mode = mode,
            IsSequence = isSequence,
            FirstPhotoTime = firstTime,
            PhotoCount = photos.Count,
            ThresholdUsed = GetThresholdForProfile(mode, photos, config)
        };
    }

    /// <summary>
    /// Bestimmt Threshold für ein Burst-Profil.
    /// Unterstützt adaptive Thresholds basierend auf durchschnittlichem Gap.
    /// </summary>
    private double GetThresholdForProfile(
        string profileName,
        List<ImportedFile> group,
        BurstDetectionConfig config)
    {

        var profile = config.LoadedProfiles.FirstOrDefault(p => p.Name == profileName);

        if (profile == null)
            return config.FallbackMaxGapSeconds;

        if (profile.Grouping.AdaptiveThreshold && group.Count >= 3)
        {
            var avgGap = CalculateAverageGap(group);
            var adaptiveThreshold = avgGap * profile.Grouping.AdaptiveMultiplier;

            return Math.Max(profile.Grouping.MaxGapSeconds, adaptiveThreshold);
        }

        return profile.Grouping.MaxGapSeconds;
    }

    /// <summary>
    /// Berechnet durchschnittlichen Gap zwischen ersten 10 Fotos einer Gruppe.
    /// </summary>
    private double CalculateAverageGap(List<ImportedFile> group)
    {
        var gaps = new List<double>();
        for (int i = 1; i < Math.Min(group.Count, 10); i++)
        {
            var prev = ParseCaptureTime(group[i - 1]);
            var curr = ParseCaptureTime(group[i]);
            gaps.Add((curr - prev).TotalSeconds);
        }
        return gaps.Count > 0 ? gaps.Average() : 3.0;
    }

    internal DateTime ParseCaptureTime(ImportedFile file)
    {
        if (!string.IsNullOrEmpty(file.CaptureTime) &&
            DateTime.TryParse(file.CaptureTime, out var parsed))
        {
            return parsed;
        }
        return DateTime.MinValue;
    }
}
