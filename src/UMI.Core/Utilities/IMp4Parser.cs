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

namespace UMI.Core.Utilities;

/// <summary>
/// Interface für Mp4Parser - ermöglicht Mocking in Tests.
/// </summary>
public interface IMp4Parser
{
    /// <summary>
    /// Liest CreateDate und Duration direkt aus MP4-Header (mvhd Box).
    /// </summary>
    Task<Mp4Metadata> ReadMetadataAsync(string videoPath, CancellationToken ct = default);

    /// <summary>
    /// Erkennt Kamera-Modell aus MP4-Header.
    /// </summary>
    Task<CameraInfo> DetectCameraModelAsync(string videoPath, CancellationToken ct = default);

    /// <summary>
    /// Erkennt kameraseitige elektronische Bildstabilisierung (EIS).
    /// Unterstützt DJI Rocksteady, GoPro HyperSmooth, etc.
    /// </summary>
    Task<EisDetectionResult> DetectEisStatusAsync(string videoPath, CancellationToken ct = default);
}
