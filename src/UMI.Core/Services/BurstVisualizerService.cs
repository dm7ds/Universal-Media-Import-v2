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

using System.Collections.Concurrent;
using MetadataExtractor;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service für Burst-Profil-Visualisierung.
/// Lädt EXIF-Daten in den Speicher und evaluiert Burst-Profile in-memory.
/// </summary>
public interface IBurstVisualizerService
{
    /// <summary>
    /// Lädt alle EXIF-Daten eines Ordners in den Speicher.
    /// Einmaliger I/O-Zugriff, danach alles im RAM.
    /// </summary>
    /// <param name="loadThumbnails">
    /// Wenn true: EXIF IFD1-Thumbnails (JPEG, byte[]) werden pro Foto geladen.
    /// Erhöht Speicherbedarf. Standard: false.
    /// </param>
    Task<VisualizerData> LoadFolderAsync(
        string folderPath,
        IProgress<ExifScanProgress>? progress = null,
        CancellationToken ct = default,
        bool loadThumbnails = false,
        int maxParallelism = 4);

    /// <summary>
    /// Evaluiert Conditions + Grouping gegen geladene Daten.
    /// Kein I/O – rein in-memory.
    /// </summary>
    VisualizerResult Evaluate(
        VisualizerData data,
        ConditionGroup matchConditions,
        GroupingConfig grouping);
}

public class BurstVisualizerService : IBurstVisualizerService
{
    private readonly MetadataReader _metadataReader;
    private readonly BurstMatchingEngine _matchingEngine;
    private readonly SequenceGroupingService _groupingService;
    private readonly IThumbnailCacheService _thumbnailCache;
    private readonly ILogger<BurstVisualizerService>? _logger;

    public BurstVisualizerService(
        MetadataReader metadataReader,
        BurstMatchingEngine matchingEngine,
        SequenceGroupingService groupingService,
        IThumbnailCacheService thumbnailCache,
        ILogger<BurstVisualizerService>? logger = null)
    {
        _metadataReader = metadataReader;
        _matchingEngine = matchingEngine;
        _groupingService = groupingService;
        _thumbnailCache = thumbnailCache;
        _logger = logger;
    }

    public async Task<VisualizerData> LoadFolderAsync(
        string folderPath,
        IProgress<ExifScanProgress>? progress = null,
        CancellationToken ct = default,
        bool loadThumbnails = false,
        int maxParallelism = 4)
    {
        var photos = System.IO.Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !FolderNameConstants.IsInternalPath(f))
            .Where(FileExtensions.IsPhoto)
            .ToList();

        var processedCount = 0;
        var bag = new ConcurrentBag<VisualizerPhoto>();

