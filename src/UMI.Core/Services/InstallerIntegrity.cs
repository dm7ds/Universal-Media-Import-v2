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

using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Ausgang der Frage „ist diese heruntergeladene Datei die erwartete?" — für den UMI-Installer
/// (I-021) und für Werkzeug-Archive (I-022) gleichermaßen.
/// </summary>
public enum InstallerVerdict
{
    /// <summary>Der SHA256 der Datei stimmt mit dem erwarteten Digest überein.</summary>
    Valid,

    /// <summary>Der SHA256 weicht ab — die Datei wurde gelöscht und darf nicht gestartet werden.</summary>
    Mismatch,

    /// <summary>
    /// Es gibt keinen brauchbaren erwarteten Digest (fehlt, falsches Präfix, falsche Länge).
    /// Eigener Zustand — weder „gültig" noch „abweichend": es wurde nichts verglichen,
    /// also wird nicht automatisch installiert.
    /// </summary>
    NotVerifiable,
}

/// <summary>
/// Ergebnis von <see cref="InstallerIntegrity.CheckAsync"/>. Bei <see cref="InstallerVerdict.Valid"/>
/// hält die Instanz die Datei mit <see cref="FileShare.Read"/> geöffnet, bis sie disposed wird:
/// Der Aufrufer startet den Installer, solange er die Instanz hält — so kann zwischen Prüfung
/// und Start niemand die Datei ändern, ersetzen oder löschen.
/// </summary>
internal sealed class InstallerCheck : IDisposable
{
    private FileStream? _handle;

    internal InstallerCheck(
        InstallerVerdict verdict, string? expectedSha256, string? actualSha256, FileStream? handle)
    {
        Verdict = verdict;
        ExpectedSha256 = expectedSha256;
        ActualSha256 = actualSha256;
        _handle = handle;
    }

    /// <summary>Ergebnis der Prüfung.</summary>
    public InstallerVerdict Verdict { get; }

    /// <summary>Erwarteter SHA256 (Hex, klein). Null bei <see cref="InstallerVerdict.NotVerifiable"/>.</summary>
    public string? ExpectedSha256 { get; }

    /// <summary>Gemessener SHA256 (Hex, klein). Null bei <see cref="InstallerVerdict.NotVerifiable"/>.</summary>
    public string? ActualSha256 { get; }

    /// <summary>Gibt den Datei-Handle frei (Datei wieder für andere Prozesse beschreibbar).</summary>
    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }
}

/// <summary>
/// Die eine Stelle, die beantwortet: „Ist diese heruntergeladene Datei die erwartete?" — einzige
/// SHA256-Berechnung, einziger Digest-Parser, einziger Vergleich. Genutzt für den Installer nach dem
/// Download (<see cref="GitHubReleaseChecker.DownloadSetupAsync"/>) und unmittelbar vor dem Start
/// (<see cref="InstallerLauncher.RunVerifiedAsync"/>) sowie für Werkzeug-Archive vor dem Entpacken
/// (<see cref="ToolInstallService.InstallAsync"/>). Der Name stammt aus I-021.
/// </summary>
internal static class InstallerIntegrity
{
    /// <summary>Präfix des GitHub-Asset-Digests (<c>digest</c>-Feld der Release-API).</summary>
    internal const string DigestPrefix = "sha256:";

    /// <summary>Länge eines SHA256 in Hex-Zeichen.</summary>
    internal const int Sha256HexLength = 64;

    private const int FileBufferSizeBytes = 81920;

