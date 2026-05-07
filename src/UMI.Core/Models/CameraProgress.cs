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

namespace UMI.Core.Models;

/// <summary>
/// Aggregierter Progress pro Kamera während des Imports.
/// </summary>
public class CameraProgress
{
    public string CameraId { get; set; } = "";
    public string CameraType { get; set; } = "";

    public int TotalFiles { get; set; }
    public long TotalBytes { get; set; }

    private int _processedFiles;
    private long _processedBytes;

    public int ProcessedFiles
    {
        get => _processedFiles;
        set => _processedFiles = value;
    }

    public long ProcessedBytes
    {
        get => _processedBytes;
        set => _processedBytes = value;
    }

    public void IncrementProcessedFiles() => System.Threading.Interlocked.Increment(ref _processedFiles);
    public void AddProcessedBytes(long bytes) => System.Threading.Interlocked.Add(ref _processedBytes, bytes);

    public string? CurrentFile { get; set; }

    public CameraPhase Phase { get; set; } = CameraPhase.Pending;
    public string? Error { get; set; }

    public double Percentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
    public bool IsComplete => Phase == CameraPhase.Done;
}

/// <summary>
/// Import-Phasen pro Kamera.
/// </summary>
public enum CameraPhase
{
    Pending,
    Scanning,
    Sequencing,
    Copying,
    Gps,
    Gyroflow,
    Done,
    Error
}
