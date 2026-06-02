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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.Data;

namespace UMI.Core.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registriert alle gemeinsamen UMI Core-Services.
    /// CLI und GUI rufen diese Methode auf, ergaenzen dann ihre spezifischen Services.
    /// VORAUSSETZUNG: UmiConfig muss bereits registriert sein (direkt oder als Lazy-Factory).
    /// </summary>
    public static IServiceCollection AddUmiCoreServices(this IServiceCollection services)
    {

        services.AddSingleton<ICameraModelDatabase>(sp =>
        {
            var configPaths = sp.GetRequiredService<ConfigPathResolver>();
            return new CameraModelDatabase(
                configPaths.CameraModelsFile,
                sp.GetService<ILogger<CameraModelDatabase>>());
        });

        services.AddSingleton<IMp4Parser, Mp4Parser>();
        services.AddSingleton<MetadataService>();
        services.AddSingleton<MetadataReader>();
        services.AddSingleton<SrtConverter>();
        services.AddSingleton<GpsService>();
        services.AddSingleton<IPostProcessingService, PostProcessingService>();
        services.AddSingleton<VerificationService>();
        services.AddSingleton<ArchiveService>();
        services.AddSingleton<ISdFingerprintService, SdFingerprintService>();
        services.AddSingleton<ICardDetectionService, CardDetectionService>();
        services.AddSingleton<ISdCardRegistryService, SdCardRegistryService>();
        services.AddSingleton<IDriveDetectionService, DriveDetectionService>();
        services.AddSingleton<IImportHistoryService, ImportHistoryService>();
        services.AddSingleton<IProcessHistoryService, ProcessHistoryService>();
        services.AddSingleton<IReviewSidecarService, ReviewSidecarService>();
        services.AddSingleton<ISequenceSidecarService, SequenceSidecarService>();

        services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();

        services.AddSingleton<BurstMatchingEngine>();
        services.AddSingleton<SequenceGroupingService>();
        services.AddSingleton<IBurstVisualizerService, BurstVisualizerService>();
        services.AddSingleton<IExifFieldAnalyzerService, ExifFieldAnalyzerService>();
        services.AddSingleton<BurstProfileLoader>();
        services.AddSingleton<AutoPresetGenerator>();
        services.AddSingleton<IFolderSortService, FolderSortService>();

        services.AddSingleton<MetadataMigrationService>();

        services.AddSingleton<LayoutResolver>();
        services.AddSingleton<FileCopyService>();
        services.AddSingleton<EisSortingService>();
        services.AddSingleton<GoProRenameService>();
        services.AddSingleton<ImportPipelineService>();
        services.AddSingleton<IImportOrchestrationService, ImportOrchestrationService>();

        services.AddSingleton<IExifToolWrapper>(sp =>
        {
            // Bundled ExifTool — ALWAYS. No fallback to config paths.
            var path = ConfigPathResolver.DefaultExifToolPath;

            return new ExifToolWrapper(
                path,
                sp.GetService<ILogger<ExifToolWrapper>>());
        });

        services.AddSingleton<IGyroflowService>(sp =>
        {
            var config = sp.GetRequiredService<UmiConfig>();
            if (!string.IsNullOrEmpty(config.GlobalPaths.Tools.Gyroflow)
                && File.Exists(config.GlobalPaths.Tools.Gyroflow))
            {
                return new GyroflowService(
                    config.GlobalPaths.Tools.Gyroflow,
                    config.Gyroflow,
                    sp.GetService<ILogger<GyroflowService>>());
            }
            return new NullGyroflowService(sp.GetService<ILogger<NullGyroflowService>>());
        });

        services.AddSingleton<IPreProcessor, MetadataBackupPreProcessor>();
        services.AddSingleton<IPreProcessor, SrtConversionPreProcessor>();
        services.AddSingleton<IPreProcessor, GpsInjectionPreProcessor>();
        services.AddSingleton<IPreProcessor, EisSortingPreProcessor>();
        services.AddSingleton<IPreProcessor, BurstDetectionPreProcessor>();

        services.AddSingleton<IPreProcessingOrchestrator>(sp =>
            new PreProcessingOrchestrator(
                sp.GetServices<IPreProcessor>(),
                sp.GetService<ILogger<PreProcessingOrchestrator>>()));

        services.AddSingleton<IPostProcessor>(sp =>
            new GyroflowPostProcessor(
                sp.GetRequiredService<IGpuTaskQueue>(),
                sp.GetRequiredService<UmiConfig>(),
                sp.GetRequiredService<ConfigPathResolver>(),
                sp.GetRequiredService<IProcessHistoryService>(),
                sp.GetService<ILogger<GyroflowPostProcessor>>()));
        services.AddSingleton<IPostProcessingOrchestrator>(sp =>
            new PostProcessingOrchestrator(
                sp.GetServices<IPostProcessor>(),
                sp.GetRequiredService<IExifToolWrapper>(),
                sp.GetRequiredService<IProcessHistoryService>(),
                sp.GetService<ILogger<PostProcessingOrchestrator>>()));

#pragma warning disable CA1416
        services.AddSingleton<IMtpService, MtpService>();
#pragma warning restore CA1416
        services.AddSingleton<IMtpDeviceDetectionService, MtpDeviceDetectionService>();
        services.AddSingleton<IMtpImportService, MtpImportService>();

        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IDriveWatcherService, DriveWatcherService>();
        }

        services.AddSingleton<ISupportBundleService>(sp =>
            new SupportBundleService(
                sp.GetRequiredService<ConfigPathResolver>(),
                sp.GetService<UmiConfig>(),
                sp.GetService<ILogger<SupportBundleService>>()));

        services.AddSingleton<IUpdateService>(sp =>
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"UMI/{GitHubReleaseChecker.GetCurrentVersion()}");
            return new GitHubReleaseChecker(httpClient, sp.GetService<ILogger<GitHubReleaseChecker>>());
        });

        services.AddSingleton<GpuTaskDatabase>(sp =>
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UMI", "gpu_queue.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            return new GpuTaskDatabase(dbPath, sp.GetService<ILogger<GpuTaskDatabase>>());
        });

        services.AddSingleton<IGpuTaskQueue>(sp =>
        {
            var config = sp.GetRequiredService<UmiConfig>();
            return new GpuTaskQueue(
                sp.GetRequiredService<GpuTaskDatabase>(),
                sp.GetRequiredService<IGyroflowService>(),
                sp.GetRequiredService<IPostProcessingService>(),
                sp.GetRequiredService<IProcessHistoryService>(),
                config.GpuQueue,
                sp.GetService<ILogger<GpuTaskQueue>>());
        });

        return services;
    }
}
