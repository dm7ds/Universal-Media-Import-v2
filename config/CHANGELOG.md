# Config Schema Changelog

Dokumentiert alle Schema-Änderungen an Config- und Preset-Dateien.
Für automatische Migration und User-Transparenz.

---

## Schema v1 (UMI v2.1.0) – Initial

**Datum:** 2026-02-16
**Status:** ✅ Implementiert

Initiales Schema. Alle Dateien bekommen `_umi_header` Block für Versionierung und Typ-Erkennung.

### Dateitypen

#### Config (config.json)
- Haupt-Konfiguration mit Kamera-Definitionen, Pfaden, Feature-Flags
- Header: `"type": "config", "schema_version": 1`
- Enthält: Cameras, GlobalPaths, Feature-Configs, Workflow-Settings

#### Burst-Profile (*.umi)
- Sport, Astro, Timelapse, HighISO
- Header: `"type": "burst_profile", "schema_version": 1`
- Felder: `name`, `priority`, `match_conditions`, `grouping`
- Lokation: `config/presets/burst/`

#### Camera Types (*.umi)
- Action, Drone, Mirrorless
- Header: `"type": "camera_type", "schema_version": 1`
- Felder: `name`, `description`, `color`, `default_features`
- Lokation: `config/presets/types/`

#### Import-Profile (*.umi)
- User-definierte Import-Konfigurationen (Delta-Merge auf Basis-Config)
- Header: `"type": "import_profile", "schema_version": 1`
- Lokation: `config/presets/profiles/` (.gitignore - User-Daten)

### Verzeichnisstruktur

```
config/
├── config.json                # Haupt-Config
├── config.template.json       # Template für neue Installationen
├── CHANGELOG.md               # Diese Datei
├── presets/
│   ├── burst/*.umi            # Burst-Detection Profile
│   ├── types/*.umi            # Kamera-Typ Definitionen
│   ├── gyroflow/*.gyroflow    # Gyroflow Presets
│   └── profiles/*.umi         # User Import-Profile (.gitignore)
└── defaults/                  # Readonly-Blaupausen für Reset/Diff
    ├── config.default.json
    ├── burst/*.umi
    └── types/*.umi
```

### Änderungen gegenüber UMI v2.0

**Vor v2.1 (Legacy):**
- `Presets/` im Root statt `config/presets/`
- `.json` Endung für alle Presets (keine Header)
- config.json im Root
- Keine defaults/ Blaupausen

**Migration:**
- Alle Loader unterstützen Legacy `.json` Dateien als Fallback
- Alte `Presets/` Struktur kann koexistieren (wird ignoriert wenn `config/` existiert)
- Automatische Erkennung: `.umi` bevorzugt, `.json` als Fallback

---

## Geplant: Schema v2 (UMI v2.2 - GUI Milestone)

**Status:** 🔜 Geplant

### Features

**JsonDiff Engine:**
- Vergleich zwischen User-Config und Defaults
- Highlight von Änderungen für Config-Export

**ConfigExchangeService:**
- Import/Export von `.umi` Dateien (Drag & Drop)
- Schema-Migration (automatisch bei `schema_version` Erhöhung)
- CLI Commands: `umi config export`, `umi config import`

**GUI-Integration:**
- Settings-Dialog mit visueller Config-Bearbeitung
- Preset-Manager (Burst, Types, Import-Profile)
- Diff-View für Config-Änderungen

### Schema-Änderungen (falls nötig)

- `schema_version: 2` in Headers
- Neue Felder werden rückwärts-kompatibel hinzugefügt
- Migration-Pfad dokumentiert

---

## Migration-Strategie

### Von Legacy (.json ohne Header) zu v1 (.umi mit Header)

1. **Automatisch beim Laden:**
   - Loader akzeptieren `.json` Files ohne Header
   - Header-Felder werden ignoriert wenn vorhanden
   - Keine Fehler bei fehlenden Headers

2. **Manuell (empfohlen):**
   ```bash
   # Vorhandene .json Presets können umbenannt werden
   mv config/presets/burst/Sport.json config/presets/burst/Sport.umi

   # Header manuell hinzufügen oder aus defaults/ kopieren
   ```

3. **Automatisch (zukünftig mit v2.2):**
   ```bash
   umi config migrate
   ```

### Von v1 zu v2 (geplant)

- Automatische Migration beim Laden
- Backup wird erstellt (config.json.bak)
- Log-Ausgabe: "Config Schema v1 -> v2 migriert"
- Bei Fehler: Fallback auf Backup

---

## Referenz: UMI File Header

Alle `.umi` Dateien (außer `.gyroflow`) enthalten einen `_umi_header` Block:

```json
{
  "_umi_header": {
    "type": "config",                     // Dateityp (config, burst_profile, camera_type, import_profile)
    "version": "2.1.0",                   // UMI-Version bei Erstellung
    "schema_version": 1,                  // Schema-Version (für Migration)
    "created": "2026-02-16T13:30:00Z",    // ISO 8601 Timestamp
    "modified": "2026-02-16T13:30:00Z",   // ISO 8601 Timestamp
    "description": "UMI Main Configuration" // Kurzbeschreibung
  },
  ...  // Rest der Datei
}
```

**Wichtig:**
- Header ist **optional** beim Lesen (Loader ignorieren unbekannte Properties)
- Header wird **empfohlen** für neue Dateien (Versionierung, Typ-Erkennung)
- `schema_version` triggert Migration (zukünftig)
