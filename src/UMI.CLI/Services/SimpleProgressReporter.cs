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

using Spectre.Console;
using UMI.Core.Constants;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using System.Threading;

namespace UMI.CLI.Services;

/// <summary>
/// Einfacher Text-basierter Progress-Reporter (kein Spectre, keine Live-Tabelle).
/// </summary>
public class SimpleProgressReporter : IProgressReporter
{
    /// <summary>
    /// Wenn gesetzt (IsSet == true): Progress-Callback pausiert das Rendering
    /// und wartet bis dieses Event zurückgesetzt wird (= Resume-Signal).
    /// Für Watch Card-Interrupt: Erlaubt Karten-Dialog während aktivem Copy.
    /// </summary>
    public ManualResetEventSlim? PauseRequested { get; set; }

    /// <summary>
    /// Wird vom Progress-Callback auf Set() gestellt sobald das Rendering pausiert ist.
    /// Signalisiert dem Aufrufer dass der Terminal wieder frei ist.
    /// </summary>
    public ManualResetEventSlim? PauseHandled { get; set; }

    private readonly ImportProgressState _state = new();
    private string? _currentCameraId;
    private bool _isDryRun;

    public async Task RunWithProgressAsync(Func<IProgressReporter, Task> importAction, bool isDryRun = false)
    {
        _isDryRun = isDryRun;
        _state.StartTime = DateTime.Now;
        await importAction(this);

        if (_state.ProcessedFiles > 0)
        {
            PrintSummary();
        }
    }

    /// <summary>
    /// Wrapper für FileCopyService mit Spectre.Console Progress (nicht Live-Tabelle!).
    /// Zeigt Gesamt-Fortschritt + aktuell kopierte Datei mit ETA.
    /// </summary>
    public async Task<CopyResult> RunCopyWithProgressAsync(
        Func<IProgress<CopyProgress>, Task<CopyResult>> copyAction,
        bool isDryRun = false)
    {
        if (isDryRun)
        {

            var dryProgress = new Progress<CopyProgress>();
            return await copyAction(dryProgress);
        }

        CopyResult? result = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(true)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {

                var totalTask = ctx.AddTask("[green]Gesamt[/]", maxValue: 100);
                long lastTotalBytes = 0;

                var activeTasks = new Dictionary<string, (ProgressTask Task, long Size, long LastCopied)>();
                var activeTasksLock = new object();

                var progress = new Progress<CopyProgress>(p =>
                {

                    if (totalTask.MaxValue == 100 && p.TotalBytes > 0)
                    {
                        totalTask.MaxValue = p.TotalBytes;
                    }

                    var delta = p.TotalCopiedBytes - lastTotalBytes;
                    if (delta > 0)
                    {
                        totalTask.Increment(delta);
                        lastTotalBytes = p.TotalCopiedBytes;
                    }

                    totalTask.Description = $"[green]Gesamt[/] {p.CompletedFiles}/{p.TotalFiles} Dateien ({p.ByteProgress})";

                    if (!string.IsNullOrEmpty(p.CurrentFile))
                    {
                        lock (activeTasksLock)
                        {
                            var fileName = p.CurrentFile;

                            if (!activeTasks.ContainsKey(fileName))
                            {
                                var newTask = ctx.AddTask($"[yellow]{fileName}[/]", maxValue: p.CurrentFileSize);
                                activeTasks[fileName] = (newTask, p.CurrentFileSize, 0);
                            }

                            var (fileTask, fileSize, lastCopied) = activeTasks[fileName];

                            var fileDelta = p.CurrentFileCopiedBytes - lastCopied;
                            if (fileDelta > 0)
                                fileTask.Increment(fileDelta);

                            fileTask.Description = $"[yellow]{fileName}[/] ({FormatHelper.FormatBytes(p.CurrentFileCopiedBytes)}/{FormatHelper.FormatBytes(fileSize)})";

                            if (fileTask.Value >= fileSize)
                            {
                                fileTask.StopTask();
                                activeTasks.Remove(fileName);
                            }
                            else
                            {
                                activeTasks[fileName] = (fileTask, fileSize, p.CurrentFileCopiedBytes);
                            }
                        }
                    }

                });

                result = await copyAction(progress);

                if (totalTask.Value < totalTask.MaxValue)
                {
                    totalTask.Increment(totalTask.MaxValue - totalTask.Value);
                }
                totalTask.StopTask();
            });

