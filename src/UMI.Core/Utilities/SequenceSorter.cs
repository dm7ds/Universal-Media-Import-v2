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
/// Sortier-Feld für die Serien-Liste im Sequenz-Reviewer (I-017).
/// </summary>
public enum SequenceSortField
{
    /// <summary>Neuer Default — behebt den I-017-Bug (Namens-Sortierung mischt Tage).</summary>
    CaptureTime,
    Name,
    PhotoCount
}

/// <summary>
/// Gruppier-Modus für die Serien-Liste im Sequenz-Reviewer (I-017).
/// </summary>
public enum SequenceGroupMode
{
    /// <summary>Flache Liste, kein Gruppieren (Default).</summary>
    None,

    /// <summary>Gruppiert nach Serien-Typ (Profil-/Modus-Name, z.B. Sport/Astro/HighISO).</summary>
    Type
}

/// <summary>
/// Die zum Sortieren nötigen Felder einer Serie, entkoppelt von der
/// GUI-eigenen ViewModel-Klasse (UMI.GUI ist von UMI.Core.Tests aus nicht
/// referenzierbar) — damit bleibt die Sortierlogik hier testbar.
/// </summary>
/// <param name="Name">Serien-Name (Sortierkriterium "Name").</param>
/// <param name="CaptureTime">
/// Aufnahmezeit-Repräsentant der Serie (üblicherweise das erste — chronologisch
/// früheste — Foto). Kann UTC oder Local sein (siehe CLAUDE.md Falle #15) und
/// kann fehlen, wenn kein Foto der Serie einen Zeitstempel hat.
/// </param>
/// <param name="PhotoCount">Anzahl Fotos in der Serie (Sortierkriterium "Anzahl Fotos").</param>
public readonly record struct SequenceSortKey(string Name, DateTime? CaptureTime, int PhotoCount);

/// <summary>
/// Sortiert Serien für den Sequenz-Reviewer (I-017). War vorher fest
/// <c>.OrderBy(n =&gt; n)</c> auf dem Serien-Namen — das mischte Aufnahmetage,
/// weil Serien-Namen nur die Uhrzeit tragen (<c>&lt;Modus&gt;_&lt;HHmmss&gt;</c>),
/// kein Datum. Jetzt wählbar: Aufnahmezeit (Default) · Name · Anzahl Fotos,
/// auf-/absteigend.
/// </summary>
public static class SequenceSorter
{
    /// <summary>
    /// Sortiert <paramref name="items"/> nach <paramref name="field"/>.
    /// </summary>
    /// <remarks>
    /// Bei <see cref="SequenceSortField.CaptureTime"/>:
    /// <list type="bullet">
    /// <item>CLAUDE.md Falle #15 — <see cref="DateTime.Kind"/> kann
    /// <see cref="DateTimeKind.Utc"/> sein (QuickTime-CreateDate). Vor dem
    /// Vergleich wird IMMER nach Lokalzeit normalisiert, sonst verschiebt sich
    /// die Reihenfolge um den Zeitzonen-Offset.</item>
    /// <item>Serien ohne Zeitstempel (<c>CaptureTime == null</c>) dürfen die
    /// Sortierung nicht sprengen: sie landen — unabhängig von
    /// <paramref name="descending"/> — immer am Ende der Liste (sonst
    /// sprängen sie beim Umschalten der Richtung ganz nach oben), intern nach
    /// Name sortiert für ein stabiles Ergebnis.</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<T> Sort<T>(
        IReadOnlyList<T> items,
        Func<T, SequenceSortKey> keySelector,
        SequenceSortField field,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keySelector);

        if (field == SequenceSortField.CaptureTime)
        {
            var withTime    = items.Where(x => keySelector(x).CaptureTime.HasValue);
            var withoutTime = items.Where(x => !keySelector(x).CaptureTime.HasValue)
                .OrderBy(x => keySelector(x).Name, StringComparer.OrdinalIgnoreCase);

            var ordered = descending
                ? withTime.OrderByDescending(x => NormalizeToLocal(keySelector(x).CaptureTime!.Value))
                : withTime.OrderBy(x => NormalizeToLocal(keySelector(x).CaptureTime!.Value));

            return ordered.Concat(withoutTime).ToList();
        }

        IEnumerable<T> result = field switch
        {
            SequenceSortField.Name => descending
                ? items.OrderByDescending(x => keySelector(x).Name, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(x => keySelector(x).Name, StringComparer.OrdinalIgnoreCase),
            SequenceSortField.PhotoCount => descending
                ? items.OrderByDescending(x => keySelector(x).PhotoCount)
                : items.OrderBy(x => keySelector(x).PhotoCount),
            _ => items
        };

        return result.ToList();
    }

    /// <summary>CLAUDE.md Falle #15 — QuickTime-CreateDate ist UTC ohne Timezone-Suffix.</summary>
    private static DateTime NormalizeToLocal(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
}
