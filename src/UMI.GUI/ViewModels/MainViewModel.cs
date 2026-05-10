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
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.GUI.Helpers;
using UMI.GUI.Resources;
using UMI.GUI.ViewModels.Wizard;
using UMI.GUI.Views;
using UMI.GUI.Views.Wizard;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the main window. Loads the config, creates camera ViewModels,
/// and exposes grouped camera lists for the UI. Cards expand inline for editing.
/// </summary>
public class MainViewModel : ViewModelBase
{

    private const int ImportTabIndex = 0;
    private const int ProcessTabIndex = 1;
    private const int SettingsTabIndex = 2;

    private readonly ILogger<MainViewModel>? _logger;
    private readonly IConfigWriterService _configWriter;
    private readonly CameraTypeLoader? _typeLoader;
    private readonly IDriveWatcherService? _driveWatcher;
    private readonly ICardDetectionService? _cardDetection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConfigPathResolver? _configPaths;
    private readonly IUpdateService? _updateService;

    /// <summary>
    /// True when the user navigated to Settings → Cameras via the "Edit Cam" button in Import.
    /// When set, Save and Cancel both navigate back to the Import tab.
    /// </summary>
    private bool _navigatedFromImport;

    /// <summary>ViewModel for the Config tab (sub-tab navigation + Tools).</summary>
    public ConfigViewModel ConfigViewModel { get; }

    /// <summary>ViewModel for the WorkbenchSection in the Import tab.</summary>
    public WorkbenchViewModel WorkbenchViewModel { get; }

    /// <summary>ViewModel for Import tab actions (Watch / Quick Import).</summary>
    public ImportViewModel ImportViewModel { get; }

    /// <summary>ViewModel for the Process tab (post-processing action cards).</summary>
    public ProcessViewModel ProcessViewModel { get; }

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            SetProperty(ref _errorMessage, value);
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    private bool _isUpdateBannerVisible;
    /// <summary>True when an update is available and the banner should be shown.</summary>
    public bool IsUpdateBannerVisible
    {
        get => _isUpdateBannerVisible;
        private set => SetProperty(ref _isUpdateBannerVisible, value);
    }

    private string _updateBannerText = string.Empty;
    /// <summary>Localized text for the update banner (e.g. "Version 2.2.0 is available").</summary>
    public string UpdateBannerText
    {
        get => _updateBannerText;
        private set => SetProperty(ref _updateBannerText, value);
    }

    private string _updateDownloadUrl = string.Empty;
    /// <summary>Direct download URL of the setup EXE.</summary>
    public string UpdateDownloadUrl
    {
        get => _updateDownloadUrl;
        private set => SetProperty(ref _updateDownloadUrl, value);
    }

    private string _updateLatestVersion = string.Empty;
    /// <summary>Latest version string — used to build the download target filename.</summary>
    private string UpdateLatestVersion
    {
        get => _updateLatestVersion;
        set => _updateLatestVersion = value;
    }

    private bool _isDownloading;
    /// <summary>True while the setup EXE is being downloaded. Controls ProgressBar visibility.</summary>
    public bool IsDownloading
    {
        get => _isDownloading;
        private set => SetProperty(ref _isDownloading, value);
    }

    private double _downloadProgress;
    /// <summary>Download progress 0.0–1.0 for the ProgressBar binding.</summary>
    public double DownloadProgress
    {
        get => _downloadProgress;
        private set => SetProperty(ref _downloadProgress, value);
    }

    private string _downloadStatusText = string.Empty;
    /// <summary>Status text shown below the progress bar during and after download.</summary>
    public string DownloadStatusText
    {
        get => _downloadStatusText;
        private set => SetProperty(ref _downloadStatusText, value);
    }

    private bool _isUpdateReadyToInstall;
    /// <summary>
    /// True after the background download finished and the setup EXE is staged in TEMP.
    /// While true, the banner button reads "Install" and triggers the installer launch
    /// (with UAC) and an app shutdown.
    /// </summary>
    public bool IsUpdateReadyToInstall
    {
        get => _isUpdateReadyToInstall;
        private set => SetProperty(ref _isUpdateReadyToInstall, value);
    }

    private string _stagedInstallerPath = string.Empty;
    /// <summary>Absolute path to the downloaded setup EXE (set by RunDownloadAsync).</summary>
    private string StagedInstallerPath
    {
        get => _stagedInstallerPath;
        set => _stagedInstallerPath = value;
    }

    private CancellationTokenSource? _downloadCts;

    private bool _checkForUpdatesOnStartup = true;
    /// <summary>Bound to the Settings-Tab toggle. Persisted to config on change.</summary>
    public bool CheckForUpdatesOnStartup
    {
        get => _checkForUpdatesOnStartup;
        set
        {
            if (SetProperty(ref _checkForUpdatesOnStartup, value))
            {
                _configWriter.UpdateSection<AppSettings>("app_settings", s => s.CheckForUpdatesOnStartup = value);
                _ = _configWriter.SaveAsync();
            }
        }
    }

    private string _configPath = string.Empty;
    public string ConfigPath
    {
        get => _configPath;
        private set => SetProperty(ref _configPath, value);
    }

