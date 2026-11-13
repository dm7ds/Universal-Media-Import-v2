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

using System.IO;
using System.IO.Compression;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using UMI.Core.Constants;

namespace UMI.Core.Services;

/// <summary>
/// Installiert Gyroflow und FFprobe: Quelle auflösen, laden, <b>vor dem Entpacken</b> über
/// <see cref="InstallerIntegrity"/> prüfen, erst dann entpacken (I-022). Hash-Berechnung,
/// Digest-Parsing und Vergleich liegen ausschließlich dort.
/// Grenze: Der Prüfwert kommt vom Herausgeber über denselben Kanal wie das Archiv (GitHub bzw. gyan.dev).
/// Das schützt gegen Transportfehler, abgebrochene Downloads und Austausch im Temp-Ordner, nicht gegen
/// einen kompromittierten Herausgeber. Die entpackten Dateien werden beim späteren Start nicht erneut geprüft.
/// </summary>
public sealed class ToolInstallService : IToolInstallService
{
    private const string GyroflowRepo = "gyroflow/gyroflow";
    private const string GyroflowAssetName = "Gyroflow-windows64.zip";
    private const string FFmpegChecksumUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256";

    private static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(15);

    private delegate Task<ToolSource?> SourceResolver(HttpClient http, ILogger? logger, CancellationToken ct);

    /// <summary>Was UMI pro Werkzeug kennt: Zielordner unter dem Tools-Ordner, Exe-Name, Quelle.</summary>
    private sealed record ToolPackage(string DirName, string ExeName, SourceResolver Resolve);

    private static readonly IReadOnlyDictionary<string, ToolPackage> Packages = new Dictionary<string, ToolPackage>
    {
        [ToolKeys.Gyroflow] = new("gyroflow", "Gyroflow.exe",
            (http, log, ct) => ToolSourceResolver.FromGitHubReleaseAsync(http, GyroflowRepo, GyroflowAssetName, log, ct)),
        [ToolKeys.FFprobe] = new("ffmpeg", "ffprobe.exe",
            (http, log, ct) => ToolSourceResolver.FromPublishedChecksumAsync(http, FFmpegChecksumUrl, log, ct)),
    };

    private readonly HttpClient _httpClient;
    private readonly string _toolsRoot;
    private readonly string _downloadDir;
    private readonly ILogger<ToolInstallService>? _logger;

    /// <summary>Haken an zwei Stellen des Ablaufs, nur für Tests (Zugriff auf das Archiv beim Entpacken, Fehler beim Aktivieren).</summary>
    internal sealed class InstallTestHooks
    {
        /// <summary>Wird unmittelbar vor dem Entpacken mit dem Archivpfad aufgerufen.</summary>
        internal Action<string>? BeforeExtract { get; init; }

        /// <summary>Wird unmittelbar vor dem Verschieben des Staging-Ordners an den Zielort aufgerufen.</summary>
        internal Action? BeforeActivate { get; init; }
    }

    /// <summary>Nur für Tests; im Betrieb null.</summary>
    internal InstallTestHooks? TestHooks { get; set; }

    /// <param name="httpClient">Einmal angelegter Client (User-Agent gesetzt, GitHub verlangt einen).</param>
    /// <param name="toolsRoot">Ordner, unter dem die Werkzeuge installiert werden (<see cref="ConfigPathResolver.ToolsRoot"/>).</param>
    /// <param name="logger">Optionaler Logger.</param>
    /// <param name="downloadDir">Ablage für das Archiv vor dem Entpacken; Standard der Temp-Ordner.</param>
    public ToolInstallService(
        HttpClient httpClient, string toolsRoot, ILogger<ToolInstallService>? logger = null, string? downloadDir = null)
    {
        _httpClient = httpClient;
        _toolsRoot = toolsRoot;
        _logger = logger;
        _downloadDir = downloadDir ?? Path.GetTempPath();
    }

    /// <inheritdoc/>
    public string? FindInstalledExecutable(string toolKey)
    {
        var package = PackageOf(toolKey);
        return FindExecutable(InstallDir(package), package.ExeName);
    }

