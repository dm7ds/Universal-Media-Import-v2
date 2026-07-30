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

using UMI.Core.Configuration;
using UMI.Core.Models;

namespace UMI.Core.Services;

/// <summary>
/// Service für Burst-Profil-Matching basierend auf EXIF-Werten.
/// Evaluiert Condition-Groups (AND/OR Logik) gegen PhotoMetadata.
/// Stateless Service ohne Dependencies.
/// </summary>
public class BurstMatchingEngine
{
    /// <summary>
    /// Bestimmt welches Burst-Profil zu diesem Foto passt.
    /// Prüft Profile in Prioritäts-Reihenfolge, erstes Match gewinnt.
    /// Gibt Profil-Name zurück oder "Single" wenn keins matcht.
    /// </summary>
    public string MatchBurstProfile(PhotoMetadata metadata, BurstDetectionConfig? config)
    {

        if (config == null || !config.Enabled || metadata.IsVideo)
            return "Single";

        if (config.LoadedProfiles is null or { Count: 0 })
            return "Single";

        foreach (var profile in config.LoadedProfiles.OrderBy(p => p.Priority))
        {
            if (EvaluateConditionGroup(metadata, profile.MatchConditions))
            {
                return profile.Name;
            }
        }

        return "Single";
    }

    /// <summary>
    /// Evaluiert eine ConditionGroup rekursiv.
    /// AND: Alle Elemente (Conditions + Groups) müssen true sein.
    /// OR: Mindestens ein Element muss true sein.
    /// Public für Testing.
    /// </summary>
    public bool EvaluateConditionGroup(PhotoMetadata metadata, ConditionGroup group)
    {
        var isAnd = group.Operator.Equals("AND", StringComparison.OrdinalIgnoreCase);

        if (group.Conditions != null)
        {
            foreach (var condition in group.Conditions)
            {
                var result = EvaluateCondition(metadata, condition);

                if (isAnd && !result) return false;
                if (!isAnd && result) return true;
            }
        }

        if (group.Groups != null)
        {
            foreach (var subGroup in group.Groups)
            {
                var result = EvaluateConditionGroup(metadata, subGroup);

                if (isAnd && !result) return false;
                if (!isAnd && result) return true;
            }
        }

        return isAnd;
    }

    /// <summary>
    /// Evaluiert eine einzelne Condition gegen die EXIF-Werte.
    /// Public für DRY-Nutzung in BurstVisualizerService.
    /// </summary>
    public bool EvaluateCondition(PhotoMetadata metadata, MatchCondition condition)
    {
        var fieldValue = GetExifFieldValue(metadata, condition.Field);
        if (fieldValue == null) return false;

        return condition.Operator switch
        {
            ">"  => fieldValue.Value > condition.Value,
            ">=" => fieldValue.Value >= condition.Value,
            "<"  => fieldValue.Value < condition.Value,
            "<=" => fieldValue.Value <= condition.Value,
            "==" => Math.Abs(fieldValue.Value - condition.Value) < 0.001,
            "!=" => Math.Abs(fieldValue.Value - condition.Value) >= 0.001,
            _ => false
        };
    }

    /// <summary>
    /// Liest numerischen EXIF-Wert aus PhotoMetadata.ExifValues Dictionary.
    /// Verfügbare Felder: ExposureTime, ISO, FocalLength, Aperture, ShutterSpeed,
    /// ExposureCompensation, ContinuousDrive, ExposureMode.
    /// </summary>
    private double? GetExifFieldValue(PhotoMetadata metadata, string fieldName)
    {
        return metadata.ExifValues.TryGetValue(fieldName, out var value) ? value : null;
    }
}
