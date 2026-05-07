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

using UMI.Core.Configuration;

namespace UMI.Core.Services;

/// <summary>
/// Zentrale Factory fuer ImportContext. Setzt Defaults aus CameraConfig,
/// CLI/GUI koennen Feature-Flags overriden.
/// </summary>
public static class ImportContextFactory
{
    public static ImportContext Create(
        string cameraId,
        CameraConfig config,
        string sourcePath,
        string workbenchPath,
        GlobalSettings globalSettings,

        bool? injectGps = null,
        bool? stabilize = null,
        string stabilizeMode = "all",
        bool forceStabilize = false,
        bool noEisSort = false,
        bool dryRun = false,
        bool fullImport = false,
        bool resetHistory = false,
        bool isAdHocFolder = false,
        bool? renameVideos = null,
        bool? goProRename = null,
        bool? postProcess = null)
    {
        return new ImportContext
        {
            CameraId = cameraId,
            Config = config,
            SourcePath = sourcePath,
            WorkbenchPath = workbenchPath,
            GlobalSettings = globalSettings,
            InjectGps = injectGps ?? config.Features.GpsInjection,
            Stabilize = stabilize ?? config.Features.Gyroflow,
            StabilizeMode = stabilizeMode,
            ForceStabilize = forceStabilize,
            NoEisSort = noEisSort,
            DryRun = dryRun,
            FullImport = fullImport,
            ResetHistory = resetHistory,
            IsAdHocFolder = isAdHocFolder,
            RenameVideos = renameVideos ?? config.Features.RenameVideos,
            GoProRename = goProRename ?? config.Features.GoProRename,
            PostProcess = postProcess ?? config.Features.PostProcess,
        };
    }
}
