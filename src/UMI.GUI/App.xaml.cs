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

using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.GUI.Resources;
using UMI.GUI.ViewModels;
using UMI.GUI.ViewModels.Wizard;
using UMI.GUI.Views;
using UMI.GUI.Views.Wizard;

namespace UMI.GUI;

/// <summary>
/// Application entry point. Sets up DI, loads config via MainViewModel, shows MainWindow.
/// No CLI dependencies (System.CommandLine, Spectre.Console) are used here.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private bool _isDebugMode;

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = Environment.GetCommandLineArgs();
        _isDebugMode = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

        if (_isDebugMode)
        {
            AllocConsole();
            Console.Title = "UMI Debug Console";
            Console.WriteLine("[DEBUG] UMI GUI starting in debug mode...");
        }

        var preloadedConfig = TryPreloadConfig();
        Log.Logger = BuildLogger(preloadedConfig?.Config.Logging.Level ?? LogLevels.Off, _isDebugMode);

        DispatcherUnhandledException += (s, ev) =>
        {
            Log.Fatal(ev.Exception, "Unhandled UI exception");
            if (_isDebugMode)
                Console.Error.WriteLine($"[FATAL] Unhandled UI exception:{Environment.NewLine}{ev.Exception}");
            ev.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            if (ev.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled domain exception");
                if (_isDebugMode)
                    Console.Error.WriteLine($"[FATAL] Unhandled domain exception:{Environment.NewLine}{ex}");
            }
        };

        TaskScheduler.UnobservedTaskException += (s, ev) =>
        {
            Log.Error(ev.Exception, "Unobserved task exception");
            if (_isDebugMode)
                Console.Error.WriteLine($"[ERROR] Unobserved task exception:{Environment.NewLine}{ev.Exception}");
            ev.SetObserved();
        };

        Log.Information("UMI GUI starting (debug={IsDebug}, level={Level})",
            _isDebugMode, preloadedConfig?.Config.Logging.Level ?? LogLevels.Off);

        _serviceProvider = BuildServiceProvider(preloadedConfig);

        var splash = new SplashWindow();
        splash.Show();

        var splashStart = DateTime.UtcNow;

        LoadAndShowAsync(_serviceProvider, splash, splashStart).ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                var ex = t.Exception.InnerException ?? t.Exception;
                Dispatcher.Invoke(() =>
                {
                    Log.Fatal(ex, "Fatal startup error");
                    MessageBox.Show(
                        $"Startup Error:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                        "UMI — Fatal Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Current.Shutdown(1);
                });
            }
        }, TaskScheduler.Default);
    }

    private static async Task LoadAndShowAsync(
        ServiceProvider serviceProvider,
        SplashWindow splash,
        DateTime splashStart)
    {
        var splashClosed = false;

        try
        {

            var configWriter = serviceProvider.GetRequiredService<IConfigWriterService>();
            var configPaths = serviceProvider.GetRequiredService<ConfigPathResolver>();

            if (!File.Exists(configPaths.ConfigFile))
            {
                Log.Information("No config.json found — starting Setup Wizard (first-run)");

                splash.Close();
                splashClosed = true;

                var wizardVm = serviceProvider.GetRequiredService<SetupWizardViewModel>();
                var wizardWindow = new SetupWizardWindow(wizardVm);
                var result = wizardWindow.ShowDialog();

                if (result != true)
                {

                    Log.Information("First-run wizard cancelled — shutting down");
                    Current.Shutdown();
                    return;
                }

                Log.Information("First-run wizard completed — config.json written by wizard");

            }

            if (File.Exists(configPaths.ConfigFile) && !configWriter.IsConfigLoaded)
            {
                await configWriter.LoadAsync(configPaths.ConfigFile);
                Log.Debug("Config pre-loaded: {Path}", configPaths.ConfigFile);
            }

            ApplyLanguage(ResolveInitialLanguage(configWriter, configPaths));

            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            var mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();

            await mainViewModel.LoadAsync();

            var gpuQueue = serviceProvider.GetRequiredService<IGpuTaskQueue>();
            var gpuConfig = serviceProvider.GetRequiredService<UmiConfig>().GpuQueue;
            if (gpuConfig.Enabled && gpuConfig.AutoStart)
            {
                _ = gpuQueue.StartAsync(CancellationToken.None);
                Log.Information("GPU Task Queue auto-started");
            }

            if (!splashClosed)
            {
                var elapsed = DateTime.UtcNow - splashStart;
                var minDisplay = TimeSpan.FromSeconds(2.2);
                if (elapsed < minDisplay)
                    await Task.Delay(minDisplay - elapsed);
            }

            mainWindow.Show();
            Current.ShutdownMode = ShutdownMode.OnLastWindowClose;

            if (!splashClosed)
                splash.Close();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed during startup initialization");
            MessageBox.Show(
                $"Startup Error:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                "UMI — Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Current.Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_serviceProvider != null)
            {
                var gpuQueue = _serviceProvider.GetService<IGpuTaskQueue>();
                if (gpuQueue is { IsRunning: true })
                {
                    Task.Run(() => gpuQueue.StopAsync(CancellationToken.None)).Wait(TimeSpan.FromSeconds(5));
                }
            }

            Log.CloseAndFlush();
            _serviceProvider?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during shutdown cleanup");
        }
        finally
        {
            base.OnExit(e);
            Environment.Exit(e.ApplicationExitCode);
        }
    }

    /// <summary>
    /// Picks the language code to apply at startup.
    /// Priority: installer hint (one-shot, just placed by Inno Setup) > existing config.json
    /// > system UI culture (de/en) > English.
    ///
    /// The installer hint takes precedence even when a config.json exists, because the user
    /// just made an explicit language pick in the setup wizard — overriding a stale value
    /// from a previous install is the desired behavior. After applying, the hint is
    /// persisted into config.json (when one is loaded) and the hint file is deleted.
    /// </summary>
    private static string ResolveInitialLanguage(IConfigWriterService configWriter, ConfigPathResolver paths)
    {
        var hint = InstallLanguageHint.Read(paths.Root);
        if (hint is not null)
        {
            if (configWriter.IsConfigLoaded)
            {
                configWriter.Config.AppSettings.Language = hint;
                try { Task.Run(() => configWriter.SaveAsync()).GetAwaiter().GetResult(); }
                catch (Exception ex) { Log.Warning(ex, "Failed to persist install-language hint to config.json"); }
            }
            InstallLanguageHint.Delete(paths.Root);
            return hint;
        }

        if (configWriter.IsConfigLoaded)
            return configWriter.Config.AppSettings.Language;

        var sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return sys is "de" or "en" ? sys : "en";
    }

    /// <summary>
    /// Sets the UI culture for the current thread and the <see cref="Strings"/> resource class
    /// based on the user's language preference from config.
    /// </summary>
    private static void ApplyLanguage(string languageCode)
    {
        try
        {
            var culture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentUICulture = culture;
            Strings.Culture = culture;
            UMI.Core.Resources.CoreStrings.Culture = culture;
            Log.Information("UI language set to {Language}", languageCode);
        }
        catch (CultureNotFoundException ex)
        {
            Log.Warning(ex, "Invalid language code '{Language}' in config — falling back to English", languageCode);

        }
    }

    /// <summary>
    /// Pre-loads config.json synchronously so the logger can be configured before any service
    /// resolution happens. Returns null when the file is missing (first-run) or unreadable —
    /// callers fall back to the default <see cref="LoggingConfig"/> (level OFF).
    /// </summary>
    private static ConfigWriterService? TryPreloadConfig()
    {
        try
        {
            var resolver = new ConfigPathResolver();
            if (!File.Exists(resolver.ConfigFile)) return null;

            var service = new ConfigWriterService();
            // Run on thread pool to avoid any UI-context deadlock during startup.
            Task.Run(() => service.LoadAsync(resolver.ConfigFile)).GetAwaiter().GetResult();
            return service;
        }
        catch
        {
            return null;
        }
    }

    private static Serilog.ILogger BuildLogger(string level, bool isDebugMode)
    {
        var cfg = new LoggerConfiguration();

        if (!string.Equals(level, LogLevels.Off, StringComparison.OrdinalIgnoreCase))
        {
            var minLevel = string.Equals(level, LogLevels.Debug, StringComparison.OrdinalIgnoreCase)
                ? LogEventLevel.Debug
                : LogEventLevel.Information;

            cfg = cfg.MinimumLevel.Is(minLevel)
                .WriteTo.File(
                    Path.Combine(AppContext.BaseDirectory, "logs", "umi-gui-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        if (isDebugMode)
        {
            cfg = cfg.MinimumLevel.Verbose()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        return cfg.CreateLogger();
    }

    private static ServiceProvider BuildServiceProvider(ConfigWriterService? preloadedConfig)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSerilog());

        services.AddSingleton<ConfigPathResolver>();
        services.AddSingleton<CameraTypeLoader>();
        if (preloadedConfig is not null)
            services.AddSingleton<IConfigWriterService>(preloadedConfig);
        else
            services.AddSingleton<IConfigWriterService, ConfigWriterService>();
        services.AddSingleton<ProfileService>();

        services.AddSingleton<UmiConfig>(sp =>
            sp.GetRequiredService<IConfigWriterService>().Config);

        services.AddSingleton<MetadataBackupConfig>(sp => sp.GetRequiredService<UmiConfig>().MetadataBackup);
        services.AddSingleton<GpsProcessingConfig>(sp => sp.GetRequiredService<UmiConfig>().GpsProcessing);
        services.AddSingleton<GyroflowConfig>(sp => sp.GetRequiredService<UmiConfig>().Gyroflow);
        services.AddSingleton<VerificationConfig>(sp => sp.GetRequiredService<UmiConfig>().Verification);
        services.AddSingleton<GlobalPaths>(sp => sp.GetRequiredService<UmiConfig>().GlobalPaths);
        services.AddSingleton<LayoutConfig>(sp => sp.GetRequiredService<UmiConfig>().Layout);
        services.AddSingleton<ArchivingConfig>(sp => sp.GetRequiredService<UmiConfig>().Archiving);
        services.AddSingleton<DuplicateHandlingConfig>(sp => sp.GetRequiredService<UmiConfig>().DuplicateHandling);
        services.AddSingleton<LensCorrectionConfig>(sp => sp.GetRequiredService<UmiConfig>().LensCorrection);
        services.AddSingleton<LoggingConfig>(sp => sp.GetRequiredService<UmiConfig>().Logging);
        services.AddSingleton<WorkflowConfig>(sp => sp.GetRequiredService<UmiConfig>().Workflow);
        services.AddSingleton<RunOptions>(sp => sp.GetRequiredService<UmiConfig>().Options);

        services.AddUmiCoreServices();

        services.AddSingleton<ToolsViewModel>();
        services.AddSingleton<ProfilesTabViewModel>();
        services.AddSingleton<DevicesTabViewModel>();
        services.AddSingleton<BurstTabViewModel>();
        services.AddSingleton<ConfigViewModel>();
        services.AddSingleton<ImportViewModel>();
        services.AddSingleton<WorkbenchViewModel>();
        services.AddSingleton<ProcessViewModel>();
        services.AddSingleton<MainViewModel>();

        services.AddTransient<SetupWizardViewModel>();

        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
