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
/// Ergebnis der SD-Karten-Erkennung (4-Tier Detection).
/// Unterscheidet klar zwischen: Kamera erkannt, unbekannte Karte, Registry übersprungen.
/// </summary>
public enum SdLookupResult
{
    /// <summary>Kamera erfolgreich erkannt (Tier 0-2).</summary>
    Matched,

    /// <summary>Echte SD-Karte, aber nicht registriert (Tier 3). Caller sollte User fragen.</summary>
    Unknown,

    /// <summary>Registry nicht anwendbar (kein Removable Drive, kein VSN, etc.). Kein Dialog nötig.</summary>
    Skipped
}

/// <summary>
/// Outcome der SD-Karten-Erkennung mit optionaler CameraId und Reason.
/// </summary>
public record SdLookupOutcome(
    SdLookupResult Result,
    string? CameraId = null,
    string? Reason = null,
    string? MatchedVsn = null);
