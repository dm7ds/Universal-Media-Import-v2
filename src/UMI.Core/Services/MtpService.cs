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

using System.Runtime.Versioning;
using MediaDevices;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Wrapper um die MediaDevices-Library für MTP-Gerätezugriff via Windows Portable Devices API.
/// Windows-only: MTP nutzt die Windows Portable Devices (WPD) API.
/// </summary>
[SupportedOSPlatform("windows7.0")]
public class MtpService : IMtpService
{
    private readonly ILogger<MtpService>? _logger;

    public MtpService(ILogger<MtpService>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<MtpDeviceInfo> GetConnectedDevices()
    {
        var result = new List<MtpDeviceInfo>();

        foreach (var device in MediaDevice.GetDevices())
        {
            using (device)
            {

                if (device.DeviceId.Contains("USBSTOR", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogDebug("Skipping non-MTP USBSTOR device: {DeviceId}", device.DeviceId);
                    continue;
                }

                if (!device.DeviceId.Contains("usb#vid_", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogDebug("Skipping non-USB WPD device: {DeviceId}", device.DeviceId);
                    continue;
                }

                try
                {
                    device.Connect();

                    var usbSerial = ParseUsbSerial(device.DeviceId);
                    var effectiveSerial = usbSerial ?? device.SerialNumber;

                    _logger?.LogDebug(
                        "MTP: Gerät gefunden (Typ={DeviceType}): {FriendlyName} [{Manufacturer}/{Model}] Serial={WpdSerial} UsbSerial={UsbSerial}",
                        device.DeviceType, device.FriendlyName, device.Manufacturer, device.Model,
                        device.SerialNumber ?? "–", usbSerial ?? "–");

                    result.Add(new MtpDeviceInfo(
                        DeviceId: device.DeviceId,
                        FriendlyName: device.FriendlyName,
                        Manufacturer: device.Manufacturer,
                        Model: device.Model,
                        SerialNumber: effectiveSerial,
                        WpdSerialNumber: usbSerial != null ? device.SerialNumber : null));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "MTP-Gerät konnte nicht verbunden werden: {DeviceId}", device.DeviceId);
                }
                finally
                {
                    try { device.Disconnect(); } catch (Exception ex) { _logger?.LogDebug(ex, "MTP disconnect failed during device enumeration"); }
                }
            }
        }

        _logger?.LogDebug("MTP: {Count} Gerät(e) gefunden", result.Count);
        return result;
    }

    /// <summary>
    /// Versucht die USB-Serial aus dem WPD DeviceId zu extrahieren.
    /// Format: \\?\usb#vid_XXX&amp;pid_YYY#USB_SERIAL#{guid}
    /// Gibt null zurück wenn das Format nicht passt oder kein USB-Gerät.
    /// Port-basierte IDs (z.B. "5&amp;12345678&amp;0&amp;1") enthalten '&amp;' und werden als instabil abgelehnt.
    /// </summary>
    internal static string? ParseUsbSerial(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return null;

        var parts = deviceId.Split('#');
        if (parts.Length < 4)
            return null;

        if (!parts[0].Contains("usb", StringComparison.OrdinalIgnoreCase))
            return null;

        var usbSerial = parts[2];

        if (string.IsNullOrEmpty(usbSerial) || usbSerial.Contains('&'))
            return null;

        return usbSerial;
    }

    /// <inheritdoc/>
    public IReadOnlyList<MtpFileInfo> ListFiles(
        string deviceId,
        string? rootPath = null,
        IReadOnlySet<string>? extensions = null)
    {
        var device = FindDevice(deviceId);
        if (device is null)
        {
            _logger?.LogWarning("MTP-Gerät nicht gefunden: {DeviceId}", deviceId);
            return Array.Empty<MtpFileInfo>();
        }

        using (device)
        {
            try
            {
                device.Connect();

                var effectiveRoot = rootPath ?? @"\";
                var files = device
                    .EnumerateFiles(effectiveRoot, "*.*", SearchOption.AllDirectories)
                    .Select(path =>
                    {
                        try
                        {
                            var info = device.GetFileInfo(path);

                            var modified = info.LastWriteTime;
                            if (modified <= DateTime.MinValue)
                                modified = info.CreationTime;
                            var dateModified = modified > DateTime.MinValue ? modified : (DateTime?)null;
                            return new MtpFileInfo(
                                FullPath: path,
                                Name: Path.GetFileName(path),
                                Length: (long)info.Length,
                                DateModified: dateModified);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug(ex, "MTP GetFileInfo failed for {Path} — device may have been disconnected", path);
                            return new MtpFileInfo(
                                FullPath: path,
                                Name: Path.GetFileName(path),
                                Length: 0,
                                DateModified: null);
                        }
                    })
                    .Where(f => extensions is null ||
                                extensions.Contains(Path.GetExtension(f.Name), StringComparer.OrdinalIgnoreCase))
                    .ToList();

                _logger?.LogDebug("MTP ListFiles: {Count} Datei(en) auf {DeviceId}", files.Count, deviceId);
                return files;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim Auflisten von MTP-Dateien auf {DeviceId}", deviceId);
                return Array.Empty<MtpFileInfo>();
            }
            finally
            {
                try { device.Disconnect(); } catch (Exception ex) { _logger?.LogDebug(ex, "MTP disconnect failed after ListFiles"); }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<string?> DownloadFileAsync(
        string deviceId,
        string mtpFilePath,
        string localTargetDir,
        CancellationToken ct = default)
    {

        ct.ThrowIfCancellationRequested();

        var device = FindDevice(deviceId);
        if (device is null)
        {
            _logger?.LogWarning("MTP-Gerät nicht gefunden: {DeviceId}", deviceId);
            return null;
        }

        using (device)
        {
            try
            {
                device.Connect();

                Directory.CreateDirectory(localTargetDir);
                var fileName = Path.GetFileName(mtpFilePath);
                var localPath = Path.Combine(localTargetDir, fileName);

                const int maxRetries = 3;
                for (var attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        device.DownloadFile(mtpFilePath, localPath);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (attempt < maxRetries)
                    {
                        _logger?.LogWarning(ex,
                            "MTP Download Retry {Attempt}/{Max}: {File}",
                            attempt, maxRetries, fileName);

                        try { if (File.Exists(localPath)) File.Delete(localPath); }
                        catch (Exception cleanupEx) { _logger?.LogDebug(cleanupEx, "Failed to delete partial MTP download: {Path}", localPath); }

                        await Task.Delay(1000 * (int)Math.Pow(2, attempt - 1), ct);

                        try { device.Disconnect(); } catch (Exception disconnectEx) { _logger?.LogDebug(disconnectEx, "MTP disconnect failed during retry reconnect"); }
                        device.Connect();
                    }
                }

                _logger?.LogDebug("MTP Download: {File} → {LocalPath}", fileName, localPath);
                return localPath;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim MTP-Download: {MtpPath}", mtpFilePath);
                return null;
            }
            finally
            {
                try { device.Disconnect(); } catch (Exception ex) { _logger?.LogDebug(ex, "MTP disconnect failed after DownloadFile"); }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<MtpDownloadResult> DownloadBatchAsync(
        string deviceId,
        IReadOnlyList<MtpFileInfo> files,
        string localTargetDir,
        IProgress<MtpDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var downloaded = 0;
            var failed = 0;
            long totalBytes = 0;
            var errors = new List<string>();

            var device = FindDevice(deviceId);
            if (device is null)
            {
                _logger?.LogWarning("MTP-Gerät nicht gefunden: {DeviceId}", deviceId);
                errors.Add($"Gerät nicht gefunden: {deviceId}");
                return new MtpDownloadResult(0, files.Count, 0, 0, errors);
            }

            using (device)
            {
                try
                {
                    device.Connect();
                    Directory.CreateDirectory(localTargetDir);

                    long bytesTotal = files.Sum(f => f.Length);

                    for (var i = 0; i < files.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();

                        var file = files[i];
                        var localPath = Path.Combine(localTargetDir, file.Name);

                        try
                        {
                            device.DownloadFile(file.FullPath, localPath);
                            downloaded++;
                            totalBytes += file.Length;

                            _logger?.LogDebug("MTP Batch [{Current}/{Total}]: {File}", i + 1, files.Count, file.Name);
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            var msg = $"{file.Name}: {ex.Message}";
                            errors.Add(msg);
                            _logger?.LogError(ex, "MTP Batch-Download fehlgeschlagen: {File}", file.Name);
                        }

                        progress?.Report(new MtpDownloadProgress(
                            Current: i + 1,
                            Total: files.Count,
                            CurrentFile: file.Name,
                            BytesDownloaded: totalBytes,
                            BytesTotal: bytesTotal));
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Kritischer Fehler im MTP Batch-Download auf {DeviceId}", deviceId);
                    errors.Add($"Kritischer Fehler: {ex.Message}");
                    failed += files.Count - downloaded;
                }
                finally
                {
                    try { device.Disconnect(); } catch (Exception ex) { _logger?.LogDebug(ex, "MTP disconnect failed after BatchDownload"); }
                }
            }

            _logger?.LogInformation(
                "MTP Batch abgeschlossen: {Downloaded} OK, {Failed} Fehler",
                downloaded, failed);

            return new MtpDownloadResult(downloaded, failed, 0, totalBytes, errors);
        }, ct);
    }

    /// <summary>
    /// Sucht ein MTP-Gerät anhand der DeviceId.
    /// Nicht-gematchte Instanzen werden sofort disposed (Dispose-Leak-Fix).
    /// Gibt null zurück, wenn das Gerät nicht gefunden wird.
    /// </summary>
    private MediaDevice? FindDevice(string deviceId)
    {
        try
        {
            MediaDevice? matched = null;
            foreach (var device in MediaDevice.GetDevices())
            {
                if (string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    matched = device;
                }
                else
                {
                    device.Dispose();
                }
            }
            return matched;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Suchen von MTP-Gerät: {DeviceId}", deviceId);
            return null;
        }
    }
}