        sw.Stop();

        if (result != null && result.CopiedFiles > 0)
        {
            var avgSpeed = result.CopiedBytes / sw.Elapsed.TotalSeconds;
            var elapsed = sw.Elapsed.TotalSeconds >= 60
                ? $"{(int)sw.Elapsed.TotalMinutes:D2}:{sw.Elapsed.Seconds:D2}"
                : $"{sw.Elapsed.TotalSeconds:F0}s";

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(
                $"[green]{result.CopiedFiles}[/] Dateien  │  [green]{FormatHelper.FormatBytes(result.CopiedBytes)}[/]  │  {elapsed}  │  Ø {FormatHelper.FormatBytes((long)avgSpeed)}/s")
                .Header("[bold green]✓ Copy Complete[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("green")));
        }

        return result ?? new CopyResult();
    }

    /// <summary>
    /// Text-basierter Copy-Progress für Watch Mode. Aktualisiert eine einzelne Zeile mit \r.
    /// Unterstützt Pause via PauseRequested/PauseHandled für Card-Dialog während aktivem Copy.
    /// </summary>
    public async Task<CopyResult> RunCopyWithTextProgressAsync(
        Func<IProgress<CopyProgress>, Task<CopyResult>> copyAction)
    {
        CopyResult? result = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lastRenderTime = DateTime.MinValue;
        var lineWidth = 0;

        var progress = new Progress<CopyProgress>(p =>
        {

            if (PauseRequested?.IsSet == true)
            {

                if (lineWidth > 0)
                    Console.Write("\r" + new string(' ', lineWidth) + "\r");
                lineWidth = 0;
                PauseHandled?.Set();
                while (PauseRequested?.IsSet == true)
                    Thread.Sleep(50);
                PauseHandled?.Reset();
            }

            if ((DateTime.Now - lastRenderTime).TotalMilliseconds < 250) return;
            lastRenderTime = DateTime.Now;

            if (p.TotalBytes <= 0) return;

            var elapsed = sw.Elapsed.TotalSeconds;
            var speed = elapsed > 0 ? (double)p.TotalCopiedBytes / elapsed : 0;
            var eta = speed > 0 ? (p.TotalBytes - p.TotalCopiedBytes) / speed : 0;
            var etaStr = eta < 1 ? "< 1s" : $"{(int)eta}s";

            var line = $"  {p.CompletedFiles}/{p.TotalFiles} Dateien | " +
                       $"{FormatHelper.FormatBytes(p.TotalCopiedBytes)} / {FormatHelper.FormatBytes(p.TotalBytes)} | " +
                       $"{FormatHelper.FormatBytes((long)speed)}/s | ETA {etaStr}";

            lineWidth = line.Length;
            Console.Write("\r" + line);
        });

        result = await copyAction(progress);
        sw.Stop();

        if (lineWidth > 0)
            Console.Write("\r" + new string(' ', lineWidth) + "\r");

        if (result != null && result.CopiedFiles > 0)
        {
            var avgSpeed = sw.Elapsed.TotalSeconds > 0 ? result.CopiedBytes / sw.Elapsed.TotalSeconds : 0;
            var elapsed = sw.Elapsed.TotalSeconds >= 60
                ? $"{(int)sw.Elapsed.TotalMinutes:D2}:{sw.Elapsed.Seconds:D2}"
                : $"{sw.Elapsed.TotalSeconds:F0}s";
            Console.WriteLine($"  ✓ {result.CopiedFiles} Dateien | {FormatHelper.FormatBytes(result.CopiedBytes)} | {elapsed} | Ø {FormatHelper.FormatBytes((long)avgSpeed)}/s");
        }

        return result ?? new CopyResult();
    }

