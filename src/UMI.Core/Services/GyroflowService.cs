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
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;

namespace UMI.Core.Services;

/// <summary>
/// Service für parallele Gyroflow-Verarbeitung.
/// </summary>
public class GyroflowService : IGyroflowService
{
    /// <summary>
    /// Gyroflow hängt IMMER "_stabilized" an den Input-Dateinamen an,
    /// unabhängig von output_filename. output_filename wird von Gyroflow CLI ignoriert.
    /// Wir setzen output_filename="" und lassen Gyroflow seinen Standard-Namen generieren,
    /// dann rename zu outputPath.
    /// </summary>
    private const string GyroflowOutputSuffix = "_stabilized";

    private readonly string _gyroflowPath;
    private readonly GyroflowConfig _config;
    private readonly ILogger<GyroflowService>? _logger;

    public GyroflowService(
        string gyroflowPath,
        GyroflowConfig config,
        ILogger<GyroflowService>? logger = null)
    {
        _gyroflowPath = gyroflowPath;
        _config = config;
        _logger = logger;

        if (!File.Exists(_gyroflowPath))
        {
            throw new FileNotFoundException($"Gyroflow nicht gefunden: {_gyroflowPath}");
        }
    }

    /// <summary>
    /// Stabilisiert ein einzelnes Video.
    /// </summary>
    public async Task<bool> StabilizeVideoAsync(
        string inputPath,
        string outputPath,
        string? presetPath = null,
        string gpuDevice = "nvidia",
        IProgress<GyroflowRenderProgress>? renderProgress = null,
        CancellationToken cancellationToken = default)
    {
        string? effectivePreset = null;

        try
        {

            inputPath = Path.GetFullPath(inputPath);
            outputPath = Path.GetFullPath(outputPath);
            if (presetPath != null) presetPath = Path.GetFullPath(presetPath);

            _logger?.LogDebug("Gyroflow Input:  {InputPath}", inputPath);
            _logger?.LogDebug("Gyroflow Output: {OutputPath}", outputPath);

            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            if (!string.IsNullOrEmpty(presetPath) && File.Exists(presetPath))
            {
                try
                {
                    var presetJson = await File.ReadAllTextAsync(presetPath, cancellationToken);
                    var preset = System.Text.Json.JsonDocument.Parse(presetJson);

                    var outputDirectory = Path.GetDirectoryName(outputPath) ?? "";

                    var outputDirWithSlash = outputDirectory.TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
                    var outputFolderUri = new Uri(outputDirWithSlash).AbsoluteUri;

                    var root = preset.RootElement.Clone();
                    using var stream = new MemoryStream();
                    using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();
                        bool hasOutput = false;

                        foreach (var prop in root.EnumerateObject())
                        {
                            if (prop.Name == "output")
                            {
                                hasOutput = true;

                                writer.WritePropertyName("output");
                                writer.WriteStartObject();

                                foreach (var outputProp in prop.Value.EnumerateObject())
                                {
                                    if (outputProp.Name == "output_filename" || outputProp.Name == "output_folder")
                                        continue;
                                    outputProp.WriteTo(writer);
                                }

                                writer.WriteString("output_filename", "");
                                writer.WriteString("output_folder", outputFolderUri);

                                writer.WriteEndObject();
                            }
                            else
                            {
                                prop.WriteTo(writer);
                            }
                        }

                        if (!hasOutput)
                        {
                            writer.WritePropertyName("output");
                            writer.WriteStartObject();
                            writer.WriteString("output_filename", "");
                            writer.WriteString("output_folder", outputFolderUri);
                            writer.WriteEndObject();
                            _logger?.LogWarning("Preset hatte keinen output-Block – wurde angelegt");
                        }

                        writer.WriteEndObject();
                    }

                    var tempPreset = Path.Combine(Path.GetTempPath(), $"gyroflow_{Guid.NewGuid()}.gyroflow");
                    await File.WriteAllBytesAsync(tempPreset, stream.ToArray(), cancellationToken);

                    if (!File.Exists(tempPreset))
                    {
                        throw new InvalidOperationException($"Temp-Preset konnte nicht geschrieben werden: {tempPreset}");
                    }

                    try
                    {
                        var tempPresetContent = await File.ReadAllTextAsync(tempPreset, cancellationToken);
                        var tempPresetDoc = JsonDocument.Parse(tempPresetContent);

                        if (!tempPresetDoc.RootElement.TryGetProperty("output", out var outputBlockProp))
                        {
                            throw new InvalidOperationException($"Temp-Preset hat keinen 'output'-Block: {tempPreset}");
                        }

                        if (!outputBlockProp.TryGetProperty("output_filename", out _) ||
                            !outputBlockProp.TryGetProperty("output_folder", out _))
                        {
                            throw new InvalidOperationException($"Temp-Preset 'output'-Block unvollständig (output_filename/output_folder fehlt): {tempPreset}");
                        }

                        var outputBlockJson = outputBlockProp.GetRawText();
                        _logger?.LogDebug("Temp-Preset output-Block (wird an Gyroflow übergeben): {OutputBlock}", outputBlockJson);
                        _logger?.LogDebug("Temp-Preset: output_filename=\"\" (Gyroflow ignoriert dieses Feld), output_folder={Folder}", outputFolderUri);
                    }
                    catch (JsonException jex)
                    {
                        throw new InvalidOperationException($"Temp-Preset ist kein gültiges JSON: {tempPreset}", jex);
                    }

                    effectivePreset = tempPreset;
                    _logger?.LogDebug("Temp-Preset Pfad: {TempPreset}", tempPreset);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "FEHLER beim Preset-Parsing für {InputPath}!", inputPath);
                    throw new InvalidOperationException($"Gyroflow-Preset konnte nicht verarbeitet werden: {presetPath}", ex);
                }
            }

