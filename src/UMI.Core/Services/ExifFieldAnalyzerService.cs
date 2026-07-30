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

using MetadataExtractor;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service zum Analysieren von EXIF-Feldern in Foto-Ordnern.
/// Findet Felder die in 100% der Bilder vorhanden + befüllt sind.
/// </summary>
public interface IExifFieldAnalyzerService
{
    /// <summary>
    /// Scannt alle Fotos im Ordner, findet Felder die in ALLEN Bildern vorhanden + befüllt sind.
    /// Gibt kategorisierte Felder mit Beispiel-Wert (vom ersten Bild) zurück.
    /// </summary>
    Task<ExifAnalysisResult> AnalyzeFolderAsync(
        string folderPath,
        IProgress<ExifScanProgress>? progress = null,
        CancellationToken ct = default);
}

public class ExifFieldAnalyzerService : IExifFieldAnalyzerService
{
    private readonly ILogger<ExifFieldAnalyzerService>? _logger;

    public ExifFieldAnalyzerService(ILogger<ExifFieldAnalyzerService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<ExifAnalysisResult> AnalyzeFolderAsync(
        string folderPath,
        IProgress<ExifScanProgress>? progress = null,
        CancellationToken ct = default)
    {

        var photos = System.IO.Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !FolderNameConstants.IsInternalPath(f))
            .Where(FileExtensions.IsPhoto)
            .ToList();

        if (photos.Count == 0)
            return new ExifAnalysisResult { TotalPhotos = 0, FieldGroups = new() };

        var allPhotoFields = new List<Dictionary<string, (string Value, string Directory)>>();

        await Task.Run(() =>
        {
            for (int i = 0; i < photos.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new ExifScanProgress(i + 1, photos.Count, Path.GetFileName(photos[i])));

                try
                {
                    var fields = ReadAllExifFields(photos[i]);
                    allPhotoFields.Add(fields);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("EXIF-Fehler bei {File}: {Error}", photos[i], ex.Message);

                }
            }
        }, ct);

        if (allPhotoFields.Count == 0)
            return new ExifAnalysisResult { TotalPhotos = photos.Count, FieldGroups = new() };

        var intersection = FindFieldIntersection(allPhotoFields);

        var groups = CategorizeFields(intersection, allPhotoFields[0]);

