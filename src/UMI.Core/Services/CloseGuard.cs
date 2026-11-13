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

using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Port auf die laufenden Importe der Anwendung (I-028). Die GUI implementiert ihn
/// mit ihrer einen Quelle fuer "laeuft ein Import?" — der Wächter zaehlt nicht selbst.
/// </summary>
public interface IImportActivity
{
    /// <summary>True, solange mindestens ein Kamera-Import laeuft.</summary>
    bool HasActiveImports { get; }

    /// <summary>Loest die vorhandene Cancellation aller laufenden Importe aus (kehrt sofort zurueck).</summary>
    void CancelAllImports();

    /// <summary>Wartet, bis kein Import mehr laeuft (Signal, kein Polling).</summary>
    Task WaitUntilIdleAsync(CancellationToken ct);
}

/// <summary>Die Rueckfrage "Import abbrechen und beenden?" — die GUI zeigt dafuer ihren Dialog.</summary>
public interface ICloseConfirmation
{
    /// <returns>true, wenn der Nutzer das Beenden trotz laufendem Import bestaetigt.</returns>
    Task<bool> ConfirmQuitWhileImportingAsync();
}

/// <summary>Ausgang von <see cref="CloseGuard.RequestCloseAsync"/>.</summary>
public enum CloseDecision
{
    /// <summary>Kein Import aktiv — schliessen wie bisher, ohne Rueckfrage.</summary>
    NothingRunning,

    /// <summary>Nutzer hat "nicht beenden" gewaehlt — Fenster bleibt offen, Importe laufen weiter.</summary>
    Declined,

    /// <summary>Bestaetigt, Importe sind sauber ausgelaufen — schliessen.</summary>
    ImportsStopped,

    /// <summary>Bestaetigt, aber die Importe haben nicht rechtzeitig angehalten — trotzdem schliessen.</summary>
    StopTimedOut,
}

/// <summary>
/// Schuetzt einen laufenden Import davor, beim Schliessen des Hauptfensters still abgebrochen zu
/// werden (I-028): fragt nach, bricht ueber die vorhandene Cancellation ab und wartet auf das
/// Auslaufen. Reine Ablauflogik — Dialog und Importzustand kommen ueber Ports herein.
/// </summary>
public sealed class CloseGuard(
    IImportActivity imports,
    ICloseConfirmation confirmation,
    ILogger<CloseGuard>? logger = null,
    TimeSpan? stopTimeout = null)
{
    /// <summary>
    /// Standard-Obergrenze fuer das Auslaufen nach der Bestaetigung. Kopieren und Prozesse stoppen binnen
    /// Millisekunden; ein laufender MTP-Dateidownload ist nicht unterbrechbar und kann laenger dauern.
    /// </summary>
    public static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _stopTimeout = stopTimeout ?? DefaultStopTimeout;

    /// <summary>True, solange mindestens ein Import laeuft (entscheidet synchron im Closing-Handler).</summary>
    public bool ImportsRunning => imports.HasActiveImports;

    /// <summary>
    /// Nach der Bestaetigung, direkt vor dem Abbrechen: ab hier wartet der Wächter bis zu
    /// <see cref="DefaultStopTimeout"/> auf das Auslaufen — die GUI zeigt in dieser Zeit einen Wartecursor.
    /// </summary>
    public event EventHandler? StoppingImports;

    /// <summary>
    /// Fragt bei laufendem Import nach, bricht bei "Ja" ab und wartet bis zu <see cref="DefaultStopTimeout"/>
    /// (bzw. dem im Konstruktor gesetzten Wert) auf das Auslaufen. Laeuft nichts, kommt keine Rueckfrage.
    /// </summary>
    public async Task<CloseDecision> RequestCloseAsync(CancellationToken ct = default)
    {
        if (!imports.HasActiveImports)
            return CloseDecision.NothingRunning;

        if (!await confirmation.ConfirmQuitWhileImportingAsync())
        {
            logger?.LogInformation("Beenden bei laufendem Import abgelehnt — Fenster bleibt offen");
            return CloseDecision.Declined;
        }

        logger?.LogInformation("Beenden bei laufendem Import bestaetigt — Importe werden abgebrochen");
        StoppingImports?.Invoke(this, EventArgs.Empty);
        imports.CancelAllImports();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_stopTimeout);
        try
        {
            await imports.WaitUntilIdleAsync(timeout.Token);
            return CloseDecision.ImportsStopped;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(
                "Importe haben nach {Seconds} s nicht angehalten — Anwendung wird trotzdem beendet",
                _stopTimeout.TotalSeconds);
            return CloseDecision.StopTimedOut;
        }
    }

    /// <summary>
    /// Windows-Abmeldung/Herunterfahren: die Sitzung endet in jedem Fall und das Schliessen ist nicht
    /// mehr ablehnbar, also keine Rueckfrage — nur die Cancellation ausloesen. Best effort: es wird
    /// nicht auf das Auslaufen gewartet (App.OnExit und Dispose brechen ohnehin ab); die Token-Callbacks
    /// laufen synchron und beenden dabei u. a. Kindprozesse wie Gyroflow.
    /// </summary>
    public void StopImportsForSessionEnd()
    {
        if (!imports.HasActiveImports) return;

        logger?.LogWarning("Sitzung endet bei laufendem Import — Importe werden ohne Rueckfrage abgebrochen");
        imports.CancelAllImports();
    }
}
