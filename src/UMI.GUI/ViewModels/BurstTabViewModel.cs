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

using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the Burst sub-tab in Settings.
/// Loads all burst-detection profiles from disk and exposes them as BurstProfileCardViewModels.
/// Provides Add and Delete commands for profile management.
/// Initialize() is called from MainViewModel.LoadAsync() after config is ready.
/// </summary>
public class BurstTabViewModel : ViewModelBase
{
    private readonly BurstProfileLoader _profileLoader;
    private readonly IBurstVisualizerService _visualizerService;
    private readonly IExifFieldAnalyzerService _scannerService;
    private readonly BurstMatchingEngine _matchingEngine;
    private readonly ISequenceSidecarService _sequenceSidecarService;

    private readonly Dictionary<string, VisualizerData> _folderDataCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>All loaded profile cards — one card per .umi file.</summary>
    public ObservableCollection<BurstProfileCardViewModel> Profiles { get; } = new();

    /// <summary>
    /// Full-screen Burst Visualizer v2 panel.
    /// Shares the <see cref="Profiles"/> reference so profile additions/removals
    /// are visible immediately in the visualizer's profile picker.
    /// </summary>
    public BurstVisualizerV2ViewModel VisualizerV2 { get; }

    /// <summary>Adds a new (unsaved) profile card to the collection.</summary>
    public ICommand AddProfileCommand { get; }

    /// <summary>Deletes a profile card and its backing .umi file.</summary>
    public ICommand DeleteProfileCommand { get; }

    public BurstTabViewModel(
        BurstProfileLoader profileLoader,
        IBurstVisualizerService visualizerService,
        IExifFieldAnalyzerService scannerService,
        BurstMatchingEngine matchingEngine,
        AutoPresetGenerator presetGenerator,
        ISequenceSidecarService sequenceSidecarService)
    {
        _profileLoader          = profileLoader;
        _visualizerService      = visualizerService;
        _scannerService         = scannerService;
        _matchingEngine         = matchingEngine;
        _sequenceSidecarService = sequenceSidecarService;

        VisualizerV2 = new BurstVisualizerV2ViewModel(
            visualizerService,
            sequenceSidecarService,
            presetGenerator,
            profileLoader,
            Profiles,
            loadFolderCached: GetOrLoadFolderDataAsync,
            onProfileAdded: AddProfileFromPreset,
            onDataLoaded: OnBurstStudioDataLoaded);

        AddProfileCommand    = new RelayCommand(ExecuteAddProfile);
        DeleteProfileCommand = new RelayCommand<BurstProfileCardViewModel>(ExecuteDeleteProfile);
    }

    /// <summary>
    /// Loads all available profiles from disk and builds BurstProfileCardViewModels.
    /// Called from MainViewModel.LoadAsync() after config is loaded.
    /// Can be called again to refresh from disk.
    /// </summary>
    public void Initialize()
    {
        Profiles.Clear();

        var names    = _profileLoader.ListAvailableProfiles();
        var profiles = _profileLoader.LoadProfiles(names);

        int nextAutoColor = 0;
        var profilesToSave = new List<BurstProfile>();

        foreach (var profile in profiles)
        {
            if (profile.ColorIndex < 0 || profile.ColorIndex >= 8)
            {
                profile.ColorIndex = nextAutoColor++ % 8;
                profilesToSave.Add(profile);
            }

            Profiles.Add(CreateCardViewModel(profile));
        }

        UpdateAllPriorities();

        if (profilesToSave.Count > 0)
            _ = PersistAutoColorsAsync(profilesToSave);
    }

    private async Task PersistAutoColorsAsync(List<BurstProfile> profiles)
    {
        foreach (var profile in profiles)
        {
            try { await _profileLoader.SaveProfileAsync(profile); }
            catch { /* best-effort — will be assigned again on next load */ }
        }
    }

    private void ExecuteAddProfile()
    {

        var usedColors = Profiles.Select(p => p.ColorIndex).Where(c => c >= 0).ToHashSet();
        int newColor = 0;
        for (int c = 0; c < 8; c++)
        {
            if (!usedColors.Contains(c)) { newColor = c; break; }
        }

        var newProfile = new BurstProfile
        {
            Name        = GenerateUniqueName(),
            Description = Strings.Burst_NewProfileDescription,
            Priority    = 0,
            ColorIndex  = newColor
        };

        var cardVm = CreateCardViewModel(newProfile);
        cardVm.IsExpanded = true;
        cardVm.IsEditing  = true;
        cardVm.IsDirty    = true;

        Profiles.Add(cardVm);
        UpdateAllPriorities();
    }

