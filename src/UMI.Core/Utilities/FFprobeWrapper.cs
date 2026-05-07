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

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Utilities;

/// <summary>
/// Wrapper für ffprobe.exe - Video-Analyse und Integritätsprüfung.
/// </summary>
public class FFprobeWrapper
{
    private readonly string _ffprobePath;
    private readonly ILogger<FFprobeWrapper>? _logger;

    public FFprobeWrapper(string ffprobePath, ILogger<FFprobeWrapper>? logger = null)
    {
        _ffprobePath = ffprobePath;
        _logger = logger;

        if (!File.Exists(_ffprobePath))
        {
            throw new FileNotFoundException($"ffprobe nicht gefunden: {_ffprobePath}");
        }
    }

    /// <summary>
    /// Prüft Video-Integrität und gibt Informationen zurück.
    /// </summary>
    public async Task<VideoInfo?> GetVideoInfoAsync(string videoPath, CancellationToken ct = default)
    {
        if (!File.Exists(videoPath))
        {
            _logger?.LogWarning("Video nicht gefunden: {Path}", videoPath);
            return null;
        }

        var args = new[]
        {
            "-v", "error",
            "-select_streams", "v:0",
            "-show_entries", "stream=codec_name,duration,width,height,r_frame_rate",
            "-of", "json",
            videoPath
        };

        try
        {
            var output = await ExecuteAsync(args, ct);

            if (string.IsNullOrWhiteSpace(output))
                return null;

            var result = JsonSerializer.Deserialize<FFprobeResult>(output);

            if (result?.Streams == null || result.Streams.Length == 0)
                return null;

            var stream = result.Streams[0];

            return new VideoInfo
            {
                CodecName = stream.CodecName ?? "unknown",
                Duration = stream.Duration != null ? double.Parse(stream.Duration, CultureInfo.InvariantCulture) : 0,
                Width = stream.Width ?? 0,
                Height = stream.Height ?? 0,
                FrameRate = ParseFrameRate(stream.RFrameRate),
                IsValid = !string.IsNullOrEmpty(stream.CodecName)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler bei ffprobe-Analyse: {Video}", videoPath);
            return null;
        }
    }

    private double ParseFrameRate(string? frameRate)
    {
        if (string.IsNullOrEmpty(frameRate))
            return 0;

        var parts = frameRate.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var denominator) &&
            denominator != 0)
        {
            return numerator / denominator;
        }

        return 0;
    }

    private async Task<string> ExecuteAsync(string[] arguments, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = string.Join(" ", arguments.Select(arg =>
                arg.Contains(' ') ? $"\"{arg}\"" : arg)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };

        var output = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                output.AppendLine(e.Data);
        };

        ct.ThrowIfCancellationRequested();

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        return output.ToString();
    }

    private class FFprobeResult
    {
        public FFprobeStream[]? Streams { get; set; }
    }

    private class FFprobeStream
    {
        [JsonPropertyName("codec_name")]
        public string? CodecName { get; set; }

        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("r_frame_rate")]
        public string? RFrameRate { get; set; }
    }
}

public class VideoInfo
{
    public string CodecName { get; set; } = string.Empty;
    public double Duration { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public bool IsValid { get; set; }
}
