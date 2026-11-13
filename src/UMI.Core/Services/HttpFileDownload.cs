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
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>Ergebnis von <see cref="HttpFileDownload.GetTextAsync"/>.</summary>
/// <param name="ResolvedUri">Adresse der Antwort nach Weiterleitungen; null, wenn der Handler sie nicht meldet.</param>
/// <param name="Text">Der Inhalt; null, wenn er das Byte-Limit überschreitet.</param>
internal sealed record BoundedText(Uri? ResolvedUri, string? Text);

/// <summary>
/// Die eine Stelle, die HTTP-Antworten streamt (<c>ResponseHeadersRead</c>): eine URL in eine Datei
/// (Installer- und Werkzeug-Download) oder eine kleine, begrenzte Textantwort.
/// Geprüft wird nicht hier, sondern vom Aufrufer über <see cref="InstallerIntegrity"/>; hier wird nur
/// geladen und bei Fehler die Teil-Datei gelöscht.
/// </summary>
internal static class HttpFileDownload
{
    private const int BufferSizeBytes = 81920;

    /// <summary>
    /// Lädt <paramref name="url"/> nach <paramref name="targetPath"/>. Bei Abbruch oder Fehler wird die
    /// Teil-Datei gelöscht und die Exception weitergereicht — nach einem Wurf liegt nichts am Ziel.
    /// Meldet Fortschritt 0.0–1.0, am Ende immer 1.0 (auch ohne Content-Length).
    /// </summary>
    internal static async Task DownloadAsync(
        HttpClient httpClient, string url, string targetPath, IProgress<double>? progress,
        ILogger? logger, CancellationToken ct)
    {
        try
        {
            await StreamToFileAsync(httpClient, url, targetPath, progress, ct).ConfigureAwait(false);
        }
        catch
        {
            // Abbruch oder Fehler: die Teil-Datei soll nicht in %TEMP% liegen bleiben.
            InstallerIntegrity.TryDelete(targetPath, logger);
            throw;
        }
    }

    /// <summary>
    /// Liest eine kleine Textantwort (z. B. eine <c>.sha256</c>-Datei) und liest dabei <b>höchstens
    /// <paramref name="maxBytes"/> Bytes</b> aus dem Netz — eine größere Antwort wird nicht gepuffert.
    /// <see cref="BoundedText.Text"/> ist null, wenn die Antwort länger ist. <see cref="BoundedText.ResolvedUri"/>
    /// ist die Adresse nach Weiterleitungen.
    /// </summary>
    internal static async Task<BoundedText> GetTextAsync(
        HttpClient httpClient, string url, int maxBytes, CancellationToken ct)
    {
        using var response = await httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buffer = new byte[maxBytes + 1];
        var filled = 0;
        int read;
        while (filled < buffer.Length
               && (read = await stream.ReadAsync(buffer.AsMemory(filled), ct).ConfigureAwait(false)) > 0)
        {
            filled += read;
        }

        var text = filled <= maxBytes ? System.Text.Encoding.UTF8.GetString(buffer, 0, filled) : null;
        return new BoundedText(response.RequestMessage?.RequestUri, text);
    }

    private static async Task StreamToFileAsync(
        HttpClient httpClient, string url, string targetPath, IProgress<double>? progress, CancellationToken ct)
    {
        using var response = await httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(
            targetPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSizeBytes, useAsync: true);

        var buffer = new byte[BufferSizeBytes];
        long bytesRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            bytesRead += read;

            if (progress is not null && totalBytes > 0)
                progress.Report((double)bytesRead / totalBytes.Value);
        }

        // Ensure 100% is reported even when Content-Length was unknown
        progress?.Report(1.0);
    }
}
