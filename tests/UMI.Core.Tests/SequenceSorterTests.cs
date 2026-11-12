// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using FluentAssertions;
using UMI.Core.Utilities;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Tests zu I-017 (Serien-Reihenfolge im Sequenz-Reviewer).
///
/// Der eigentliche Bug: <c>.OrderBy(n =&gt; n)</c> sortierte nach dem
/// Serien-NAMEN (<c>&lt;Modus&gt;_&lt;HHmmss&gt;</c>, kein Datum). Über
/// mehrere Tage hinweg landete z.B. "Sport_090000" von Tag 2 vor
/// "Sport_191856" von Tag 1. <see cref="SequenceSorter"/> ersetzt das durch
/// eine wählbare Sortierung mit Aufnahmezeit als Default.
/// </summary>
public class SequenceSorterTests
{
    private readonly record struct Seq(string Name, DateTime? CaptureTime, int PhotoCount);

    private static SequenceSortKey KeySelector(Seq s) =>
        new(s.Name, s.CaptureTime, s.PhotoCount);

    private static IReadOnlyList<Seq> Sort(IReadOnlyList<Seq> items, SequenceSortField field, bool descending = false) =>
        SequenceSorter.Sort(items, KeySelector, field, descending);

    // ---------------------------------------------------------------
    // Der eigentliche Bug: Aufnahmezeit-Sortierung darf Tagesgrenzen
    // nicht mit der Namens-Sortierung verwechseln.
    // ---------------------------------------------------------------

    [Fact]
    public void CaptureTime_OrdersAcrossDayBoundaries_NotByName()
    {
        // Namens-Sortierung würde "Sport_090000" (Tag 2) VOR "Sport_191856"
        // (Tag 1) einsortieren, weil im Namen kein Datum steckt — genau der
        // I-017-Bug. Aufnahmezeit-Sortierung muss die tatsächliche
        // Chronologie treffen: Astro_2 (Tag 1 früh) < Sport_1 (Tag 1 abends)
        // < Sport_2 (Tag 2 morgens).
        var items = new List<Seq>
        {
            new("Sport_090000", new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Local), 5),
            new("Sport_191856", new DateTime(2026, 8, 1, 19, 18, 56, DateTimeKind.Local), 8),
            new("Astro_010000", new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Local), 12),
        };

        var sorted = Sort(items, SequenceSortField.CaptureTime);

        sorted.Select(s => s.Name).Should().ContainInOrder(
            new[] { "Astro_010000", "Sport_191856", "Sport_090000" },
            "weil Aufnahmezeit-Sortierung die echte Chronologie trifft, nicht den Namen");
    }

    [Fact]
    public void CaptureTime_Descending_ReversesOrder()
    {
        var items = new List<Seq>
        {
            new("A", new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Local), 1),
            new("B", new DateTime(2026, 8, 2, 8, 0, 0, DateTimeKind.Local), 1),
        };

        var sorted = Sort(items, SequenceSortField.CaptureTime, descending: true);

        sorted.Select(s => s.Name).Should().ContainInOrder("B", "A");
    }

    // ---------------------------------------------------------------
    // Falle #15: QuickTime-CreateDate ist UTC. Ohne Normalisierung
    // verschiebt sich die Reihenfolge um den Zeitzonen-Offset.
    // ---------------------------------------------------------------

    [Fact]
    public void CaptureTime_NormalizesUtcToLocal_BeforeComparing()
    {
        // 23:30 UTC == 01:30 Local (+2h, z.B. CEST) am FOLGETAG.
        // Ohne UTC->Local-Normalisierung würde die UTC-Serie fälschlich VOR
        // der Local-Serie vom selben (lokalen) Tag einsortiert.
        var utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

        var utcMoment   = new DateTime(2026, 8, 1, 23, 30, 0, DateTimeKind.Utc);
        var localMoment = (utcMoment + utcOffset).AddMinutes(-5); // 5 Min vor dem UTC-Moment (lokal betrachtet)
        localMoment = DateTime.SpecifyKind(localMoment, DateTimeKind.Local);

        var items = new List<Seq>
        {
            new("UtcSeries",   utcMoment,   1),
            new("LocalSeries", localMoment, 1),
        };

        var sorted = Sort(items, SequenceSortField.CaptureTime);

        sorted.Select(s => s.Name).Should().ContainInOrder(
            new[] { "LocalSeries", "UtcSeries" },
            "die UTC-Zeit muss vor dem Vergleich in Lokalzeit gewandelt werden (CLAUDE.md Falle #15)");
    }

    // ---------------------------------------------------------------
    // Fotos/Serien ohne Zeitstempel dürfen die Sortierung nicht sprengen.
    // ---------------------------------------------------------------

    [Fact]
    public void CaptureTime_SequencesWithoutTimestamp_SortLast_RegardlessOfDirection()
    {
        var items = new List<Seq>
        {
            new("NoTime", null, 1),
            new("Early",  new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Local), 1),
            new("Late",   new DateTime(2026, 8, 2, 8, 0, 0, DateTimeKind.Local), 1),
        };

        Sort(items, SequenceSortField.CaptureTime, descending: false)
            .Select(s => s.Name).Last().Should().Be("NoTime");

        Sort(items, SequenceSortField.CaptureTime, descending: true)
            .Select(s => s.Name).Last().Should().Be("NoTime",
                "eine zeitstempel-lose Serie darf beim Umschalten der Richtung nicht nach oben springen");
    }

    // ---------------------------------------------------------------
    // Name / PhotoCount als Sortierkriterien (weiterhin wählbar).
    // ---------------------------------------------------------------

    [Fact]
    public void Name_SortsAlphabetically()
    {
        var items = new List<Seq> { new("Sport_2", null, 1), new("Astro_1", null, 1) };

        Sort(items, SequenceSortField.Name).Select(s => s.Name)
            .Should().ContainInOrder("Astro_1", "Sport_2");
    }

    [Fact]
    public void PhotoCount_SortsAscendingByDefault_DescendingWhenRequested()
    {
        var items = new List<Seq> { new("A", null, 12), new("B", null, 3), new("C", null, 7) };

        Sort(items, SequenceSortField.PhotoCount).Select(s => s.PhotoCount)
            .Should().ContainInOrder(3, 7, 12);

        Sort(items, SequenceSortField.PhotoCount, descending: true).Select(s => s.PhotoCount)
            .Should().ContainInOrder(12, 7, 3);
    }
}
