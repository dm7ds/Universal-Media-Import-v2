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

using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service für GPS Smart-Matching und Optimization.
/// Nutzt Hybrid-Strategie: Mp4Parser (schnell) mit ExifTool-Fallback.
/// </summary>
public class GpsService
{
    private readonly IExifToolWrapper _exifTool;
    private readonly IMp4Parser _mp4Parser;
    private readonly MetadataReader _metadataReader;
    private readonly GpsProcessingConfig _config;
    private readonly GlobalPaths _globalPaths;
    private readonly ILogger<GpsService>? _logger;

    private static readonly XNamespace GpxNs = "http://www.topografix.com/GPX/1/1";
    private const string GpxCreator = "UMI v2.0";

    public GpsService(
        IExifToolWrapper exifTool,
        IMp4Parser mp4Parser,
        GpsProcessingConfig config,
        GlobalPaths globalPaths,
        ILogger<GpsService>? logger = null,
        MetadataReader? metadataReader = null)
    {
        _exifTool = exifTool;
        _mp4Parser = mp4Parser;
        _metadataReader = metadataReader ?? new MetadataReader(null);
        _config = config;
        _globalPaths = globalPaths;
        _logger = logger;
    }

    /// <summary>
    /// Optimiert GPS für ein Video (Smart-Matching).
    /// </summary>
    public async Task<string?> OptimizeGpsForVideoAsync(string videoPath, string gpxDirectory, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogDebug("GPS-Matching für Video: {File}", Path.GetFileName(videoPath));

            DateTime startTime;
            TimeSpan duration;
            string source;

            var nativeMetadata = await _mp4Parser.ReadMetadataAsync(videoPath);

            if (nativeMetadata.IsValid && nativeMetadata.CreateDate.HasValue)
            {

                startTime = nativeMetadata.CreateDate.Value;
                duration = nativeMetadata.Duration;
                source = "MP4 nativ";

                _logger?.LogDebug("Metadata (nativ MP4): CreateDate={CreateDate} UTC, Duration={Duration:F1}s",
                    startTime.ToString("yyyy-MM-dd HH:mm:ss"), duration.TotalSeconds);
            }
            else
            {

                _logger?.LogDebug("MP4-Parsing fehlgeschlagen, Fallback auf ExifTool für {File}",
                    Path.GetFileName(videoPath));

                (startTime, duration) = await ReadMetadataViaExifToolAsync(videoPath, ct);

                if (startTime == DateTime.MinValue)
                {
                    _logger?.LogWarning("Kein CreateDate aus ExifTool für {File}", Path.GetFileName(videoPath));
                    return null;
                }

                source = "ExifTool";
            }

            var endTime = startTime + duration;

            var effectiveBuffer = duration == TimeSpan.Zero
                ? TimeSpan.FromHours(2).TotalSeconds
                : _config.TimeBufferSeconds;

            _logger?.LogDebug(
                "Video-Zeitfenster ({Source}): {Start:yyyy-MM-dd HH:mm:ss} - {End:HH:mm:ss} UTC (Duration: {Duration:F1}s, Buffer: ±{Buffer}s)",
                source, startTime, endTime, duration.TotalSeconds, effectiveBuffer);

            var gpxFiles = Directory.GetFiles(gpxDirectory, "*.gpx");

            if (gpxFiles.Length == 0)
            {
                _logger?.LogWarning("Keine GPX-Dateien im Verzeichnis: {Dir}", gpxDirectory);
                return null;
            }

            _logger?.LogDebug("Scanne {Count} GPX-Dateien für Video-Zeitfenster", gpxFiles.Length);

            var overlappingTracks = new List<string>();
            var nearestTrack = (File: "", TimeDiff: TimeSpan.MaxValue);

            var videoStart = startTime.AddSeconds(-effectiveBuffer);
            var videoEnd = endTime.AddSeconds(effectiveBuffer);

            foreach (var gpxFile in gpxFiles)
            {
                var fileName = Path.GetFileName(gpxFile);

                var timeRange = GetGpxTimeRange(gpxFile);
                if (timeRange == null)
                {
                    _logger?.LogDebug("  ⏩ {File}: Keine Zeitstempel (überspringe)", fileName);
                    continue;
                }

                var overlaps = timeRange.Value.End >= videoStart && timeRange.Value.Start <= videoEnd;

                if (overlaps)
                {
                    overlappingTracks.Add(gpxFile);
                    _logger?.LogDebug(
                        "  ✓ {File}: {Start:yyyy-MM-dd HH:mm:ss} - {End:HH:mm:ss} UTC → MATCH",
                        fileName, timeRange.Value.Start, timeRange.Value.End);
                }
                else
                {

                    var timeDiffStart = (timeRange.Value.Start - videoEnd).Duration();
                    var timeDiffEnd = (videoStart - timeRange.Value.End).Duration();
                    var minDiff = timeDiffStart < timeDiffEnd ? timeDiffStart : timeDiffEnd;

                    if (minDiff < nearestTrack.TimeDiff)
                    {
                        nearestTrack = (fileName, minDiff);
                    }

                    _logger?.LogDebug(
                        "  ✗ {File}: {Start:yyyy-MM-dd HH:mm:ss} - {End:HH:mm:ss} UTC (Abstand: {Diff})",
                        fileName, timeRange.Value.Start, timeRange.Value.End,
                        minDiff < TimeSpan.FromHours(1)
                            ? $"{minDiff.TotalMinutes:F1} min"
                            : $"{minDiff.TotalHours:F1} h");
                }
            }

            if (overlappingTracks.Count == 0)
            {
                if (nearestTrack.TimeDiff < TimeSpan.MaxValue)
                {
                    _logger?.LogWarning(
                        "Keine überlappenden GPX-Tracks. Nächster Track: {File} (Abstand: {Diff})",
                        nearestTrack.File,
                        nearestTrack.TimeDiff < TimeSpan.FromHours(1)
                            ? $"{nearestTrack.TimeDiff.TotalMinutes:F1} min"
                            : $"{nearestTrack.TimeDiff.TotalHours:F1} h");
                }
                else
                {
                    _logger?.LogWarning("Keine überlappenden GPX-Tracks gefunden (Video: {Start:yyyy-MM-dd HH:mm:ss} UTC)",
                        startTime);
                }

                return null;
            }

            var allPoints = new List<GpsPoint>();

            foreach (var track in overlappingTracks)
            {

                var points = ExtractGpsPoints(track, startTime, endTime, (int)effectiveBuffer);
                allPoints.AddRange(points);
            }

            if (allPoints.Count < _config.MinPointsThreshold)
            {
                _logger?.LogWarning("Zu wenig GPS-Punkte: {Count}", allPoints.Count);
                return null;
            }

            allPoints = allPoints.OrderBy(p => p.Time).ToList();

            var optimizedPath = PathHelper.GetUmiPath(_globalPaths.Workbench, videoPath, FolderNameConstants.UmiSubDir.Gps, FolderNameConstants.OptimizedGpxSuffix);

            var gpsDir = Path.GetDirectoryName(optimizedPath);
            Directory.CreateDirectory(gpsDir!);

            CreateOptimizedGpx(allPoints, optimizedPath);

            _logger?.LogDebug("✓ GPS optimiert: {Count} Punkte -> {File}",
                allPoints.Count, Path.GetFileName(optimizedPath));

            return optimizedPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler bei GPS-Optimization: {Video}", videoPath);
            return null;
        }
    }

    /// <summary>
    /// Injiziert optimiertes GPS in Video.
    /// </summary>
    public async Task<bool> InjectOptimizedGpsAsync(string videoPath, string gpxDirectory, CancellationToken ct = default)
    {
        var optimizedGpx = await OptimizeGpsForVideoAsync(videoPath, gpxDirectory, ct);

        if (optimizedGpx == null)
            return false;

        var success = await _exifTool.InjectGpsFromGpxAsync(videoPath, optimizedGpx, overwriteOriginal: true, ct);

        if (success && !_config.KeepOptimizedGpx)
        {
            try
            {
                File.Delete(optimizedGpx);
            }
            catch {  }
        }

        return success;
    }

    private (DateTime Start, DateTime End)? GetGpxTimeRange(string gpxPath)
    {
        try
        {
            var doc = XDocument.Load(gpxPath);
            var times = doc.Descendants(GpxNs + "time")
                .Select(e => DateTime.Parse(e.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal))
                .OrderBy(t => t)
                .ToArray();

            if (times.Length == 0)
                return null;

            return (times.First(), times.Last());
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Lesen der GPX-Zeitspanne: {File}", Path.GetFileName(gpxPath));
            return null;
        }
    }

    private List<GpsPoint> ExtractGpsPoints(string gpxPath, DateTime start, DateTime end, int bufferSeconds)
    {
        var points = new List<GpsPoint>();

        try
        {
            var startWithBuffer = start.AddSeconds(-bufferSeconds);
            var endWithBuffer = end.AddSeconds(bufferSeconds);

            var doc = XDocument.Load(gpxPath);
            var trackPoints = doc.Descendants(GpxNs + "trkpt");

            foreach (var trkpt in trackPoints)
            {
                var timeElem = trkpt.Element(GpxNs + "time");
                var eleElem = trkpt.Element(GpxNs + "ele");

                if (timeElem == null)
                    continue;

                var time = DateTime.Parse(timeElem.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

                if (time >= startWithBuffer && time <= endWithBuffer)
                {

                    var lat = double.Parse(trkpt.Attribute("lat")?.Value ?? "0", CultureInfo.InvariantCulture);
                    var lon = double.Parse(trkpt.Attribute("lon")?.Value ?? "0", CultureInfo.InvariantCulture);
                    var ele = eleElem != null ? double.Parse(eleElem.Value, CultureInfo.InvariantCulture) : 0;

                    points.Add(new GpsPoint
                    {
                        Latitude = lat,
                        Longitude = lon,
                        Elevation = ele,
                        Time = time
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim GPX-Parsing: {File}", Path.GetFileName(gpxPath));
        }

        return points;
    }

    private void CreateOptimizedGpx(List<GpsPoint> points, string outputPath)
    {
        var gpx = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(GpxNs + "gpx",
                new XAttribute("version", "1.1"),
                new XAttribute("creator", GpxCreator),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XName.Get("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance"),
                    "http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd"),
                new XElement(GpxNs + "trk",
                    new XElement(GpxNs + "name", "Optimized Track"),
                    new XElement(GpxNs + "trkseg",
                        points.Select(p => new XElement(GpxNs + "trkpt",
                            new XAttribute("lat", p.Latitude),
                            new XAttribute("lon", p.Longitude),
                            new XElement(GpxNs + "ele", p.Elevation),
                            new XElement(GpxNs + "time", p.Time.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                        ))
                    )
                )
            )
        );

        gpx.Save(outputPath);
    }

    /// <summary>
    /// Liest CreateDate und Duration via ExifTool (Fallback für Nicht-MP4 oder Parse-Fehler).
    /// </summary>
    private async Task<(DateTime StartTime, TimeSpan Duration)> ReadMetadataViaExifToolAsync(string videoPath, CancellationToken ct = default)
    {
        try
        {

            var metadata = await _exifTool.ReadMetadataAsync(videoPath,
                new[] { "CreateDate", "GPSDateTime", "Duration", "MediaDuration" }, ct);

            DateTime startTime;
            string timezoneStrategy;

            if (metadata.TryGetValue("GPSDateTime", out var gpsDateObj) && gpsDateObj != null)
            {

                var gpsDateStr = gpsDateObj.ToString()!;
                var gpsDate = ParseExifDate(gpsDateStr);

                if (gpsDate != null)
                {
                    startTime = DateTime.SpecifyKind(gpsDate.Value, DateTimeKind.Utc);
                    timezoneStrategy = "GPSDateTime (UTC)";
                    _logger?.LogDebug("Zeitstempel-Quelle: GPSDateTime (UTC) = {Date}", gpsDateStr);
                }
                else
                {
                    _logger?.LogWarning("GPSDateTime konnte nicht geparst werden: {Value}", gpsDateStr);
                    return (DateTime.MinValue, TimeSpan.Zero);
                }
            }
            else
            {

                string? createDateStr = null;
                if (metadata.TryGetValue("CreateDate", out var createDateObj) && createDateObj != null)
                {
                    createDateStr = createDateObj.ToString();
                    if (!string.IsNullOrEmpty(createDateStr))
                    {
                        _logger?.LogDebug("CreateDate gefunden: {Date}", createDateStr);
                    }
                }

                if (string.IsNullOrEmpty(createDateStr))
                {
                    _logger?.LogWarning("Kein CreateDate oder GPSDateTime in Video: {File}", Path.GetFileName(videoPath));
                    return (DateTime.MinValue, TimeSpan.Zero);
                }

                var createDate = ParseExifDate(createDateStr);
                if (createDate == null)
                {
                    _logger?.LogWarning("CreateDate konnte nicht geparst werden: {Value}", createDateStr);
                    return (DateTime.MinValue, TimeSpan.Zero);
                }

                var fileInfo = new FileInfo(videoPath);
                var fileNameDate = _metadataReader.ExtractDateFromFile(fileInfo);

                var diffAsLocal = Math.Abs((createDate.Value - fileNameDate).TotalMinutes);
                var diffAsUtc = Math.Abs((createDate.Value - fileNameDate.ToUniversalTime()).TotalMinutes);

                if (diffAsLocal <= 2)
                {

                    var localTime = DateTime.SpecifyKind(createDate.Value, DateTimeKind.Local);
                    startTime = localTime.ToUniversalTime();
                    timezoneStrategy = "CreateDate ist Lokalzeit (passt zu DJI-Dateiname), konvertiere zu UTC";
                    _logger?.LogDebug("Zeitstempel: CreateDate={CreateDate}, Dateiname={FileName}, Diff={DiffMin:F1}min → Lokalzeit",
                        createDateStr, fileNameDate, diffAsLocal);
                }
                else if (diffAsUtc <= 2)
                {

                    startTime = DateTime.SpecifyKind(createDate.Value, DateTimeKind.Utc);
                    timezoneStrategy = "CreateDate ist bereits UTC (Offset zu DJI-Dateiname = Timezone)";
                    _logger?.LogDebug("Zeitstempel: CreateDate={CreateDate}, Dateiname={FileName}, Diff zu UTC={DiffMin:F1}min → bereits UTC",
                        createDateStr, fileNameDate, diffAsUtc);
                }
                else
                {

                    startTime = DateTime.SpecifyKind(createDate.Value, DateTimeKind.Utc);
                    timezoneStrategy = "CreateDate-Timezone unklar, behandle als UTC (ISO-Standard)";
                    _logger?.LogWarning("CreateDate-Timezone unklar. CreateDate={CreateDate}, Dateiname={FileName}, DiffLocal={DiffLocal:F1}min, DiffUtc={DiffUtc:F1}min → ISO-Standard (UTC)",
                        createDateStr, fileNameDate, diffAsLocal, diffAsUtc);
                }

                _logger?.LogDebug("Zeitstempel-Quelle: {Strategy} = {Utc} UTC",
                    timezoneStrategy, startTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            TimeSpan duration = TimeSpan.Zero;
            foreach (var key in new[] { "Duration", "MediaDuration" })
            {
                if (metadata.TryGetValue(key, out var durObj) && durObj != null)
                {
                    duration = ParseDuration(durObj.ToString());
                    if (duration > TimeSpan.Zero)
                    {
                        _logger?.LogDebug("Duration gefunden als {Key}: {Value}", key, durObj);
                        break;
                    }
                }
            }

            _logger?.LogDebug("ExifTool Metadata: {Strategy}, Duration={Duration:F1}s",
                timezoneStrategy, duration.TotalSeconds);

            return (startTime, duration);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim ExifTool-Metadata-Reading: {File}", Path.GetFileName(videoPath));
            return (DateTime.MinValue, TimeSpan.Zero);
        }
    }

    private DateTime? ParseExifDate(string dateStr)
    {

        if (DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var result))
        {
            return result;
        }

        return null;
    }

    internal TimeSpan ParseDuration(string? durationStr)
    {
        if (string.IsNullOrEmpty(durationStr))
            return TimeSpan.Zero;

        try
        {
            var trimmed = durationStr.Trim();

            if (trimmed.Contains(':'))
            {
                var parts = trimmed.Split(':');

                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                    {
                        return TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(seconds));
                    }
                }

                else if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out var ts))
                {
                    return ts;
                }
            }

            else if (trimmed.EndsWith(" s", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = trimmed.Replace(" s", "").Replace("s", "");
                if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                    return TimeSpan.FromSeconds(seconds);
            }

            else if (trimmed.EndsWith(" min", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = trimmed.Replace(" min", "").Replace("min", "");
                if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
                    return TimeSpan.FromMinutes(minutes);
            }

            else if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var secs))
            {
                return TimeSpan.FromSeconds(secs);
            }

            _logger?.LogWarning("Unbekanntes Duration-Format: {Duration}", durationStr);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Parsen der Duration: {Duration}", durationStr);
        }

        return TimeSpan.Zero;
    }

    private class GpsPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Elevation { get; set; }
        public DateTime Time { get; set; }
    }
}