    private string _appVersion = ResolveAssemblyVersion();
    /// <summary>
    /// Display string of the app version, e.g. "v2.1.1". Comes from the assembly metadata
    /// (which Directory.Build.props feeds at compile time) — NOT from config.json's schema
    /// version field, which tracks the on-disk config schema and is unrelated to the app
    /// release version.
    /// </summary>
    public string AppVersion
    {
        get => _appVersion;
        private set => SetProperty(ref _appVersion, value);
    }

    private static string ResolveAssemblyVersion()
    {
        var v = typeof(MainViewModel).Assembly.GetName().Version;
        return v is null ? "v?.?.?" : $"v{v.Major}.{v.Minor}.{v.Build}";
    }

    private int _selectedMainTabIndex;
    /// <summary>
    /// Index of the currently selected main TabControl tab.
    /// 0 = Import, 1 = Settings.
    /// Bound to SelectedIndex on the main TabControl in MainWindow.xaml.
    /// </summary>
    public int SelectedMainTabIndex
    {
        get => _selectedMainTabIndex;
        set => SetProperty(ref _selectedMainTabIndex, value);
    }

    private int _totalCameraCount;
    /// <summary>Total number of cameras in config (including disabled).</summary>
    public int TotalCameraCount
    {
        get => _totalCameraCount;
        private set
        {
            SetProperty(ref _totalCameraCount, value);
            OnPropertyChanged(nameof(CameraStatusText));
            OnPropertyChanged(nameof(CameraCountText));
        }
    }

    private int _enabledCameraCount;

    /// <summary>Statusbar text showing active/total cameras.</summary>
    public string CameraStatusText => string.Format(Strings.Main_CameraStatusText, _enabledCameraCount, TotalCameraCount);

    /// <summary>Header pill text showing total camera count.</summary>
    public string CameraCountText => string.Format(
        TotalCameraCount == 1 ? Strings.Main_CameraGroupCount_Singular : Strings.Main_CameraGroupCount_Plural,
        TotalCameraCount);

    private bool _isSaving;
    /// <summary>True while a save operation is in progress.</summary>
    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    /// <summary>
    /// Brief status message after save (success/error).
    /// Delegates to the inherited <see cref="ViewModelBase.StatusMessage"/> so
    /// <see cref="ViewModelBase.ScheduleClearStatus"/> can clear it without a duplicate CTS.
    /// </summary>
    public string? SaveStatusMessage
    {
        get => StatusMessage;
        private set
        {
            StatusMessage = value;
            OnPropertyChanged(nameof(SaveStatusMessage));
        }
    }

    private const int ProfilesTabIndex = 1;
    private const int BurstConfigTabIndex = 4;

    private AppMode _appMode;

    /// <summary>
    /// Current operating mode (Dau / Simple / Advanced).
    /// Persisted immediately to config.json under "app_settings.mode".
    /// When switching away from Advanced, navigates away from tabs hidden in that mode.
    /// </summary>
    public AppMode AppMode
    {
        get => _appMode;
        set
        {
            if (SetProperty(ref _appMode, value))
            {
                _configWriter.UpdateSection<AppSettings>("app_settings", s => s.Mode = value);
                _ = _configWriter.SaveAsync();

                OnPropertyChanged(nameof(IsDauMode));
                OnPropertyChanged(nameof(IsSimpleMode));
                OnPropertyChanged(nameof(IsAdvancedMode));
                OnPropertyChanged(nameof(AppModeLabel));
                OnPropertyChanged(nameof(IsProcessTabVisible));

                if (value == AppMode.Dau && SelectedMainTabIndex == ProcessTabIndex)
                    SelectedMainTabIndex = ImportTabIndex;

                if (value != AppMode.Advanced &&
                    (ConfigViewModel.SelectedTabIndex == ProfilesTabIndex ||
                     ConfigViewModel.SelectedTabIndex == BurstConfigTabIndex))
                {
                    ConfigViewModel.SelectedTabIndex = 0;
                }

                RebuildAllCameraBubbleLists();

                _selectedAppModeItem = AppModeOptions.FirstOrDefault(o => o.Mode == value);
                OnPropertyChanged(nameof(SelectedAppModeItem));
            }
        }
    }

    /// <summary>True only in Dau (Easy) mode.</summary>
    public bool IsDauMode => _appMode == AppMode.Dau;

    /// <summary>True when the Process tab should be visible (hidden in Dau mode).</summary>
    public bool IsProcessTabVisible => !IsDauMode;

    /// <summary>
    /// True in Dau AND Simple mode (both are "non-Advanced").
    /// Keeps existing ConfigTab bindings (Profiles/Burst tab visibility) working.
    /// </summary>
    public bool IsSimpleMode => _appMode != AppMode.Advanced;

    /// <summary>True only in Advanced mode. Backward-compat for existing XAML bindings.</summary>
    public bool IsAdvancedMode => _appMode == AppMode.Advanced;

    /// <summary>Human-readable label for the current mode (from SSOT AppModeLabels).</summary>
    public string AppModeLabel => _appMode switch
    {
        AppMode.Dau      => AppModeLabels.Dau,
        AppMode.Simple   => AppModeLabels.Simple,
        AppMode.Advanced => AppModeLabels.Advanced,
        _                => AppModeLabels.Simple
    };

