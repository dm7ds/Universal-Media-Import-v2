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
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Utilities;

/// <summary>
/// Wrapper für ExifTool.exe - Lesen und Schreiben von EXIF-Metadaten.
/// </summary>
public class ExifToolWrapper : IExifToolWrapper
{
    private readonly string _exifToolPath;
    private readonly ILogger<ExifToolWrapper>? _logger;

    public ExifToolWrapper(string exifToolPath, ILogger<ExifToolWrapper>? logger = null)
    {
        _exifToolPath = exifToolPath;
        _logger = logger;

        if (!string.IsNullOrEmpty(_exifToolPath) && !File.Exists(_exifToolPath))
        {
            throw new FileNotFoundException($"ExifTool nicht gefunden: {_exifToolPath}");
        }
    }

    /// <inheritdoc />
    public bool IsAvailable => !string.IsNullOrEmpty(_exifToolPath);

    /// <summary>
    /// Liest Metadaten aus einer Datei (JSON-Format).
    /// </summary>
    public async Task<Dictionary<string, object?>> ReadMetadataAsync(string filePath, string[]? fields = null, CancellationToken ct = default)
    {
        var args = new List<string> { "-json", "-n" };

        if (fields != null && fields.Length > 0)
        {
            args.AddRange(fields.Select(f => $"-{f}"));
        }
        else
        {
            args.Add("-All");
        }

        args.Add(filePath);

        var output = await ExecuteAsync(args.ToArray(), ct);

        if (string.IsNullOrWhiteSpace(output))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            var jsonArray = JsonSerializer.Deserialize<JsonElement[]>(output);
            if (jsonArray == null || jsonArray.Length == 0)
            {
                return new Dictionary<string, object?>();
            }

            var result = new Dictionary<string, object?>();
            var firstElement = jsonArray[0];

            foreach (var property in firstElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => property.Value.ToString()
                };
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Fehler beim Parsen der ExifTool-Ausgabe");
            return new Dictionary<string, object?>();
        }
    }

    /// <summary>
    /// Schreibt Metadaten in eine Datei.
    /// </summary>
    public async Task<bool> WriteMetadataAsync(string filePath, Dictionary<string, object?> metadata, bool overwriteOriginal = true, CancellationToken ct = default)
    {
        var args = new List<string>();

        foreach (var (key, value) in metadata)
        {
            if (value != null)
            {
                args.Add($"-{key}={value}");
            }
        }

        if (overwriteOriginal)
        {
            args.Add("-overwrite_original");
        }

        args.Add("-m");
        args.Add(filePath);

        try
        {
            await ExecuteAsync(args.ToArray(), ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "ExifTool WriteMetadata failed for {Path}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Injiziert GPS-Daten aus einer GPX-Datei in ein Video.
    /// </summary>
    public async Task<bool> InjectGpsFromGpxAsync(string videoPath, string gpxPath, bool overwriteOriginal = true, CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "-geotag",
            gpxPath,
            "-geotime<CreateDate"
        };

        if (overwriteOriginal)
        {
            args.Add("-overwrite_original");
        }

        args.Add(videoPath);

        try
        {
            await ExecuteAsync(args.ToArray(), ct);
            _logger?.LogInformation("GPS injiziert: {Video} <- {Gpx}", Path.GetFileName(videoPath), Path.GetFileName(gpxPath));
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim GPS-Inject: {Video}", videoPath);
            return false;
        }
    }

    /// <summary>
    /// Kopiert alle Metadaten von einer Quelldatei in eine Zieldatei via ExifTool -TagsFromFile.
    /// </summary>
    public async Task<bool> CopyTagsFromFileAsync(string sourcePath, string destPath, bool overwriteOriginal = true, CancellationToken ct = default)
    {
        var args = new List<string>
        {
            $"-TagsFromFile",
            sourcePath,
            "-All:All"
        };

        if (overwriteOriginal)
        {
            args.Add("-overwrite_original");
        }

        args.Add(destPath);

        try
        {
            await ExecuteAsync(args.ToArray(), ct);
            _logger?.LogInformation("Tags kopiert: {Source} → {Dest}",
                Path.GetFileName(sourcePath), Path.GetFileName(destPath));
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Tag-Copy: {Source} → {Dest}", sourcePath, destPath);
            return false;
        }
    }

    /// <summary>
    /// Liest einen binären Tag aus einer Datei (z.B. -b -ThumbnailImage).
    /// Liest stdout als rohen Byte-Stream. Auf Windows können binäre Daten
    /// durch text-mode Pipes korrumpiert werden — der Caller muss JPEG-Magic validieren.
    /// </summary>
    public async Task<byte[]?> ReadBinaryTagAsync(string filePath, string tagName, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger?.LogDebug("ExifTool not configured — skipping binary tag read");
            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _exifToolPath,
            Arguments = $"-b -{tagName} \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger?.LogDebug("ExifTool binary: {Args}", startInfo.Arguments);

        ct.ThrowIfCancellationRequested();

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms, ct);

        var exited = process.WaitForExit(5000);
        if (!exited)
        {
            _logger?.LogWarning("ExifTool binary read timeout für {Tag} in {File}", tagName, filePath);
            try { process.Kill(); } catch (Exception ex) { _logger?.LogDebug(ex, "Failed to kill ExifTool process after timeout"); }
            return null;
        }

        var bytes = ms.ToArray();
        _logger?.LogDebug("ExifTool binary: {Tag} → {Bytes} Bytes", tagName, bytes.Length);
        return bytes.Length > 0 ? bytes : null;
    }

    /// <summary>
    /// Führt ExifTool mit den angegebenen Argumenten aus.
    /// </summary>
    private async Task<string> ExecuteAsync(string[] arguments, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger?.LogDebug("ExifTool not configured — skipping execution");
            return string.Empty;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _exifToolPath,
            Arguments = string.Join(" ", arguments.Select(arg =>
                arg.Contains(' ') ? $"\"{arg}\"" : arg)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        _logger?.LogDebug("ExifTool: {Args}", startInfo.Arguments);

        ct.ThrowIfCancellationRequested();

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            _logger?.LogWarning("ExifTool Warnung (Exit {Code}): {Error}", process.ExitCode, error.Trim());
        }

        return output;
    }
}
