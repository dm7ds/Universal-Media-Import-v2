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

using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Implements <see cref="ISequenceSidecarService"/> by reading/writing a
/// <c>.umi-sequences.json</c> file under {Workbench}/.umi/sequences/.
/// Uses <see cref="JsonDefaults"/> for all serialisation (SSOT).
/// </summary>
public class SequenceSidecarService : ISequenceSidecarService
{
    private readonly GlobalPaths _globalPaths;
    private readonly ILogger<SequenceSidecarService>? _logger;

    public SequenceSidecarService(
        GlobalPaths globalPaths,
        ILogger<SequenceSidecarService>? logger = null)
    {
        _globalPaths = globalPaths;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SequenceSidecar?> ReadAsync(string folderPath, CancellationToken ct = default)
    {
        var sidecarPath = PathHelper.GetSidecarPath(_globalPaths.Workbench, folderPath, FolderNameConstants.UmiSubDir.Sequences, FolderNameConstants.SequenceSidecarFile);
        if (!File.Exists(sidecarPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(sidecarPath, ct);
            return JsonSerializer.Deserialize<SequenceSidecar>(json, JsonDefaults.ReadOptions);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read sequence sidecar: {Path}", sidecarPath);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(string folderPath, SequenceSidecar sidecar, CancellationToken ct = default)
    {
        var sidecarPath = PathHelper.GetSidecarPath(_globalPaths.Workbench, folderPath, FolderNameConstants.UmiSubDir.Sequences, FolderNameConstants.SequenceSidecarFile);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
            var json = JsonSerializer.Serialize(sidecar, JsonDefaults.WriteOptions);
            await File.WriteAllTextAsync(sidecarPath, json, ct);
            _logger?.LogDebug("Sequence sidecar written: {Path}", sidecarPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to write sequence sidecar: {Path}", sidecarPath);
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string folderPath, CancellationToken ct = default)
    {
        var sidecarPath = PathHelper.GetSidecarPath(_globalPaths.Workbench, folderPath, FolderNameConstants.UmiSubDir.Sequences, FolderNameConstants.SequenceSidecarFile);

        try
        {
            if (File.Exists(sidecarPath))
            {
                File.Delete(sidecarPath);
                _logger?.LogDebug("Sequence sidecar deleted: {Path}", sidecarPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete sequence sidecar: {Path}", sidecarPath);
        }

        return Task.CompletedTask;
    }
}
