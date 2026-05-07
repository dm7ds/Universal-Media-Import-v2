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
/// Implements process history tracking by reading/writing .history.json files
/// under {Workbench}/.umi/history/ via PathHelper.GetUmiPath (SSOT).
/// </summary>
public class ProcessHistoryService : IProcessHistoryService
{
    private readonly GlobalPaths _globalPaths;
    private readonly ILogger<ProcessHistoryService>? _logger;

    public ProcessHistoryService(
        GlobalPaths globalPaths,
        ILogger<ProcessHistoryService>? logger = null)
    {
        _globalPaths = globalPaths;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProcessHistory?> ReadAsync(string videoPath, CancellationToken ct = default)
    {
        var historyPath = PathHelper.GetUmiPath(_globalPaths.Workbench, videoPath, FolderNameConstants.UmiSubDir.History, FolderNameConstants.HistoryJsonSuffix);
        if (!File.Exists(historyPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(historyPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ProcessHistory>(json, JsonDefaults.ReadOptions);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read process history: {File}", Path.GetFileName(videoPath));
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task WriteEntryAsync(string videoPath, string step,
        Dictionary<string, string>? details = null, CancellationToken ct = default)
    {
        var historyPath = PathHelper.GetUmiPath(_globalPaths.Workbench, videoPath, FolderNameConstants.UmiSubDir.History, FolderNameConstants.HistoryJsonSuffix);

        try
        {

            var dir = Path.GetDirectoryName(historyPath);
            Directory.CreateDirectory(dir!);

            ProcessHistory history;
            if (File.Exists(historyPath))
            {
                var existingJson = await File.ReadAllTextAsync(historyPath, ct).ConfigureAwait(false);
                history = JsonSerializer.Deserialize<ProcessHistory>(existingJson, JsonDefaults.ReadOptions)
                          ?? new ProcessHistory
                          {
                              FileName = Path.GetFileName(videoPath),
                              Entries = new List<ProcessHistoryEntry>()
                          };
            }
            else
            {
                history = new ProcessHistory
                {
                    FileName = Path.GetFileName(videoPath),
                    Entries = new List<ProcessHistoryEntry>()
                };
            }

            history.Entries.Add(new ProcessHistoryEntry
            {
                Step = step,
                Timestamp = DateTime.UtcNow,
                Details = details
            });

            var json = JsonSerializer.Serialize(history, JsonDefaults.WriteOptions);
            await File.WriteAllTextAsync(historyPath, json, ct).ConfigureAwait(false);

            _logger?.LogDebug("History entry written: {Step} for {File}", step, Path.GetFileName(videoPath));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to write history entry ({Step}): {File}",
                step, Path.GetFileName(videoPath));
        }
    }
}
