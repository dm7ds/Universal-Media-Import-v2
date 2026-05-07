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

using System.CommandLine;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.CLI.Wizards;
using UMI.Core.Services;
using UMI.Core.Wizards;

namespace UMI.CLI.Commands;

/// <summary>
/// Setup Command — Ersteinrichtung via First-Run-Wizard.
/// Subcommand "camera" startet den Camera-Setup-Wizard.
/// </summary>
public static class SetupCommand
{
    /// <summary>
    /// Erstellt und konfiguriert den "setup"-Command inkl. "camera"-Subcommand.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("setup", "Initial setup — wizard for basic configuration");

        command.SetHandler(async context =>
        {
            var ct         = context.GetCancellationToken();
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;

            await ExecuteSetupAsync(configPath, ct);
        });

        var cameraCommand = new Command("camera", "Set up a new camera");
        cameraCommand.SetHandler(async context =>
        {
            var ct         = context.GetCancellationToken();
            var configPath = context.ParseResult.GetValueForOption(Program.ConfigOption)!;

            await ExecuteCameraSetupAsync(configPath, ct);
        });

        command.AddCommand(cameraCommand);

        return command;
    }

    private static async Task ExecuteSetupAsync(string configPath, CancellationToken ct)
    {
        if (!ConsoleHelper.IsInteractiveTerminal())
        {
            ConsoleHelper.WriteError(CliStrings.Setup_NeedInteractive);
            return;
        }

        ConsoleHelper.WriteBanner(CliStrings.Setup_Banner);

        var configWriter = new ConfigWriterService();

        var resolvedConfigPath = ResolveConfigPath(configPath);

        if (File.Exists(resolvedConfigPath))
        {
            ConsoleHelper.WriteInfo(string.Format(CliStrings.Setup_EditingConfig, resolvedConfigPath));
            await configWriter.LoadAsync(resolvedConfigPath, ct);
        }
        else
        {
            ConsoleHelper.WriteInfo(string.Format(CliStrings.Setup_CreatingConfig, resolvedConfigPath));
            EnsureConfigDirectory(resolvedConfigPath);
            configWriter.InitializeNew(resolvedConfigPath);
        }

        var renderer = new SpectreWizardRenderer();
        var runner   = new WizardRunner(renderer);
        var wizard   = new FirstRunWizard(configWriter);

        var completed = await runner.RunAsync(wizard.GetSteps(), ct);

        if (!completed)
        {
            ConsoleHelper.WriteWarning(CliStrings.Setup_Aborted);
            return;
        }

        var confirmed = await renderer.ShowSummaryAsync(
            CliStrings.Setup_SavePrompt,
            BuildSummaryItems(configWriter),
            ct);

        if (!confirmed)
        {
            ConsoleHelper.WriteWarning(CliStrings.Setup_SaveCancelled);
            return;
        }

        await configWriter.SaveAsync(ct);
        ConsoleHelper.WriteSuccess(string.Format(CliStrings.Setup_Saved, resolvedConfigPath));

        if (wizard.WantsCameraSetup)
        {
            ConsoleHelper.WriteInfo(CliStrings.Setup_StartCameraWizard);
            Console.WriteLine();
            await RunCameraWizardAsync(configWriter, ct);
        }
    }

    private static async Task ExecuteCameraSetupAsync(string configPath, CancellationToken ct)
    {
        if (!ConsoleHelper.IsInteractiveTerminal())
        {
            ConsoleHelper.WriteError(CliStrings.Setup_CameraNeedInteractive);
            return;
        }

        ConsoleHelper.WriteBanner(CliStrings.Setup_CameraBanner);

        var configWriter       = new ConfigWriterService();
        var resolvedConfigPath = ResolveConfigPath(configPath);

        if (File.Exists(resolvedConfigPath))
        {
            await configWriter.LoadAsync(resolvedConfigPath, ct);
        }
        else
        {
            ConsoleHelper.WriteInfo(string.Format(CliStrings.Setup_CreatingConfig, resolvedConfigPath));
            EnsureConfigDirectory(resolvedConfigPath);
            configWriter.InitializeNew(resolvedConfigPath);
        }

        await RunCameraWizardAsync(configWriter, ct);
    }

    /// <summary>
    /// Startet den First-Run-Wizard fuer eine Config-Datei.
    /// Oeffentliche Variante fuer Aufrufe aus ConfigCommand/CameraCommand.
    /// </summary>
    public static async Task RunSetupWizardAsync(string configPath, CancellationToken ct)
    {
        await ExecuteSetupAsync(configPath, ct);
    }

    /// <summary>
    /// Startet den Camera-Setup-Wizard fuer eine Config-Datei.
    /// Oeffentliche Variante fuer Aufrufe aus ConfigCommand/CameraCommand.
    /// </summary>
    public static async Task RunCameraWizardAsync(string configPath, CancellationToken ct)
    {
        await ExecuteCameraSetupAsync(configPath, ct);
    }

    /// <summary>
    /// Fuehrt den Camera-Setup-Wizard aus (geteilt zwischen First-Run und direktem Aufruf).
    /// </summary>
    private static async Task RunCameraWizardAsync(IConfigWriterService configWriter, CancellationToken ct)
    {
        var renderer    = new SpectreWizardRenderer();
        var runner      = new WizardRunner(renderer);
        var cameraWizard = new CameraSetupWizard(configWriter);

        var completed = await runner.RunAsync(cameraWizard.GetSteps(), ct);

        if (!completed)
        {
            ConsoleHelper.WriteWarning(CliStrings.Setup_CameraAborted);
            return;
        }

        if (cameraWizard.CameraAdded)
        {
            ConsoleHelper.WriteInfo(CliStrings.Setup_CameraAddMore);
            ConsoleHelper.WriteInfo(CliStrings.Setup_CameraRegisterCards);
        }
    }

    /// <summary>
    /// Loest den Config-Pfad auf (relativ → absolut, mit ConfigPathResolver wenn nur Dateiname).
    /// </summary>
    private static string ResolveConfigPath(string configPath)
    {

        if (configPath == "config.json" || Path.GetFileName(configPath) == configPath)
        {
            var resolver = new ConfigPathResolver();
            return resolver.ConfigFile;
        }

        return Path.GetFullPath(configPath);
    }

    /// <summary>
    /// Stellt sicher dass das Verzeichnis fuer die Config-Datei existiert.
    /// </summary>
    private static void EnsureConfigDirectory(string configPath)
    {
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Erstellt die Summary-Tabellen-Eintraege aus dem ConfigWriter-State.
    /// </summary>
    private static IReadOnlyList<(string Label, string Value)> BuildSummaryItems(
        IConfigWriterService configWriter)
    {
        var config = configWriter.Config;
        var items  = new List<(string Label, string Value)>
        {
            ("Workbench",       string.IsNullOrEmpty(config.GlobalPaths.Workbench)
                                    ? CliStrings.Setup_NotSet     : config.GlobalPaths.Workbench),
            ("ExifTool",        string.IsNullOrEmpty(config.GlobalPaths.Tools.ExifTool)
                                    ? CliStrings.Setup_NotSet     : config.GlobalPaths.Tools.ExifTool),
            ("Gyroflow",        string.IsNullOrEmpty(config.GlobalPaths.Tools.Gyroflow)
                                    ? CliStrings.Setup_NotConfigured : config.GlobalPaths.Tools.Gyroflow),
            ("GPS Track Folder", string.IsNullOrEmpty(config.GlobalPaths.GpxSource)
                                    ? CliStrings.Setup_NotConfigured : config.GlobalPaths.GpxSource)
        };

        return items;
    }
}