    public void OnScanStart(string cameraId, string cameraType)
    {
        _currentCameraId = cameraId;

        _state.Cameras[cameraId] = new CameraProgress
        {
            CameraId = cameraId,
            CameraType = cameraType,
            Phase = CameraPhase.Scanning
        };

        Console.WriteLine();
        Console.WriteLine("▶ Scanne Source...");
    }

    public void OnScanComplete(string cameraId, int fileCount, long totalBytes)
    {
        if (!_state.Cameras.TryGetValue(cameraId, out var cam)) return;

        cam.TotalFiles = fileCount;
        cam.TotalBytes = totalBytes;
        cam.Phase = CameraPhase.Copying;

        Console.WriteLine($"  {fileCount} Dateien gescannt ({FormatHelper.FormatBytes(totalBytes)})");
        Console.WriteLine();
    }

    public void OnCopyProgress(string cameraId, CopyProgress progress)
    {

        if (!_state.Cameras.TryGetValue(cameraId, out var cam)) return;

        cam.ProcessedFiles = progress.CompletedFiles;
        cam.ProcessedBytes = progress.TotalCopiedBytes;
    }

    public void OnCopyComplete(string cameraId)
    {
        if (!_state.Cameras.TryGetValue(cameraId, out var cam)) return;

        cam.Phase = CameraPhase.Done;
        cam.CurrentFile = null;

    }

    public void OnPhaseStart(string phase, int totalItems)
    {
        var phaseObj = GetPhase(phase);
        if (phaseObj == null) return;

        phaseObj.Name = phase;
        phaseObj.Total = totalItems;
        phaseObj.IsActive = true;
    }

    public void OnPhaseProgress(string phase, string item)
    {
        var phaseObj = GetPhase(phase);
        if (phaseObj == null) return;

        phaseObj.IncrementProcessed();
    }

    public void OnPhaseComplete(string phase)
    {
        var phaseObj = GetPhase(phase);
        if (phaseObj == null) return;

        phaseObj.IsActive = false;
        phaseObj.IsComplete = true;
    }

    public void OnError(string cameraId, string message)
    {
        if (!_state.Cameras.TryGetValue(cameraId, out var cam)) return;

        cam.Phase = CameraPhase.Error;
        cam.Error = message;

        Console.WriteLine();
        Console.WriteLine($"  ✗ Fehler: {message}");
    }

    public void OnComplete(ImportProgressState finalState)
    {

    }

    private PhaseProgress? GetPhase(string name) => name.ToLower() switch
    {
        "scan" => _state.Scan,
        "copy" => _state.Copy,
        "gps" => _state.Gps,
        ToolKeys.Gyroflow => _state.Gyroflow,
        _ => null
    };

    private static string BuildSimpleBar(double pct, int width)
    {
        var filled = (int)(pct / 100.0 * width);
        var empty = width - filled;
        filled = Math.Max(0, Math.Min(width, filled));
        empty = Math.Max(0, Math.Min(width, empty));

        var filledStr = new string('█', filled);
        var emptyStr = new string('░', empty);
        return $"[{filledStr}{emptyStr}]";
    }

    private static string TruncateFileName(string? fileName, int maxLength)
    {
        if (string.IsNullOrEmpty(fileName)) return "...";
        if (fileName.Length <= maxLength) return fileName;

        return fileName.Substring(0, maxLength - 3) + "...";
    }

    private void PrintSummary()
    {
        var elapsed = DateTime.Now - (_state.StartTime ?? DateTime.Now);

        Console.WriteLine();
        Console.WriteLine("╭─Import Complete──────────────────╮");
        Console.WriteLine($"│ {_state.ProcessedFiles} files, {FormatHelper.FormatBytes(_state.ProcessedBytes)} in {elapsed.TotalSeconds:F1}s");
        Console.WriteLine("╰──────────────────────────────────╯");
    }
}
