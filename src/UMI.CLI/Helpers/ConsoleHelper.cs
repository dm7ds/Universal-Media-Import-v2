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

using System.Text;
using System.Text.Json;
using Spectre.Console;
using UMI.CLI.Resources;
using UMI.Core.Services;

namespace UMI.CLI.Helpers;

/// <summary>
/// Zentrale Helfer-Klasse für formatierte Console-Ausgaben.
/// Verhindert Copy-Paste von Banner/Output-Code in Commands.
/// </summary>
public static class ConsoleHelper
{
    /// <summary>
    /// Prüft ob das Terminal interaktiv ist (unterstützt ANSI, nicht umgeleitet).
    /// </summary>
    /// <returns>True wenn Spectre.Console Live-Tabellen genutzt werden können, sonst False</returns>
    public static bool IsInteractiveTerminal()
    {

        if (Console.IsOutputRedirected) return false;
        if (Console.IsErrorRedirected) return false;

        try
        {

            return AnsiConsole.Profile.Capabilities.Interactive;
        }
        catch
        {

            return false;
        }
    }
    /// <summary>
    /// Schreibt einen formatierten Banner mit Titel und automatischer Version-Zeile.
    /// Version wird aus BuildInfo.FormattedVersionLine ermittelt.
    /// </summary>
    /// <param name="title">Banner-Titel (max. 60 Zeichen für beste Darstellung)</param>
    public static void WriteBanner(string title)
    {
        WriteBannerInternal(title, BuildInfo.FormattedVersionLine);
    }

    /// <summary>
    /// Schreibt einen formatierten Banner mit Titel und optionaler Build-Info (intern).
    /// </summary>
    /// <param name="title">Banner-Titel (max. 60 Zeichen)</param>
    /// <param name="buildInfo">Build-Info (z.B. "Build abc1234 (2026-02-15)"), optional</param>
    private static void WriteBannerInternal(string title, string? buildInfo)
    {
        const int innerWidth = 60;

        var paddingTotal = innerWidth - title.Length;
        var paddingLeft = paddingTotal / 2;
        var paddingRight = paddingTotal - paddingLeft;

        Console.WriteLine("╔" + new string('═', innerWidth) + "╗");
        Console.WriteLine("║" + new string(' ', paddingLeft) + title + new string(' ', paddingRight) + "║");

        if (!string.IsNullOrEmpty(buildInfo))
        {
            var infoPaddingTotal = innerWidth - buildInfo.Length;
            var infoPaddingLeft = infoPaddingTotal / 2;
            var infoPaddingRight = infoPaddingTotal - infoPaddingLeft;
            Console.WriteLine("║" + new string(' ', infoPaddingLeft) + buildInfo + new string(' ', infoPaddingRight) + "║");
        }

        Console.WriteLine("╚" + new string('═', innerWidth) + "╝");
        Console.WriteLine();
    }

    /// <summary>
    /// Schreibt eine Option mit Boolean-Status (✓/✗).
    /// </summary>
    /// <param name="name">Option-Name</param>
    /// <param name="enabled">True = Aktiviert (✓), False = Deaktiviert (✗)</param>
    public static void WriteOption(string name, bool enabled)
    {
        var symbol = enabled ? "✓" : "✗";
        var status = enabled ? CliStrings.Common_Enabled : CliStrings.Common_Disabled;
        var color = enabled ? ConsoleColor.Green : ConsoleColor.Gray;

        Console.Write($"{name}: ");
        Console.ForegroundColor = color;
        Console.WriteLine($"{symbol} {status}");
        Console.ResetColor();
    }

    /// <summary>
    /// Schreibt eine Option mit String-Wert.
    /// </summary>
    /// <param name="name">Option-Name</param>
    /// <param name="value">Option-Wert</param>
    public static void WriteOption(string name, string value)
    {
        Console.WriteLine($"{name}: {value}");
    }

    /// <summary>
    /// Schreibt eine Trennlinie.
    /// </summary>
    public static void WriteSeparator()
    {
        Console.WriteLine(new string('─', 60));
    }

    /// <summary>
    /// Schreibt eine Erfolgs-Nachricht in Grün.
    /// </summary>
    /// <param name="message">Nachricht</param>
    public static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Schreibt eine Warn-Nachricht in Gelb.
    /// </summary>
    /// <param name="message">Nachricht</param>
    public static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Schreibt eine Fehler-Nachricht in Rot.
    /// </summary>
    /// <param name="message">Nachricht</param>
    public static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Schreibt eine formatierte Zusammenfassung mit Statistiken.
    /// </summary>
    /// <param name="title">Zusammenfassungs-Titel</param>
    /// <param name="stats">Statistiken als Key-Value-Paare</param>
    public static void WriteSummary(string title, Dictionary<string, object> stats)
    {
        Console.WriteLine();
        WriteSeparator();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"📊 {title}");
        Console.ResetColor();
        WriteSeparator();

