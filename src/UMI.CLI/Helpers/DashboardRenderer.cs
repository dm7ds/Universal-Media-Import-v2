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
using UMI.CLI.Resources;
using UMI.Core.Utilities;

namespace UMI.CLI.Helpers;

/// <summary>
/// ANSI-cursor-gesteuerte statische Dashboard-UI fuer Watch/Quick-Commands.
/// Thread-safe: Alle oeffentlichen Methoden koennen von beliebigen Threads aufgerufen werden.
/// </summary>
public class DashboardRenderer
{

    public enum SlotPhase
    {
        Detected,
        Queued,
        Scanning,
        Copying,
        Done,
        Error
    }

    /// <summary>Ein Import-Slot im Dashboard.</summary>
    public record ImportSlot
    {
        public int Id { get; init; }
        public string Label { get; set; } = string.Empty;
        public SlotPhase Phase { get; set; }
        public string? Detail { get; set; }

        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public long TotalBytes { get; set; }
        public long CopiedBytes { get; set; }
        public double? SpeedBytesPerSec { get; set; }
        public double? EtaSeconds { get; set; }

        public List<string>? SubLines { get; set; }

        public SlotPhase? LastLoggedPhase { get; set; }
    }

    private readonly object _renderLock = new();
    private readonly string _bannerTitle;
    private readonly bool _interactive;
    private readonly List<ImportSlot> _slots = new();
    private readonly List<ConsoleHelper.SessionEntry> _sessionEntries = new();
    private string[] _modeInfoLines = Array.Empty<string>();
    private string[] _sourceLines = Array.Empty<string>();
    private string _statusLine = "";
    private int _contentLines;
    private int _nextSlotId;
    private bool _inDialog;
    private DateTime _lastRedrawTime = DateTime.MinValue;

    private static readonly TimeSpan RedrawThrottle = TimeSpan.FromMilliseconds(100);

    /// <param name="bannerTitle">Banner-Titel (z.B. "UMI - Drive-Watcher")</param>
    /// <param name="interactive">True wenn Terminal interaktiv (ANSI-Support).
    /// False = Fallback auf normales Scrolling (z.B. Pipe/Redirect).</param>
    public DashboardRenderer(string bannerTitle, bool interactive = true)
    {
        _bannerTitle = bannerTitle;
        _interactive = interactive;
    }

    /// <summary>Setzt die Mode-Info-Zeilen (unter dem Banner).</summary>
    public void SetModeInfo(params string[] lines)
    {
        lock (_renderLock)
        {
            _modeInfoLines = lines;
        }
    }

    /// <summary>Setzt die konfigurierten Kamera-Zeilen (Fixed-Path, MTP-Quellen).</summary>
    public void SetConfiguredSources(params string[] lines)
    {
        lock (_renderLock)
        {
            _sourceLines = lines;
        }
    }

    /// <summary>
    /// Rendert den Header (Banner + Mode-Info + Quellen).
    /// Oeffentliche Methode — wird einmal beim Start aufgerufen. Setzt den Content-Anker.
    /// </summary>
    public void RenderHeader()
    {
        lock (_renderLock)
        {
            RenderHeaderInternal();
            _contentLines = 0;
        }
    }

    /// <summary>
    /// Interne Header-Rendering-Logik. Muss unter _renderLock aufgerufen werden.
    /// Wird von RenderHeader() und EndDialog() genutzt.
    /// </summary>
    private void RenderHeaderInternal()
    {
        ConsoleHelper.WriteBanner(_bannerTitle);

        foreach (var line in _modeInfoLines)
            Console.WriteLine(line);

        foreach (var line in _sourceLines)
            Console.WriteLine(line);

        if (_modeInfoLines.Length > 0 || _sourceLines.Length > 0)
            Console.WriteLine();
    }

