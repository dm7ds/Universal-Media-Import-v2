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
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Utilities;

/// <summary>
/// Konvertiert DJI SRT-Dateien zu GPX-Dateien.
/// </summary>
public class SrtConverter
{
    private readonly ILogger<SrtConverter>? _logger;
    private static readonly XNamespace GpxNs = "http://www.topografix.com/GPX/1/1";
    private static readonly Regex GpsLinePattern = new(
        @"latitude\s*:\s*([-\d.]+).*?long(?:i|ti)tude\s*:\s*([-\d.]+).*?altitude\s*:\s*([-\d.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GpsCompactPattern = new(
        @"GPS\s*\(\s*([-\d.]+)\s*,\s*([-\d.]+)\s*,\s*([-\d.]+)\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimePattern = new(
        @"(\d{2}):(\d{2}):(\d{2}),(\d{3})",
        RegexOptions.Compiled);

    public SrtConverter(ILogger<SrtConverter>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Konvertiert eine SRT-Datei zu GPX.
    /// </summary>
    /// <param name="srtPath">Pfad zur SRT-Datei</param>
    /// <param name="outputPath">Pfad zur GPX-Datei (optional, default: gleicher Name mit .gpx)</param>
    public async Task<string?> ConvertSrtToGpxAsync(string srtPath, string? outputPath = null)
    {
        try
        {
            if (!File.Exists(srtPath))
            {
                _logger?.LogWarning("SRT-Datei nicht gefunden: {Path}", srtPath);
                return null;
            }

            var points = await ParseSrtFileAsync(srtPath);

            if (points.Count == 0)
            {
                _logger?.LogWarning("Keine GPS-Daten in SRT gefunden: {File}", Path.GetFileName(srtPath));
                return null;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.ChangeExtension(srtPath, ".gpx");
            }

            CreateGpxFile(points, outputPath);

            _logger?.LogInformation("SRT → GPX konvertiert: {Count} Punkte -> {File}",
                points.Count, Path.GetFileName(outputPath));

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler bei SRT-Konvertierung: {File}", Path.GetFileName(srtPath));
            return null;
        }
    }

    private async Task<List<GpsPoint>> ParseSrtFileAsync(string srtPath)
    {
        var points = new List<GpsPoint>();
        var lines = await File.ReadAllLinesAsync(srtPath);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            var match = GpsLinePattern.Match(line);
            if (!match.Success)
            {
                match = GpsCompactPattern.Match(line);
            }

            if (match.Success)
            {
                var lat = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var lon = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var alt = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

                DateTime timestamp = DateTime.UtcNow;
                if (i > 0)
                {
                    var timeLine = lines[i - 1].Trim();

                    var timeMatch = TimePattern.Match(timeLine);
                    if (timeMatch.Success)
                    {
                        var hours = int.Parse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                        var minutes = int.Parse(timeMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                        var seconds = int.Parse(timeMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                        var ms = int.Parse(timeMatch.Groups[4].Value, CultureInfo.InvariantCulture);

                        timestamp = DateTime.UtcNow.Date.Add(new TimeSpan(0, hours, minutes, seconds, ms));
                    }
                }

                points.Add(new GpsPoint
                {
                    Latitude = lat,
                    Longitude = lon,
                    Elevation = alt,
                    Time = timestamp
                });
            }
        }

        return points;
    }

    private void CreateGpxFile(List<GpsPoint> points, string outputPath)
    {
        var gpx = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(GpxNs + "gpx",
                new XAttribute("version", "1.1"),
                new XAttribute("creator", "UMI v2.0 - SRT Converter"),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XName.Get("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance"),
                    "http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd"),
                new XElement(GpxNs + "trk",
                    new XElement(GpxNs + "name", "Converted from SRT"),
                    new XElement(GpxNs + "trkseg",
                        points.Select(p => new XElement(GpxNs + "trkpt",
                            new XAttribute("lat", p.Latitude.ToString(CultureInfo.InvariantCulture)),
                            new XAttribute("lon", p.Longitude.ToString(CultureInfo.InvariantCulture)),
                            new XElement(GpxNs + "ele", p.Elevation.ToString(CultureInfo.InvariantCulture)),
                            new XElement(GpxNs + "time", p.Time.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
                        ))
                    )
                )
            )
        );

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        gpx.Save(outputPath);
    }

    private class GpsPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Elevation { get; set; }
        public DateTime Time { get; set; }
    }
}
