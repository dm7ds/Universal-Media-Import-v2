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
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.ViewModels.Wizard.Steps;

namespace UMI.GUI.ViewModels.Wizard;

/// <summary>
/// Orchestrates the first-run setup wizard.
/// Manages step navigation (Next / Back), step filtering per app mode,
/// loopback for the "add another camera" flow, and final config persistence.
/// </summary>
public class SetupWizardViewModel : ViewModelBase, IDisposable
{
    private readonly IConfigWriterService _configWriter;
    private readonly ConfigPathResolver _pathResolver;

    private WelcomeStepViewModel? _welcomeStep;
    private WorkbenchStepViewModel? _workbenchStep;
    private SourceDetectionStepViewModel? _sourceDetectionStep;
    private CameraConfirmStepViewModel? _cameraConfirmStep;
    private AddCardsStepViewModel? _addCardsStep;
    private MoreCamerasStepViewModel? _moreCamerasStep;
    private ToolsStepViewModel? _toolsStep;
    private GpsStepViewModel? _gpsStep;
    private SummaryStepViewModel? _summaryStep;

    private readonly ICardDetectionService _cardDetection;
    private readonly IDriveWatcherService _driveWatcher;
    private readonly CameraTypeLoader _typeLoader;
    private readonly BurstProfileLoader? _burstProfileLoader;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<ToolsViewModel>? _toolsLogger;

    /// <summary>The ordered list of steps visible in the current app mode.</summary>
    public ObservableCollection<WizardStepViewModelBase> ActiveSteps { get; } = new();