    /// <summary>Items bound to the 3-way mode ComboBox in the header.</summary>
    public ObservableCollection<AppModeItem> AppModeOptions { get; } = new()
    {
        new() { Mode = AppMode.Dau,      Label = AppModeLabels.Dau,      Description = Strings.Main_AppModeEasyDesc },
        new() { Mode = AppMode.Simple,   Label = AppModeLabels.Simple,   Description = Strings.Main_AppModeStandardDesc },
        new() { Mode = AppMode.Advanced, Label = AppModeLabels.Advanced,  Description = Strings.Main_AppModeAdvancedDesc },
    };

    private AppModeItem? _selectedAppModeItem;
    /// <summary>Bound to the ComboBox SelectedItem in the header. Changes <see cref="AppMode"/> and persists.</summary>
    public AppModeItem? SelectedAppModeItem
    {
        get => _selectedAppModeItem;
        set
        {
            if (value != null && SetProperty(ref _selectedAppModeItem, value))
            {
                AppMode = value.Mode;
            }
        }
    }

    /// <summary>Opens the About dialog.</summary>
    public ICommand ShowAboutCommand { get; }

    /// <summary>Opens the Help window. Optional string parameter scrolls to that section anchor.</summary>
    public ICommand ShowHelpCommand { get; }

    /// <summary>Opens the Help window and navigates to the section relevant to the currently active tab.</summary>
    public ICommand ShowContextHelpCommand { get; }

    /// <summary>Opens the Setup Wizard modal. Pre-fills current config. Reloads on Finish.</summary>
    public ICommand ShowSetupWizardCommand { get; }

    /// <summary>Toggles inline expansion of a camera card.</summary>
    public ICommand ToggleExpandCommand { get; }

    /// <summary>Saves changes to the specified camera via ConfigWriterService.</summary>
    public ICommand SaveCameraCommand { get; }

    /// <summary>Reverts unsaved changes on the specified camera.</summary>
    public ICommand RevertCameraCommand { get; }

    /// <summary>Navigates to Settings → Devices tab (replaces old card management dialog).</summary>
    public ICommand ShowCardManagementCommand { get; }

    /// <summary>
    /// Navigates to Settings → Cameras and expands the matching camera card.
    /// Triggered from the "Edit Cam" button in Import expanded view.
    /// </summary>
    public ICommand EditCameraInSettingsCommand { get; }

    /// <summary>Opens the Add Camera dialog to register a new camera.</summary>
    public ICommand AddCameraCommand { get; }

    /// <summary>Deletes a camera from config after confirmation.</summary>
    public ICommand DeleteCameraCommand { get; }

    /// <summary>Cancels camera edit mode and navigates back to Import if applicable.</summary>
    public ICommand CancelCameraEditCommand { get; }

    /// <summary>Dismisses the update banner (sets IsUpdateBannerVisible = false).</summary>
    public ICommand DismissUpdateBannerCommand { get; }

    /// <summary>Starts the installer download and shows progress in the banner.</summary>
    public ICommand DownloadUpdateCommand { get; }

    /// <summary>Cancels an in-progress installer download.</summary>
    public ICommand CancelDownloadCommand { get; }

    /// <summary>Navigates to the Import tab (index 0). Used by the header tab button.</summary>
    public ICommand NavigateToImportCommand { get; }

    /// <summary>Navigates to the Process tab (index 1). Used by the header tab button.</summary>
    public ICommand NavigateToProcessCommand { get; }

    /// <summary>Navigates to the Settings tab (index 2). Used by the header tab button.</summary>
    public ICommand NavigateToSettingsCommand { get; }

    /// <summary>
    /// Reorders camera groups (CameraGroups) via drag-and-drop.
    /// CommandParameter: (int OldIndex, int NewIndex) tuple from DragDropReorderBehavior.
    /// </summary>
    public ICommand ReorderGroupsCommand { get; }

    /// <summary>All cameras from config, as flat list.</summary>
    public ObservableCollection<CameraViewModel> Cameras { get; } = new();

    /// <summary>Cameras grouped by CameraType for the grouped list view.</summary>
    public ObservableCollection<CameraGroupViewModel> CameraGroups { get; } = new();