    /// <summary>
    /// Zerlegt <c>sha256:&lt;64 Hex&gt;</c>. Alles andere (null, anderes Präfix, andere Länge,
    /// Nicht-Hex-Zeichen) ist kein brauchbarer Digest.
    /// </summary>
    /// <param name="digest">Rohwert aus der Release-API.</param>
    /// <param name="sha256Hex">Die 64 Hex-Zeichen in Kleinschreibung, sonst leer.</param>
    internal static bool TryParseDigest(string? digest, out string sha256Hex)
    {
        sha256Hex = string.Empty;
        if (digest is null || !digest.StartsWith(DigestPrefix, StringComparison.Ordinal))
            return false;

        var hex = digest.AsSpan(DigestPrefix.Length);
        if (hex.Length != Sha256HexLength)
            return false;

        foreach (var c in hex)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        sha256Hex = hex.ToString().ToLowerInvariant();
        return true;
    }

    /// <summary>True, wenn <paramref name="digest"/> ein Vergleich möglich macht.</summary>
    internal static bool IsVerifiable(string? digest) => TryParseDigest(digest, out _);

    /// <summary>
    /// Bringt einen als reine Hex-Zeichenkette veröffentlichten SHA256 (Inhalt einer <c>.sha256</c>-Datei,
    /// Whitespace/Zeilenende am Rand erlaubt) in die Digest-Form <c>sha256:&lt;hex&gt;</c>, die
    /// <see cref="TryParseDigest"/> prüft. Ob der Wert brauchbar ist, entscheidet allein dieser Parser.
    /// </summary>
    internal static string? FromBareHex(string? bareHex)
        => bareHex is null ? null : DigestPrefix + bareHex.Trim();

    /// <summary>
    /// Öffnet <paramref name="path"/> mit <see cref="FileShare.Read"/>, bildet über genau diesen
    /// Stream den SHA256 und vergleicht ihn mit <paramref name="expectedDigest"/>.
    /// <list type="bullet">
    /// <item><see cref="InstallerVerdict.Valid"/>: der Handle bleibt in der Rückgabe offen.</item>
    /// <item><see cref="InstallerVerdict.Mismatch"/>: Handle geschlossen, Datei gelöscht, Warning mit
    /// erwartetem und gemessenem Hash geloggt.</item>
    /// <item><see cref="InstallerVerdict.NotVerifiable"/>: nichts geöffnet, nichts gelöscht.</item>
    /// </list>
    /// Wirft <see cref="IOException"/>/<see cref="UnauthorizedAccessException"/>, wenn die Datei
    /// nicht lesbar ist (fehlt, von einem Schreiber gesperrt) — der Aufrufer startet dann nicht.
    /// </summary>
    public static async Task<InstallerCheck> CheckAsync(
        string path, string? expectedDigest, ILogger? logger, CancellationToken ct)
    {
        if (!TryParseDigest(expectedDigest, out var expectedHex))
        {
            logger?.LogWarning(
                "Download nicht pruefbar: erwarteter Digest fehlt oder ist ungueltig ({Digest}).",
                expectedDigest ?? "<null>");
            return new InstallerCheck(InstallerVerdict.NotVerifiable, null, null, null);
        }

        FileStream? stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, FileBufferSizeBytes, useAsync: true);
        try
        {
            var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            var actualHex = Convert.ToHexString(hash).ToLowerInvariant();

            if (string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
            {
                var valid = new InstallerCheck(InstallerVerdict.Valid, expectedHex, actualHex, stream);
                stream = null; // Besitz geht an InstallerCheck
                return valid;
            }

            logger?.LogWarning(
                "Download-Pruefsumme weicht ab, Datei wird geloescht: {Path} erwartet={Expected} gemessen={Actual}",
                path, expectedHex, actualHex);
            stream.Dispose();
            stream = null;
            TryDelete(path, logger);
            return new InstallerCheck(InstallerVerdict.Mismatch, expectedHex, actualHex, null);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    /// <summary>
    /// Löscht eine nicht mehr brauchbare heruntergeladene Datei (abweichend oder Teil-Download).
    /// Ein Löschfehler wird geloggt, nicht geworfen.
    /// </summary>
    internal static void TryDelete(string path, ILogger? logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Datei konnte nicht geloescht werden: {Path}", path);
        }
    }
}
