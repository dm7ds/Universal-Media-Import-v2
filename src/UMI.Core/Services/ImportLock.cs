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

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// File-basiertes Import-Lock zur Verhinderung paralleler Ausführung.
/// OS-managed via FileStream → crashsafe, kein Stale-Lock Problem.
/// </summary>
public interface IImportLock : IDisposable
{
    /// <summary>
    /// Versucht Lock zu erwerben.
    /// Returns: true wenn erfolgreich, false wenn bereits gelockt.
    /// </summary>
    /// <param name="workbenchPath">Workbench-Pfad (Lock-File landet dort)</param>
    /// <param name="blockedByInfo">Info über blockierenden Prozess (JSON) oder null</param>
    bool TryAcquire(string workbenchPath, out string? blockedByInfo);

    /// <summary>
    /// Gibt Lock frei (wird auch automatisch via Dispose aufgerufen).
    /// </summary>
    void Release();
}

/// <summary>
/// Lock-Info Struktur (wird als JSON ins Lock-File geschrieben).
/// </summary>
public class LockInfo
{
    public int Pid { get; set; }
    public string Started { get; set; } = "";
    public string Source { get; set; } = "";
    public string Command { get; set; } = "";
}

public class ImportLock : IImportLock
{
    private FileStream? _lockStream;
    private string? _lockFilePath;
    private readonly ILogger? _logger;

    private string? _source;
    private string _command = "import";

    public ImportLock(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Setzt Source-Info (für Lock-File JSON).
    /// Muss VOR TryAcquire() aufgerufen werden.
    /// </summary>
    public void SetSource(string source)
    {
        _source = source;
    }

    /// <summary>
    /// Setzt Command-Info (für Lock-File JSON).
    /// Muss VOR TryAcquire() aufgerufen werden.
    /// </summary>
    public void SetCommand(string command)
    {
        _command = command;
    }

    /// <summary>
    /// Versucht Lock zu erwerben via FileStream (OS-Level).
    /// </summary>
    public bool TryAcquire(string workbenchPath, out string? blockedByInfo)
    {
        blockedByInfo = null;

        if (_lockStream != null)
        {
            _logger?.LogWarning("Lock bereits erworben, überspringe TryAcquire");
            return true;
        }

        try
        {
            var umiDir = Path.Combine(workbenchPath, FolderNameConstants.UmiDir);
            System.IO.Directory.CreateDirectory(umiDir);
            _lockFilePath = Path.Combine(umiDir, FolderNameConstants.UmiLock);

            _lockStream = new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            WriteLockInfo();

            _logger?.LogDebug("Import-Lock erworben: {LockFile}", _lockFilePath);
            return true;
        }
        catch (IOException ex)
        {

            _logger?.LogDebug(ex, "Lock-File ist bereits gelockt, versuche Info zu lesen");

            try
            {

                using var readStream = new FileStream(
                    _lockFilePath!,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                using var reader = new StreamReader(readStream, Encoding.UTF8);
                blockedByInfo = reader.ReadToEnd();

                _logger?.LogDebug("Import blockiert durch anderen Prozess: {Info}", blockedByInfo);
            }
            catch (Exception readEx)
            {
                _logger?.LogWarning(readEx, "Konnte Lock-Info nicht lesen");
                blockedByInfo = "Unbekannter Prozess (Lock-Info nicht lesbar)";
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Erwerben des Import-Locks");
            return false;
        }
    }

    /// <summary>
    /// Gibt Lock frei.
    /// </summary>
    public void Release()
    {
        if (_lockStream == null)
            return;

        try
        {

            _lockStream.SetLength(0);
            _lockStream.Flush();

            _lockStream.Dispose();
            _lockStream = null;

            if (_lockFilePath != null && File.Exists(_lockFilePath))
            {
                try
                {
                    File.Delete(_lockFilePath);
                    _logger?.LogDebug("Lock-File gelöscht: {LockFile}", _lockFilePath);
                }
                catch
                {

                }
            }

            _logger?.LogDebug("Import-Lock freigegeben");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Freigeben des Import-Locks");
        }
    }

    /// <summary>
    /// Dispose ruft Release auf (Safety Net für using-Pattern).
    /// </summary>
    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Schreibt Lock-Info als JSON ins Lock-File.
    /// </summary>
    private void WriteLockInfo()
    {
        if (_lockStream == null)
            return;

        try
        {
            var lockInfo = new LockInfo
            {
                Pid = Environment.ProcessId,
                Started = DateTime.Now.ToString("o"),
                Source = _source ?? "ALL",
                Command = _command
            };

            var json = JsonSerializer.Serialize(lockInfo, JsonDefaults.WriteOptions);

            var bytes = Encoding.UTF8.GetBytes(json);

            _lockStream.SetLength(0);
            _lockStream.Write(bytes, 0, bytes.Length);
            _lockStream.Flush();

            _logger?.LogDebug("Lock-Info geschrieben: {Info}", json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Konnte Lock-Info nicht schreiben");
        }
    }
}
