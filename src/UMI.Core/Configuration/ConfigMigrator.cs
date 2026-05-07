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

using System.Text.Json.Nodes;

namespace UMI.Core.Configuration;

/// <summary>
/// One-step config schema migration. <see cref="From"/> and <see cref="To"/> are the
/// version strings on either side; <see cref="Migrate"/> mutates the raw JSON node
/// in place so subsequent steps and the final deserialization pick up the changes.
/// </summary>
/// <remarks>
/// Live example (template — leave commented until needed):
/// <code>
/// new ConfigMigration(
///     From: "2.0",
///     To:   "2.1",
///     Description: "Add app_settings.language with default 'en'",
///     Migrate: node => {
///         var app = node["app_settings"] ?? (node["app_settings"] = new JsonObject());
///         if (app["language"] is null) app["language"] = "en";
///     })
/// </code>
/// </remarks>
public sealed record ConfigMigration(
    string From,
    string To,
    string Description,
    Action<JsonNode> Migrate);

/// <summary>
/// Walks <see cref="ConfigMigration"/> steps to bring an on-disk config.json from
/// any older version up to <see cref="UmiConfig.CurrentVersion"/>.
/// </summary>
/// <remarks>
/// Add new migrations to <see cref="_migrations"/> in chronological order. Each
/// step's <c>To</c> must match the next step's <c>From</c>; the chain ends when
/// the version equals <see cref="UmiConfig.CurrentVersion"/>.
///
/// Currently empty — config schema has been at "2.0" since the v2 rewrite, no
/// breaking changes have shipped. The infrastructure is in place so the first
/// real schema bump can be a one-line addition.
/// </remarks>
public static class ConfigMigrator
{
    /// <summary>
    /// Ordered list of migration steps. Append new entries; never reorder or remove
    /// past entries — older configs would skip them otherwise.
    /// </summary>
    private static readonly IReadOnlyList<ConfigMigration> _migrations = Array.Empty<ConfigMigration>();

    /// <summary>
    /// Returns true if the given config version is older than <see cref="UmiConfig.CurrentVersion"/>
    /// AND a migration path is defined.
    /// </summary>
    public static bool NeedsMigration(string fromVersion)
    {
        if (string.IsNullOrWhiteSpace(fromVersion)) return false;
        if (fromVersion == UmiConfig.CurrentVersion) return false;
        return CanReachCurrent(fromVersion);
    }

    /// <summary>
    /// Applies migrations in order until the node is at <see cref="UmiConfig.CurrentVersion"/>.
    /// Mutates <paramref name="rawNode"/> in place. Throws if no path exists from
    /// <paramref name="fromVersion"/> — caller decides whether to abort or fall back to defaults.
    /// Returns a human-readable list of applied steps for logging.
    /// </summary>
    public static IReadOnlyList<string> Migrate(JsonNode rawNode, string fromVersion)
    {
        var applied = new List<string>();
        var current = fromVersion;

        while (current != UmiConfig.CurrentVersion)
        {
            var step = FindStep(current);
            if (step is null)
                throw new InvalidOperationException(
                    $"No config migration step defined from version '{current}' towards '{UmiConfig.CurrentVersion}'.");

            step.Migrate(rawNode);
            applied.Add($"{step.From} -> {step.To}: {step.Description}");
            current = step.To;
        }

        // Update the version field on the migrated node so the final deserialization
        // sees the new version.
        rawNode["version"] = UmiConfig.CurrentVersion;
        return applied;
    }

    /// <summary>
    /// Reports the chain that would be applied without mutating anything.
    /// </summary>
    public static IReadOnlyList<ConfigMigration> PlanFor(string fromVersion)
    {
        var plan = new List<ConfigMigration>();
        var current = fromVersion;
        while (current != UmiConfig.CurrentVersion)
        {
            var step = FindStep(current);
            if (step is null) break;
            plan.Add(step);
            current = step.To;
        }
        return plan;
    }

    private static ConfigMigration? FindStep(string from) =>
        _migrations.FirstOrDefault(m => m.From == from);

    private static bool CanReachCurrent(string from)
    {
        var current = from;
        var visited = new HashSet<string> { current };
        while (current != UmiConfig.CurrentVersion)
        {
            var step = FindStep(current);
            if (step is null) return false;
            if (!visited.Add(step.To)) return false;   // cycle guard
            current = step.To;
        }
        return true;
    }
}