    /// <summary>Fuegt einen neuen Import-Slot hinzu und triggert Redraw.</summary>
    /// <returns>Slot-ID fuer spaetere Updates.</returns>
    public int AddSlot(string cameraId, string sourceLabel, SlotPhase phase, string? detail = null)
    {
        lock (_renderLock)
        {
            var slot = new ImportSlot
            {
                Id = _nextSlotId++,
                Label = $"{cameraId} ({sourceLabel})",
                Phase = phase,
                Detail = detail
            };
            _slots.Add(slot);

            RedrawInternal(force: true);
            return slot.Id;
        }
    }

    /// <summary>Aktualisiert Phase/Detail/Label eines Slots und triggert Redraw.</summary>
    /// <param name="slotId">Slot-ID (aus AddSlot).</param>
    /// <param name="phase">Neue Phase.</param>
    /// <param name="detail">Optionaler Detail-Text (z.B. Scan-Fortschritt). Null = unveraendert.</param>
    /// <param name="label">Optionales Label-Update (z.B. "J:\ → OA5 (SD)"). Null = kein Update.</param>
    /// <param name="subLines">Optionale Sub-Lines (z.B. Fehlerdetails pro Datei). Null = keine.</param>
    public void UpdateSlot(int slotId, SlotPhase phase, string? detail = null, string? label = null, List<string>? subLines = null)
    {
        lock (_renderLock)
        {
            var slot = _slots.FirstOrDefault(s => s.Id == slotId);
            if (slot == null) return;

            slot.Phase = phase;
            slot.Detail = detail;
            if (label != null) slot.Label = label;
            slot.SubLines = subLines;

            RedrawInternal(force: true);
        }
    }

    /// <summary>Aktualisiert Copy-Progress eines Slots und triggert Redraw (throttled).</summary>
    public void UpdateProgress(int slotId, int completedFiles, int totalFiles,
                               long copiedBytes, long totalBytes,
                               double? speedBytesPerSec = null, double? etaSeconds = null)
    {
        lock (_renderLock)
        {
            var slot = _slots.FirstOrDefault(s => s.Id == slotId);
            if (slot == null) return;

            slot.Phase = SlotPhase.Copying;
            slot.CompletedFiles = completedFiles;
            slot.TotalFiles = totalFiles;
            slot.CopiedBytes = copiedBytes;
            slot.TotalBytes = totalBytes;
            slot.SpeedBytesPerSec = speedBytesPerSec;
            slot.EtaSeconds = etaSeconds;

            RedrawInternal(force: false);
        }
    }

    /// <summary>Fuegt einen Session-Eintrag hinzu (kumulative Statistik).</summary>
    public void AddSessionEntry(ConsoleHelper.SessionEntry entry)
    {
        lock (_renderLock)
        {
            _sessionEntries.Add(entry);
            RedrawInternal(force: true);
        }
    }

    /// <summary>
    /// Pausiert Dashboard-Rendering.
    /// Aufrufer kann danach normal Console.Write/ReadLine nutzen (z.B. Karten-Dialog).
    /// </summary>
    public void BeginDialog()
    {
        lock (_renderLock)
        {
            if (!_interactive) return;
            _inDialog = true;

        }
    }

    /// <summary>
    /// Beendet Dialog-Modus. Kompletter Screen-Clear + Header + Content neu rendern.
    /// DECSC/DECRC funktioniert NICHT wenn der Dialog das Terminal gescrollt hat.
    /// Der buffered single-write aus Bug-1-Fix macht den Redraw quasi instant.
    /// </summary>
    /// <param name="extraLinesWritten">Parameter wird nicht mehr benoetigt (Backward-Kompatibilitaet).</param>
    public void EndDialog(int extraLinesWritten = 0)
    {
        lock (_renderLock)
        {
            if (!_interactive)
            {
                _inDialog = false;
                return;
            }

            _inDialog = false;

            Console.Write("\x1b[2J");
            Console.Write("\x1b[H");

            RenderHeaderInternal();

            _contentLines = 0;
            RedrawInternal(force: true);
        }
    }

    /// <summary>Setzt die Status-Zeile am Ende des Dashboards.</summary>
    public void SetStatusLine(string message)
    {
        lock (_renderLock)
        {
            _statusLine = message;
            RedrawInternal(force: true);
        }
    }

