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

namespace UMI.Core.Services;

/// <summary>
/// Lädt die optionalen Hilfswerkzeuge (Gyroflow, FFprobe) herunter und installiert sie —
/// ausschließlich nach erfolgreicher SHA256-Prüfung des Archivs (I-022).
/// Der einzige Weg, ein heruntergeladenes Werkzeug-Archiv zu entpacken.
/// </summary>
public interface IToolInstallService
{
    /// <summary>
    /// Pfad der bereits von UMI installierten ausführbaren Datei von <paramref name="toolKey"/>
    /// (<see cref="Constants.ToolKeys"/>), sonst null. Wirft <see cref="ArgumentException"/> bei einem
    /// Schlüssel, den UMI nicht herunterladen kann.
    /// </summary>
    string? FindInstalledExecutable(string toolKey);

    /// <summary>
    /// Ermittelt Download-URL und veröffentlichten SHA256 des Werkzeugs, lädt das Archiv, prüft es
    /// <b>vor dem Entpacken</b> und entpackt es nur bei <see cref="ToolInstallOutcome.Installed"/>.
    /// <list type="bullet">
    /// <item>Kein brauchbarer Prüfwert: nichts wird geladen (<see cref="ToolInstallOutcome.NotVerifiable"/>).</item>
    /// <item>Abweichender Hash: Archiv gelöscht, nichts entpackt (<see cref="ToolInstallOutcome.Mismatch"/>).</item>
    /// <item>Archiv ohne die erwartete Datei: bisherige Installation bleibt unverändert
    /// (<see cref="ToolInstallOutcome.ExecutableNotFound"/>).</item>
    /// <item>Bisherige Installation nicht ersetzbar (Werkzeug läuft, Dateien gesperrt): sie bleibt vollständig
    /// stehen (<see cref="ToolInstallOutcome.InUse"/>).</item>
    /// </list>
    /// Das Archiv liegt nach jedem Ausgang nicht mehr im Temp-Ordner, auch nicht als Teil-Datei bei Abbruch
    /// oder Fehler. Meldet Download-Fortschritt 0.0–1.0 via <paramref name="progress"/>; nach 1.0 folgen
    /// Prüfung und Entpacken. Wirft:
    /// <see cref="OperationCanceledException"/> (Abbruch),
    /// <see cref="System.Net.Http.HttpRequestException"/> (Netzwerk, HTTP-Fehlerstatus),
    /// <see cref="TimeoutException"/> (Quelle antwortet nicht),
    /// <see cref="System.Text.Json.JsonException"/> (unlesbare GitHub-Antwort),
    /// <see cref="IOException"/> und <see cref="UnauthorizedAccessException"/> (Dateisystem; scheitert das Aktivieren,
    /// ist die alte Installation wiederhergestellt),
    /// <see cref="System.IO.InvalidDataException"/> (Archiv ist nach bestandener Prüfung keine lesbare ZIP) und
    /// <see cref="ArgumentException"/> (unbekannter Schlüssel).
    /// </summary>
    Task<ToolInstallResult> InstallAsync(string toolKey, IProgress<double>? progress, CancellationToken ct);
}

/// <summary>Ausgang von <see cref="IToolInstallService.InstallAsync"/>.</summary>
public enum ToolInstallOutcome
{
    /// <summary>Hash stimmt, Archiv entpackt, ausführbare Datei gefunden.</summary>
    Installed,

    /// <summary>
    /// Die Quelle liefert keinen brauchbaren Prüfwert. Es wurde nichts geladen und nichts installiert;
    /// der Nutzer kann das Werkzeug manuell installieren und den Pfad angeben.
    /// </summary>
    NotVerifiable,

    /// <summary>Der SHA256 des Archivs weicht vom veröffentlichten ab — Archiv gelöscht, nichts entpackt.</summary>
    Mismatch,

    /// <summary>Hash stimmt, das Archiv enthält aber die erwartete ausführbare Datei nicht.</summary>
    ExecutableNotFound,

    /// <summary>
    /// Hash stimmt, die bisherige Installation lässt sich aber nicht ersetzen, weil Dateien darin in Benutzung
    /// sind (das Werkzeug läuft). Nichts wurde verändert; der Nutzer soll das Werkzeug schließen und es erneut versuchen.
    /// </summary>
    InUse,
}

/// <summary>Ergebnis von <see cref="IToolInstallService.InstallAsync"/>.</summary>
/// <param name="Outcome">Ausgang der Installation.</param>
/// <param name="ExecutablePath">Voller Pfad der installierten Datei; nur bei <see cref="ToolInstallOutcome.Installed"/> gesetzt.</param>
public sealed record ToolInstallResult(ToolInstallOutcome Outcome, string? ExecutablePath = null);
