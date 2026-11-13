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

using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using UMI.Core.Constants;
using UMI.Core.Services;
using UMI.GUI.Resources;
using UMI.GUI.Helpers;

namespace UMI.GUI.ViewModels;

/// <summary>
/// ViewModel for the Tools config sub-tab.
/// Reads and writes ExifTool, Gyroflow, FFprobe paths via IConfigWriterService.
/// </summary>
public class ToolsViewModel : ViewModelBase
{
    private readonly IConfigWriterService _configWriter;
    private readonly IToolInstallService _toolInstaller;
    private readonly ISupportBundleService? _supportBundle;
    private readonly ILogger<ToolsViewModel>? _logger;

    private string _exifToolPath = string.Empty;
    /// <summary>Path to exiftool.exe (required).</summary>
    public string ExifToolPath
    {
        get => _exifToolPath;
        set
        {
            if (SetProperty(ref _exifToolPath, value))
            {
                OnPropertyChanged(nameof(ExifToolStatus));
                OnPropertyChanged(nameof(ExifToolIsValid));
                OnPropertyChanged(nameof(ExifToolIsEmpty));
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ToolPathStatus ExifToolStatus => GetStatus(ExifToolPath, required: true);
    public bool ExifToolIsValid  => ExifToolStatus == ToolPathStatus.Valid;
    public bool ExifToolIsEmpty  => ExifToolStatus == ToolPathStatus.RequiredMissing;

    private string _gyroflowPath = string.Empty;
    /// <summary>Path to gyroflow (optional).</summary>
    public string GyroflowPath
    {
        get => _gyroflowPath;
        set
        {
            if (SetProperty(ref _gyroflowPath, value))
            {
                OnPropertyChanged(nameof(GyroflowStatus));
                OnPropertyChanged(nameof(GyroflowIsValid));
            }
        }
    }

    public ToolPathStatus GyroflowStatus => GetStatus(GyroflowPath, required: false);
    public bool GyroflowIsValid => GyroflowStatus == ToolPathStatus.Valid;

    private string _ffprobePath = string.Empty;
    /// <summary>Path to ffprobe.exe (optional).</summary>
    public string FFprobePath
    {
        get => _ffprobePath;
        set
        {
            if (SetProperty(ref _ffprobePath, value))
            {
                OnPropertyChanged(nameof(FFprobeStatus));
                OnPropertyChanged(nameof(FFprobeIsValid));
            }
        }
    }

    public ToolPathStatus FFprobeStatus => GetStatus(FFprobePath, required: false);
    public bool FFprobeIsValid => FFprobeStatus == ToolPathStatus.Valid;

    private string _gpsTrackFolder = string.Empty;
    /// <summary>Path to the folder containing GPX track files (GlobalPaths.GpxSource).</summary>
    public string GpsTrackFolder
    {
        get => _gpsTrackFolder;
        set
        {
            if (SetProperty(ref _gpsTrackFolder, value))
                OnPropertyChanged(nameof(GpsTrackFolderExists));
        }
    }

    /// <summary>True when the GPS track folder path is set and the directory exists.</summary>
    public bool GpsTrackFolderExists => !string.IsNullOrWhiteSpace(GpsTrackFolder)
                                        && Directory.Exists(GpsTrackFolder);

    private string _selectedLanguage = "en";
    /// <summary>
    /// Selected UI language code (BCP 47). Supported: "en", "de".
    /// Persists to config.json under app_settings.language.
    /// </summary>
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                SaveLanguage(value);
                ShowRestartHint = true;
            }
        }
    }

    private bool _showRestartHint;
    /// <summary>True after language was changed — signals the UI to show a restart hint.</summary>
    public bool ShowRestartHint
    {
        get => _showRestartHint;
        private set => SetProperty(ref _showRestartHint, value);
    }

    /// <summary>Available languages for the dropdown.</summary>
    public static IReadOnlyList<LanguageOption> AvailableLanguages { get; } = new[]
    {
        new LanguageOption("en", "English"),
        new LanguageOption("de", "Deutsch"),
    };

