// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;
using FluentAssertions;

namespace UMI.Core.Tests;

/// <summary>
/// I-028 F-1: Der Beenden-Dialog darf nichts versprechen, was nicht fuer jeden Importweg gilt.
/// "Beim naechsten Import wird die unvollstaendige Datei neu kopiert" stimmt nur fuer den Kartenpfad
/// (MTP ueberspringt vorhandene Ziele ohne Groessenpruefung, I-029); Nachbearbeitung holt niemand nach.
/// </summary>
public class CloseGuardMessageTests
{
    private const string MessageKey = "CloseGuard_Message";

    private static readonly string[] Resources =
    [
        Path.Combine("src", "UMI.GUI", "Resources", "Strings.resx"),
        Path.Combine("src", "UMI.GUI", "Resources", "Strings.de.resx"),
    ];

    // Wörter, die ein Nachholen/Wiederholen zusagen (EN und DE).
    private static readonly string[] Promises =
    [
        "next import", "again", "catch up", "later",
        "nächsten Import", "nächste Import", "erneut", "wieder", "nachgeholt", "später",
    ];

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UMI.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("UMI.sln not found above the test binaries");
    }

    private static string ReadMessage(string resourceRelativePath)
    {
        var doc = XDocument.Load(Path.Combine(FindRepoRoot(), resourceRelativePath));
        var value = doc.Root!.Elements("data")
            .Single(d => (string?)d.Attribute("name") == MessageKey)
            .Element("value")!.Value;
        return value;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Message_PromisesNoRepeatOrCatchUp(int resourceIndex)
    {
        var message = ReadMessage(Resources[resourceIndex]);

        foreach (var promise in Promises)
            message.Should().NotContainEquivalentOf(promise);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Message_StatesTheThreeConsequences(int resourceIndex)
    {
        var message = ReadMessage(Resources[resourceIndex]);

        message.Split('\n').Count(l => l.TrimStart().StartsWith('•')).Should().Be(3);
    }
}
