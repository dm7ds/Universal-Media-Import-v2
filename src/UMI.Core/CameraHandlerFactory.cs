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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace UMI.Core;

/// <summary>
/// Factory zum Verwalten und Laden von Kamera-Handlers.
/// Unterstützt Plugin-Architektur für modulare Kamera-Integration.
/// </summary>
public class CameraHandlerFactory
{
    private readonly Dictionary<string, ICameraHandler> _handlers = new();
    private readonly ILogger<CameraHandlerFactory>? _logger;

    public CameraHandlerFactory(ILogger<CameraHandlerFactory>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registriert einen Kamera-Handler.
    /// </summary>
    public void RegisterHandler(ICameraHandler handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        if (string.IsNullOrWhiteSpace(handler.CameraId))
            throw new ArgumentException("CameraId darf nicht leer sein.", nameof(handler));

        if (_handlers.ContainsKey(handler.CameraId))
        {
            _logger?.LogWarning("Kamera-Handler '{CameraId}' wird überschrieben.", handler.CameraId);
        }

        _handlers[handler.CameraId] = handler;
        _logger?.LogDebug("Kamera-Handler '{CameraId}' registriert: {DisplayName}",
            handler.CameraId, handler.DisplayName);
    }

    /// <summary>
    /// Gibt einen Handler für die angegebene Kamera-ID zurück.
    /// </summary>
    public ICameraHandler? GetHandler(string cameraId)
    {
        if (string.IsNullOrWhiteSpace(cameraId))
            throw new ArgumentException("CameraId darf nicht leer sein.", nameof(cameraId));

        return _handlers.TryGetValue(cameraId, out var handler) ? handler : null;
    }

    /// <summary>
    /// Gibt alle registrierten Handler zurück.
    /// </summary>
    public IEnumerable<ICameraHandler> GetAllHandlers()
    {
        return _handlers.Values;
    }

    /// <summary>
    /// Lädt alle Handlers aus einer Assembly.
    /// Sucht nach Klassen die ICameraHandler implementieren.
    /// </summary>
    public void LoadHandlersFromAssembly(Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ICameraHandler).IsAssignableFrom(t));

        foreach (var type in handlerTypes)
        {
            try
            {

                var instance = Activator.CreateInstance(type) as ICameraHandler;
                if (instance != null)
                {
                    RegisterHandler(instance);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim Laden von Handler-Typ '{TypeName}'", type.Name);
            }
        }
    }

    /// <summary>
    /// Lädt Handlers aus allen DLLs in einem Verzeichnis.
    /// Ermöglicht Plugin-Architektur (z.B. "plugins/" Ordner).
    /// </summary>
    public void LoadHandlersFromDirectory(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            _logger?.LogWarning("Plugin-Verzeichnis existiert nicht: {Directory}", pluginDirectory);
            return;
        }

        var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                LoadHandlersFromAssembly(assembly);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim Laden von Plugin '{DllPath}'", dllPath);
            }
        }
    }

    /// <summary>
    /// Gibt die Anzahl registrierter Handlers zurück.
    /// </summary>
    public int Count => _handlers.Count;

    /// <summary>
    /// Prüft ob ein Handler für die angegebene Kamera-ID existiert.
    /// </summary>
    public bool HasHandler(string cameraId)
    {
        return _handlers.ContainsKey(cameraId);
    }

    /// <summary>
    /// Entfernt alle registrierten Handlers.
    /// </summary>
    public void Clear()
    {
        _handlers.Clear();
        _logger?.LogInformation("Alle Kamera-Handler wurden entfernt.");
    }
}