        return new ExifAnalysisResult
        {
            TotalPhotos = allPhotoFields.Count,
            FieldGroups = groups
        };
    }

    /// <summary>
    /// Liest ALLE EXIF-Tags eines Fotos via MetadataExtractor.
    /// Key = TagName, Value = (Description, DirectoryName)
    /// </summary>
    private Dictionary<string, (string Value, string Directory)> ReadAllExifFields(string filePath)
    {
        var result = new Dictionary<string, (string Value, string Directory)>();

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    var description = tag.Description;
                    if (string.IsNullOrWhiteSpace(description))
                        continue;

                    var key = tag.Name;
                    if (result.ContainsKey(key))
                    {

                        key = $"{directory.Name}.{tag.Name}";
                    }

                    result[key] = (description, directory.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("MetadataExtractor Fehler bei {File}: {Error}", filePath, ex.Message);
            throw;
        }

        return result;
    }

    /// <summary>
    /// Findet Felder die in ALLEN Bildern vorhanden und nicht-leer sind.
    /// </summary>
    private HashSet<string> FindFieldIntersection(List<Dictionary<string, (string Value, string Directory)>> allPhotos)
    {
        if (allPhotos.Count == 0)
            return new HashSet<string>();

        var intersection = new HashSet<string>(allPhotos[0].Keys);

        for (int i = 1; i < allPhotos.Count; i++)
        {
            var currentKeys = allPhotos[i].Keys.ToHashSet();
            intersection.IntersectWith(currentKeys);

            if (intersection.Count == 0)
                break;
        }

        var toRemove = new List<string>();
        foreach (var key in intersection)
        {
            bool hasInvalidValueInAny = allPhotos.Any(photo =>
            {
                var (value, _) = photo[key];
                return string.IsNullOrWhiteSpace(value) ||
                       value.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                       value.Equals("(none)", StringComparison.OrdinalIgnoreCase) ||
                       value.Equals("n/a", StringComparison.OrdinalIgnoreCase);
            });

            if (hasInvalidValueInAny)
                toRemove.Add(key);
        }

        foreach (var key in toRemove)
            intersection.Remove(key);

        return intersection;
    }

    /// <summary>
    /// Ordnet Felder in Kategorien ein (Shooting, Exposure, Focus, Camera, Image, Time, File).
    /// </summary>
    private List<ExifFieldGroup> CategorizeFields(
        HashSet<string> fieldNames,
        Dictionary<string, (string Value, string Directory)> samplePhoto)
    {
        var categories = new Dictionary<string, List<ExifFieldInfo>>
        {
            ["Shooting"] = new(),
            ["Exposure"] = new(),
            ["Focus"] = new(),
            ["Camera"] = new(),
            ["Image"] = new(),
            ["Time"] = new(),
            ["File"] = new(),
            ["Other"] = new()
        };

        foreach (var fieldName in fieldNames)
        {
            if (!samplePhoto.TryGetValue(fieldName, out var fieldData))
                continue;

            var (value, directory) = fieldData;
            var category = DetermineCategory(fieldName, directory);

            double? numericValue = ExifValueParser.TryParseNumericValue(value);

            var fieldInfo = new ExifFieldInfo
            {
                FieldName = fieldName,
                Directory = directory,
                Category = category,
                SampleValue = value,
                NumericValue = numericValue,
                IsPresentInAll = true
            };

            categories[category].Add(fieldInfo);
        }

        return categories
            .Where(kvp => kvp.Value.Count > 0)
            .Select(kvp => new ExifFieldGroup
            {
                Category = kvp.Key,
                Fields = kvp.Value.OrderBy(f => f.FieldName).ToList()
            })
            .OrderBy(g => GetCategoryOrder(g.Category))
            .ToList();
    }

    /// <summary>
    /// Bestimmt die Kategorie basierend auf Tag-Name und Directory.
    /// </summary>
    private string DetermineCategory(string tagName, string directory)
    {
        var tagLower = tagName.ToLowerInvariant();
        var dirLower = directory.ToLowerInvariant();

        if (dirLower.Contains("camera settings") ||
            dirLower.Contains("makernote") ||
            tagLower.Contains("drive") ||
            tagLower.Contains("shooting") ||
            tagLower.Contains("continuous") ||
            tagLower.Contains("burst"))
            return "Shooting";

        if (tagLower.Contains("exposure") ||
            tagLower.Contains("shutter") ||
            tagLower.Contains("iso") ||
            tagLower.Contains("aperture") ||
            tagLower.Contains("f-number") ||
            tagLower.Contains("f number") ||
            tagLower.Contains("ev") ||
            tagLower.Contains("compensation"))
            return "Exposure";

        if (tagLower.Contains("focus") ||
            tagLower.Contains("af ") ||
            tagLower.Contains("autofocus"))
            return "Focus";

        if (tagLower.Contains("make") ||
            tagLower.Contains("model") ||
            tagLower.Contains("lens") ||
            tagLower.Contains("serial") ||
            tagLower.Contains("firmware") ||
            tagLower.Contains("software"))
            return "Camera";

        if (tagLower.Contains("width") ||
            tagLower.Contains("height") ||
            tagLower.Contains("resolution") ||
            tagLower.Contains("orientation") ||
            tagLower.Contains("color") ||
            tagLower.Contains("bits"))
            return "Image";

        if (tagLower.Contains("date") ||
            tagLower.Contains("time") ||
            tagLower.Contains("timestamp"))
            return "Time";

        if (dirLower.Contains("file") ||
            tagLower.Contains("file") ||
            tagLower.Contains("size"))
            return "File";

        return "Other";
    }

    /// <summary>
    /// Sortier-Order für Kategorien.
    /// </summary>
    private int GetCategoryOrder(string category)
    {
        return category switch
        {
            "Shooting" => 1,
            "Exposure" => 2,
            "Focus" => 3,
            "Camera" => 4,
            "Image" => 5,
            "Time" => 6,
            "File" => 7,
            "Other" => 99,
            _ => 100
        };
    }
}
