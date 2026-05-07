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

namespace UMI.Core.Wizards.Steps;

/// <summary>
/// Wizard-Step fuer Video- und Foto-Dateiendungen.
/// Defaults kommen vom gewaehlten Kamera-Typ (Step 2).
/// Validierung: Jede Extension beginnt mit ".".
/// </summary>
public class FileTypesStep : IWizardStep
{
    private const string VideoKey = "video_extensions";
    private const string PhotoKey = "photo_extensions";

    private readonly Func<string> _getCameraType;
    private readonly Func<CameraTypeStep?> _getTypeStep;

    /// <summary>Geparste Video-Extensions nach Apply.</summary>
    public string[] VideoExtensions { get; private set; } = [];

    /// <summary>Geparste Foto-Extensions nach Apply.</summary>
    public string[] PhotoExtensions { get; private set; } = [];

    /// <summary>
    /// Erstellt den Step.
    /// <paramref name="getCameraType"/> liefert den Kamera-Typ-String aus Step 2.
    /// <paramref name="getTypeStep"/> liefert den CameraTypeStep fuer Definition-Zugriff.
    /// </summary>
    public FileTypesStep(Func<string> getCameraType, Func<CameraTypeStep?> getTypeStep)
    {
        _getCameraType = getCameraType;
        _getTypeStep   = getTypeStep;
    }

    /// <inheritdoc/>
    public string Title => "Dateitypen";

    /// <inheritdoc/>
    public string Description => "Welche Dateiendungen sollen importiert werden?";

    /// <inheritdoc/>
    public bool CanSkip => false;

    /// <inheritdoc/>
    public IReadOnlyList<WizardField> Fields
    {
        get
        {
            var cameraType = _getCameraType();
            var typeStep   = _getTypeStep();

            var videoDefault = CameraTypeStep.GetDefaultVideoExtensions(cameraType);
            var photoDefault = CameraTypeStep.GetDefaultPhotoExtensions(cameraType);

            return
            [
                new WizardField(
                    Key:          VideoKey,
                    Label:        "Video-Endungen",
                    Type:         WizardFieldType.Text,
                    DefaultValue: videoDefault,
                    Required:     false,
                    HelpText:     "Kommasepariert, z.B. .mp4, .mov, .avi"),

                new WizardField(
                    Key:          PhotoKey,
                    Label:        "Foto-Endungen",
                    Type:         WizardFieldType.Text,
                    DefaultValue: photoDefault,
                    Required:     false,
                    HelpText:     "Kommasepariert, z.B. .jpg, .dng, .cr3")
            ];
        }
    }

    /// <inheritdoc/>
    public Task<WizardStepResult> ValidateAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var videoRaw = values.GetValueOrDefault(VideoKey) as string ?? string.Empty;
        var photoRaw = values.GetValueOrDefault(PhotoKey) as string ?? string.Empty;

        var videoExts = ParseExtensions(videoRaw);
        var photoExts = ParseExtensions(photoRaw);

        var invalidVideo = videoExts.Where(e => !e.StartsWith('.')).ToList();
        if (invalidVideo.Count > 0)
            return Task.FromResult(new WizardStepResult(
                false,
                $"Video-Extensions muessen mit '.' beginnen: {string.Join(", ", invalidVideo)}"));

        var invalidPhoto = photoExts.Where(e => !e.StartsWith('.')).ToList();
        if (invalidPhoto.Count > 0)
            return Task.FromResult(new WizardStepResult(
                false,
                $"Foto-Extensions muessen mit '.' beginnen: {string.Join(", ", invalidPhoto)}"));

        return Task.FromResult(new WizardStepResult(true));
    }

    /// <inheritdoc/>
    public Task ApplyAsync(
        Dictionary<string, object?> values,
        CancellationToken ct = default)
    {
        var videoRaw = values.GetValueOrDefault(VideoKey) as string ?? string.Empty;
        var photoRaw = values.GetValueOrDefault(PhotoKey) as string ?? string.Empty;

        VideoExtensions = [.. ParseExtensions(videoRaw)];
        PhotoExtensions = [.. ParseExtensions(photoRaw)];

        return Task.CompletedTask;
    }

    /// <summary>
    /// Parst kommaseparierte Extension-Liste und normalisiert auf Lowercase.
    /// Leerzeichen und leere Eintraege werden entfernt.
    /// </summary>
    public static IEnumerable<string> ParseExtensions(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        foreach (var part in raw.Split(','))
        {
            var ext = part.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(ext))
                yield return ext;
        }
    }
}
