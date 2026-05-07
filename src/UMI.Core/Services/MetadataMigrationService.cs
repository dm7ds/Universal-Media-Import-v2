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

using Microsoft.Extensions.Logging;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Result record for a metadata migration run.
/// </summary>
public record MigrationResult
{
    /// <summary>Number of files successfully moved to the new location.</summary>
    public int FilesMoved { get; init; }

    /// <summary>Number of empty .metadata/ directories removed after migration.</summary>
    public int DirectoriesRemoved { get; init; }

    /// <summary>Errors encountered during migration (skipped files, IO errors, etc.).</summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>True when no errors were recorded.</summary>
    public bool Success => Errors.Count == 0;
}

/// <summary>
/// Migrates metadata files from the old per-tag <c>.metadata/</c> layout and
/// scattered sidecar files into the central <c>{Workbench}/.umi/</c> structure.
/// </summary>
/// <remarks>
/// Old layout:
/// <code>
/// {Workbench}/{tag}/.metadata/{rel}/file.meta.json
/// {Workbench}/{tag}/.metadata/{rel}/file.history.json
/// {Workbench}/{tag}/.metadata/{rel}/file_optimized.gpx
/// {Workbench}/{tag}/.metadata/thumbnails/{rel}/file.thumb.jpg
/// {Workbench}/{tag}/{cameraDir}/{mediaDir}/.umi-review.json
/// {Workbench}/{tag}/{cameraDir}/{mediaDir}/.umi-sequences.json
/// </code>
///
/// New layout (all under <c>{Workbench}/.umi/</c>):
/// <code>
/// .umi/metadata/{tag}/{rel}/file.meta.json
/// .umi/history/{tag}/{rel}/file.history.json
/// .umi/gps/{tag}/{rel}/file_optimized.gpx
/// .umi/thumbnails/{tag}/{rel}/file.thumb.jpg
/// .umi/review/{tag}/{cameraDir}/{mediaDir}/.umi-review.json
/// .umi/sequences/{tag}/{cameraDir}/{mediaDir}/.umi-sequences.json
/// </code>
///
/// All target paths are computed via <see cref="PathHelper.GetUmiPath"/> and
/// <see cref="PathHelper.GetSidecarPath"/> (SSOT — no magic literals).
/// </remarks>
public class MetadataMigrationService
{

    private readonly ILogger<MetadataMigrationService>? _logger;

