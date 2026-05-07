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

using System.Text.Json.Serialization;

namespace UMI.Core.Configuration;

public class ArchivingConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("include_delivery_by_default")]
    public bool IncludeDeliveryByDefault { get; set; } = false;

    [JsonPropertyName("auto_cleanup_workbench")]
    public bool AutoCleanupWorkbench { get; set; } = false;

    [JsonPropertyName("create_readme")]
    public bool CreateReadme { get; set; } = true;

    [JsonPropertyName("project_structure")]
    public ProjectStructure ProjectStructure { get; set; } = new();
}

public class ProjectStructure
{
    [JsonPropertyName("raw")]
    public string Raw { get; set; } = "RAW";

    [JsonPropertyName("delivery")]
    public string Delivery { get; set; } = "DELIVERY";

    [JsonPropertyName("edit")]
    public string Edit { get; set; } = "EDIT";

    [JsonPropertyName("project")]
    public string Project { get; set; } = "_PROJECT";
}