    public MainViewModel(
        IConfigWriterService configWriter,
        ConfigViewModel configViewModel,
        WorkbenchViewModel workbenchViewModel,
        ImportViewModel importViewModel,
        ProcessViewModel processViewModel,
        IServiceProvider serviceProvider,
        CameraTypeLoader? typeLoader = null,
        IDriveWatcherService? driveWatcher = null,
        ICardDetectionService? cardDetection = null,
        ConfigPathResolver? configPaths = null,
        ILogger<MainViewModel>? logger = null,
        IUpdateService? updateService = null)
    {
        _configWriter = configWriter;
        ConfigViewModel = configViewModel;
        WorkbenchViewModel = workbenchViewModel;
        ImportViewModel = importViewModel;
        ProcessViewModel = processViewModel;
        _typeLoader = typeLoader;
        _driveWatcher = driveWatcher;
        _cardDetection = cardDetection;
        _configPaths = configPaths;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _updateService = updateService;

        _selectedAppModeItem = AppModeOptions.FirstOrDefault(o => o.Mode == _appMode);

        DismissUpdateBannerCommand = new RelayCommand(() => IsUpdateBannerVisible = false);
        // Banner button has two roles: "Cancel" while a download runs (handled by
        // CancelDownloadCommand via XAML DataTrigger), "Install" once the staged
        // setup is ready. CanExecute=true only in the latter state.
        DownloadUpdateCommand = new RelayCommand(ExecuteDownloadUpdate, () => IsUpdateReadyToInstall);
        CancelDownloadCommand = new RelayCommand(ExecuteCancelDownload, () => IsDownloading);

        ShowAboutCommand = new RelayCommand(ExecuteShowAbout);
        ShowHelpCommand = new RelayCommand<string?>(ExecuteShowHelp);
        ShowContextHelpCommand = new RelayCommand(ExecuteShowContextHelp);
        ShowSetupWizardCommand = new RelayCommand(ExecuteShowSetupWizard);
        ToggleExpandCommand = new RelayCommand<CameraViewModel>(ExecuteToggleExpand);
        SaveCameraCommand = new RelayCommand<CameraViewModel>(ExecuteSaveCamera);
        RevertCameraCommand = new RelayCommand<CameraViewModel>(ExecuteRevertCamera);
        ShowCardManagementCommand = new RelayCommand<CameraViewModel>(ExecuteShowCardManagement);
        EditCameraInSettingsCommand = new RelayCommand<CameraViewModel>(ExecuteEditCameraInSettings);
        AddCameraCommand = new RelayCommand(ExecuteAddCamera);
        DeleteCameraCommand = new RelayCommand<CameraViewModel>(ExecuteDeleteCamera);
        CancelCameraEditCommand = new RelayCommand<CameraViewModel>(ExecuteCancelCameraEdit);
        NavigateToImportCommand = new RelayCommand(() => SelectedMainTabIndex = ImportTabIndex);
        NavigateToProcessCommand = new RelayCommand(() => SelectedMainTabIndex = ProcessTabIndex);
        NavigateToSettingsCommand = new RelayCommand(() => SelectedMainTabIndex = SettingsTabIndex);
        ReorderGroupsCommand = new RelayCommand<(int, int)>(ExecuteReorderGroups);
    }

    /// <summary>
    /// Loads the configuration and builds the camera list.
    /// Call this from App.xaml.cs after the window is created.
    /// </summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var configPaths = new ConfigPathResolver();
            ConfigPath = configPaths.ConfigFile;

            _logger?.LogInformation("Loading config: {Path}", ConfigPath);

            if (!File.Exists(ConfigPath))
            {
                ErrorMessage = string.Format(Strings.Main_NoConfigFound, ConfigPath);
                return;
            }

            UmiConfig config;
            if (_configWriter.IsConfigLoaded)
            {
                config = _configWriter.Config;
                _logger?.LogDebug("Config already pre-loaded, reusing existing instance");
            }
            else
            {
                config = await _configWriter.LoadAsync(ConfigPath);
            }

            // Auto-repair: cameras created via the Setup Wizard before the
            // BuildFromPreset(null) bug got fixed have empty FileTypes lists,
            // which makes DiscoverSourceFiles match nothing — user reports
            // "lots of CR3/DNG files in the folder, UMI finds nothing". Pull
            // the defaults from the camera-type preset and persist on the spot.
            await RepairEmptyCameraFileTypesAsync(config);

            // AppVersion is set once from assembly metadata in the field initializer
            // (ResolveAssemblyVersion). config.Version is the JSON schema version and
            // belongs nowhere near the UI release-version display.

            _appMode = config.AppSettings.Mode;
            _checkForUpdatesOnStartup = config.AppSettings.CheckForUpdatesOnStartup;
            OnPropertyChanged(nameof(CheckForUpdatesOnStartup));
            _selectedAppModeItem = AppModeOptions.FirstOrDefault(o => o.Mode == _appMode);
            OnPropertyChanged(nameof(AppMode));
            OnPropertyChanged(nameof(IsDauMode));
            OnPropertyChanged(nameof(IsAdvancedMode));
            OnPropertyChanged(nameof(IsSimpleMode));
            OnPropertyChanged(nameof(AppModeLabel));
            OnPropertyChanged(nameof(IsProcessTabVisible));
            OnPropertyChanged(nameof(SelectedAppModeItem));

            foreach (var old in Cameras)
                old.PropertyChanged -= OnCameraPropertyChanged;
            Cameras.Clear();
            TotalCameraCount = config.Cameras.Count;

            foreach (var (id, cam) in config.Cameras.OrderBy(c => c.Value.SortOrder).ThenBy(c => c.Value.Name))
            {
                var vm = new CameraViewModel(id, cam, _typeLoader, _configPaths);
                vm.RefreshStorageSummary(config);

                vm.OnFeatureSaveRequested = () => _configWriter.SaveAsync();

                vm.GetIsAdvancedMode = () => IsAdvancedMode;
                vm.GetIsDauMode = () => IsDauMode;
                vm.RebuildSettingsBubbles();
                vm.PropertyChanged += OnCameraPropertyChanged;
                Cameras.Add(vm);
            }

            UpdateEnabledCount();

            ConfigViewModel.Tools.RefreshFromConfig();

            ConfigViewModel.Profiles.Cameras = Cameras;
            ConfigViewModel.Profiles.ConfigWriter = _configWriter;
            ConfigViewModel.Profiles.Initialize();

            ImportViewModel.Cameras = Cameras;

            ImportViewModel.DevicesTab = ConfigViewModel.Devices;

            WorkbenchViewModel.Initialize();

            ConfigViewModel.Devices.Initialize();

            ConfigViewModel.BurstTab.Initialize();