    /// <summary>Initialises the service, optionally with a logger.</summary>
    public MetadataMigrationService(ILogger<MetadataMigrationService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns <c>true</c> when the workbench contains at least one old-style
    /// <c>.metadata/</c> directory or a sidecar file placed directly next to media.
    /// </summary>
    /// <param name="workbenchRoot">Absolute path to the Workbench root.</param>
    public bool IsMigrationNeeded(string workbenchRoot)
    {
        if (!Directory.Exists(workbenchRoot))
            return false;

        foreach (var tagDir in EnumerateTagDirectories(workbenchRoot))
        {
            var metadataDir = Path.Combine(tagDir, FolderNameConstants.MetadataDir);
            if (Directory.Exists(metadataDir))
                return true;

            if (ContainsSidecarFiles(tagDir))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Migrates all old-format metadata files in <paramref name="workbenchRoot"/>
    /// to the new central <c>.umi/</c> structure.
    /// </summary>
    /// <param name="workbenchRoot">Absolute path to the Workbench root (SSOT: config.GlobalPaths.Workbench).</param>
    /// <param name="progress">Optional per-file progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Migration statistics and any errors encountered.</returns>
    public async Task<MigrationResult> MigrateAsync(
        string workbenchRoot,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var context = new MigrationContext();
        var fullWorkbench = Path.GetFullPath(workbenchRoot);

        foreach (var tagDir in EnumerateTagDirectories(fullWorkbench))
        {
            ct.ThrowIfCancellationRequested();

            var metadataDir = Path.Combine(tagDir, FolderNameConstants.MetadataDir);
            if (Directory.Exists(metadataDir))
            {
                await MigrateMetadataDirAsync(
                    fullWorkbench, tagDir, metadataDir,
                    progress, ct, context).ConfigureAwait(false);

                if (IsDirectoryEmpty(metadataDir))
                {
                    TryDeleteDirectory(metadataDir, context);
                }
                else
                {
                    _logger?.LogWarning(
                        "Das .metadata/-Verzeichnis ist nach der Migration nicht leer und wird nicht gelöscht: {Dir}",
                        metadataDir);
                }
            }

            await MigrateSidecarFilesAsync(
                fullWorkbench, tagDir,
                progress, ct, context).ConfigureAwait(false);
        }

        return new MigrationResult
        {
            FilesMoved         = context.FilesMoved,
            DirectoriesRemoved = context.DirsRemoved,
            Errors             = context.Errors
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mutable migration-run state — avoids illegal ref params in async methods.
    /// </summary>
    private sealed class MigrationContext
    {
        public int FilesMoved   { get; set; }
        public int DirsRemoved  { get; set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Enumerates all yyyy-MM-dd sub-directories directly under the workbench root.
    /// </summary>
    private static IEnumerable<string> EnumerateTagDirectories(string workbenchRoot)
    {
        if (!Directory.Exists(workbenchRoot))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(workbenchRoot))
        {
            if (PathHelper.DateFolderPattern.IsMatch(Path.GetFileName(dir)))
                yield return dir;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="tagDir"/> or any sub-directory
    /// contains a <c>.umi-review.json</c> or <c>.umi-sequences.json</c> file.
    /// </summary>
    private static bool ContainsSidecarFiles(string tagDir)
    {
        foreach (var file in Directory.EnumerateFiles(tagDir, "*.json", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (name is FolderNameConstants.ReviewSidecarFile or FolderNameConstants.SequenceSidecarFile)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Migrates all files inside a single <c>.metadata/</c> directory.
    /// Thumbnail files (under the <c>thumbnails/</c> sub-folder) are routed separately.
    /// </summary>
    private async Task MigrateMetadataDirAsync(
        string workbenchRoot,
        string tagDir,
        string metadataDir,
        IProgress<string>? progress,
        CancellationToken ct,
        MigrationContext context)
    {
        var thumbnailsSubDir = Path.Combine(metadataDir, FolderNameConstants.UmiThumbnails);

        foreach (var oldFile in Directory.EnumerateFiles(metadataDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(oldFile);

            var isThumbnailFile = oldFile.StartsWith(
                thumbnailsSubDir, StringComparison.OrdinalIgnoreCase);

            if (isThumbnailFile)
            {
                await MigrateThumbnailFileAsync(
                    workbenchRoot, tagDir, thumbnailsSubDir, oldFile,
                    progress, context).ConfigureAwait(false);
            }
            else if (fileName.EndsWith(FolderNameConstants.MetaJsonSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var targetPath = BuildUmiPathFromMetadataFile(
                    workbenchRoot, tagDir, metadataDir, oldFile,
                    FolderNameConstants.MetaJsonSuffix, FolderNameConstants.UmiSubDir.Metadata);

                MoveFileAsync(oldFile, targetPath, progress, context);
            }
            else if (fileName.EndsWith(FolderNameConstants.HistoryJsonSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var targetPath = BuildUmiPathFromMetadataFile(
                    workbenchRoot, tagDir, metadataDir, oldFile,
                    FolderNameConstants.HistoryJsonSuffix, FolderNameConstants.UmiSubDir.History);

                MoveFileAsync(oldFile, targetPath, progress, context);
            }
            else if (fileName.EndsWith(FolderNameConstants.OptimizedGpxSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var targetPath = BuildUmiPathFromMetadataFile(
                    workbenchRoot, tagDir, metadataDir, oldFile,
                    FolderNameConstants.OptimizedGpxSuffix, FolderNameConstants.UmiSubDir.Gps);

                MoveFileAsync(oldFile, targetPath, progress, context);
            }
            else
            {
                _logger?.LogDebug(
                    "Unbekannter Dateityp in .metadata/ — wird übersprungen: {File}", oldFile);
            }
        }
    }

    /// <summary>
    /// Migrates a single thumbnail file from
    /// <c>.metadata/thumbnails/{rel}/{file}.thumb.jpg</c> to
    /// <c>.umi/thumbnails/{tag}/{rel}/{file}.thumb.jpg</c>.
    /// </summary>
    private async Task MigrateThumbnailFileAsync(
        string workbenchRoot,
        string tagDir,
        string thumbnailsSubDir,
        string oldFile,
        IProgress<string>? progress,
        MigrationContext context)
    {
        var fileName = Path.GetFileName(oldFile);

        string suffix;
        if (fileName.EndsWith(FolderNameConstants.ThumbnailSuffix, StringComparison.OrdinalIgnoreCase))
            suffix = FolderNameConstants.ThumbnailSuffix;
        else if (fileName.EndsWith(FolderNameConstants.PosterSuffix, StringComparison.OrdinalIgnoreCase))
            suffix = FolderNameConstants.PosterSuffix;
        else
        {
            _logger?.LogDebug(
                "Unbekannter Thumbnail-Dateityp — wird übersprungen: {File}", oldFile);
            return;
        }

        var relDirFromThumbnails = Path.GetRelativePath(
            thumbnailsSubDir, Path.GetDirectoryName(oldFile)!);

        var virtualMediaDir = (relDirFromThumbnails == ".")
            ? tagDir
            : Path.Combine(tagDir, relDirFromThumbnails);

        var baseName         = fileName[..^suffix.Length];
        var virtualMediaPath = Path.Combine(virtualMediaDir, baseName);

        var targetPath = PathHelper.GetUmiPath(
            workbenchRoot, virtualMediaPath,
            FolderNameConstants.UmiSubDir.Thumbnails, suffix);

        MoveFileAsync(oldFile, targetPath, progress, context);
    }

    /// <summary>
    /// Migrates scattered <c>.umi-review.json</c> and <c>.umi-sequences.json</c>
    /// sidecar files that sit directly inside sub-directories of a tag folder.
    /// </summary>
    private async Task MigrateSidecarFilesAsync(
        string workbenchRoot,
        string tagDir,
        IProgress<string>? progress,
        CancellationToken ct,
        MigrationContext context)
    {
        foreach (var oldFile in Directory.EnumerateFiles(tagDir, "*.json", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var fileName   = Path.GetFileName(oldFile);
            var folderPath = Path.GetDirectoryName(oldFile)!;

            string targetPath;

            if (fileName.Equals(FolderNameConstants.ReviewSidecarFile, StringComparison.OrdinalIgnoreCase))
            {
                targetPath = PathHelper.GetSidecarPath(
                    workbenchRoot, folderPath,
                    FolderNameConstants.UmiSubDir.Review, FolderNameConstants.ReviewSidecarFile);
            }
            else if (fileName.Equals(FolderNameConstants.SequenceSidecarFile, StringComparison.OrdinalIgnoreCase))
            {
                targetPath = PathHelper.GetSidecarPath(
                    workbenchRoot, folderPath,
                    FolderNameConstants.UmiSubDir.Sequences, FolderNameConstants.SequenceSidecarFile);
            }
            else
            {
                continue;
            }

            if (Path.GetFullPath(oldFile).Equals(
                    Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug(
                    "Sidecar ist bereits am Ziel — wird übersprungen: {File}", oldFile);
                continue;
            }

            MoveFileAsync(oldFile, targetPath, progress, context);
        }
    }

    /// <summary>
    /// Reconstructs the virtual media path from an old metadata file path
    /// and delegates target-path computation to <see cref="PathHelper.GetUmiPath"/>.
    /// </summary>
    /// <param name="workbenchRoot">Workbench root (passed through to GetUmiPath).</param>
    /// <param name="tagDir">Absolute path to the tag directory (yyyy-MM-dd).</param>
    /// <param name="metadataDir">Absolute path to the <c>.metadata/</c> directory.</param>
    /// <param name="oldFile">Absolute path to the old metadata file.</param>
    /// <param name="suffix">Known compound suffix to strip (e.g. ".meta.json").</param>
    /// <param name="subDir">Target sub-directory enum value under <c>.umi/</c>.</param>
    private static string BuildUmiPathFromMetadataFile(
        string workbenchRoot,
        string tagDir,
        string metadataDir,
        string oldFile,
        string suffix,
        FolderNameConstants.UmiSubDir subDir)
    {
        var fileName = Path.GetFileName(oldFile);
        var fileDir  = Path.GetDirectoryName(oldFile)!;

        var relDirFromMetadata = Path.GetRelativePath(metadataDir, fileDir);

        var virtualMediaDir = (relDirFromMetadata == ".")
            ? tagDir
            : Path.Combine(tagDir, relDirFromMetadata);

        var baseName         = fileName[..^suffix.Length];
        var virtualMediaPath = Path.Combine(virtualMediaDir, baseName);

        return PathHelper.GetUmiPath(workbenchRoot, virtualMediaPath, subDir, suffix);
    }

    /// <summary>
    /// Moves <paramref name="source"/> to <paramref name="destination"/>.
    /// Skips (and records as error) when the destination file already exists.
    /// Creates the destination directory on demand.
    /// </summary>
    private void MoveFileAsync(
        string source,
        string destination,
        IProgress<string>? progress,
        MigrationContext context)
    {
        try
        {
            if (File.Exists(destination))
            {
                var msg = $"Ziel existiert bereits — wird übersprungen: {destination}";
                _logger?.LogWarning("{Message}", msg);
                context.Errors.Add(msg);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(source, destination);

            _logger?.LogDebug("Migriert: {Source} → {Destination}", source, destination);
            progress?.Report(destination);
            context.FilesMoved++;
        }
        catch (Exception ex)
        {
            var msg = $"Fehler beim Verschieben von {source}: {ex.Message}";
            _logger?.LogError(ex, "{Message}", msg);
            context.Errors.Add(msg);
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="directory"/> contains no files or
    /// sub-directories (checked recursively).
    /// </summary>
    private static bool IsDirectoryEmpty(string directory)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
                             .Any();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes <paramref name="directory"/> recursively.
    /// Increments <see cref="MigrationContext.DirsRemoved"/> on success.
    /// </summary>
    private void TryDeleteDirectory(string directory, MigrationContext context)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
            _logger?.LogDebug("Leeres .metadata/-Verzeichnis entfernt: {Dir}", directory);
            context.DirsRemoved++;
        }
        catch (Exception ex)
        {
            var msg = $"Verzeichnis konnte nicht gelöscht werden: {directory} — {ex.Message}";
            _logger?.LogWarning("{Message}", msg);
            context.Errors.Add(msg);
        }
    }
}