    /// <summary>
    /// Vollstaendiger Redraw des Content-Bereichs (alles unter Header).
    /// Thread-safe, throttled (max alle 100ms).
    /// Im Non-Interactive-Modus: Nur neue Aenderungen als Scroll-Output.
    /// </summary>
    public void Redraw()
    {
        lock (_renderLock)
        {
            RedrawInternal(force: false);
        }
    }

    /// <summary>
    /// Interner Redraw — muss unter _renderLock aufgerufen werden.
    /// Bug 1 Fix: Buffered Single-Write statt Clear+zeilenweises Rendern (verhindert Flackern).
    /// </summary>
    private void RedrawInternal(bool force)
    {
        if (_inDialog) return;
        if (!force && (DateTime.UtcNow - _lastRedrawTime) < RedrawThrottle) return;

        if (_interactive)
        {
            MoveCursorToAnchor();

            var sb = new StringBuilder();
            var newLines = BuildContentBuffer(sb);

            sb.Append("\x1b[J");

            Console.Write(sb.ToString());
            _contentLines = newLines;
        }
        else
        {
            RenderContent();
        }

        _lastRedrawTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Baut den gesamten Content-Bereich in einen StringBuilder.
    /// Jede Zeile wird mit \x1b[2K (clear line) prefixed fuer sauberes Ueberschreiben.
    /// Gibt die Anzahl der geschriebenen Zeilen zurueck.
    /// </summary>
    private int BuildContentBuffer(StringBuilder sb)
    {
        var lines = 0;

        if (_slots.Count > 0)
        {
            AppendLine(sb, ref lines, "\u2500\u2500 Imports " + new string('\u2500', 48));
            foreach (var slot in _slots)
            {
                var color = GetAnsiColor(slot.Phase);
                var reset = "\x1b[0m";
                AppendLine(sb, ref lines, $"  {color}{FormatSlotLine(slot)}{reset}");
                if (slot.SubLines != null)
                {
                    foreach (var subLine in slot.SubLines)
                    {
                        AppendLine(sb, ref lines, $"    {color}{subLine}{reset}");
                    }
                }
            }
            AppendLine(sb, ref lines, "");
        }

        if (_sessionEntries.Count > 0)
        {
            var totalVideos = _sessionEntries.Sum(e => e.Videos);
            var totalPhotos = _sessionEntries.Sum(e => e.Photos);
            var totalBytes = _sessionEntries.Sum(e => e.Bytes);
            var parts = new List<string>();
            if (totalVideos > 0) parts.Add($"{totalVideos} Videos");
            if (totalPhotos > 0) parts.Add($"{totalPhotos} Fotos");
            parts.Add(FormatHelper.FormatBytes(totalBytes));
            AppendLine(sb, ref lines, $"  \x1b[36m\U0001F4CA Session: {string.Join(", ", parts)}\x1b[0m");
            AppendLine(sb, ref lines, "");
        }

        if (!string.IsNullOrEmpty(_statusLine))
        {
            AppendLine(sb, ref lines, _statusLine);
        }

        return lines;
    }

    /// <summary>Haengt eine Zeile an den StringBuilder an (mit Line-Clear Prefix).</summary>
    private static void AppendLine(StringBuilder sb, ref int lineCount, string text)
    {
        sb.Append("\x1b[2K");
        sb.AppendLine(text);
        lineCount++;
    }

    /// <summary>Gibt den ANSI-Farbcode fuer eine Slot-Phase zurueck.</summary>
    private static string GetAnsiColor(SlotPhase phase) => phase switch
    {
        SlotPhase.Done     => "\x1b[32m",
        SlotPhase.Error    => "\x1b[31m",
        SlotPhase.Copying  => "\x1b[36m",
        SlotPhase.Queued   => "\x1b[90m",
        SlotPhase.Scanning => "\x1b[33m",
        _                  => "\x1b[37m"
    };

    /// <summary>Slots + Session + Status rendern (NUR fuer Non-Interactive-Pfad).</summary>
    private void RenderContent()
    {
        RenderSlots();
        RenderSessionSummary();
        RenderStatusLine();
    }

    /// <summary>Import-Slots Sektion rendern (Non-Interactive).</summary>
    private void RenderSlots()
    {
        if (_slots.Count == 0) return;

        Console.WriteLine("\u2500\u2500 Imports " + new string('\u2500', 51));

        foreach (var slot in _slots)
        {

            if (slot.Phase != slot.LastLoggedPhase)
            {
                Console.WriteLine("  " + FormatSlotLine(slot));
                slot.LastLoggedPhase = slot.Phase;
            }
        }

        Console.WriteLine("");
    }

    /// <summary>Session-Statistik Sektion rendern (Non-Interactive).</summary>
    private void RenderSessionSummary()
    {
        if (_sessionEntries.Count == 0) return;

        var totalVideos = _sessionEntries.Sum(e => e.Videos);
        var totalPhotos = _sessionEntries.Sum(e => e.Photos);
        var totalBytes = _sessionEntries.Sum(e => e.Bytes);

        var parts = new List<string>();
        if (totalVideos > 0) parts.Add($"{totalVideos} Videos");
        if (totalPhotos > 0) parts.Add($"{totalPhotos} Fotos");
        parts.Add(FormatHelper.FormatBytes(totalBytes));

        Console.WriteLine($"  \U0001F4CA Session: {string.Join(", ", parts)}");
        Console.WriteLine("");
    }

    /// <summary>Status-Zeile am Ende des Dashboards rendern (Non-Interactive).</summary>
    private void RenderStatusLine()
    {
        if (string.IsNullOrEmpty(_statusLine)) return;
        Console.WriteLine(_statusLine);
    }

    /// <summary>Cursor um _contentLines Zeilen nach oben bewegen.</summary>
    private void MoveCursorToAnchor()
    {
        if (_contentLines > 0)
        {
            Console.Write($"\x1b[{_contentLines}A");
        }
    }

    /// <summary>Slot in eine formatierte Zeile umwandeln.</summary>
    private string FormatSlotLine(ImportSlot slot)
    {
        return slot.Phase switch
        {
            SlotPhase.Detected => $"\u2753 {slot.Label} \u2014 {CliStrings.Dashboard_Detected}",
            SlotPhase.Queued   => $"\u23F3 {slot.Label} \u2014 {CliStrings.Dashboard_Queued}",
            SlotPhase.Scanning => $"\U0001F50D {slot.Label} \u2014 {CliStrings.Dashboard_Scanning}{slot.Detail}",
            SlotPhase.Copying  => FormatCopyingLine(slot),
            SlotPhase.Done     => $"\u2713 {slot.Label} \u2014 {slot.Detail}",
            SlotPhase.Error    => $"\u2717 {slot.Label} \u2014 {slot.Detail}",
            _                  => $"? {slot.Label}"
        };
    }

    /// <summary>Copying-Zeile mit Progress-Details formatieren.</summary>
    private static string FormatCopyingLine(ImportSlot slot)
    {
        var parts = new List<string>();

        if (slot.TotalFiles > 0)
        {
            parts.Add($"{slot.CompletedFiles}/{slot.TotalFiles}");
        }

        if (slot.TotalBytes > 0)
        {
            parts.Add($"{FormatHelper.FormatBytes(slot.CopiedBytes)} / {FormatHelper.FormatBytes(slot.TotalBytes)}");
        }

        if (slot.SpeedBytesPerSec.HasValue && slot.SpeedBytesPerSec.Value > 0)
        {
            parts.Add($"{FormatHelper.FormatBytes((long)slot.SpeedBytesPerSec.Value)}/s");
        }

        if (slot.EtaSeconds.HasValue && slot.EtaSeconds.Value > 0)
        {
            var eta = (int)Math.Ceiling(slot.EtaSeconds.Value);
            parts.Add($"ETA {eta}s");
        }

        var detail = parts.Count > 0 ? " | " + string.Join(" | ", parts) : "";
        return $"\u25B6 {slot.Label}{detail}";
    }
}
