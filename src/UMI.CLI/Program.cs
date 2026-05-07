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

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using UMI.CLI.Commands;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.Cameras;
using UMI.Data;

namespace UMI.CLI;

class Program
{

    public static readonly LoggingLevelSwitch ConsoleLogLevel = new LoggingLevelSwitch(LogEventLevel.Warning);

    public static readonly LoggingLevelSwitch FileLogLevel = new LoggingLevelSwitch(LogEventLevel.Debug);

    public static readonly Option<bool> VerboseOption = new(["-v", "--verbose"], "Debug-Logging aktivieren (zeigt alle Details)");
    public static readonly Option<bool> QuietOption = new(["-q", "--quiet"], "Nur Fehler anzeigen");
    public static readonly Option<string> ConfigOption = new("--config", () => "config.json", "Pfad zur config.json");
    public static readonly Option<bool> DryRunOption = new("--dry-run", "Simulation ohne Dateiänderungen");
    public static readonly Option<string?> ProfileOption = new("--profile", "Config-Profil (aus Presets/profiles/)");

    [SupportedOSPlatform("windows")]
    static async Task<int> Main(string[] args)
    {

        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "umi-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                levelSwitch: ConsoleLogLevel,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                levelSwitch: FileLogLevel,
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var rootCommand = new RootCommand("UMI - Universal Media Import v2.1");

            rootCommand.AddGlobalOption(VerboseOption);
            rootCommand.AddGlobalOption(QuietOption);
            rootCommand.AddGlobalOption(ConfigOption);
            rootCommand.AddGlobalOption(DryRunOption);
            rootCommand.AddGlobalOption(ProfileOption);

            rootCommand.AddCommand(SetupCommand.Create());
            rootCommand.AddCommand(ImportCommand.Create());
            rootCommand.AddCommand(ProcessCommand.Create());
            rootCommand.AddCommand(RestoreCommand.Create());
            rootCommand.AddCommand(VerifyCommand.Create());
            rootCommand.AddCommand(ArchiveCommand.Create());
            rootCommand.AddCommand(TestCameraCommand.Create());
            rootCommand.AddCommand(ProfilesCommand.Create());
            rootCommand.AddCommand(ExifScanCommand.Create());
            rootCommand.AddCommand(WatchCommand.Create());
            rootCommand.AddCommand(QuickCommand.Create());
            rootCommand.AddCommand(GpsCommand.Create());
            rootCommand.AddCommand(UpdateCommand.Create());

            rootCommand.SetHandler(() =>
            {
                var resolver = new ConfigPathResolver();
                if (!File.Exists(resolver.ConfigFile))
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Keine config.json gefunden.");
                    Console.ResetColor();
                    Console.WriteLine("Starte 'umi config setup' fuer die Ersteinrichtung.");
                    Console.WriteLine();
                }
            });

            var builder = new CommandLineBuilder(rootCommand)
                .AddMiddleware(async (context, next) =>
                {
                    if (context.ParseResult.GetValueForOption(QuietOption))
                        ConsoleLogLevel.MinimumLevel = LogEventLevel.Fatal;
                    else if (context.ParseResult.GetValueForOption(VerboseOption))
                        ConsoleLogLevel.MinimumLevel = LogEventLevel.Debug;
                    await next(context);
                })
                .UseVersionOption()
                .UseHelp()
                .UseHelpBuilder(_ =>
                {
                    var helpBuilder = new HelpBuilder(LocalizationResources.Instance);

                    helpBuilder.CustomizeLayout(_ =>
                        new HelpSectionDelegate[]
                        {
                            HelpBuilder.Default.SynopsisSection(),
                            HelpBuilder.Default.CommandUsageSection(),
                            HelpBuilder.Default.CommandArgumentsSection(),
                            HelpBuilder.Default.SubcommandsSection(),
                            HelpBuilder.Default.OptionsSection(),
                            HelpBuilder.Default.AdditionalArgumentsSection(),
                        });
                    return helpBuilder;
                })
                .UseEnvironmentVariableDirective()
                .UseParseDirective()
                .UseSuggestDirective()
                .RegisterWithDotnetSuggest()
                .UseTypoCorrections()
                .UseParseErrorReporting()
                .UseExceptionHandler()
                .CancelOnProcessTermination()
                .Build();

            return await builder.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unerwarteter Fehler");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static async Task<ServiceProvider> BuildServiceProviderAsync(string configPath = "config.json", string? profileName = null)
    {
        var services = new ServiceCollection();

        var configPaths = new ConfigPathResolver();
        services.AddSingleton(configPaths);

        if (configPath == "config.json" || Path.GetFileName(configPath) == configPath)
        {
            configPath = configPaths.ConfigFile;
        }

        services.AddLogging(builder =>
        {
            builder.AddSerilog();
        });

        var profileService = new ProfileService(configPaths);
        services.AddSingleton(profileService);

        var typeLoader = new CameraTypeLoader(configPaths);
        services.AddSingleton(typeLoader);

        var configLoader = new ConfigLoader(
            profileService: profileService,
            typeLoader: typeLoader,
            configPaths: configPaths);
        var config = await configLoader.LoadAsync(configPath, profileName);

        services.AddSingleton(config);

        services.AddSingleton(config.MetadataBackup);
        services.AddSingleton(config.GpsProcessing);
        services.AddSingleton(config.Gyroflow);
        services.AddSingleton(config.Verification);
        services.AddSingleton(config.GlobalPaths);
        services.AddSingleton(config.Layout);
        services.AddSingleton(config.Archiving);
        services.AddSingleton(config.DuplicateHandling);
        services.AddSingleton(config.LensCorrection);
        services.AddSingleton(config.Logging);
        services.AddSingleton(config.Workflow);
        services.AddSingleton(config.Options);

        var configWriter = new ConfigWriterService();
        await configWriter.LoadAsync(configPath);
        services.AddSingleton<IConfigWriterService>(configWriter);

        if (!string.IsNullOrEmpty(config.GlobalPaths.Tools.FFprobe))
        {
            services.AddSingleton(sp =>
                new FFprobeWrapper(
                    config.GlobalPaths.Tools.FFprobe,
                    sp.GetService<ILogger<FFprobeWrapper>>()));
        }

        services.AddUmiCoreServices();

        services.AddSingleton<CameraHandlerFactory>(sp =>
        {
            var factory = new CameraHandlerFactory(sp.GetService<ILogger<CameraHandlerFactory>>());

            foreach (var (cameraId, cameraConfig) in config.Cameras)
            {
                if (!cameraConfig.Enabled)
                    continue;

                var handler = new UniversalCameraHandler(
                    sp.GetService<ILogger<UniversalCameraHandler>>());

                handler.Initialize(cameraId, cameraConfig);
                factory.RegisterHandler(handler);
            }

            return factory;
        });

        return services.BuildServiceProvider();
    }
}
