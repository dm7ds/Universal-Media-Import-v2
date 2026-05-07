# Camera Type Definitions

Jede `.json` Datei hier definiert einen Kamera-Typ mit Default-Features.
Kameras referenzieren ihren Typ in `config.json` via `camera_type`.

## Eigenen Typ erstellen

1. Neue `.json` Datei erstellen (z.B. `360Camera.json`)
2. Name, Description, Color und Default-Features setzen
3. In `config.json` bei der Kamera `"camera_type": "360Camera"` setzen

## Feature-Vererbung

```
Type-Defaults → config.json Overrides → Profil-Overrides → CLI-Flags
```

Jede Stufe überschreibt nur was explizit gesetzt ist.

**Beispiel:** Wenn eine Action-Kamera in config.json `"eis_detection": false` setzt,
wird dieser Wert verwendet statt des Typ-Defaults (`true`).

## Typ-Struktur

```json
{
  "name": "Action",
  "description": "Action-Kameras (GoPro, DJI Osmo, Insta360)",
  "color": "#f97316",
  "default_features": {
    "gps_injection": true,
    "gyroflow": true,
    "eis_detection": true,
    "burst_detection": false,
    "metadata_backup": true,
    "lens_correction": false
  }
}
```

### Felder erklärt

- **name**: Eindeutiger Typ-Name (wird in `camera_type` verwendet)
- **description**: Beschreibung für Dokumentation/GUI
- **color**: Hex-Farbcode für GUI-Darstellung. CLI ignoriert diesen Wert.
- **default_features**: Standard-Features für diesen Typ

## Verfügbare Typen

| Typ | Beschreibung | Typische Kameras |
|-----|--------------|------------------|
| **Action** | Action-Kameras mit GPS/Gyro | GoPro, DJI Osmo, Insta360 |
| **Drone** | Drohnen mit GPS | DJI Mavic, Mini, Air |
| **Mirrorless** | Spiegellose Systemkameras | Canon R, Sony A, Nikon Z |

## Color

Hex-Farbcode für die GUI-Darstellung:
- Action: `#f97316` (Orange)
- Drone: `#06b6d4` (Cyan)
- Mirrorless: `#a855f7` (Purple)

CLI ignoriert diese Farben und nutzt fallback auf Weiß.

## Feature-Liste

Alle verfügbaren Features in `default_features`:

| Feature | Beschreibung |
|---------|--------------|
| `gps_injection` | GPS-Daten aus GPX in Videos injizieren |
| `metadata_backup` | Metadata-Backup/Restore |
