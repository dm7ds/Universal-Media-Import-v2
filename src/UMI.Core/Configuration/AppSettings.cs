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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace UMI.Core.Configuration;

/// <summary>
/// JsonConverter-Wrapper für AppMode, der lowercase-Serialisierung erzwingt
/// ("dau", "simple", "advanced" statt "Dau", "Simple", "Advanced").
/// JsonNamingPolicy.CamelCase produziert für einsilbige Wörter exakt lowercase.
/// </summary>
public class LowercaseAppModeConverter : JsonStringEnumConverter<AppMode>
{
    public LowercaseAppModeConverter() : base(JsonNamingPolicy.CamelCase) { }
}

/// <summary>
/// Betriebsmodus der App — steuert Sichtbarkeit von Features und Optionen.
/// </summary>
[JsonConverter(typeof(LowercaseAppModeConverter))]
public enum AppMode
{
    /// <summary>Minimalistisch — nur das Nötigste sichtbar (für Gelegenheitsnutzer).</summary>
    Dau,

    /// <summary>Standard-Ansicht — ausgewogene Feature-Darstellung (Default).</summary>
    Simple,

    /// <summary>Alle Features und Optionen sichtbar (für Power-User).</summary>
    Advanced
}

/// <summary>
/// App-weite GUI-Einstellungen (persistiert in config.json unter "app_settings").
/// Enthält Darstellungs- und Verhaltensoptionen die nichts mit dem Import-Prozess zu tun haben.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// UI language code (BCP 47). Supported: "en", "de". Default: "en".
    /// Applied at app startup to <see cref="System.Threading.Thread.CurrentUICulture"/>.
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    /// <summary>
    /// Betriebsmodus der App. Steuert welche Features und Optionen sichtbar sind.
    /// Default: Simple.
    /// </summary>
    [JsonPropertyName("mode")]
    public AppMode Mode { get; set; } = AppMode.Simple;

    /// <summary>
    /// Legacy: Wenn true, zeigt die GUI erweiterte Features an (Advanced Mode).
    /// Wird nur ausgewertet wenn "mode" nicht in der JSON gesetzt ist.
    /// Neu: Nutze stattdessen <see cref="Mode"/>.
    /// </summary>
    [JsonPropertyName("advanced_mode")]
    [Obsolete("Verwende Mode = AppMode.Advanced statt AdvancedMode = true.")]
    public bool AdvancedMode { get; set; } = false;

    /// <summary>
    /// Last folder path used for photo export in the Sequence Reviewer.
    /// Null when no export has been performed yet.
    /// Persisted via IConfigWriterService.UpdateSection&lt;AppSettings&gt;("app_settings", ...).
    /// </summary>
    [JsonPropertyName("last_export_path")]
    public string? LastExportPath { get; set; }

    /// <summary>
    /// Wenn true, prüft die GUI beim Start automatisch auf neue Versionen.
    /// Default: true.
    /// </summary>
    [JsonPropertyName("check_for_updates_on_startup")]
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>
    /// Führt Backward-Compat-Migration durch: Wenn "mode" nicht gesetzt war
    /// (noch auf Default = Simple) und "advanced_mode" = true gesetzt ist,
    /// wird Mode auf Advanced gesetzt.
    ///
    /// Diese Methode muss nach dem Deserialisieren aufgerufen werden.
    /// Sie ist ein No-op wenn "mode" explizit gesetzt wurde.
    /// </summary>
    /// <param name="modeWasExplicitlySet">
    /// True wenn "mode" im JSON-Dokument vorhanden war (dann gewinnt mode, AdvancedMode wird ignoriert).
    /// </param>
    public void ApplyLegacyMigration(bool modeWasExplicitlySet)
    {
#pragma warning disable CS0618
        if (!modeWasExplicitlySet && AdvancedMode)
        {
            Mode = AppMode.Advanced;
        }
#pragma warning restore CS0618
    }
}
