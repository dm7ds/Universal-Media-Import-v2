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

using System.Diagnostics;
using UMI.Core.Constants;
using UMI.Core.Services;

namespace UMI.Core.Wizards.Steps;

/// <summary>
/// Wizard-Step fuer ExifTool-Erkennung und -Konfiguration.
/// Prueft zuerst mitgelieferte Pfade, dann PATH. Validiert via "exiftool -ver".
/// </summary>
public class ExifToolStep(IConfigWriterService configWriter) : IWizardStep
{
    private const string FieldKey = "exiftool_path";
    private const string FoundKey = "exiftool_found_info";

    private readonly string? _detectedPath = DetectExifTool();

    /// <inheritdoc/>
    public string Title => "ExifTool";

    /// <inheritdoc/>
    public string Description => "ExifTool wird benoetigt um Metadaten zu lesen und zu schreiben.";

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields
    {
        get
        {
            if (_detectedPath != null)
            {

                return
                [
                    new WizardField(
                        Key: FoundKey,
                        Label: $"ExifTool automatisch gefunden:\n  {_detectedPath}\n\nVersion wird beim Fortfahren geprueft.",
                        Type: WizardFieldType.Info)
                ];
            }

            return
            [
                new WizardField(
                    Key: FieldKey,
                    Label: "Pfad zu exiftool.exe",
                    Type: WizardFieldType.Path,
                    Required: true,
                    HelpText: "ExifTool nicht gefunden. Download: https://exiftool.org/")
            ];
        }
    }

    /// <inheritdoc/>
    public async Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var path = _detectedPath ?? values.GetValueOrDefault(FieldKey) as string;

        if (string.IsNullOrWhiteSpace(path))
            return new WizardStepResult(false, "Kein ExifTool-Pfad angegeben.");

        path = Path.GetFullPath(path);

        if (!File.Exists(path))
            return new WizardStepResult(false, $"ExifTool nicht gefunden: {path}");

        var version = await RunExifToolVersionAsync(path, ct);
        if (version == null)
            return new WizardStepResult(false, $"ExifTool konnte nicht ausgefuehrt werden: {path}");

        return new WizardStepResult(true);
    }

    /// <inheritdoc/>
    public async Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var path = _detectedPath ?? values.GetValueOrDefault(FieldKey) as string ?? string.Empty;
        path = Path.GetFullPath(path);

        configWriter.SetToolPath(ToolKeys.ExifTool, path);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Auto-Detect ExifTool in dieser Reihenfolge:
    /// 1. ConfigPathResolver.DefaultExifToolPath ({tools/exiftool/exiftool.exe})
    /// 2. {AppContext.BaseDirectory}/tools/exiftool.exe (Fallback, älteres Layout)
    /// 3. {AppContext.BaseDirectory}/exiftool.exe
    /// 4. where exiftool (PATH-Scan)
    /// </summary>
    private static string? DetectExifTool()
    {
        var baseDir = AppContext.BaseDirectory;

        var defaultPath = ConfigPathResolver.DefaultExifToolPath;
        if (File.Exists(defaultPath))
            return defaultPath;

        var toolsPath = Path.Combine(baseDir, "tools", "exiftool.exe");
        if (File.Exists(toolsPath))
            return toolsPath;

        var sideBySide = Path.Combine(baseDir, "exiftool.exe");
        if (File.Exists(sideBySide))
            return sideBySide;

        return ToolDetectionHelper.FindViaWhere("exiftool");
    }

    /// <summary>
    /// Fuehrt "exiftool -ver" aus und gibt die Versionsnummer zurueck (oder null bei Fehler).
    /// </summary>
    private static async Task<string?> RunExifToolVersionAsync(string exifToolPath, CancellationToken ct)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exifToolPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-ver");

            using var process = Process.Start(startInfo);
            if (process == null)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            var version = output.Trim();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch
        {
            return null;
        }
    }
}
