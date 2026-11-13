// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using FluentAssertions;

namespace UMI.Core.Tests;

/// <summary>
/// Gemeinsame Mechanik der Quelltext-Wächter (<see cref="ProcessStartGuardTests"/>, <see cref="SingleSourceGuardTests"/>):
/// alle <c>src/**/*.cs</c> zeilenweise nach einem Muster durchsuchen und die Trefferdateien beidseitig gegen
/// eine Allowlist mit Begründung halten.
/// </summary>
internal static class SourceGuard
{
    /// <summary>
    /// Trefferdateien gegen die Allowlist: eine Datei außerhalb ist rot, ein Eintrag ohne Treffer ebenfalls
    /// (damit die Liste nicht verrottet). <paramref name="remedy"/> sagt, was stattdessen zu tun ist.
    /// </summary>
    internal static void AssertAllowlistMatches(
        IReadOnlySet<string> hits, IReadOnlyDictionary<string, string> allowlist, string what, string remedy)
    {
        var unlisted = hits.Where(f => !allowlist.ContainsKey(f)).ToList();
        var stale = allowlist.Keys.Where(f => !hits.Contains(f)).ToList();

        unlisted.Should().BeEmpty(
            "{0} darf nur in Allowlist-Dateien vorkommen. Neue Fundstelle(n): {1}. {2} " +
            "Ist die Stelle legitim, mit Begründung in die Allowlist eintragen.",
            what, string.Join(", ", unlisted), remedy);
        stale.Should().BeEmpty(
            "Allowlist-Eintrag(e) ohne Treffer für {0} — Eintrag entfernen, damit die Liste nicht verrottet: {1}",
            what, string.Join(", ", stale));
    }

    /// <summary>Alle <c>src/**/*.cs</c> (ohne bin/obj), deren Code (ohne Zeilenkommentare) das Muster enthält.</summary>
    internal static SortedSet<string> FilesMatching(Regex pattern)
    {
        var root = FindRepoRoot();
        var src = Path.Combine(root, "src");
        var hits = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/bin/") || relative.Contains("/obj/"))
                continue;

            var code = File.ReadLines(file).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
            if (code.Any(line => pattern.IsMatch(line)))
                hits.Add(relative);
        }

        return hits;
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "UMI.Core", "UMI.Core.csproj")))
                return dir.FullName;
        }

        throw new InvalidOperationException("Repo-Root (src/UMI.Core/UMI.Core.csproj) oberhalb von " + AppContext.BaseDirectory + " nicht gefunden.");
    }
}