    private int _currentStepIndex;
    /// <summary>Zero-based index into <see cref="ActiveSteps"/>.</summary>
    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        private set
        {
            if (SetProperty(ref _currentStepIndex, value))
            {
                OnPropertyChanged(nameof(CurrentStep));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(IsLastStep));
                OnPropertyChanged(nameof(IsFirstStep));
            }
        }
    }

    /// <summary>The step the user is currently on.</summary>
    public WizardStepViewModelBase CurrentStep => ActiveSteps[CurrentStepIndex];

    /// <summary>True when the user can advance to the next step.</summary>
    public bool CanGoNext => CurrentStepIndex < ActiveSteps.Count - 1 && CurrentStep.IsValid;

    /// <summary>True when the user can go back to the previous step.</summary>
    public bool CanGoBack => CurrentStepIndex > 0;

    /// <summary>True when the current step is the last one (shows Finish instead of Next).</summary>
    public bool IsLastStep => CurrentStepIndex == ActiveSteps.Count - 1;

    /// <summary>True when the current step is the first one (hides the Back button).</summary>
    public bool IsFirstStep => CurrentStepIndex == 0;

    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand CancelCommand { get; }

    /// <summary>Shared state container passed to all steps.</summary>
    public WizardSession Session { get; }

    private bool _isBusy;
    /// <summary>True while FinishAsync is persisting data.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Raised after FinishAsync completes successfully.</summary>
    public event EventHandler? WizardCompleted;

    /// <summary>Raised when the user clicks Cancel.</summary>
    public event EventHandler? WizardCancelled;

    private WizardStepViewModelBase? _hookedStep;

    public SetupWizardViewModel(
        IConfigWriterService configWriter,
        ICardDetectionService cardDetection,
        IDriveWatcherService driveWatcher,
        ConfigPathResolver pathResolver,
        CameraTypeLoader typeLoader,
        BurstProfileLoader? burstProfileLoader = null,
        ILogger<ToolsViewModel>? toolsLogger = null)
    {
        _configWriter = configWriter;
        _pathResolver = pathResolver;

        _cardDetection = cardDetection;
        _driveWatcher = driveWatcher;
        _typeLoader = typeLoader;
        _burstProfileLoader = burstProfileLoader;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _toolsLogger = toolsLogger;

        Session = new WizardSession();

        NextCommand   = new RelayCommand(ExecuteNext,   () => CanGoNext);
        BackCommand   = new RelayCommand(ExecuteBack,   () => CanGoBack);
        FinishCommand = new RelayCommand(ExecuteFinish, () => IsLastStep && CurrentStep.IsValid && !IsBusy);
        CancelCommand = new RelayCommand(ExecuteCancel);

        RebuildActiveSteps(Session.Mode);
    }

    private WelcomeStepViewModel GetWelcomeStep()
    {
        if (_welcomeStep is not null) return _welcomeStep;
        _welcomeStep = new WelcomeStepViewModel(Session);
        _welcomeStep.ModeChanged = mode =>
        {
            Session.Mode = mode;
            RebuildActiveSteps(mode);
        };
        return _welcomeStep;
    }

    private WorkbenchStepViewModel GetWorkbenchStep()
        => _workbenchStep ??= new WorkbenchStepViewModel(Session);

    private SourceDetectionStepViewModel GetSourceDetectionStep()
        => _sourceDetectionStep ??= new SourceDetectionStepViewModel(Session, _driveWatcher, _cardDetection, _dispatcher);

    private CameraConfirmStepViewModel GetCameraConfirmStep()
        => _cameraConfirmStep ??= new CameraConfirmStepViewModel(Session, _configWriter, _typeLoader);

    private AddCardsStepViewModel GetAddCardsStep()
        => _addCardsStep ??= new AddCardsStepViewModel(Session, _driveWatcher, _dispatcher);

    private MoreCamerasStepViewModel GetMoreCamerasStep()
    {
        if (_moreCamerasStep is not null) return _moreCamerasStep;
        _moreCamerasStep = new MoreCamerasStepViewModel(Session);
        _moreCamerasStep.RequestLoopBack = ExecuteLoopBack;
        return _moreCamerasStep;
    }

    private ToolsStepViewModel GetToolsStep()
        => _toolsStep ??= new ToolsStepViewModel(Session, _configWriter, _toolsLogger);

    private GpsStepViewModel GetGpsStep()
        => _gpsStep ??= new GpsStepViewModel(Session);

    private SummaryStepViewModel GetSummaryStep()
        => _summaryStep ??= new SummaryStepViewModel(Session);

    /// <summary>
    /// Rebuilds <see cref="ActiveSteps"/> for the given <paramref name="mode"/>.
    /// Called from WelcomeStep when the user selects a mode, and once in the constructor.
    ///
    /// Mode → Steps:
    ///   Dau:      Welcome, Workbench, SourceDetection, CameraConfirm, AddCards, MoreCameras, Summary
    ///   Simple:   + Tools, GPS                                                               (9 steps)
    ///   Advanced: + Tools, GPS                                                               (9 steps)
    /// </summary>
    public void RebuildActiveSteps(AppMode mode)
    {

        UnhookCurrentStep();

        ActiveSteps.Clear();

        ActiveSteps.Add(GetWelcomeStep());
        ActiveSteps.Add(GetWorkbenchStep());
        ActiveSteps.Add(GetSourceDetectionStep());
        ActiveSteps.Add(GetCameraConfirmStep());
        ActiveSteps.Add(GetAddCardsStep());
        ActiveSteps.Add(GetMoreCamerasStep());

        if (mode == AppMode.Simple || mode == AppMode.Advanced)
        {
            ActiveSteps.Add(GetToolsStep());
            ActiveSteps.Add(GetGpsStep());
        }

        ActiveSteps.Add(GetSummaryStep());

        _currentStepIndex = 0;
        OnPropertyChanged(nameof(CurrentStepIndex));
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(IsFirstStep));

        SyncStepIndicatorFlags();
        HookCurrentStep();
    }

    private async void ExecuteNext()
    {
        if (!CanGoNext) return;
        try
        {
            await CurrentStep.OnLeaveAsync();
            UnhookCurrentStep();
            CurrentStepIndex++;
            SyncStepIndicatorFlags();
            HookCurrentStep();
            await CurrentStep.OnEnterAsync();
            OnPropertyChanged(nameof(CurrentStep));
        }
        catch (Exception ex)
        {
            _toolsLogger?.LogError(ex, "Wizard: ExecuteNext failed");
            SyncStepIndicatorFlags();
        }
    }

    private async void ExecuteBack()
    {
        if (!CanGoBack) return;
        try
        {
            await CurrentStep.OnLeaveAsync();
            UnhookCurrentStep();
            CurrentStepIndex--;
            SyncStepIndicatorFlags();
            HookCurrentStep();
            await CurrentStep.OnEnterAsync();
            OnPropertyChanged(nameof(CurrentStep));
        }
        catch (Exception ex)
        {
            _toolsLogger?.LogError(ex, "Wizard: ExecuteBack failed");
            SyncStepIndicatorFlags();
        }
    }

    private async void ExecuteLoopBack()
    {
        try
        {
            await CurrentStep.OnLeaveAsync();
            UnhookCurrentStep();

            Session.DetectedFingerprint = null;
            Session.DetectedModel = null;
            Session.DetectedDriveLetter = null;

            var targetIndex = ActiveSteps.IndexOf(GetSourceDetectionStep());
            if (targetIndex < 0)
                targetIndex = 0;

            CurrentStepIndex = targetIndex;
            SyncStepIndicatorFlags();
            HookCurrentStep();
            await CurrentStep.OnEnterAsync();
            OnPropertyChanged(nameof(CurrentStep));
        }
        catch (Exception ex)
        {
            _toolsLogger?.LogError(ex, "Wizard: ExecuteLoopBack failed");
            SyncStepIndicatorFlags();
        }
    }

    private async void ExecuteFinish()
    {
        if (!IsLastStep || !CurrentStep.IsValid || IsBusy) return;
        IsBusy = true;
        try
        {
            await FinishAsync();
        }
        catch (Exception ex)
        {
            // Without an explicit catch, async void exceptions bubble up to
            // App.xaml.cs's DispatcherUnhandledException, which logs via Serilog
            // and sets Handled=true. With the default LogLevel=Off the user sees
            // exactly nothing — the click looks like it does nothing. Surface the
            // error inline so the wizard can be debugged without a log file.
            _toolsLogger?.LogError(ex, "Wizard FinishAsync failed");
            System.Windows.MessageBox.Show(
                $"Setup konnte nicht abgeschlossen werden:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "UMI Setup",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Persists the wizard session to config.json.
    /// 1. InitializeNew (First-Run path)
    /// 2. SetWorkbenchPath
    /// 3. AddCamera for every entry in Session.Cameras
    /// 4. SD card registration for every SourceAssignment in Session.Cameras[].Sources
    /// 5. SetGpxSourcePath (Simple + Advanced mode)
    /// 6. Save
    /// 7. Persist AppMode
    /// 8. Raise WizardCompleted
    /// </summary>
    private async Task FinishAsync()
    {

        _configWriter.InitializeNew(_pathResolver.ConfigFile);

        // Persist whichever language the GUI is currently running in (set earlier by
        // App.xaml.cs ResolveInitialLanguage from the installer hint, system culture or
        // a previous config). Robust against the hint already having been consumed at
        // startup — we trust the live UI culture here.
        var liveLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (liveLang is not "de" and not "en") liveLang = "en";
        _configWriter.UpdateSection<AppSettings>(
            "app_settings",
            s => s.Language = liveLang);

        _configWriter.SetWorkbenchPath(Session.WorkbenchPath);

        if (_toolsStep is not null)
        {
            if (!string.IsNullOrWhiteSpace(_toolsStep.Tools.ExifToolPath))
                _configWriter.SetToolPath(ToolKeys.ExifTool, _toolsStep.Tools.ExifToolPath);
            if (!string.IsNullOrWhiteSpace(_toolsStep.Tools.GyroflowPath))
                _configWriter.SetToolPath(ToolKeys.Gyroflow, _toolsStep.Tools.GyroflowPath);
            if (!string.IsNullOrWhiteSpace(_toolsStep.Tools.FFprobePath))
                _configWriter.SetToolPath(ToolKeys.FFprobe, _toolsStep.Tools.FFprobePath);
        }

        foreach (var entry in Session.Cameras)
        {
            // Pull the default Features + FileTypes from the camera-type preset
            // (config/presets/types/<Type>.umi) so cameras created via the wizard
            // come out with the right RAW extensions and feature flags. Passing
            // null here used to leave Photo + Video lists empty — DiscoverSourceFiles
            // then matched zero files, which is why "found nothing" reports keep
            // happening with CR3/DNG-shooting bodies.
            var typeDef = _typeLoader.GetType(entry.CameraType);
            var features = CameraFeatures.BuildFromPreset(typeDef?.Features);
            var cameraConfig = new CameraConfig
            {
                Name = entry.DisplayName,
                CameraType = entry.CameraType,
                Enabled = true,
                FolderName = entry.FolderName,
                Features = features,
                FileTypes = CameraFileTypes.BuildFromPreset(typeDef?.DefaultFileTypes),
                BurstDetectionConfig = features.BurstDetection
                    ? new BurstDetectionConfig
                    {
                        Enabled        = true,
                        // Prefer type-preset defaults; fall back to all disk profiles if preset has none.
                        ActiveProfiles = typeDef?.DefaultBurstProfiles is { Count: > 0 }
                            ? new List<string>(typeDef.DefaultBurstProfiles)
                            : _burstProfileLoader?.ListAvailableProfiles() ?? new List<string>(),
                    }
                    : null,
            };

            _configWriter.AddCamera(entry.CameraId, cameraConfig);

            foreach (var source in entry.Sources.Where(s => s.SourceType == "sd" && !string.IsNullOrWhiteSpace(s.DriveLetter)))
            {

                string? volumeSerial = source.VolumeSerial;
                string? volumeLabel  = source.VolumeLabel;
                string? diskSerial   = source.DiskSerial;

                diskSerial = VolumeInfoReader.IsFakeDiskSerial(diskSerial) ? null : diskSerial;
                long    diskSize     = source.DiskSizeBytes;

                if (string.IsNullOrWhiteSpace(volumeSerial))
                {
                    var cardInfo = await Task.Run(() => VolumeInfoReader.ReadSdCardInfo(source.DriveLetter));
                    volumeSerial = cardInfo.VolumeSerial;
                    volumeLabel  ??= cardInfo.VolumeLabel;
                    diskSerial   ??= VolumeInfoReader.IsFakeDiskSerial(cardInfo.DiskSerial) ? null : cardInfo.DiskSerial;
                    if (diskSize == 0) diskSize = cardInfo.DiskSizeBytes;
                }

                if (!string.IsNullOrWhiteSpace(volumeSerial))
                {
                    _configWriter.RegisterSdCard(
                        volumeSerial,
                        SdCardRegistrationHelper.Create(
                            entry.CameraId,
                            label: volumeLabel,
                            diskSerial: diskSerial,
                            sizeBytes: diskSize));
                }
            }
        }

        if (Session.Mode != AppMode.Dau && !string.IsNullOrWhiteSpace(Session.GpsFolder))
            _configWriter.SetGpxSourcePath(Session.GpsFolder);

        _configWriter.UpdateSection<AppSettings>(
            "app_settings",
            s => s.Mode = Session.Mode);

        await _configWriter.SaveAsync();

        WizardCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteCancel()
        => WizardCancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Syncs IsCurrentStep / IsCompleted on every step in ActiveSteps.
    /// Called after any navigation change so the step indicator highlights correctly.
    /// </summary>
    private void SyncStepIndicatorFlags()
    {
        for (var i = 0; i < ActiveSteps.Count; i++)
        {
            ActiveSteps[i].IsCurrentStep = i == _currentStepIndex;
            ActiveSteps[i].IsCompleted   = i < _currentStepIndex;
        }
    }

    private void HookCurrentStep()
    {
        _hookedStep = CurrentStep;
        if (_hookedStep is not null)
            _hookedStep.PropertyChanged += OnCurrentStepPropertyChanged;
    }

    private void UnhookCurrentStep()
    {
        if (_hookedStep is not null)
        {
            _hookedStep.PropertyChanged -= OnCurrentStepPropertyChanged;
            _hookedStep = null;
        }
    }

    private void OnCurrentStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WizardStepViewModelBase.IsValid))
        {
            OnPropertyChanged(nameof(CanGoNext));

            if (IsLastStep)
                RelayCommand.RaiseCanExecuteChanged();
        }
    }

    public void Dispose()
    {
        UnhookCurrentStep();
        if (_sourceDetectionStep is IDisposable sourceDisposable)
            sourceDisposable.Dispose();
        if (_addCardsStep is IDisposable addCardsDisposable)
            addCardsDisposable.Dispose();
        GC.SuppressFinalize(this);
    }

}
