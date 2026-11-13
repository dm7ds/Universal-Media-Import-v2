// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Wächter (I-022): Heruntergeladene Dateien haben genau einen Weg — laden, prüfen, entpacken. Wer woanders
/// entpackt, einen SHA256 bildet oder eine HTTP-Antwort streamt, umgeht die Prüfung oder baut eine zweite
/// Hash-/Lade-Stelle. Format und Grenzen wie <see cref="ProcessStartGuardTests"/> (zeilenweise, Dateiebene,
/// Zeilenkommentare ausgenommen; ein absichtlich versteckter Aufruf — Alias, Reflection — wird nicht erkannt).
/// Die Allowlisten wurden per Grep über alle <c>src/**/*.cs</c> erhoben.
/// </summary>
public class SingleSourceGuardTests
{
    private static readonly Regex ExtractPattern =
        new(@"ExtractToDirectory|ZipFile\.Extract|ExtractToFile", RegexOptions.Compiled);

    private static readonly Regex HashPattern =
        new(@"\bSHA(?:1|256|384|512)\.|\bSHA(?:1|256|384|512)(?:Managed|CryptoServiceProvider)\b|HashData|ComputeHash", RegexOptions.Compiled);

    private static readonly Regex StreamingResponsePattern =
        new(@"ResponseHeadersRead", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> ExtractAllowlist = new Dictionary<string, string>
    {
        ["src/UMI.Core/Services/ToolInstallService.cs"] =
            "Einzige Stelle, die ein Werkzeug-Archiv entpackt — erst nach InstallerIntegrity.CheckAsync, mit offenem Prüf-Handle (I-022)",
    };

    private static readonly IReadOnlyDictionary<string, string> HashAllowlist = new Dictionary<string, string>
    {
        ["src/UMI.Core/Services/InstallerIntegrity.cs"] =
            "Einzige SHA256-Berechnung, einziger Digest-Parser und Vergleich für Installer und Werkzeuge (I-021/I-022)",
    };

    private static readonly IReadOnlyDictionary<string, string> StreamingResponseAllowlist = new Dictionary<string, string>
    {
        ["src/UMI.Core/Services/HttpFileDownload.cs"] =
            "Einzige Stelle, die HTTP-Antworten streamt: Download in Datei (Installer, Werkzeuge) und begrenzter Text (I-022)",
    };

    [Fact]
    public void ArchiveExtraction_OnlyInToolInstallService_AndEntryStillHits()
        => SourceGuard.AssertAllowlistMatches(
            SourceGuard.FilesMatching(ExtractPattern), ExtractAllowlist, "ZIP entpacken (ExtractToDirectory/ExtractToFile)",
            "Werkzeug-Archive werden ausschließlich über IToolInstallService.InstallAsync entpackt, nach der SHA256-Prüfung.");

    [Fact]
    public void Sha256Hashing_OnlyInInstallerIntegrity_AndEntryStillHits()
        => SourceGuard.AssertAllowlistMatches(
            SourceGuard.FilesMatching(HashPattern), HashAllowlist, "SHA-Hash berechnen (SHA256.*/HashData/ComputeHash)",
            "Hash-Berechnung, Digest-Parsing und Vergleich gehören ausschließlich in InstallerIntegrity — dort aufrufen statt neu bauen.");

    [Fact]
    public void ResponseStreaming_OnlyInHttpFileDownload_AndEntryStillHits()
        => SourceGuard.AssertAllowlistMatches(
            SourceGuard.FilesMatching(StreamingResponsePattern), StreamingResponseAllowlist, "HTTP-Antwort streamen (ResponseHeadersRead)",
            "Downloads laufen über HttpFileDownload.DownloadAsync, kleine Textantworten über HttpFileDownload.GetTextAsync.");
}
