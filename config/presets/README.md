# Presets Directory

Dieses Verzeichnis enthält Presets und Konfigurationsprofile für UMI (Universal Media Import).

## Struktur

```
Presets/
├── gyroflow/           # Gyroflow-Stabilisierungs-Presets (.gyroflow Dateien)
│   └── OA5_Default.gyroflow
├── profiles/           # Config-Profile (Delta-Overrides)
│   ├── Action-R10.json
│   └── Drone-Only.json
└── README.md
```

## 📁 gyroflow/

Enthält Gyroflow-Presets für Kamera-spezifische Stabilisierung.

- **Format**: `.gyroflow` Dateien (JSON-basiert)
- **Verwendung**: Werden in `config.json` unter `cameras.<ID>.custom_settings.gyroflow.preset` referenziert
- **Pfad-Angabe**: Relativ zu `Presets/` (z.B. `"gyroflow/OA5_Default.gyroflow"`)

### Gyroflow-Presets erstellen

1. Öffne ein Video in Gyroflow
2. Konfiguriere Stabilisierung (Lens Profile, Sync, etc.)
3. Speichere als `.gyroflow` Preset
4. Kopiere Datei nach `Presets/gyroflow/`
5. Referenziere in `config.json`

## 📁 profiles/

Enthält Config-Profile für verschiedene Import-Szenarien.

- **Format**: JSON-Dateien mit **Delta-Overrides** (nicht vollständige Configs!)
- **Verwendung**: `umi import --profile Action-R10`
- **Merge-Reihenfolge**: `config.json` → `Profile` → `CLI-Flags` (höchste Priorität)

### Profile erstellen

Manuell:

```json
{
  "_description": "Import nur Action-Kameras mit GPS",
  "_created": "2025-01-10T14:30:00Z",

  "cameras": {
    "OA5": {
      "enabled": true,
      "features": {
        "gps_injection": true,
        "gyroflow": false
      }
    },
    "R10": {
      "enabled": false
    }
  }
}
```

**Wichtig**: Profile enthalten nur die **Änderungen** zur Basis-Config, nicht die komplette Konfiguration.

### Profile verwalten

```bash
# Alle Profile auflisten
umi profiles list

# Profil-Inhalt anzeigen
umi profiles show Action-R10

# Profil löschen
umi profiles delete Action-R10
```

### Metadata-Felder

Profile können optionale Metadata enthalten (werden beim Merge ignoriert):

- `_profile`: Profilname (automatisch gesetzt)
- `_description`: Beschreibung des Profils
- `_created`: Erstellungsdatum (ISO 8601)

## 🔒 Version Control

- ✅ **gyroflow/**: Presets werden versioniert (im Git)
- ❌ **profiles/**: User-spezifische Profile werden **NICHT** versioniert (`.gitignore`)

Grund: Profile enthalten oft user-spezifische Pfade oder Präferenzen.

## 📖 Beispiel-Workflows

### Workflow 1: Action-Kameras mit GPS

Profil `Action-GPS.json`:
```json
{
  "_description": "Action-Kameras (OA5) mit GPS-Injection",
  "cameras": {
    "OA5": { "enabled": true },
    "R10": { "enabled": false },
    "M2P": { "enabled": false }
  },
  "options": {
    "gps": true,
    "stabilize": false
  }
}
```

Verwendung:
```bash
umi import --profile Action-GPS
```

### Workflow 2: Nur Drohnen

Profil `Drone-Only.json`:
```json
{
  "_description": "Nur Drohnen (M2P)",
  "cameras": {
    "OA5": { "enabled": false },
    "R10": { "enabled": false },
    "M2P": { "enabled": true }
  }
}
```

Verwendung:
```bash
umi import --profile Drone-Only --gps
```

## 🛠️ Technische Details

### Deep Merge-Strategie

Profile werden mit **Deep Merge** auf `JsonNode`-Level kombiniert:

1. **Base**: `config.json` wird geladen
2. **Override**: Profil-JSON wird darüber gemerged (rekursiv)
3. **CLI-Flags**: Überschreiben finale Werte (höchste Priorität)

Beispiel:
```
config.json:           cameras.OA5.enabled = true
Profile:               cameras.OA5.enabled = false  ← überschreibt
CLI --source R10:      (filtert OA5 aus)            ← höchste Priorität
```

### Pfad-Konventionen

- **Absolute Pfade**: Bleiben unverändert
- **Relative Pfade**: Relativ zu `Presets/` Verzeichnis
- **Nur Dateiname**: Automatische Suche in Standard-Ordnern

### Sicherheit

- Profilnamen werden **sanitized** (keine `..`, `/`, `\`)
- Verhindert Path-Traversal-Angriffe
- Nur `.json` Dateien werden als Profile erkannt

## 📚 Weiterführende Dokumentation

- **ARCHITECTURE.md**: Technische Architektur-Details
- **CLAUDE.md**: Code-Quality-Regeln und Entwickler-Guidelines
- **MEMORY.md**: Projekt-Historie und Bug-Tracking