        await Parallel.ForEachAsync(
            photos.Select((path, index) => (path, index)),
            new ParallelOptions { MaxDegreeOfParallelism = maxParallelism, CancellationToken = ct },
            async (item, token) =>
            {
                token.ThrowIfCancellationRequested();
                var count = Interlocked.Increment(ref processedCount);
                progress?.Report(new ExifScanProgress(count, photos.Count, Path.GetFileName(item.path)));

                try
                {
                    var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(item.path);
                    var metadata = _metadataReader.ReadPhotoMetadata(item.path, directories);

                    EnrichExifValues(metadata.ExifValues, directories);

                    byte[]? thumbnailData = null;
                    if (loadThumbnails)
                    {
                        thumbnailData = await _thumbnailCache.GetThumbnailAsync(item.path, token)
                            .ConfigureAwait(false);
                    }

                    bag.Add(new VisualizerPhoto
                    {
                        FileName = Path.GetFileName(item.path),
                        RelativePath = Path.GetRelativePath(folderPath, item.path),
                        CaptureTime = metadata.CreateDate,
                        ExifValues = metadata.ExifValues,
                        ExifStringValues = BuildStringValues(metadata),
                        ThumbnailData = thumbnailData
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("Fehler beim Laden von {File}: {Error}", item.path, ex.Message);
                }

                await Task.CompletedTask;
            });

        return new VisualizerData
        {
            FolderPath = folderPath,
            Photos = bag.OrderBy(p => p.CaptureTime ?? DateTime.MinValue).ToList()
        };
    }

    public VisualizerResult Evaluate(
        VisualizerData data,
        ConditionGroup matchConditions,
        GroupingConfig grouping)
    {
        var matched = new List<(VisualizerPhoto Photo, PhotoMetadata Metadata)>();
        var unmatched = new List<VisualizerPhotoResult>();

        foreach (var photo in data.Photos)
        {
            var metadata = ConvertToPhotoMetadata(photo);
            var isMatch = _matchingEngine.EvaluateConditionGroup(metadata, matchConditions);

            if (isMatch)
            {
                matched.Add((photo, metadata));
            }
            else
            {
                var failedCondition = FindFailedCondition(metadata, matchConditions);
                unmatched.Add(new VisualizerPhotoResult
                {
                    FileName = photo.FileName,
                    RelativePath = photo.RelativePath,
                    CaptureTime = photo.CaptureTime,
                    RuleMatched = false,
                    FailedCondition = failedCondition,
                    DisplayValues = photo.ExifStringValues
                });
            }
        }

        var sequences = new List<VisualizerSequence>();
        var orphanedMatches = new List<VisualizerPhotoResult>();
        int? largestSubMin = null;

        if (matched.Count > 0)
        {

            var sorted = matched.OrderBy(m => m.Photo.CaptureTime ?? DateTime.MinValue).ToList();

            var currentGroup = new List<(VisualizerPhoto Photo, PhotoMetadata Metadata)> { sorted[0] };
            var allGroups = new List<List<(VisualizerPhoto Photo, PhotoMetadata Metadata)>>();

            for (int i = 1; i < sorted.Count; i++)
            {
                var prev = sorted[i - 1].Photo.CaptureTime;
                var curr = sorted[i].Photo.CaptureTime;

                if (prev.HasValue && curr.HasValue)
                {
                    var gap = (curr.Value - prev.Value).TotalSeconds;

                    if (gap <= grouping.MaxGapSeconds)
                    {
                        currentGroup.Add(sorted[i]);
                    }
                    else
                    {
                        allGroups.Add(currentGroup);
                        currentGroup = new List<(VisualizerPhoto Photo, PhotoMetadata Metadata)> { sorted[i] };
                    }
                }
                else
                {
                    allGroups.Add(currentGroup);
                    currentGroup = new List<(VisualizerPhoto Photo, PhotoMetadata Metadata)> { sorted[i] };
                }
            }

            if (currentGroup.Count > 0)
                allGroups.Add(currentGroup);

            if (grouping.StableFields is { Count: > 0 })
            {
                var refinedGroups = new List<List<(VisualizerPhoto Photo, PhotoMetadata Metadata)>>();
                foreach (var group in allGroups)
                {
                    refinedGroups.AddRange(SplitByStableFields(group, grouping.StableFields));
                }
                allGroups = refinedGroups;
            }

            int seqIndex = 1;
            foreach (var group in allGroups)
            {
                if (group.Count >= grouping.MinCount)
                {
                    var seqPhotos = new List<VisualizerPhotoResult>();
                    for (int i = 0; i < group.Count; i++)
                    {
                        double? gap = null;
                        if (i > 0)
                        {
                            var prev = group[i - 1].Photo.CaptureTime;
                            var curr = group[i].Photo.CaptureTime;
                            if (prev.HasValue && curr.HasValue)
                                gap = (curr.Value - prev.Value).TotalSeconds;
                        }

                        seqPhotos.Add(new VisualizerPhotoResult
                        {
                            FileName = group[i].Photo.FileName,
                            RelativePath = group[i].Photo.RelativePath,
                            CaptureTime = group[i].Photo.CaptureTime,
                            GapSeconds = gap,
                            RuleMatched = true,
                            DisplayValues = group[i].Photo.ExifStringValues
                        });
                    }

                    var avgGap = seqPhotos.Where(p => p.GapSeconds.HasValue)
                        .Select(p => p.GapSeconds!.Value)
                        .DefaultIfEmpty(0)
                        .Average();

                    sequences.Add(new VisualizerSequence
                    {
                        SequenceName = $"Sequence_{seqIndex++}",
                        Photos = seqPhotos,
                        AverageGap = avgGap,
                        TimeRange = BuildTimeRange(seqPhotos)
                    });
                }
                else
                {

                    foreach (var item in group)
                    {
                        orphanedMatches.Add(new VisualizerPhotoResult
                        {
                            FileName = item.Photo.FileName,
                            RelativePath = item.Photo.RelativePath,
                            CaptureTime = item.Photo.CaptureTime,
                            RuleMatched = true,
                            DisplayValues = item.Photo.ExifStringValues
                        });
                    }

                    if (!largestSubMin.HasValue || group.Count > largestSubMin.Value)
                        largestSubMin = group.Count;
                }
            }
        }

        return new VisualizerResult
        {
            MatchedCount = matched.Count,
            UnmatchedCount = unmatched.Count,
            Sequences = sequences,
            OrphanedMatches = orphanedMatches,
            Unmatched = unmatched,
            LargestSubMinGroup = orphanedMatches.Count > 0 ? orphanedMatches.Count : null,
            EffectiveThreshold = grouping.MaxGapSeconds
        };
    }

    /// <summary>
    /// Splits a group into sub-groups where all StableFields have the same value.
    /// Photos are already time-sorted. We iterate and start a new sub-group whenever
    /// a stable field's value changes from the previous photo.
    /// </summary>
    private static List<List<(VisualizerPhoto Photo, PhotoMetadata Metadata)>> SplitByStableFields(
        List<(VisualizerPhoto Photo, PhotoMetadata Metadata)> group,
        List<string> stableFields)
    {
        if (group.Count <= 1)
            return new List<List<(VisualizerPhoto, PhotoMetadata)>> { group };

        var result = new List<List<(VisualizerPhoto, PhotoMetadata)>>();
        var current = new List<(VisualizerPhoto, PhotoMetadata)> { group[0] };

        for (int i = 1; i < group.Count; i++)
        {
            bool fieldChanged = false;
            foreach (var field in stableFields)
            {
                var prevVal = GetStableFieldValue(group[i - 1].Photo, field);
                var currVal = GetStableFieldValue(group[i].Photo, field);

                if (prevVal != currVal)
                {
                    fieldChanged = true;
                    break;
                }
            }

            if (fieldChanged)
            {
                result.Add(current);
                current = new List<(VisualizerPhoto, PhotoMetadata)>();
            }
            current.Add(group[i]);
        }
        result.Add(current);
        return result;
    }

    private static double? GetStableFieldValue(VisualizerPhoto photo, string fieldName)
    {
        return photo.ExifValues.TryGetValue(fieldName, out var val) ? val : null;
    }

    /// <summary>
    /// Konvertiert VisualizerPhoto zu PhotoMetadata für Matching-Engine.
    /// </summary>
    private PhotoMetadata ConvertToPhotoMetadata(VisualizerPhoto photo)
    {
        return new PhotoMetadata
        {
            FilePath = photo.FileName,
            FileName = photo.FileName,
            CreateDate = photo.CaptureTime,
            IsVideo = false,
            ExifValues = photo.ExifValues
        };
    }

    /// <summary>
    /// Baut String-Darstellungen der EXIF-Werte.
    /// </summary>
    private Dictionary<string, string> BuildStringValues(PhotoMetadata metadata)
    {
        var result = new Dictionary<string, string>();

        foreach (var (key, value) in metadata.ExifValues)
        {
            result[key] = FormatExifValue(key, value);
        }

        return result;
    }

    /// <summary>
    /// Formatiert EXIF-Wert für Anzeige.
    /// </summary>
    private string FormatExifValue(string fieldName, double value)
    {

        if (fieldName.Contains("Exposure", StringComparison.OrdinalIgnoreCase) && value < 1)
        {
            return $"1/{(int)(1 / value)}";
        }

        if (fieldName.Contains("ISO", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("Drive", StringComparison.OrdinalIgnoreCase))
        {
            return ((int)value).ToString();
        }

        return value.ToString("F2");
    }

    /// <summary>
    /// Findet die erste Condition die fehlschlug.
    /// Nutzt BurstMatchingEngine.EvaluateCondition (DRY — keine duplizierte Logik).
    /// </summary>
    private string? FindFailedCondition(PhotoMetadata metadata, ConditionGroup group)
    {
        if (group.Conditions != null)
        {
            foreach (var condition in group.Conditions)
            {
                if (!_matchingEngine.EvaluateCondition(metadata, condition))
                {
                    return $"{condition.Field} {condition.Operator} {condition.Value}";
                }
            }
        }

        if (group.Groups != null)
        {
            foreach (var subGroup in group.Groups)
            {
                var failed = FindFailedCondition(metadata, subGroup);
                if (failed != null)
                    return failed;
            }
        }

        return null;
    }

    /// <summary>
    /// Reichert ExifValues mit ALLEN numerischen EXIF-Feldern via MetadataExtractor an.
    /// Existierende Keys (Built-in 8 Felder) werden NICHT überschrieben.
    /// </summary>
    private void EnrichExifValues(
        Dictionary<string, double> exifValues, IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        try
        {
            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    var description = tag.Description;
                    if (string.IsNullOrWhiteSpace(description))
                        continue;

                    var numericValue = ExifValueParser.TryParseNumericValue(description);
                    if (numericValue == null)
                        continue;

                    var key = tag.Name;

                    if (exifValues.ContainsKey(key))
                        continue;

                    var prefixedKey = $"{directory.Name}.{tag.Name}";
                    if (!exifValues.ContainsKey(prefixedKey))
                    {
                        exifValues[key] = numericValue.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("EnrichExifValues Fehler: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Baut TimeRange String (HH:mm:ss - HH:mm:ss).
    /// </summary>
    private string BuildTimeRange(List<VisualizerPhotoResult> photos)
    {
        if (photos.Count == 0) return "";

        var first = photos.First().CaptureTime;
        var last = photos.Last().CaptureTime;

        if (!first.HasValue || !last.HasValue) return "";

        return $"{first.Value:HH:mm:ss} - {last.Value:HH:mm:ss}";
    }
}
