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
/// Analysiert eine Selektion von VisualizerPhotos und generiert automatisch
/// passende Match-Conditions und Gruppierungs-Konfiguration.
///
/// Algorithmus:
/// 1. Sammle alle numerischen EXIF-Felder der selektierten Fotos.
/// 2. Berechne pro Feld Mittelwert und normalisierte Varianz (Varianz / |Mittelwert|).
/// 3. Felder mit normalisierter Varianz unter dem Threshold werden zu MatchConditions.
/// 4. Erkenne Serien-Grenzen via Median-basiertem Clustering der Gaps.
/// 5. MaxGapSeconds = größter Intra-Serien-Gap × GapBufferFactor.
/// 6. MinCount = max(AbsoluteMinCount, kleinste erkannte Serie).
/// </summary>
public class AutoPresetGenerator
{

    private const double GapBufferFactor = 1.5;

    private const double FallbackMaxGapSeconds = 3.0;

    private const int AbsoluteMinCount = 3;

    private const double SeriesBoundaryFactor = 10.0;

    private const double FallbackBoundaryGapSeconds = 30.0;

    /// <summary>
    /// Photographically relevant EXIF fields for burst detection.
    /// Only these fields are considered by the variance analysis.
    /// SSOT — also referenced by GUI's BuiltInExifFields.
    /// </summary>
    public static readonly IReadOnlySet<string> RelevantFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "ExposureTime", "ISO", "FocalLength", "Aperture",
        "ShutterSpeed", "ExposureCompensation", "ContinuousDrive", "ExposureMode"
    };

    /// <summary>
    /// Generiert Preset-Vorschläge aus einer Foto-Selektion.
    /// </summary>
    /// <param name="selectedPhotos">Die selektierten Fotos aus dem Visualizer.</param>
    /// <param name="numericVarianceThreshold">
    /// Schwellwert für normalisierte Varianz (Varianz / |Mittelwert|).
    /// Felder darunter gelten als stabil und werden zu MatchConditions.
    /// Standard: 0.05 (5 % Abweichung erlaubt).
    /// </param>
    /// <param name="fieldFilter">
    /// Optionaler Filter: nur diese EXIF-Felder werden analysiert.
    /// Wenn null, wird <see cref="RelevantFields"/> als Fallback verwendet.
    /// </param>
    /// <returns>Generiertes Preset-Ergebnis mit Conditions, Grouping und Erklärungen.</returns>
    public GeneratedPresetResult GenerateFromSelection(
        IReadOnlyList<VisualizerPhoto> selectedPhotos,
        double numericVarianceThreshold = 0.05,
        IReadOnlySet<string>? fieldFilter = null)
    {
        var explanationLines = new List<string>();
        var suggestedConditions = new List<MatchCondition>();
        var suggestedStableFields = new List<string>();
        var effectiveFilter = fieldFilter ?? RelevantFields;

        if (selectedPhotos.Count == 0)
        {
            explanationLines.Add("Keine Fotos selektiert — leeres Preset generiert.");
            return new GeneratedPresetResult
            {
                SuggestedConditions = suggestedConditions,
                SuggestedGrouping = new GroupingConfig
                {
                    MaxGapSeconds = FallbackMaxGapSeconds,
                    MinCount = AbsoluteMinCount
                },
                ExplanationLines = explanationLines,
                SuggestedStableFields = null
            };
        }

        var series = SplitIntoSeries(selectedPhotos);

        var fieldValues = new Dictionary<string, List<double>>();

        foreach (var photo in selectedPhotos)
        {
            foreach (var (field, value) in photo.ExifValues)
            {
                if (!effectiveFilter.Contains(field)) continue;

                if (!fieldValues.TryGetValue(field, out var values))
                {
                    values = new List<double>();
                    fieldValues[field] = values;
                }
                values.Add(value);
            }
        }

        int totalCount = selectedPhotos.Count;

        foreach (var (field, values) in fieldValues)
        {

            if (values.Count < totalCount)
                continue;

            var globalMean = values.Average();

            if (Math.Abs(globalMean) < double.Epsilon)
                continue;

            var globalVariance = values.Select(v => (v - globalMean) * (v - globalMean)).Average();
            var globalNormalizedVariance = globalVariance / Math.Abs(globalMean);

            if (globalNormalizedVariance < numericVarianceThreshold)
            {

                var roundedMean = Math.Round(globalMean, 4);
                suggestedConditions.Add(new MatchCondition
                {
                    Field = field,
                    Operator = "==",
                    Value = roundedMean
                });

                explanationLines.Add(
                    $"Stabiles Feld '{field}': Mittelwert={roundedMean:F4}, " +
                    $"norm. Varianz={globalNormalizedVariance:F4} (< {numericVarianceThreshold:F4})");
            }
            else if (series.Count > 1)
            {

                bool allSeriesStable = true;
                var seriesMeans = new List<double>();

                foreach (var serie in series)
                {
                    var serieValues = serie
                        .Where(p => p.ExifValues.ContainsKey(field))
                        .Select(p => p.ExifValues[field])
                        .ToList();

                    if (serieValues.Count < serie.Count)
                    {
                        allSeriesStable = false;
                        break;
                    }

                    var serieMean = serieValues.Average();

                    if (Math.Abs(serieMean) < double.Epsilon)
                    {

                        seriesMeans.Add(Math.Round(serieMean, 4));
                        continue;
                    }

                    var serieVariance = serieValues.Select(v => (v - serieMean) * (v - serieMean)).Average();
                    var serieNormalizedVariance = serieVariance / Math.Abs(serieMean);

                    if (serieNormalizedVariance >= numericVarianceThreshold)
                    {
                        allSeriesStable = false;
                        break;
                    }

                    seriesMeans.Add(Math.Round(serieMean, 4));
                }

                if (allSeriesStable && seriesMeans.Count > 0)
                {
                    var distinctValues = seriesMeans.Distinct().ToList();
                    if (distinctValues.Count > 1)
                    {

                        suggestedStableFields.Add(field);
                        var valuesStr = string.Join(", ", seriesMeans);
                        explanationLines.Add(
                            $"Serie-stabil: {field} (Werte pro Serie: {valuesStr}) " +
                            "→ suggested as Stable Field");
                    }

                    else
                    {
                        var roundedMean = distinctValues[0];
                        suggestedConditions.Add(new MatchCondition
                        {
                            Field = field,
                            Operator = "==",
                            Value = roundedMean
                        });
                        explanationLines.Add(
                            $"Stabiles Feld '{field}': Mittelwert={roundedMean:F4} " +
                            "(per-Serie-Analyse, global-Varianz knapp über Threshold)");
                    }
                }
            }
        }

        if (suggestedConditions.Count == 0)
        {
            explanationLines.Add(
                $"Kein EXIF-Feld war stabil genug (Threshold: {numericVarianceThreshold:F4}). " +
                "Conditions-Liste ist leer — manuell ergänzen.");
        }
        else
        {
            explanationLines.Insert(0,
                $"{suggestedConditions.Count} stabile EXIF-Felder aus {totalCount} Fotos erkannt.");
        }

        var groupingConfig = DeriveGroupingConfig(series, totalCount, explanationLines);

        return new GeneratedPresetResult
        {
            SuggestedConditions = suggestedConditions,
            SuggestedGrouping = groupingConfig,
            ExplanationLines = explanationLines,
            SuggestedStableFields = suggestedStableFields.Count > 0 ? suggestedStableFields : null
        };
    }

    /// <summary>
    /// Splits time-sorted photos into series based on gap analysis.
    /// A gap > median × SeriesBoundaryFactor starts a new series.
    /// Photos without CaptureTime are placed into a single fallback series.
    /// </summary>
    private static List<List<VisualizerPhoto>> SplitIntoSeries(
        IReadOnlyList<VisualizerPhoto> photos)
    {
        var timedPhotos = photos
            .Where(p => p.CaptureTime.HasValue)
            .OrderBy(p => p.CaptureTime!.Value)
            .ToList();

        if (timedPhotos.Count < 2)
        {

            return new List<List<VisualizerPhoto>> { photos.ToList() };
        }

        var gaps = new double[timedPhotos.Count - 1];
        for (int i = 1; i < timedPhotos.Count; i++)
        {
            gaps[i - 1] = (timedPhotos[i].CaptureTime!.Value - timedPhotos[i - 1].CaptureTime!.Value)
                .TotalSeconds;
        }

        var sortedGaps = gaps.OrderBy(g => g).ToArray();
        double medianGap = sortedGaps.Length % 2 == 1
            ? sortedGaps[sortedGaps.Length / 2]
            : (sortedGaps[sortedGaps.Length / 2 - 1] + sortedGaps[sortedGaps.Length / 2]) / 2.0;

        double boundaryThreshold = medianGap < double.Epsilon
            ? FallbackBoundaryGapSeconds
            : medianGap * SeriesBoundaryFactor;

        var series = new List<List<VisualizerPhoto>>();
        var currentSeries = new List<VisualizerPhoto> { timedPhotos[0] };

        for (int i = 0; i < gaps.Length; i++)
        {
            if (gaps[i] > boundaryThreshold)
            {
                series.Add(currentSeries);
                currentSeries = new List<VisualizerPhoto>();
            }
            currentSeries.Add(timedPhotos[i + 1]);
        }
        series.Add(currentSeries);

        return series;
    }

    /// <summary>
    /// Leitet MaxGapSeconds und MinCount aus den bereits berechneten Serien ab.
    /// MaxGapSeconds = größter Intra-Serien-Gap × GapBufferFactor.
    /// MinCount = max(AbsoluteMinCount, kleinste erkannte Serie).
    /// </summary>
    private static GroupingConfig DeriveGroupingConfig(
        List<List<VisualizerPhoto>> series,
        int totalCount,
        List<string> explanationLines)
    {
        double maxGapSeconds = FallbackMaxGapSeconds;
        int minCount = AbsoluteMinCount;

        int timedPhotoCount = series.SelectMany(s => s).Count(p => p.CaptureTime.HasValue);

        if (timedPhotoCount < 2)
        {
            explanationLines.Add(
                $"Keine ausreichenden Zeitstempel — MaxGapSeconds Fallback: {maxGapSeconds:F2}s");
            explanationLines.Add($"MinCount={minCount} (Fallback: AbsoluteMinCount)");

            return new GroupingConfig
            {
                MaxGapSeconds = maxGapSeconds,
                MinCount = minCount
            };
        }

        double intraSeriesMaxGap = 0;
        int smallestSeriesCount = int.MaxValue;

        foreach (var serie in series)
        {
            if (serie.Count < smallestSeriesCount)
                smallestSeriesCount = serie.Count;

            for (int i = 1; i < serie.Count; i++)
            {
                if (!serie[i].CaptureTime.HasValue || !serie[i - 1].CaptureTime.HasValue)
                    continue;

                var gap = (serie[i].CaptureTime!.Value - serie[i - 1].CaptureTime!.Value)
                    .TotalSeconds;
                if (gap > intraSeriesMaxGap)
                    intraSeriesMaxGap = gap;
            }
        }

        maxGapSeconds = Math.Round(intraSeriesMaxGap * GapBufferFactor, 2);

        if (maxGapSeconds < double.Epsilon)
        {
            maxGapSeconds = FallbackMaxGapSeconds;
            explanationLines.Add(
                $"Alle Serien haben nur 1 Foto — MaxGapSeconds Fallback: {maxGapSeconds:F2}s");
        }

        var allTimedPhotos = series.SelectMany(s => s)
            .Where(p => p.CaptureTime.HasValue)
            .OrderBy(p => p.CaptureTime!.Value)
            .ToList();

        double medianGap = 0;
        double boundaryThreshold = FallbackBoundaryGapSeconds;

        if (allTimedPhotos.Count >= 2)
        {
            var gaps = new double[allTimedPhotos.Count - 1];
            for (int i = 1; i < allTimedPhotos.Count; i++)
                gaps[i - 1] = (allTimedPhotos[i].CaptureTime!.Value - allTimedPhotos[i - 1].CaptureTime!.Value).TotalSeconds;

            var sortedGaps = gaps.OrderBy(g => g).ToArray();
            medianGap = sortedGaps.Length % 2 == 1
                ? sortedGaps[sortedGaps.Length / 2]
                : (sortedGaps[sortedGaps.Length / 2 - 1] + sortedGaps[sortedGaps.Length / 2]) / 2.0;

            boundaryThreshold = medianGap < double.Epsilon
                ? FallbackBoundaryGapSeconds
                : medianGap * SeriesBoundaryFactor;
        }

        if (series.Count > 1)
        {
            var seriesSizes = string.Join(", ", series.Select(s => s.Count));
            explanationLines.Add(
                $"{series.Count} Serien erkannt in der Selektion ({seriesSizes} Fotos)");
            explanationLines.Add(
                $"Serien-Grenz-Threshold: {boundaryThreshold:F2}s " +
                $"(Median {medianGap:F2}s × {SeriesBoundaryFactor})");
            explanationLines.Add(
                $"Intra-Serien MaxGap: {maxGapSeconds:F2}s " +
                $"(größter Intra-Gap {intraSeriesMaxGap:F2}s × {GapBufferFactor})");
        }
        else
        {
            explanationLines.Add(
                $"1 Serie erkannt — MaxGapSeconds={maxGapSeconds:F2}s " +
                $"(größte Lücke {intraSeriesMaxGap:F2}s × {GapBufferFactor})");
        }

        minCount = Math.Max(AbsoluteMinCount, smallestSeriesCount);
        explanationLines.Add(
            $"MinCount={minCount} (max({AbsoluteMinCount}, kleinste Serie: {smallestSeriesCount}))");

        return new GroupingConfig
        {
            MaxGapSeconds = maxGapSeconds,
            MinCount = minCount
        };
    }
}