            ConfigViewModel.Devices.OnDeviceAssignmentsChanged = RefreshAllStorageSummaries;

            BuildGroups();

            ImportViewModel.InitializeCardStatus();

            _logger?.LogInformation("Config loaded: {Count}/{Total} cameras active", Cameras.Count, TotalCameraCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading config");
            ErrorMessage = string.Format(Strings.Main_ErrorLoadingConfig, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }

        // Fire-and-forget Update-Check — nach dem Laden, damit Startup nicht verzögert wird.
        if (_checkForUpdatesOnStartup && _updateService is not null)
            _ = CheckForUpdatesAsync();
    }

    /// <summary>
    /// Führt den Update-Check aus und setzt IsUpdateBannerVisible wenn eine neue Version verfügbar ist.
    /// Bei Fund startet auch direkt der Hintergrund-Download — der User soll bei Erscheinen des
    /// Banners die Installation einklicken können, ohne extra auf einen Download zu warten.
    /// Fire-and-forget: Exceptions werden geloggt, nie geworfen.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var result = await _updateService!.CheckForUpdatesAsync().ConfigureAwait(false);
            if (!result.IsUpdateAvailable)
                return;

            UpdateDownloadUrl = result.DownloadUrl;
            UpdateLatestVersion = result.LatestVersion;
            UpdateBannerText = string.Format(Strings.Update_BannerText, result.LatestVersion);

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                IsUpdateBannerVisible = true);

            // Background-fetch the installer right away so the user only has to
            // click "Install" once we're ready.
            if (!string.IsNullOrEmpty(UpdateDownloadUrl))
                _ = RunDownloadAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Update-Check fehlgeschlagen.");
        }
    }

    /// <summary>
    /// Triggered by the banner button. Two distinct phases share this command:
    ///  - Download still running → no-op (CanExecute gates this; user sees "Cancel" instead)
    ///  - Download finished, staged installer ready → launch installer + shut down.
    /// </summary>
    private void ExecuteDownloadUpdate()
    {
        if (IsUpdateReadyToInstall)
            LaunchStagedInstaller();
    }

    private void ExecuteCancelDownload()
    {
        _downloadCts?.Cancel();
    }

    private async Task RunDownloadAsync()
    {
        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;

        var version = string.IsNullOrEmpty(UpdateLatestVersion) ? "latest" : UpdateLatestVersion;
        var targetPath = Path.Combine(Path.GetTempPath(), $"UMI_Setup_{version}.exe");

        IsDownloading = true;
        IsUpdateReadyToInstall = false;
        DownloadProgress = 0;
        DownloadStatusText = Strings.Update_Downloading;
        RelayCommand.RaiseCanExecuteChanged();

        try
        {
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p;
            });

            await _updateService!.DownloadSetupAsync(UpdateDownloadUrl, targetPath, progress, ct)
                .ConfigureAwait(false);

            // Stage the file for the install button — no confirmation dialog, the user
            // explicitly clicks "Install" on the banner when they're ready.
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StagedInstallerPath = targetPath;
                IsUpdateReadyToInstall = true;
                UpdateBannerText = string.Format(Strings.Update_BannerReady, version);
            });
        }
        catch (OperationCanceledException)
        {
            DownloadStatusText = Strings.Update_DownloadCanceled;
            DownloadProgress = 0;
            _logger?.LogInformation("Update-Download abgebrochen.");
        }
        catch (Exception ex)
        {
            DownloadStatusText = string.Format(Strings.Update_DownloadError, ex.Message);
            DownloadProgress = 0;
            _logger?.LogWarning(ex, "Update-Download fehlgeschlagen: {Url}", UpdateDownloadUrl);
        }
        finally
        {
            IsDownloading = false;
            RelayCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Launches the previously-downloaded installer via Shell (triggers UAC) and shuts down
    /// the GUI so the installer can replace the binaries. No-op if the staged file is missing.
    /// </summary>
    private void LaunchStagedInstaller()
    {
        if (string.IsNullOrEmpty(StagedInstallerPath) || !File.Exists(StagedInstallerPath))
        {
            _logger?.LogWarning("Install-Klick ohne gestagete Setup-EXE — Pfad: {Path}", StagedInstallerPath);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = StagedInstallerPath,
                UseShellExecute = true
            });
            System.Windows.Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Installer-Launch fehlgeschlagen: {Path}", StagedInstallerPath);
        }
    }

    /// <summary>
    /// Recalculates the cached enabled-camera count and raises PropertyChanged for CameraStatusText.
    /// Called after camera list changes or when a camera's Enabled state toggles.
    /// </summary>
    private void UpdateEnabledCount()
    {
        _enabledCameraCount = Cameras.Count(c => c.IsEnabled);
        OnPropertyChanged(nameof(CameraStatusText));
    }

    /// <summary>
    /// Listens for IsEnabled changes on individual camera ViewModels so the cached
    /// enabled count stays in sync without re-enumerating on every property get.
    /// </summary>
    private void OnCameraPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CameraViewModel.IsEnabled))
            UpdateEnabledCount();
    }

    private void BuildGroups()
    {
        CameraGroups.Clear();

        var typeOrderList = _configWriter.Config.Layout.CameraTypeOrder;

        var groups = Cameras
            .GroupBy(c => c.CameraType)
            .OrderBy(g =>
            {
                var idx = typeOrderList.IndexOf(g.Key);
                return idx >= 0 ? idx : int.MaxValue;
            })
            .ThenBy(g => g.Key);

        foreach (var group in groups)
        {
            var sortedCameras = group.OrderBy(c => c.SortOrder).ThenBy(c => c.Name);
            CameraGroups.Add(new CameraGroupViewModel(group.Key, sortedCameras, _configWriter, _logger));
        }
    }

    private void ExecuteShowAbout()
    {
        try
        {
            var aboutDialog = new AboutDialog(AppVersion);

            var owner = Application.Current.Windows
                .OfType<MainWindow>()
                .FirstOrDefault();

            if (owner != null)
                aboutDialog.Owner = owner;

            aboutDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open About dialog");
        }
    }

    private void ExecuteShowHelp(string? anchor)
    {
        try
        {
            var language = _configWriter.IsConfigLoaded
                ? _configWriter.Config.AppSettings?.Language
                : null;
            var helpWindow = new HelpWindow(language);

            var owner = Application.Current.Windows
                .OfType<MainWindow>()
                .FirstOrDefault();

            if (owner != null)
                helpWindow.Owner = owner;

            helpWindow.Show();

            if (!string.IsNullOrWhiteSpace(anchor))
            {
                const string chapterPrefix = "chapter:";
                if (anchor.StartsWith(chapterPrefix, StringComparison.Ordinal))
                    helpWindow.OpenChapter(anchor[chapterPrefix.Length..]);
                else
                    helpWindow.ScrollToSection(anchor);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open Help window");
        }
    }

    private void ExecuteShowContextHelp()
    {
        var anchor = SelectedMainTabIndex switch
        {
            ImportTabIndex   => Strings.Help_AnchorImport,
            ProcessTabIndex  => ProcessViewModel.HelpAnchor,
            SettingsTabIndex => ConfigViewModel.HelpAnchor,
            _ => string.Empty
        };
        ((RelayCommand<string?>)ShowHelpCommand).Execute(anchor);
    }

    private void ExecuteShowSetupWizard()
    {
        try
        {
            var wizardVm = _serviceProvider.GetRequiredService<SetupWizardViewModel>();

            wizardVm.Session.Mode = _appMode;
            wizardVm.Session.WorkbenchPath = _configWriter.Config.GlobalPaths.Workbench ?? string.Empty;

            wizardVm.RebuildActiveSteps(wizardVm.Session.Mode);

            var owner = Application.Current.Windows
                .OfType<MainWindow>()
                .FirstOrDefault();

            var wizardWindow = new SetupWizardWindow(wizardVm);
            if (owner != null)
                wizardWindow.Owner = owner;

            var result = wizardWindow.ShowDialog();

            if (result == true)
            {

                _ = LoadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open Setup Wizard");
            MessageBox.Show(
                string.Format(Strings.Main_WizardErrorMessage, ex.Message),
                Strings.Main_WizardErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ExecuteToggleExpand(CameraViewModel? camera)
    {
        if (camera == null) return;

        var expanding = !camera.IsExpanded;

        foreach (var cam in Cameras)
        {
            if (cam != camera && cam.IsExpanded)
            {
                cam.IsExpanded = false;
                cam.IsChangingType = false;
            }
        }

        camera.IsExpanded = expanding;
        if (!expanding)
            camera.IsChangingType = false;
    }

    private void ExecuteEditCameraInSettings(CameraViewModel? camera)
    {
        if (camera == null) return;

        _navigatedFromImport = true;

        SelectedMainTabIndex = SettingsTabIndex;

        ConfigViewModel.SelectedTabIndex = 0;

        foreach (var cam in Cameras)
        {
            cam.IsExpanded = cam.CameraId == camera.CameraId;
            if (!cam.IsExpanded)
                cam.IsChangingType = false;
        }

        var target = Cameras.FirstOrDefault(c => c.CameraId == camera.CameraId);
        target?.StartEditCommand.Execute(null);
    }

    private async void ExecuteSaveCamera(CameraViewModel? camera)
    {
        if (camera == null) return;

        if (!camera.IsDirty)
        {
            camera.IsEditing = false;
            HandlePostSaveNavigation();
            return;
        }

        IsSaving = true;
        SaveStatusMessage = null;

        try
        {

            camera.ApplyEdits();

            await _configWriter.SaveAsync();

            camera.IsEditing = false;

            camera.IsChangingType = false;

            BuildGroups();

            UpdateEnabledCount();

            SaveStatusMessage = Strings.Common_Saved;
            _logger?.LogInformation("Camera {CameraId} saved", camera.CameraId);

            ScheduleClearStatus();

            HandlePostSaveNavigation();
        }
        catch (Exception ex)
        {
            SaveStatusMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Error saving camera {CameraId}", camera.CameraId);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Navigates back to the Import tab if we arrived here via the "Edit Cam" button.
    /// </summary>
    private void HandlePostSaveNavigation()
    {
        if (_navigatedFromImport)
        {
            _navigatedFromImport = false;
            SelectedMainTabIndex = ImportTabIndex;
        }
    }

    private void ExecuteCancelCameraEdit(CameraViewModel? camera)
    {
        if (camera == null) return;

        camera.CancelEditCommand.Execute(null);

        if (_navigatedFromImport)
        {
            _navigatedFromImport = false;
            SelectedMainTabIndex = ImportTabIndex;
        }
    }

    private async void ExecuteDeleteCamera(CameraViewModel? camera)
    {
        if (camera == null) return;

        var result = MessageBox.Show(
            string.Format(Strings.Main_DeleteCameraConfirm, camera.EditName),
            Strings.Main_DeleteCameraTitle,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        try
        {
            _configWriter.RemoveCamera(camera.CameraId);
            await _configWriter.SaveAsync();
            ReloadCamerasFromConfig();
            _logger?.LogInformation("Camera {CameraId} deleted", camera.CameraId);
        }
        catch (Exception ex)
        {
            SaveStatusMessage = string.Format(Strings.Main_ErrorDeletingCamera, ex.Message);
            _logger?.LogError(ex, "Error deleting camera {CameraId}", camera.CameraId);
        }
    }

    private void ExecuteRevertCamera(CameraViewModel? camera)
    {
        camera?.RevertEdits();
    }

    /// <summary>
    /// Refreshes storage icons on all camera cards and rescans connected devices.
    /// Called via DevicesTabViewModel.OnDeviceAssignmentsChanged after Save / Delete / Add-Device.
    /// The rescan ensures newly registered devices get a green dot immediately after the dialog closes.
    /// </summary>
    /// <summary>
    /// Walks the loaded config, finds cameras whose FileTypes were saved with
    /// empty Photo + Video arrays (the BuildFromPreset(null) bug fixed in
    /// SetupWizardViewModel.FinishAsync), backfills them from the matching
    /// camera-type preset and persists. No-op when nothing is broken.
    /// </summary>
    private async Task RepairEmptyCameraFileTypesAsync(UmiConfig config)
    {
        if (_typeLoader is null) return;

        var dirty = false;
        foreach (var (id, cam) in config.Cameras)
        {
            var photoEmpty = cam.FileTypes.Photo is null || cam.FileTypes.Photo.Length == 0;
            var videoEmpty = cam.FileTypes.Video is null || cam.FileTypes.Video.Length == 0;
            if (!photoEmpty && !videoEmpty) continue;

            var typeDef = _typeLoader.GetType(cam.CameraType);
            if (typeDef?.DefaultFileTypes is null) continue;

            var rebuilt = UMI.Core.CameraFileTypes.BuildFromPreset(typeDef.DefaultFileTypes);
            // Only override the side that was empty — keep any user customisation.
            if (photoEmpty && rebuilt.Photo is { Length: > 0 })
                cam.FileTypes.Photo = rebuilt.Photo;
            if (videoEmpty && rebuilt.Video is { Length: > 0 })
                cam.FileTypes.Video = rebuilt.Video;

            _logger?.LogInformation(
                "Auto-repaired empty FileTypes on camera '{CameraId}' from type preset '{Type}': photo={Photo}, video={Video}",
                id, cam.CameraType,
                cam.FileTypes.Photo?.Length ?? 0,
                cam.FileTypes.Video?.Length ?? 0);
            dirty = true;
        }

        if (dirty)
        {
            try { await _configWriter.SaveAsync(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Persisting auto-repaired FileTypes failed"); }
        }
    }

    private void RefreshAllStorageSummaries()
    {
        var config = _configWriter.Config;
        foreach (var cam in Cameras)
            cam.RefreshStorageSummary(config);

        ImportViewModel.RescanConnectedDevices();
    }

    /// <summary>
    /// Rebuilds the Settings→Cam bubble lists on all camera cards.
    /// Called when the app mode (Simple/Advanced) changes so the feature filter
    /// takes effect immediately without requiring a full config reload.
    /// </summary>
    private void RebuildAllCameraBubbleLists()
    {
        foreach (var cam in Cameras)
            cam.RebuildSettingsBubbles();
    }

    private void ExecuteShowCardManagement(CameraViewModel? camera)
    {

        SelectedMainTabIndex = SettingsTabIndex;
        ConfigViewModel.SelectedTabIndex = 2;
    }

    private void ExecuteAddCamera()
    {
        try
        {
            var owner = Application.Current.Windows
                .OfType<MainWindow>()
                .FirstOrDefault();

            if (_typeLoader is null)
            {
                _logger?.LogWarning("Add Camera dialog: CameraTypeLoader not available");
                return;
            }

            var dialogVm = new AddCameraDialogViewModel(_configWriter, _typeLoader, _driveWatcher, _cardDetection, _logger);

            dialogVm.CameraAdded += (_, _) => ReloadCamerasFromConfig();

            var dialog = new AddCameraDialog(dialogVm) { Owner = owner };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open Add Camera dialog");
        }
    }

    /// <summary>
    /// Rebuilds the Cameras collection and groups from the current config.
    /// Called after a camera is added via the Add Camera dialog.
    /// </summary>
    private void ReloadCamerasFromConfig()
    {
        var config = _configWriter.Config;
        foreach (var old in Cameras)
            old.PropertyChanged -= OnCameraPropertyChanged;
        Cameras.Clear();
        TotalCameraCount = config.Cameras.Count;

        foreach (var (id, cam) in config.Cameras.OrderBy(c => c.Value.SortOrder).ThenBy(c => c.Value.Name))
        {
            var vm = new CameraViewModel(id, cam, _typeLoader);
            vm.RefreshStorageSummary(config);

            vm.OnFeatureSaveRequested = () => _configWriter.SaveAsync();

            vm.GetIsAdvancedMode = () => IsAdvancedMode;
            vm.GetIsDauMode = () => IsDauMode;
            vm.RebuildSettingsBubbles();
            vm.PropertyChanged += OnCameraPropertyChanged;
            Cameras.Add(vm);
        }

        UpdateEnabledCount();
        BuildGroups();

        ConfigViewModel.Devices.Initialize();

        ConfigViewModel.Profiles.Initialize();

        ConfigViewModel.Devices.OnDeviceAssignmentsChanged = RefreshAllStorageSummaries;

        ConfigViewModel.Devices.OnDeviceAssignmentsChanged?.Invoke();
    }

    /// <summary>
    /// Reorders camera groups (CameraGroups) after a drag-and-drop operation.
    /// Moves the group in the observable collection and persists the new type order.
    /// </summary>
    private async void ExecuteReorderGroups((int OldIndex, int NewIndex) indices)
    {
        var (oldIndex, newIndex) = indices;
        if (oldIndex == newIndex) return;
        if (oldIndex < 0 || oldIndex >= CameraGroups.Count) return;
        if (newIndex < 0 || newIndex >= CameraGroups.Count) return;

        CameraGroups.Move(oldIndex, newIndex);

        var orderedTypes = CameraGroups.Select(g => g.TypeKey).ToList();

        try
        {
            _configWriter.ReorderCameraTypes(orderedTypes);
            await _configWriter.SaveAsync();
            _logger?.LogInformation("Camera groups reordered");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reordering camera groups");
        }
    }
}

/// <summary>
/// Represents a group of cameras with the same CameraType.
/// Owns the ReorderCamerasCommand so the DropCallback in ImportTab.xaml can bind directly
/// to this group's DataContext — eliminating the ambiguous index-range group search in
/// MainViewModel that could match the wrong group when multiple groups have equal sizes.
/// </summary>
public class CameraGroupViewModel : ViewModelBase
{
    private readonly IConfigWriterService _configWriter;
    private readonly ILogger? _logger;

    public string TypeKey { get; }
    public string Label { get; }
    public ObservableCollection<CameraViewModel> Cameras { get; } = new();

    /// <summary>
    /// Hex color string for this group's camera type.
    /// Sourced from the first camera's TypeColor (which reads from the .umi definition).
    /// Falls back to static theme colors when no definition is loaded.
    /// Consumed by HexColorToBrushConverter in ImportTab/ConfigTab XAML.
    /// </summary>
    public string GroupColor { get; }

    /// <summary>
    /// Command invoked by DragDropReorderBehavior when the user drops a camera card
    /// within this group. Parameter: (int OldIndex, int NewIndex) tuple.
    /// Bound directly to this group's DataContext in ImportTab.xaml, so indices always
    /// refer unambiguously to this group's Cameras collection.
    /// </summary>
    public ICommand ReorderCamerasCommand { get; }

    public CameraGroupViewModel(
        string cameraType,
        IEnumerable<CameraViewModel> cameras,
        IConfigWriterService configWriter,
        ILogger? logger = null)
    {
        _configWriter = configWriter;
        _logger = logger;

        TypeKey = cameraType;
        Label = cameraType switch
        {
            "Action"      => Strings.CameraGroup_Action,
            "Drone"       => Strings.CameraGroup_Drone,
            "Mirrorless"  => Strings.CameraGroup_Mirrorless,
            "DSLR"        => Strings.CameraGroup_DSLR,
            "Dashcam"     => Strings.CameraGroup_Dashcam,
            _             => cameraType
        };

        var cameraList = cameras.ToList();
        foreach (var cam in cameraList)
            Cameras.Add(cam);

        GroupColor = cameraList.FirstOrDefault()?.TypeColor
                     ?? CameraTypeColors.GetHexColor(cameraType);

        ReorderCamerasCommand = new RelayCommand<(int, int)>(ExecuteReorderCameras);
    }

    /// <summary>
    /// Reorders cameras within this group after a drag-and-drop operation.
    /// Indices are relative to this group's own Cameras collection, so there is
    /// no ambiguity about which group owns them.
    /// </summary>
    private async void ExecuteReorderCameras((int OldIndex, int NewIndex) indices)
    {
        var (oldIndex, newIndex) = indices;
        if (oldIndex == newIndex) return;
        if (oldIndex < 0 || oldIndex >= Cameras.Count) return;
        if (newIndex < 0 || newIndex >= Cameras.Count) return;

        Cameras.Move(oldIndex, newIndex);

        var orderedIds = Cameras.Select(c => c.CameraId).ToList();

        try
        {
            _configWriter.ReorderCameras(orderedIds);
            await _configWriter.SaveAsync();
            _logger?.LogInformation("Cameras reordered in group '{Group}'", TypeKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reordering cameras in group '{Group}'", TypeKey);
        }
    }

    public int Count => Cameras.Count;

    public string CountText => string.Format(
        Cameras.Count == 1 ? Strings.Main_CameraGroupCount_Singular : Strings.Main_CameraGroupCount_Plural,
        Cameras.Count);
}

/// <summary>
/// Represents one selectable option in the header's 3-way App Mode ComboBox.
/// Used by <see cref="MainViewModel.AppModeOptions"/> and bound via DisplayMemberPath="Label".
/// </summary>
public class AppModeItem
{
    public AppMode Mode { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