    /// <summary>
    /// Available log levels for the dropdown. Order = display order in the UI.
    /// </summary>
    public IReadOnlyList<LogLevelOption> AvailableLogLevels { get; } = new[]
    {
        new LogLevelOption(LogLevels.Off,   Strings.ToolsConfig_LogLevel_Off),
        new LogLevelOption(LogLevels.Info,  Strings.ToolsConfig_LogLevel_Info),
        new LogLevelOption(LogLevels.Debug, Strings.ToolsConfig_LogLevel_Debug),
    };

    private string _logLevel = LogLevels.Off;
    /// <summary>
    /// Active log level. Bound to <c>config.Logging.Level</c>. Setting <see cref="LogLevels.Off"/>
    /// disables file logging on next start; INFO/DEBUG control verbosity. Restart required for
    /// the new level to take effect.
    /// </summary>
    public string LogLevel
    {
        get => _logLevel;
        set
        {
            if (SetProperty(ref _logLevel, value ?? LogLevels.Off))
                SaveLogLevel(_logLevel);
        }
    }

    /// <summary>
    /// Brief status message shown after save operations.
    /// Delegates to the inherited <see cref="ViewModelBase.StatusMessage"/> so
    /// <see cref="ViewModelBase.ScheduleClearStatus"/> can clear it without a duplicate CTS.
    /// </summary>
    public string? SaveMessage
    {
        get => StatusMessage;
        private set
        {
            StatusMessage = value;
            OnPropertyChanged(nameof(SaveMessage));
        }
    }

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (SetProperty(ref _isDownloading, value))
                RelayCommand.RaiseCanExecuteChanged();
        }
    }

    private int _downloadProgress;
    public int DownloadProgress
    {
        get => _downloadProgress;
        private set => SetProperty(ref _downloadProgress, value);
    }

    private string? _downloadStatusMessage;
    public string? DownloadStatusMessage
    {
        get => _downloadStatusMessage;
        private set => SetProperty(ref _downloadStatusMessage, value);
    }

    private string? _downloadErrorMessage;
    /// <summary>
    /// Why the last tool install did not happen (checksum mismatch, no checksum available, network error).
    /// Stays until the next attempt — unlike <see cref="DownloadStatusMessage"/>, which is only shown while downloading.
    /// </summary>
    public string? DownloadErrorMessage
    {
        get => _downloadErrorMessage;
        private set => SetProperty(ref _downloadErrorMessage, value);
    }

    public ICommand BrowseExifToolCommand       { get; }
    public ICommand BrowseGyroflowCommand       { get; }
    public ICommand BrowseFFprobeCommand        { get; }
    public ICommand BrowseGpsTrackFolderCommand { get; }
    public ICommand ResetExifToolDefaultCommand { get; }
    public ICommand DownloadGyroflowCommand     { get; }
    public ICommand DownloadFFprobeCommand      { get; }
    public ICommand CreateSupportBundleCommand  { get; }

    /// <summary>The bundled default path for ExifTool. SSOT: ConfigPathResolver.DefaultExifToolPath.</summary>
    public string ExifToolDefaultPath => ConfigPathResolver.DefaultExifToolPath;

    /// <summary>Log-Verzeichnis für das Support-Bundle-UI-Label. SSOT: ConfigPathResolver.LogDirectory.</summary>
    public static string LogDirectoryDisplay => ConfigPathResolver.LogDirectory;

    /// <summary>True when the bundled ExifTool exists and current path differs from it.</summary>
    public bool CanResetExifToolDefault() =>
        File.Exists(ExifToolDefaultPath) &&
        !string.Equals(
            Path.GetFullPath(ExifToolPath.Length > 0 ? ExifToolPath : "."),
            Path.GetFullPath(ExifToolDefaultPath),
            StringComparison.OrdinalIgnoreCase);

    public ToolsViewModel(
        IConfigWriterService configWriter,
        IToolInstallService toolInstaller,
        ISupportBundleService? supportBundle = null,
        ILogger<ToolsViewModel>? logger = null)
    {
        _configWriter = configWriter;
        _toolInstaller = toolInstaller;
        _supportBundle = supportBundle;
        _logger = logger;

        BrowseExifToolCommand       = new RelayCommand(() => ExecuteBrowse(ToolKeys.ExifTool, path => ExifToolPath = path));
        BrowseGyroflowCommand       = new RelayCommand(() => ExecuteBrowse(ToolKeys.Gyroflow, path => GyroflowPath = path));
        BrowseFFprobeCommand        = new RelayCommand(() => ExecuteBrowse(ToolKeys.FFprobe,  path => FFprobePath  = path));
        BrowseGpsTrackFolderCommand = new RelayCommand(ExecuteBrowseGpsFolder);
        ResetExifToolDefaultCommand = new RelayCommand(ExecuteResetExifToolDefault, CanResetExifToolDefault);

        DownloadGyroflowCommand = new RelayCommand(
            () => DownloadAndInstallAsync(ToolKeys.Gyroflow),
            () => !IsDownloading);
        DownloadFFprobeCommand = new RelayCommand(
            () => DownloadAndInstallAsync(ToolKeys.FFprobe),
            () => !IsDownloading);
        CreateSupportBundleCommand = new RelayCommand(ExecuteCreateSupportBundle, () => !IsSaving);
    }

    /// <summary>
    /// Refresh paths from the current config (called after config is loaded).
    /// </summary>
    public void RefreshFromConfig()
    {
        var config = _configWriter.Config;
        if (config is null) return;

        var tools = config.GlobalPaths?.Tools;
        if (tools is not null)
        {
            _exifToolPath = tools.ExifTool ?? string.Empty;
            _gyroflowPath = tools.Gyroflow ?? string.Empty;
            _ffprobePath  = tools.FFprobe  ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_exifToolPath) && File.Exists(ExifToolDefaultPath))
                _exifToolPath = ExifToolDefaultPath;

            OnPropertyChanged(nameof(ExifToolPath));
            OnPropertyChanged(nameof(GyroflowPath));
            OnPropertyChanged(nameof(FFprobePath));
            OnPropertyChanged(nameof(ExifToolStatus));
            OnPropertyChanged(nameof(ExifToolIsValid));
            OnPropertyChanged(nameof(ExifToolIsEmpty));
            OnPropertyChanged(nameof(GyroflowStatus));
            OnPropertyChanged(nameof(GyroflowIsValid));
            OnPropertyChanged(nameof(FFprobeStatus));
            OnPropertyChanged(nameof(FFprobeIsValid));
        }

        _gpsTrackFolder = config.GlobalPaths?.GpxSource ?? string.Empty;
        OnPropertyChanged(nameof(GpsTrackFolder));
        OnPropertyChanged(nameof(GpsTrackFolderExists));

        _logLevel = NormalizeLogLevel(config.Logging?.Level);
        OnPropertyChanged(nameof(LogLevel));

        _selectedLanguage = config.AppSettings?.Language ?? "en";
        _showRestartHint = false;
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(ShowRestartHint));
    }

    private void ExecuteResetExifToolDefault()
    {
        var defaultPath = ExifToolDefaultPath;
        if (!File.Exists(defaultPath)) return;

        ExifToolPath = defaultPath;
        SaveToolPath(ToolKeys.ExifTool, defaultPath);
    }

    private void ExecuteBrowseGpsFolder()
    {
        var folder = Helpers.DialogHelper.BrowseFolder(Strings.Tools_SelectGpsFolder, GpsTrackFolder);
        if (folder is null) return;

        GpsTrackFolder = folder;
        SaveGpsTrackFolder(folder);
    }

    private async void SaveGpsTrackFolder(string folder)
    {
        IsSaving = true;
        SaveMessage = null;
        try
        {
            _configWriter.SetGpxSourcePath(folder);
            await _configWriter.SaveAsync();
            SaveMessage = Strings.Tools_GpsPathSaved;
            _logger?.LogInformation("GPS track folder saved: {Path}", folder);
            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            SaveMessage = string.Format(Strings.Common_ErrorSaving, ex.Message);
            _logger?.LogError(ex, "Failed to save GPS track folder");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static string NormalizeLogLevel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return LogLevels.Off;
        if (string.Equals(raw, LogLevels.Debug, StringComparison.OrdinalIgnoreCase)) return LogLevels.Debug;
        if (string.Equals(raw, LogLevels.Info,  StringComparison.OrdinalIgnoreCase)) return LogLevels.Info;
        return LogLevels.Off;
    }

    private async void SaveLogLevel(string level)
    {
        IsSaving = true;
        SaveMessage = null;
        try
        {
            _configWriter.Config.Logging.Level = level;
            await _configWriter.SaveAsync();
            SaveMessage = Strings.Tools_LogLevelSaved;
            _logger?.LogInformation("Logging level set to {Level}", level);
            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            SaveMessage = string.Format(Strings.Common_ErrorSaving, ex.Message);
            _logger?.LogError(ex, "Failed to save logging level");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async void SaveLanguage(string languageCode)
    {
        IsSaving = true;
        SaveMessage = null;
        try
        {
            _configWriter.Config.AppSettings.Language = languageCode;
            await _configWriter.SaveAsync();
            SaveMessage = Strings.Tools_LanguageSaved;
            _logger?.LogInformation("Language set to {Language}", languageCode);
            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            SaveMessage = string.Format(Strings.Common_ErrorSaving, ex.Message);
            _logger?.LogError(ex, "Failed to save language setting");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ExecuteBrowse(string toolKey, Action<string> setter)
    {
        var dlg = new OpenFileDialog
        {
            Title  = string.Format(Strings.Tools_SelectExecutable, toolKey),
            Filter = Strings.Tools_ExecutableFilter,
            CheckFileExists = true
        };

        if (dlg.ShowDialog() == true)
        {
            var path = Path.GetFullPath(dlg.FileName);
            setter(path);
            SaveToolPath(toolKey, path);
        }
    }

    private async void SaveToolPath(string toolKey, string path)
    {
        IsSaving = true;
        SaveMessage = null;
        try
        {
            _configWriter.SetToolPath(toolKey, path);
            await _configWriter.SaveAsync();
            SaveMessage = string.Format(Strings.Tools_PathSaved, toolKey);
            _logger?.LogInformation("Tool path saved: {ToolKey} = {Path}", toolKey, path);

            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            SaveMessage = string.Format(Strings.Common_ErrorSaving, ex.Message);
            _logger?.LogError(ex, "Failed to save tool path for {ToolKey}", toolKey);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Installs a tool through <see cref="IToolInstallService"/> — the only path from download to extracted
    /// executable, and it verifies the archive's SHA256 before unpacking. This method only drives the UI.
    /// </summary>
    private async void DownloadAndInstallAsync(string toolKey)
    {
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadErrorMessage = null;
        DownloadStatusMessage = string.Format(Strings.Tools_Downloading, toolKey);
        try
        {
            if (UseExistingInstallation(toolKey))
                return;

            var progress = new Progress<double>(fraction =>
            {
                DownloadProgress = (int)(fraction * 100);
                if (fraction >= 1.0)
                    DownloadStatusMessage = string.Format(Strings.Tools_Extracting, toolKey);
            });
            var result = await _toolInstaller.InstallAsync(toolKey, progress, CancellationToken.None);
            ReportInstallResult(toolKey, result);
        }
        catch (Exception ex)
        {
            DownloadErrorMessage = string.Format(Strings.Common_ErrorFormat, ex.Message);
            _logger?.LogError(ex, "Failed to download {ToolKey}", toolKey);
        }
        finally
        {
            IsDownloading = false;
            DownloadStatusMessage = null;
        }
    }

    /// <summary>
    /// If UMI already installed the tool, asks whether to download again. On "no" the existing
    /// path is configured. Returns true when the existing installation is used (no download).
    /// </summary>
    private bool UseExistingInstallation(string toolKey)
    {
        var existingFullPath = _toolInstaller.FindInstalledExecutable(toolKey);
        if (existingFullPath is null)
            return false;

        var answer = System.Windows.MessageBox.Show(
            string.Format(Strings.Tools_AlreadyInstalledMessage, Path.GetFileName(existingFullPath), existingFullPath),
            Strings.Tools_AlreadyInstalledTitle,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (answer == System.Windows.MessageBoxResult.Yes)
            return false;

        ApplyToolPath(toolKey, existingFullPath);
        DownloadStatusMessage = Strings.Tools_FoundExistingPath;
        ScheduleClearStatus();
        return true;
    }

    /// <summary>Turns the install outcome into a configured path or a message that stays visible.</summary>
    private void ReportInstallResult(string toolKey, ToolInstallResult result)
    {
        switch (result.Outcome)
        {
            case ToolInstallOutcome.Installed when result.ExecutablePath is { } installedPath:
                ApplyToolPath(toolKey, installedPath);
                DownloadStatusMessage = string.Format(Strings.Tools_InstalledSuccess, toolKey);
                ScheduleClearStatus();
                break;
            case ToolInstallOutcome.Mismatch:
                DownloadErrorMessage = string.Format(Strings.Tools_ChecksumMismatch, toolKey);
                break;
            case ToolInstallOutcome.NotVerifiable:
                DownloadErrorMessage = string.Format(Strings.Tools_NotVerifiable, toolKey);
                break;
            case ToolInstallOutcome.InUse:
                DownloadErrorMessage = string.Format(Strings.Tools_InUse, toolKey);
                break;
            default:
                DownloadErrorMessage = string.Format(Strings.Tools_ExeNotFound, toolKey);
                break;
        }
    }

    private void ApplyToolPath(string toolKey, string fullPath)
    {
        if (toolKey == ToolKeys.Gyroflow)
            GyroflowPath = fullPath;
        else if (toolKey == ToolKeys.FFprobe)
            FFprobePath = fullPath;

        SaveToolPath(toolKey, fullPath);
    }

    private async void ExecuteCreateSupportBundle()
    {
        var folder = DialogHelper.BrowseFolder(Strings.Tools_SupportBundle);
        if (folder is null) return;

        IsSaving = true;
        SaveMessage = Strings.Tools_SupportBundleCreating;
        RelayCommand.RaiseCanExecuteChanged();
        try
        {
            var service = _supportBundle;
            if (service is null)
            {
                // Fallback: keine DI-Instanz — direktes Erstellen mit Resolver
                var resolver = new UMI.Core.Services.ConfigPathResolver();
                service = new UMI.Core.Services.SupportBundleService(resolver, logger: _logger as ILogger<UMI.Core.Services.SupportBundleService>);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var zipPath = await service.CreateBundleAsync(folder, cts.Token);

            SaveMessage = string.Format(Strings.Tools_SupportBundleDone, zipPath);
            _logger?.LogInformation("Support bundle created: {Path}", zipPath);

            // Öffne den Explorer am Zielordner
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{zipPath}\""); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Could not open Explorer for {Path}", zipPath); }

            ScheduleClearStatus();
        }
        catch (Exception ex)
        {
            SaveMessage = string.Format(Strings.Tools_SupportBundleError, ex.Message);
            _logger?.LogError(ex, "Failed to create support bundle");
        }
        finally
        {
            IsSaving = false;
            RelayCommand.RaiseCanExecuteChanged();
        }
    }

    private static ToolPathStatus GetStatus(string path, bool required)
    {
        if (string.IsNullOrWhiteSpace(path))
            return required ? ToolPathStatus.RequiredMissing : ToolPathStatus.OptionalEmpty;

        return File.Exists(path) ? ToolPathStatus.Valid : ToolPathStatus.NotFound;
    }
}

/// <summary>Represents a selectable UI language.</summary>
/// <param name="Code">BCP 47 language code (e.g. "en", "de").</param>
/// <param name="DisplayName">Human-readable name (e.g. "English", "Deutsch").</param>
public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>Option for the log-level dropdown — pairs the config code with a localized label.</summary>
/// <param name="Code">Code stored in <c>config.Logging.Level</c> — one of <see cref="LogLevels"/>.</param>
/// <param name="DisplayName">Localized label shown in the ComboBox.</param>
public sealed record LogLevelOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>Describes the validation state of a tool path.</summary>
public enum ToolPathStatus
{
    /// <summary>Path is set and the file exists.</summary>
    Valid,
    /// <summary>Path is empty but the tool is required.</summary>
    RequiredMissing,
    /// <summary>Path is set but the file does not exist.</summary>
    NotFound,
    /// <summary>Path is empty, tool is optional — no highlight.</summary>
    OptionalEmpty,
}
