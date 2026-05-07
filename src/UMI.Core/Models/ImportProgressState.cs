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
/// Gesamt-Progress über alle Kameras + Phasen während eines Imports.
/// </summary>
public class ImportProgressState
{
    public Dictionary<string, CameraProgress> Cameras { get; set; } = new();

    public PhaseProgress Scan { get; set; } = new();
    public PhaseProgress Copy { get; set; } = new();
    public PhaseProgress Gps { get; set; } = new();
    public PhaseProgress Gyroflow { get; set; } = new();

    public int TotalFiles => Cameras.Values.Sum(c => c.TotalFiles);
    public long TotalBytes => Cameras.Values.Sum(c => c.TotalBytes);
    public int ProcessedFiles => Cameras.Values.Sum(c => c.ProcessedFiles);
    public long ProcessedBytes => Cameras.Values.Sum(c => c.ProcessedBytes);
    public double Percentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;

    public DateTime? StartTime { get; set; }
    public TimeSpan? EstimatedRemaining { get; set; }
}

/// <summary>
/// Progress für eine Pipeline-Phase (Scan, Copy, GPS, Gyroflow).
/// </summary>
public class PhaseProgress
{
    public string Name { get; set; } = "";
    public int Total { get; set; }

    private int _processed;
    public int Processed
    {
        get => _processed;
        set => _processed = value;
    }

    public void IncrementProcessed() => System.Threading.Interlocked.Increment(ref _processed);

    public bool IsActive { get; set; }
    public bool IsComplete { get; set; }
    public double Percentage => Total > 0 ? (double)Processed / Total * 100 : 0;
}
