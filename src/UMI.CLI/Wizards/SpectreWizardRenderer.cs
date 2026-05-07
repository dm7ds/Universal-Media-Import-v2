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

using Spectre.Console;
using UMI.CLI.Helpers;
using UMI.Core.Wizards;

namespace UMI.CLI.Wizards;

/// <summary>
/// CLI-Implementierung von IWizardRenderer auf Basis von Spectre.Console.
/// Mappt jeden WizardFieldType auf den passenden Spectre.Console-Prompt.
/// </summary>
public class SpectreWizardRenderer : IWizardRenderer
{
    /// <inheritdoc/>
    public Task<Dictionary<string, object?>> RenderStepAsync(
        IWizardStep step,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureInteractive();

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold cyan]{Markup.Escape(step.Title)}[/]").RuleStyle("cyan"));

        if (!string.IsNullOrWhiteSpace(step.Description))
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(step.Description)}[/]");

        AnsiConsole.WriteLine();

        var values = new Dictionary<string, object?>();

        foreach (var field in step.Fields)
        {
            ct.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(field.HelpText))
                AnsiConsole.MarkupLine($"[grey italic]{Markup.Escape(field.HelpText)}[/]");

            values[field.Key] = field.Type switch
            {
                WizardFieldType.Text           => RenderText(field),
                WizardFieldType.Path           => RenderPath(field),
                WizardFieldType.Selection      => RenderSelection(field),
                WizardFieldType.MultiSelection => RenderMultiSelection(field),
                WizardFieldType.Toggle         => RenderToggle(field),
                WizardFieldType.Info           => RenderInfo(field),
                _                              => null
            };
        }

        return Task.FromResult(values);
    }

    /// <inheritdoc/>
    public Task<bool> ShowSummaryAsync(
        string title,
        IReadOnlyList<(string Label, string Value)> items,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureInteractive();

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold yellow]{Markup.Escape(title)}[/]").RuleStyle("yellow"));

        if (items.Count > 0)
        {
            var table = new Table()
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold]Einstellung[/]"))
                .AddColumn(new TableColumn("[bold]Wert[/]"));

            foreach (var (label, value) in items)
                table.AddRow(Markup.Escape(label), Markup.Escape(value));

            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
        var confirmed = AnsiConsole.Confirm("Bestätigen?", defaultValue: true);
        return Task.FromResult(confirmed);
    }

    /// <inheritdoc/>
    public Task ShowErrorAsync(string message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        AnsiConsole.MarkupLine($"[red bold]Fehler:[/] [red]{Markup.Escape(message)}[/]");
        AnsiConsole.WriteLine();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShowInfoAsync(string title, string message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureInteractive();

        var panel = new Panel(Markup.Escape(message))
        {
            Header = new PanelHeader($"[bold]{Markup.Escape(title)}[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("cyan")
        };

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> AskSkipAsync(string stepTitle, string description, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureInteractive();

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold cyan]{Markup.Escape(stepTitle)}[/]").RuleStyle("cyan"));

        if (!string.IsNullOrWhiteSpace(description))
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(description)}[/]");

        AnsiConsole.WriteLine();

        var skip = !AnsiConsole.Confirm("Diesen Schritt konfigurieren?", defaultValue: true);
        return Task.FromResult(skip);
    }

    private static string RenderText(WizardField field)
    {
        var prompt = new TextPrompt<string>($"[green]{Markup.Escape(field.Label)}[/]");

        if (!field.Required)
            prompt.AllowEmpty();

        if (field.DefaultValue is string defaultStr && !string.IsNullOrEmpty(defaultStr))
            prompt.DefaultValue(defaultStr);

        if (field.Required)
            prompt.Validate(v => !string.IsNullOrWhiteSpace(v)
                ? ValidationResult.Success()
                : ValidationResult.Error("[red]Dieses Feld ist erforderlich.[/]"));

        return AnsiConsole.Prompt(prompt);
    }

    private static string RenderPath(WizardField field)
    {
        var prompt = new TextPrompt<string>($"[green]{Markup.Escape(field.Label)}[/]");

        if (!field.Required)
            prompt.AllowEmpty();

        if (field.DefaultValue is string defaultPath && !string.IsNullOrEmpty(defaultPath))
            prompt.DefaultValue(defaultPath);

        prompt.Validate(v =>
        {
            if (string.IsNullOrWhiteSpace(v))
                return field.Required
                    ? ValidationResult.Error("[red]Pfad darf nicht leer sein.[/]")
                    : ValidationResult.Success();

            var normalized = Path.GetFullPath(v);

            if (Directory.Exists(normalized) || File.Exists(normalized))
                return ValidationResult.Success();

            var parent = Path.GetDirectoryName(normalized);
            if (parent != null && Directory.Exists(parent))
                return ValidationResult.Success();

            return ValidationResult.Error("[red]Pfad existiert nicht und kann nicht erstellt werden.[/]");
        });

        return AnsiConsole.Prompt(prompt);
    }

    private static string RenderSelection(WizardField field)
    {
        var options = field.Options ?? [];

        if (options.Count == 0)
            throw new InvalidOperationException(
                $"WizardField '{field.Key}' (Selection) hat keine Options definiert.");

        IEnumerable<string> orderedOptions = options;
        if (field.DefaultValue is string defaultSel && options.Contains(defaultSel))
        {
            orderedOptions = [defaultSel, .. options.Where(o => o != defaultSel)];
        }

        var prompt = new SelectionPrompt<string>()
            .Title($"[green]{Markup.Escape(field.Label)}[/]")
            .AddChoices(orderedOptions);

        return AnsiConsole.Prompt(prompt);
    }

    private static List<string> RenderMultiSelection(WizardField field)
    {
        var options = field.Options ?? [];

        if (options.Count == 0)
            throw new InvalidOperationException(
                $"WizardField '{field.Key}' (MultiSelection) hat keine Options definiert.");

        var prompt = new MultiSelectionPrompt<string>()
            .Title($"[green]{Markup.Escape(field.Label)}[/]")
            .NotRequired()
            .AddChoices(options);

        if (field.DefaultValue is IEnumerable<string> defaults)
        {
            foreach (var d in defaults)
            {
                if (options.Contains(d))
                    prompt.Select(d);
            }
        }

        return AnsiConsole.Prompt(prompt);
    }

    private static bool RenderToggle(WizardField field)
    {
        var defaultBool = field.DefaultValue is bool b && b;
        return AnsiConsole.Confirm($"[green]{Markup.Escape(field.Label)}[/]", defaultValue: defaultBool);
    }

    private static object? RenderInfo(WizardField field)
    {
        var panel = new Panel(Markup.Escape(field.Label))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey")
        };
        AnsiConsole.Write(panel);
        return null;
    }

    private static void EnsureInteractive()
    {
        if (!ConsoleHelper.IsInteractiveTerminal())
            throw new InvalidOperationException(
                "Wizard kann nur in einem interaktiven Terminal ausgeführt werden. " +
                "Piped/redirected output wird nicht unterstützt.");
    }
}
