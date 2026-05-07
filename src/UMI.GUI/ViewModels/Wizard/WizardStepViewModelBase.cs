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

namespace UMI.GUI.ViewModels.Wizard;

/// <summary>
/// Base class for all wizard step view models.
/// Each step has a title, description, and a validity flag that controls
/// whether the wizard's Next button is enabled.
/// </summary>
public abstract class WizardStepViewModelBase : ViewModelBase
{
    /// <summary>Human-readable title shown in the wizard step indicator.</summary>
    public abstract string StepTitle { get; }

    /// <summary>Explanatory sub-text shown below the step title.</summary>
    public abstract string StepDescription { get; }

    private bool _isValid;
    /// <summary>
    /// True when the user has completed this step and may proceed to the next one.
    /// Derived steps set this via the protected setter.
    /// </summary>
    public bool IsValid
    {
        get => _isValid;
        protected set => SetProperty(ref _isValid, value);
    }

    private bool _isCurrentStep;
    /// <summary>
    /// True when this step is the currently active step.
    /// Set by SetupWizardViewModel when navigating — drives the step indicator highlight.
    /// </summary>
    public bool IsCurrentStep
    {
        get => _isCurrentStep;
        set => SetProperty(ref _isCurrentStep, value);
    }

    private bool _isCompleted;
    /// <summary>
    /// True when this step has been visited and the user has moved past it.
    /// Set by SetupWizardViewModel when navigating forward — drives the step indicator dim/check.
    /// </summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    /// <summary>
    /// Called when this step becomes the active step.
    /// Override to start watchers, pre-populate fields, etc.
    /// </summary>
    public virtual Task OnEnterAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Called when the user navigates away from this step (forward or back).
    /// Override to stop watchers, persist intermediate data, etc.
    /// </summary>
    public virtual Task OnLeaveAsync(CancellationToken ct = default) => Task.CompletedTask;
}