        foreach (var (key, value) in stats)
        {
            Console.WriteLine($"  {key}: {value}");
        }

        WriteSeparator();
    }

    /// <summary>
    /// Schreibt eine Info-Nachricht in Cyan.
    /// </summary>
    /// <param name="message">Nachricht</param>
    public static void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"ℹ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Schreibt einen Progress-Indikator.
    /// </summary>
    /// <param name="current">Aktueller Fortschritt</param>
    /// <param name="total">Gesamt-Anzahl</param>
    /// <param name="message">Optionale Nachricht</param>
    public static void WriteProgress(int current, int total, string? message = null)
    {
        var percentage = (int)((double)current / total * 100);
        var progressText = $"[{current}/{total}] {percentage}%";

        if (!string.IsNullOrEmpty(message))
        {
            progressText += $" - {message}";
        }

        Console.WriteLine(progressText);
    }

    /// <summary>
    /// Schreibt gefärbten Text ohne Newline (für inline-Färbung).
    /// </summary>
    /// <param name="text">Text zum Schreiben</param>
    /// <param name="color">Farbe</param>
    public static void WriteColored(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    /// <summary>
    /// Erstellt CancellationTokenSource mit Ctrl+C Handler für graceful shutdown.
    /// </summary>
    /// <returns>CancellationTokenSource der bei Ctrl+C cancelt</returns>
    public static CancellationTokenSource CreateCancellationSource()
    {
        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine();
            WriteWarning(CliStrings.Common_CancelRequested);
        };

        return cts;
    }

    /// <summary>
    /// Wartet auf User-Input für SD-Kartenwechsel im Sequential-Modus.
    /// </summary>
    /// <param name="cameraId">Kamera-ID für die die Karte eingelegt werden soll</param>
    /// <param name="current">Aktuelle Position in der Sequenz</param>
    /// <param name="total">Gesamtanzahl der Kameras</param>
    /// <returns>'continue', 'skip', oder 'quit'</returns>
    public static string WaitForCardSwap(string cameraId, int current, int total)
    {
        Console.WriteLine();
        WriteSeparator();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(string.Format(CliStrings.CardSwap_InsertCard, current, total, cameraId));
        Console.ResetColor();
        Console.Write(CliStrings.CardSwap_Prompt);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return "continue";
                case ConsoleKey.S:
                    Console.WriteLine($" → {CliStrings.CardSwap_Skipped}");
                    return "skip";
                case ConsoleKey.Q:
                    Console.WriteLine($" → {CliStrings.CardSwap_Quit}");
                    return "quit";
            }
        }
    }

    /// <summary>
    /// Ein Import-Eintrag für die Session-Statistik.
    /// </summary>
    public record SessionEntry(string CameraId, string SourceLabel, int Videos, int Photos, long Bytes);

    /// <summary>
    /// Gibt kumulative Session-Statistik aus (für Watch/Quick nach jedem Import).
    /// </summary>
    public static void PrintSessionSummary(IReadOnlyList<SessionEntry> entries)
    {
        if (entries.Count == 0) return;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  \U0001F4CA Session:");
        Console.ResetColor();

        foreach (var e in entries)
        {
            var parts = new List<string>();
            if (e.Videos > 0) parts.Add($"{e.Videos} Videos");
            if (e.Photos > 0) parts.Add($"{e.Photos} Fotos");
            parts.Add(UMI.Core.Utilities.FormatHelper.FormatBytes(e.Bytes));

            Console.WriteLine($"    {e.CameraId} ({e.SourceLabel}) — {string.Join(", ", parts)}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Lädt und zeigt Lock-Info an (wenn vorhanden).
    /// Kapselt Deserialisierung + Anzeige für DRY (Findings 1 + 6).
    /// </summary>
    public static void PrintLockInfo(string? lockInfoJson)
    {
        if (string.IsNullOrEmpty(lockInfoJson)) return;

        try
        {
            var lockInfo = JsonSerializer.Deserialize<LockInfo>(lockInfoJson);
            if (lockInfo == null) return;

            Console.WriteLine(CliStrings.Common_BlockedBy);
            Console.WriteLine($"  {CliStrings.Common_LockInfoPid}     {lockInfo.Pid}");
            Console.WriteLine($"  {CliStrings.Common_LockInfoStarted} {lockInfo.Started}");
            Console.WriteLine($"  {CliStrings.Common_LockInfoSource}  {lockInfo.Source}");
            Console.WriteLine($"  {CliStrings.Common_LockInfoCommand} {lockInfo.Command}");
        }
        catch
        {
            Console.WriteLine($"{CliStrings.Common_LockInfo}: {lockInfoJson}");
        }
    }
}