            if (!string.IsNullOrEmpty(presetPath) && effectivePreset == null)
            {
                _logger?.LogError("KRITISCH: Preset {PresetPath} konnte nicht geladen werden!", presetPath);
                throw new InvalidOperationException($"Gyroflow-Preset konnte nicht geladen werden: {presetPath}");
            }

            var args = new List<string>
            {
                inputPath
            };

            if (effectivePreset != null)
            {
                args.Add("--preset");
                args.Add(effectivePreset);
            }

            if (!string.IsNullOrEmpty(gpuDevice))
            {
                args.Add("-r");
                args.Add(gpuDevice);
            }

            args.Add("--stdout-progress");

            var startInfo = new ProcessStartInfo
            {
                FileName = _gyroflowPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var debugCommand = $"{_gyroflowPath} {string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a))}";
            _logger?.LogDebug("Gyroflow Command: {Command}", debugCommand);

            _logger?.LogDebug("Stabilisiere: {File}", Path.GetFileName(inputPath));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(_config.TimeoutMinutes));

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            using var killOnCancel = cts.Token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        _logger?.LogWarning("Gyroflow killed on cancel: {File}", Path.GetFileName(inputPath));
                    }
                }
                catch (InvalidOperationException) { }
            });

            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            var stdoutLines = new List<string>();
            var progressRegex = new Regex(
                @"Rendering progress:\s*(\d+)/(\d+)\s+frames\s+\([\d.]+%\)\s+ETA\s+(.+)",
                RegexOptions.Compiled);

            var fileName = Path.GetFileName(inputPath);

            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(cts.Token);
                if (line == null) break;

                cts.CancelAfter(TimeSpan.FromMinutes(_config.TimeoutMinutes));

                stdoutLines.Add(line);

                if (renderProgress != null)
                {
                    var match = progressRegex.Match(line);
                    if (match.Success)
                    {
                        var cur = int.Parse(match.Groups[1].Value);
                        var tot = int.Parse(match.Groups[2].Value);
                        renderProgress.Report(new GyroflowRenderProgress
                        {
                            CurrentFrame = cur,
                            TotalFrames  = tot,
                            Percent      = tot > 0 ? (double)cur / tot * 100 : 0,
                            Eta          = match.Groups[3].Value.Trim(),
                            FileName     = fileName,
                        });
                    }
                }
            }

            var stderr = await stderrTask;
            var stdout = string.Join(Environment.NewLine, stdoutLines);

            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode == 0)
            {

                var inputBaseName = Path.GetFileNameWithoutExtension(inputPath);
                var extension = Path.GetExtension(inputPath);
                var gyroflowFilename = $"{inputBaseName}{GyroflowOutputSuffix}{extension}";

                var expectedInOutputDir = Path.Combine(outputDir!, gyroflowFilename);
                var inputDir = Path.GetDirectoryName(inputPath) ?? "";
                var expectedInInputDir = Path.Combine(inputDir, gyroflowFilename);

                string? actualOutput = null;

                if (File.Exists(expectedInOutputDir))
                {
                    actualOutput = expectedInOutputDir;
                    _logger?.LogDebug("Gyroflow Output im Stabilized-Ordner: {File}", actualOutput);
                }
                else if (File.Exists(expectedInInputDir))
                {
                    actualOutput = expectedInInputDir;
                    _logger?.LogWarning("Gyroflow hat output_folder ignoriert, Output neben Input: {File}", actualOutput);
                }
                else
                {
                    _logger?.LogError("Gyroflow Exit 0, aber Output fehlt: {Expected} | {Fallback}",
                        expectedInOutputDir, expectedInInputDir);
                    throw new FileNotFoundException($"Gyroflow Output fehlt: {expectedInOutputDir}");
                }

                const long MinValidSize = 1024;
                var actualFileInfo = new FileInfo(actualOutput);

                if (actualFileInfo.Length < MinValidSize)
                {
                    _logger?.LogError("Gyroflow Output zu klein: {Size} bytes", actualFileInfo.Length);
                    throw new InvalidOperationException($"Gyroflow Output zu klein: {actualFileInfo.Length} bytes");
                }

                if (!string.Equals(actualOutput, outputPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    File.Move(actualOutput, outputPath);
                    _logger?.LogDebug("Renamed: {From} → {To}", Path.GetFileName(actualOutput), Path.GetFileName(outputPath));
                }

                _logger?.LogDebug("Stabilisierung OK: {File} ({Size:F1} MB)",
                    Path.GetFileName(outputPath), new FileInfo(outputPath).Length / 1024.0 / 1024.0);
                return true;
            }
            else
            {
                _logger?.LogError("Gyroflow fehlgeschlagen (Exit {Code}): {File}", process.ExitCode, Path.GetFileName(inputPath));
                if (!string.IsNullOrEmpty(stderr))
                    _logger?.LogError("Gyroflow stderr: {Error}", stderr);
                if (!string.IsNullOrEmpty(stdout))
                    _logger?.LogDebug("Gyroflow stdout: {Output}", stdout);
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            CleanupPartialOutput(inputPath, outputPath);

            if (cancellationToken.IsCancellationRequested)
            {

                _logger?.LogWarning("Gyroflow cancelled by user: {File}", inputPath);
                throw;
            }

            _logger?.LogWarning("Gyroflow timeout after {Minutes}m: {File}", _config.TimeoutMinutes, inputPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler bei Gyroflow: {File}", inputPath);
            return false;
        }
        finally
        {

            if (effectivePreset != null && File.Exists(effectivePreset))
            {
                try
                {
                    File.Delete(effectivePreset);
                    _logger?.LogDebug("Temp-Preset gelöscht: {TempPreset}", effectivePreset);
                }
                catch (Exception cleanupEx)
                {
                    _logger?.LogWarning(cleanupEx, "Fehler beim Löschen des Temp-Presets: {TempPreset}", effectivePreset);
                }
            }
        }
    }

    /// <summary>
    /// Stabilisiert mehrere Videos parallel.
    /// </summary>
    public async Task<BatchStabilizationResult> StabilizeBatchAsync(
        List<VideoStabilizationJob> jobs,
        IProgress<StabilizationProgress>? progress = null,
        IProgress<GyroflowRenderProgress>? renderProgress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchStabilizationResult
        {
            TotalVideos = jobs.Count
        };

        if (!_config.ParallelEnabled || jobs.Count == 1)
        {

            foreach (var job in jobs)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                progress?.Report(new StabilizationProgress
                {
                    Current = result.SuccessfulVideos + result.FailedVideos,
                    Total = result.TotalVideos,
                    CurrentFile = Path.GetFileName(job.InputPath)
                });

                var success = await StabilizeVideoAsync(
                    job.InputPath,
                    job.OutputPath,
                    job.PresetPath,
                    job.GpuDevice,
                    renderProgress,
                    cancellationToken);

                if (success)
                {
                    result.SuccessfulVideos++;
                    result.SucceededFiles.Add(Path.GetFileName(job.InputPath));
                }
                else
                {
                    result.FailedVideos++;
                    result.FailedFiles.Add(Path.GetFileName(job.InputPath));
                }

                progress?.Report(new StabilizationProgress
                {
                    Current = result.SuccessfulVideos + result.FailedVideos,
                    Total = result.TotalVideos,
                    CurrentFile = Path.GetFileName(job.InputPath)
                });
            }

            return result;
        }

        var maxParallel = _config.AutoDetectCores
            ? Math.Max(1, Environment.ProcessorCount - 1)
            : _config.ParallelJobs;

        _logger?.LogDebug("Starte parallele Verarbeitung: {Count} Videos, {Jobs} Jobs",
            jobs.Count, maxParallel);

        var sorted = jobs.OrderByDescending(j =>
        {
            try
            {
                return new FileInfo(j.InputPath).Length;
            }
            catch
            {
                return 0;
            }
        }).ToList();

        using var semaphore = new SemaphoreSlim(maxParallel);
        var completed = 0;

        var tasks = sorted.Select(async job =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var success = await StabilizeVideoAsync(
                    job.InputPath,
                    job.OutputPath,
                    job.PresetPath,
                    job.GpuDevice,
                    renderProgress,
                    cancellationToken);

                lock (result)
                {
                    if (success)
                    {
                        result.SuccessfulVideos++;
                        result.SucceededFiles.Add(Path.GetFileName(job.InputPath));
                    }
                    else
                    {
                        result.FailedVideos++;
                        result.FailedFiles.Add(Path.GetFileName(job.InputPath));
                    }

                    completed++;
                }

                progress?.Report(new StabilizationProgress
                {
                    Current = completed,
                    Total = result.TotalVideos,
                    CurrentFile = Path.GetFileName(job.InputPath),
                    ActiveJobs = maxParallel - semaphore.CurrentCount
                });

                return success;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        CleanupStalePresets();

        return result;
    }

    /// <summary>
    /// Löscht unvollständige Output-Dateien nach Cancel/Timeout.
    /// </summary>
    private void CleanupPartialOutput(string inputPath, string outputPath)
    {
        try
        {
            var outputDir = Path.GetDirectoryName(outputPath);
            var inputBaseName = Path.GetFileNameWithoutExtension(inputPath);
            var extension = Path.GetExtension(inputPath);
            var stabilizedName = $"{inputBaseName}{GyroflowOutputSuffix}{extension}";

            var candidates = new[]
            {
                outputPath,
                outputDir != null ? Path.Combine(outputDir, stabilizedName) : null,
                Path.Combine(Path.GetDirectoryName(inputPath)!, stabilizedName),
            };

            foreach (var candidate in candidates)
            {
                if (candidate != null && File.Exists(candidate))
                {
                    File.Delete(candidate);
                    _logger?.LogDebug("Partial output gelöscht: {File}", candidate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Cleanup partial output: {File}", inputPath);
        }
    }

    /// <summary>
    /// TEIL E: Bereinigt verwaiste Temp-Presets (älter als 1 Stunde).
    /// </summary>
    private void CleanupStalePresets()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var staleThreshold = TimeSpan.FromHours(1);
            var now = DateTime.UtcNow;

            var stalePresets = Directory.GetFiles(tempPath, "gyroflow_*.gyroflow")
                .Where(f =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(f);
                        return (now - fileInfo.LastWriteTimeUtc) > staleThreshold;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToList();

            if (stalePresets.Count > 0)
            {
                _logger?.LogDebug("Cleanup: {Count} verwaiste Temp-Presets gefunden (älter als 1h)", stalePresets.Count);

                foreach (var stalePreset in stalePresets)
                {
                    try
                    {
                        File.Delete(stalePreset);
                        _logger?.LogDebug("Gelöscht: {Preset}", Path.GetFileName(stalePreset));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Fehler beim Löschen: {Preset}", Path.GetFileName(stalePreset));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler bei CleanupStalePresets");
        }
    }
}

public class VideoStabilizationJob
{
    public required string InputPath { get; set; }
    public required string OutputPath { get; set; }
    public string? PresetPath { get; set; }
    public string GpuDevice { get; set; } = "nvidia";
}

public class BatchStabilizationResult
{
    public int TotalVideos { get; set; }
    public int SuccessfulVideos { get; set; }
    public int FailedVideos { get; set; }

    public List<string> SucceededFiles { get; set; } = new List<string>();
    public List<string> FailedFiles { get; set; } = new List<string>();

    /// <summary>
    /// Mapping: Original-Pfad (InputPath / ImportedFileInfo.DestPath) → Finaler Pfad nach Move.
    /// Wird von PostStabilizeWorkflowAsync befüllt. Leer bei DryRun oder fehlgeschlagenen Jobs.
    /// </summary>
    public Dictionary<string, string> OutputFiles { get; set; } = new();
}

public class StabilizationProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public int ActiveJobs { get; set; }
}
