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

namespace UMI.Core.Utilities;

/// <summary>
/// Berechnet die geschätzte Restzeit (ETA) basierend auf bisherigem Durchsatz.
/// </summary>
public class EtaCalculator
{
    private DateTime _startTime;
    private readonly Queue<(DateTime time, long bytes)> _samples = new();
    private readonly object _lock = new();

    /// <summary>
    /// Startet die Zeitmessung.
    /// </summary>
    public void Start() => _startTime = DateTime.Now;

    /// <summary>
    /// Fügt ein Sample hinzu (aktuelle Gesamt-Bytes).
    /// </summary>
    public void AddSample(long totalProcessedBytes)
    {
        lock (_lock)
        {
            _samples.Enqueue((DateTime.Now, totalProcessedBytes));

            while (_samples.Count > 20)
            {
                _samples.Dequeue();
            }
        }
    }

    /// <summary>
    /// Schätzt die verbleibende Zeit basierend auf dem gleitenden Durchschnitt.
    /// </summary>
    public TimeSpan? EstimateRemaining(long totalBytes, long processedBytes)
    {
        lock (_lock)
        {
            if (_samples.Count < 2) return null;

            var first = _samples.First();
            var last = _samples.Last();
            var elapsed = (last.time - first.time).TotalSeconds;
            var bytesInWindow = last.bytes - first.bytes;

            if (elapsed <= 0 || bytesInWindow <= 0) return null;

            var bytesPerSecond = bytesInWindow / elapsed;
            var remainingBytes = totalBytes - processedBytes;

            return TimeSpan.FromSeconds(remainingBytes / bytesPerSecond);
        }
    }
}