    /// <inheritdoc/>
    public async Task<ToolInstallResult> InstallAsync(string toolKey, IProgress<double>? progress, CancellationToken ct)
    {
        var package = PackageOf(toolKey);
        var source = await ResolveAsync(package, ct).ConfigureAwait(false);

        // Ohne brauchbaren Prüfwert gibt es nichts zu vergleichen -> gar nicht erst laden (fail closed).
        if (source is null || !InstallerIntegrity.IsVerifiable(source.Digest))
        {
            _logger?.LogWarning("Werkzeug {Tool} nicht installiert: kein pruefbarer Digest ({Digest}).",
                toolKey, source?.Digest ?? "<keine Quelle>");
            return new ToolInstallResult(ToolInstallOutcome.NotVerifiable);
        }

        var archive = Path.Combine(_downloadDir, $"umi_{toolKey}_{Guid.NewGuid():N}.zip");
        try
        {
            await HttpFileDownload.DownloadAsync(_httpClient, source.Url, archive, progress, _logger, ct)
                .ConfigureAwait(false);

            // Der Handle bleibt bis zum Ende des Entpackens offen: niemand kann das Archiv dazwischen ändern.
            using var check = await InstallerIntegrity.CheckAsync(archive, source.Digest, _logger, ct)
                .ConfigureAwait(false);

            return check.Verdict switch
            {
                InstallerVerdict.Valid => ExtractAndPublish(package, archive),
                InstallerVerdict.Mismatch => new ToolInstallResult(ToolInstallOutcome.Mismatch),
                _ => new ToolInstallResult(ToolInstallOutcome.NotVerifiable),
            };
        }
        finally
        {
            InstallerIntegrity.TryDelete(archive, _logger);
        }
    }

    private async Task<ToolSource?> ResolveAsync(ToolPackage package, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ResolveTimeout);
        try
        {
            return await package.Resolve(_httpClient, _logger, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Download source did not answer within {ResolveTimeout.TotalSeconds:0}s.");
        }
    }

    /// <summary>
    /// Entpackt in einen Staging-Ordner neben dem Ziel und ersetzt die bisherige Installation erst,
    /// wenn die erwartete Datei darin liegt — ein fehlgeschlagenes Entpacken lässt die alte Installation stehen.
    /// </summary>
    private ToolInstallResult ExtractAndPublish(ToolPackage package, string archive)
    {
        var targetDir = InstallDir(package);
        var stagingDir = targetDir + ".staging-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(stagingDir);
            TestHooks?.BeforeExtract?.Invoke(archive);
            ZipFile.ExtractToDirectory(archive, stagingDir);

            return FindExecutable(stagingDir, package.ExeName) is null
                ? new ToolInstallResult(ToolInstallOutcome.ExecutableNotFound)
                : Publish(package, stagingDir, targetDir);
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    /// <summary>
    /// Ersetzt die bisherige Installation ohne Zwischenzustand: erst umbenennen, dann den Staging-Ordner
    /// an die Stelle verschieben, bei Fehler zurückrollen. Ein Löschen des alten Ordners vor dem Verschieben
    /// würde bei einem laufenden Werkzeug (gesperrte EXE) erst die freien Dateien entfernen und dann scheitern.
    /// Lässt sich der alte Ordner nicht umbenennen, bleibt er vollständig stehen (<see cref="ToolInstallOutcome.InUse"/>).
    /// </summary>
    private ToolInstallResult Publish(ToolPackage package, string stagingDir, string targetDir)
    {
        string? backupDir = null;
        if (Directory.Exists(targetDir))
        {
            backupDir = targetDir + ".old-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.Move(targetDir, backupDir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger?.LogWarning(ex, "Vorhandene Installation nicht ersetzbar (Dateien in Benutzung?): {Path}", targetDir);
                return new ToolInstallResult(ToolInstallOutcome.InUse);
            }
        }

        try
        {
            TestHooks?.BeforeActivate?.Invoke();
            Directory.Move(stagingDir, targetDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogError(ex, "Neue Installation konnte nicht aktiviert werden, alte wird wiederhergestellt: {Path}", targetDir);
            RestoreBackup(backupDir, targetDir);
            throw;
        }

        if (backupDir is not null)
            TryDeleteDirectory(backupDir);

        return FindExecutable(targetDir, package.ExeName) is { } exe
            ? new ToolInstallResult(ToolInstallOutcome.Installed, exe)
            : new ToolInstallResult(ToolInstallOutcome.ExecutableNotFound);
    }

    private void RestoreBackup(string? backupDir, string targetDir)
    {
        if (backupDir is null)
            return;
        try
        {
            Directory.Move(backupDir, targetDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogError(ex, "Alte Installation konnte nicht wiederhergestellt werden, sie liegt in: {Path}", backupDir);
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Ordner konnte nicht geloescht werden: {Path}", path);
        }
    }

    private string InstallDir(ToolPackage package) => Path.Combine(_toolsRoot, package.DirName);

    private static string? FindExecutable(string dir, string exeName)
        => Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, exeName, SearchOption.AllDirectories).Select(Path.GetFullPath).FirstOrDefault()
            : null;

    private static ToolPackage PackageOf(string toolKey)
        => Packages.TryGetValue(toolKey, out var package)
            ? package
            : throw new ArgumentException($"UMI kann '{toolKey}' nicht herunterladen.", nameof(toolKey));
}
