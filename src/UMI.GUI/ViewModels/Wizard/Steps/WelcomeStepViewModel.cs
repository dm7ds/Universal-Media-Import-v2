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
using System.Windows.Input;
using UMI.Core.Configuration;
using UMI.GUI.Resources;

namespace UMI.GUI.ViewModels.Wizard.Steps;

/// <summary>
/// Step 1 — Welcome screen. User selects the app mode (Dau / Simple / Advanced).
/// </summary>
public class WelcomeStepViewModel : WizardStepViewModelBase
{
    private readonly WizardSession _session;

    public override string StepTitle => Strings.Wizard_WelcomeTitle;
    public override string StepDescription => Strings.Wizard_WelcomeDescription;

    public ObservableCollection<ModeOption> ModeOptions { get; }

    private ModeOption? _selectedMode;
    /// <summary>Currently selected mode option. Sets Session.Mode and IsSelected on each item.</summary>
    public ModeOption? SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value) && value is not null)
            {

                foreach (var opt in ModeOptions)
                    opt.IsSelected = ReferenceEquals(opt, value);

                _session.Mode = value.Mode;
                IsValid = true;
                ModeChanged?.Invoke(value.Mode);
            }
        }
    }

    public ICommand SelectModeCommand { get; }

    /// <summary>
    /// Invoked when the user selects a different mode.
    /// SetupWizardViewModel wires this to RebuildActiveSteps.
    /// </summary>
    public Action<AppMode>? ModeChanged { get; set; }

    public WelcomeStepViewModel(WizardSession session)
    {
        _session = session;

        ModeOptions = new ObservableCollection<ModeOption>
        {
            new ModeOption
            {
                Mode = AppMode.Dau,
                Title = AppModeLabels.Dau,
                Description = Strings.Wizard_ModeEasyDesc,
                IconPath = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M11,17V16H9V14H13V13H10A1,1 0 0,1 9,12V9A1,1 0 0,1 10,8H11V7H13V8H15V10H11V11H14A1,1 0 0,1 15,12V15A1,1 0 0,1 14,16H13V17H11Z"
            },
            new ModeOption
            {
                Mode = AppMode.Simple,
                Title = AppModeLabels.Simple,
                Description = Strings.Wizard_ModeStandardDesc,
                IconPath = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M11,7H13V13H11V7M11,15H13V17H11V15Z"
            },
            new ModeOption
            {
                Mode = AppMode.Advanced,
                Title = AppModeLabels.Advanced,
                Description = Strings.Wizard_ModeAdvancedDesc,
                IconPath = "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.68 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z"
            }
        };

        SelectModeCommand = new RelayCommand<ModeOption>(opt => { if (opt is not null) SelectedMode = opt; });

        var defaultOpt = ModeOptions.FirstOrDefault(o => o.Mode == _session.Mode) ?? ModeOptions[1];
        SelectedMode = defaultOpt;
    }

    public override Task OnEnterAsync(CancellationToken ct = default)
    {

        var currentOpt = ModeOptions.FirstOrDefault(o => o.Mode == _session.Mode);
        if (currentOpt is not null && !ReferenceEquals(currentOpt, _selectedMode))
        {

            _selectedMode = currentOpt;
            foreach (var opt in ModeOptions)
                opt.IsSelected = ReferenceEquals(opt, currentOpt);
            OnPropertyChanged(nameof(SelectedMode));
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Represents a selectable app mode option shown as a card in the Welcome step.
/// Implements INotifyPropertyChanged so IsSelected drives highlight in XAML (WPF Falle #13).
/// </summary>
public class ModeOption : ViewModelBase
{
    public AppMode Mode { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    /// <summary>WPF Path geometry data for the mode icon.</summary>
    public string IconPath { get; init; } = string.Empty;

    private bool _isSelected;
    /// <summary>True when this option is the currently selected one. Drives selection highlight in XAML.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