    private void ExecuteDeleteProfile(BurstProfileCardViewModel? profile)
    {
        if (profile == null) return;

        _profileLoader.DeleteProfile(profile.Name);
        Profiles.Remove(profile);
    }

    /// <summary>
    /// Moves a profile card from <paramref name="fromIndex"/> to <paramref name="toIndex"/>
    /// and recalculates all priorities so they match the new visual order.
    /// Called from BurstTab.xaml.cs code-behind after a successful DnD drop.
    /// </summary>
    public void MoveProfile(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || toIndex < 0
            || fromIndex >= Profiles.Count || toIndex >= Profiles.Count
            || fromIndex == toIndex)
            return;

        var item = Profiles[fromIndex];
        Profiles.RemoveAt(fromIndex);
        Profiles.Insert(toIndex, item);

        UpdateAllPriorities();
    }

    /// <summary>
    /// Assigns Priority values based on collection index (index * 10 + 10).
    /// This is the SSOT for priorities — the Priority field in each card is
    /// a derived value, never manually entered.
    /// </summary>
    private void UpdateAllPriorities()
    {
        for (int i = 0; i < Profiles.Count; i++)
            Profiles[i].Priority = (i * 10) + 10;
    }

    /// <summary>
    /// Clears IsDropTargetAbove and IsDropTargetBelow on all profile cards.
    /// Called from BurstTab.xaml.cs code-behind in Card_DragLeave and Card_Drop.
    /// </summary>
    public void ClearAllDropIndicators()
    {
        foreach (var p in Profiles)
        {
            p.IsDropTargetAbove = false;
            p.IsDropTargetBelow = false;
        }
    }

    private BurstProfileCardViewModel CreateCardViewModel(BurstProfile profile)
    {
        return new BurstProfileCardViewModel(
            profile,
            _profileLoader,
            _visualizerService,
            _scannerService,
            _matchingEngine,
            loadFolderCached: GetOrLoadFolderDataAsync);
    }

    /// <summary>
    /// Returns cached <see cref="VisualizerData"/> for the given folder, or loads it
    /// via <see cref="IBurstVisualizerService.LoadFolderAsync"/> on cache miss.
    /// Shared by BurstVisualizerV2ViewModel and all VisualizerViewModels (profile cards)
    /// so that expensive ExifTool thumbnail extraction happens only once per folder.
    /// </summary>
    private async Task<VisualizerData> GetOrLoadFolderDataAsync(
        string folderPath,
        IProgress<ExifScanProgress>? progress,
        CancellationToken ct)
    {
        var normalizedPath = Path.GetFullPath(folderPath);

        if (_folderDataCache.TryGetValue(normalizedPath, out var cached))
            return cached;

        var data = await _visualizerService.LoadFolderAsync(normalizedPath, progress, ct, loadThumbnails: true);
        _folderDataCache[normalizedPath] = data;
        return data;
    }

    private string GenerateUniqueName()
    {
        var existing = Profiles.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int i = 1;
        while (existing.Contains($"NewProfile{i}")) i++;
        return $"NewProfile{i}";
    }

    /// <summary>
    /// Called by <see cref="BurstVisualizerV2ViewModel"/> via the <c>onDataLoaded</c> callback
    /// when the Burst Studio loads a folder. Auto-evaluates ALL profile cards with the shared
    /// <see cref="VisualizerData"/> so the user sees results immediately without manual load per card.
    /// </summary>
    private async void OnBurstStudioDataLoaded(VisualizerData data)
    {
        foreach (var profile in Profiles)
        {
            try
            {
                await profile.AutoEvaluateAsync(data);
            }
            catch (Exception)
            {

            }
        }
    }

    /// <summary>
    /// Called by <see cref="BurstVisualizerV2ViewModel"/> via the <c>onProfileAdded</c> callback
    /// after the user accepts an auto-generated preset.
    /// The profile has already been saved to disk by the visualizer — this method only
    /// adds a new card to <see cref="Profiles"/> and updates priorities.
    /// </summary>
    public void AddProfileFromPreset(BurstProfile profile)
    {
        var cardVm = CreateCardViewModel(profile);
        Profiles.Add(cardVm);
        UpdateAllPriorities();
    }
}
