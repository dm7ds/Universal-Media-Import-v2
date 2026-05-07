# Burst-Profile – Automatische Sequenz-Erkennung

> **Was ist das?** Burst-Profile erkennen automatisch zusammengehörige Foto-Sequenzen
> (Sport-Serien, Astro-Stacks, Timelapses) und gruppieren sie in eigene Ordner.
> Keine manuelle Sortierung mehr.

---

## Übersicht

UMI analysiert EXIF-Daten jedes Fotos und entscheidet anhand von Profilen, ob es
zu einer Sequenz gehört. Profile sind JSON-Dateien in diesem Ordner – erweiterbar
ohne Code-Änderungen.

### Mitgelieferte Profile

| Profil | Erkennt | Beispiel |
|--------|---------|---------|
| **Sport** | Serienbilder (Continuous Drive) | 30 Fotos in 2 Sekunden, Fußball-Torschuss |
| **Astro** | Langzeitbelichtungen für Stacking | 50x 8s Belichtung, Milchstraße |
| **Timelapse** | Intervall-Aufnahmen | Alle 5s ein Foto, Sonnenuntergang über 2 Stunden |

### So funktioniert's

```
Foto wird importiert
  ↓
1. MATCHING: Welches Profil passt?
   → Sport-Profil prüft: Ist ContinuousDrive >= 1? (Serienbild-Modus)
   → Astro-Profil prüft: Ist ExposureTime >= 1.0? (Langzeitbelichtung)
   → Timelapse-Profil prüft: Ist ContinuousDrive == 0? (Einzelauslösung)
   → Erstes Match gewinnt (Prioritäts-Reihenfolge)

2. GRUPPIERUNG: Gehört das Foto zur gleichen Sequenz?
   → Zeitabstand zum vorherigen Foto berechnen
   → Abstand < max_gap_seconds? → Gleiche Sequenz
   → Abstand > max_gap_seconds? → Neue Sequenz

3. VALIDIERUNG: Ist die Sequenz groß genug?
   → Sequenz hat >= min_count Fotos? → Eigener Ordner (z.B. Sport_154111/)
   → Sequenz hat < min_count Fotos? → Wird als Einzelfotos behandelt
```

---

## Profil-Referenz

### Dateiformat

Jedes Profil ist eine JSON-Datei in `Presets/burst/`:

```json
{
  "name": "MeinProfil",
  "description": "Beschreibung für GUI und Doku",
  "priority": 50,
  "match_conditions": { ... },
  "grouping": { ... }
}
```

### Felder

#### Basis

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|-------------|
| `name` | string | ✅ | Eindeutiger Name. Wird als Ordnername verwendet |
| `description` | string | optional | Beschreibung (für GUI) |
| `priority` | int | ✅ | Niedrigere Zahl = höhere Priorität. Erstes Match gewinnt |

#### match_conditions (Wann matcht das Profil?)

Definiert EXIF-basierte Bedingungen. Unterstützt AND/OR-Logik und Verschachtelung:

```json
{
  "match_conditions": {
    "operator": "AND",
    "conditions": [
      { "field": "ContinuousDrive", "operator": ">=", "value": 1 }
    ],
    "groups": [
      {
        "operator": "OR",
        "conditions": [
          { "field": "ISO", "operator": ">=", "value": 3200 },
          { "field": "ExposureTime", "operator": ">=", "value": 1.0 }
        ]
      }
    ]
  }
}
```

Dieses Beispiel matcht wenn:
`ContinuousDrive >= 1 AND (ISO >= 3200 OR ExposureTime >= 1.0)`

**Operatoren für Conditions:**

| Operator | Bedeutung | Beispiel |
|----------|-----------|---------|
| `>` | Größer als | `ExposureTime > 1.0` |
| `>=` | Größer oder gleich | `ContinuousDrive >= 1` |
| `<` | Kleiner als | `ExposureTime < 0.01` |
| `<=` | Kleiner oder gleich | `ISO <= 800` |
| `==` | Gleich | `ContinuousDrive == 0` |
| `!=` | Ungleich | `ExposureMode != 6` |

**Geplant (v2):** String-Operatoren `contains`, `=` (String-Vergleich) für Felder wie `DriveMode = "High Speed Continuous"`.

**Verfügbare EXIF-Felder:**

| Feld | Quelle | Typ | Werte-Beispiele |
|------|--------|-----|-----------------|
| `ExposureTime` | EXIF SubIFD | Sekunden (double) | `0.0005` (1/2000s), `8.0` (8s), `30.0` (30s) |
| `ISO` | EXIF SubIFD | Ganzzahl | `100`, `800`, `3200`, `12800` |
| `FocalLength` | EXIF SubIFD | mm (double) | `24.0`, `70.0`, `200.0` |
| `Aperture` | EXIF SubIFD | f-Wert (double) | `1.4`, `2.8`, `8.0`, `22.0` |
| `ShutterSpeed` | EXIF SubIFD | APEX-Wert (double) | `11.0` (≈1/2000s), `-3.0` (≈8s) |
| `ExposureCompensation` | EXIF SubIFD | EV (double) | `-2.0`, `0.0`, `+1.5` |
| `ContinuousDrive` | Canon Makernote | Ganzzahl | `0`=Single, `1`=Continuous, `5`=High Speed |
| `ExposureMode` | Canon Makernote | Ganzzahl | `0`=Auto, `3`=Av, `4`=Manual, `6`=Bulb |

> **Hinweis:** Canon Makernote Felder (ContinuousDrive, ExposureMode) sind nur bei
> Canon-Kameras verfügbar. Für andere Hersteller nutze Standard-EXIF-Felder oder
> erstelle Profile mit dem EXIF-Scanner (GUI → Burst-Profile → EXIF-Felder analysieren).

#### grouping (Wie werden Fotos gruppiert?)

| Feld | Typ | Default | Beschreibung |
|------|-----|---------|-------------|
| `max_gap_seconds` | double | 3.0 | Maximale Zeitlücke zwischen zwei Fotos derselben Sequenz. Größere Lücke → neue Sequenz |
| `min_count` | int | 3 | Minimale Fotos pro Sequenz. Kleinere Gruppen → Einzelfotos (kein eigener Ordner) |
| `adaptive_threshold` | bool | false | Threshold dynamisch anpassen? |
| `adaptive_multiplier` | double | 2.0 | Multiplikator: `threshold = max(max_gap, avgGap × multiplier)` |

### Adaptive Threshold erklärt

Bei aktiviertem `adaptive_threshold` berechnet UMI den tatsächlichen durchschnittlichen
Abstand (avgGap) innerhalb einer Sequenz und passt den Threshold an:

```
threshold = max(max_gap_seconds, avgGap × adaptive_multiplier)
                ↑ Floor                  ↑ Dynamisch
```

**Regel: Adaptive darf nur LOCKERN, nie VERSCHÄRFEN.** `max_gap_seconds` ist immer das Minimum.

**Beispiel Sport (adaptive_multiplier=2.0):**
```
High-Speed Burst: avgGap = 0.1s → adaptive = 0.2s → threshold = max(2.0, 0.2) = 2.0s ✓
Langsame Serie:   avgGap = 1.5s → adaptive = 3.0s → threshold = max(2.0, 3.0) = 3.0s ✓
```
→ High-Speed bekommt den Floor (2s), langsame Serie wird etwas lockerer.

**Beispiel Timelapse (adaptive_multiplier=1.5):**
```
3s-Intervall:  avgGap = 3.0s → adaptive = 4.5s  → threshold = max(15, 4.5)  = 15s
5s-Intervall:  avgGap = 5.0s → adaptive = 7.5s  → threshold = max(15, 7.5)  = 15s
10s-Intervall: avgGap = 10.0s → adaptive = 15.0s → threshold = max(15, 15.0) = 15s
```
→ Timelapses nutzen hauptsächlich den Floor, adaptive fängt Ausreißer ab.

---

## Mitgelieferte Profile im Detail

### Sport.json (Priority 10)

```json
{
  "name": "Sport",
  "description": "Erkennt Serienbilder (Continuous Drive). Für Action, Sport, Wildlife – alles wo die Kamera im Serienbild-Modus rattert.",
  "priority": 10,
  "match_conditions": {
    "operator": "AND",
    "conditions": [
      { "field": "ContinuousDrive", "operator": ">=", "value": 1 }
    ]
  },
  "grouping": {
    "max_gap_seconds": 2,
    "min_count": 3,
    "adaptive_threshold": true,
    "adaptive_multiplier": 2.0
  }
}
```

| | |
|---|---|
| **Erkennt wenn** | Kamera im Serienbild-Modus (ContinuousDrive ≥ 1) |
| **Gap** | 2s (High-Speed Bursts haben 0.05-0.5s Gap) |
| **Min. Fotos** | 3 (auch kurze Bursts werden erkannt) |
| **Adaptive** | Ja – lockert bei langsamen Serien |
| **Typisches Ergebnis** | `Sport_154111/` → 30 Fotos |

### Astro.json (Priority 20)

```json
{
  "name": "Astro",
  "description": "Erkennt Langzeitbelichtungen für Astro-Stacking. Fotos mit >= 1s Belichtung werden gruppiert.",
  "priority": 20,
  "match_conditions": {
    "operator": "AND",
    "conditions": [
      { "field": "ExposureTime", "operator": ">=", "value": 1.0 }
    ]
  },
  "grouping": {
    "max_gap_seconds": 90,
    "min_count": 10,
    "adaptive_threshold": false,
    "adaptive_multiplier": 2.0
  }
}
```

| | |
|---|---|
| **Erkennt wenn** | Belichtungszeit ≥ 1 Sekunde |
| **Gap** | 90s (zwischen Langzeitbelichtungen kann viel passieren – Kamera adjustiert, Dark Frame, etc.) |
| **Min. Fotos** | 10 (Astro-Stacking braucht genug Frames) |
| **Adaptive** | Nein – Astro-Gaps sind zu unregelmäßig |
| **Typisches Ergebnis** | `Astro_214902/` → 50 Fotos |

### Timelapse.json (Priority 30)

```json
{
  "name": "Timelapse",
  "description": "Intervall-Aufnahmen (1-10s zwischen Bildern). Erkennt regelmäßige Einzelauslösungen unabhängig von Belichtungszeit oder Kameramodus. Adaptive Threshold passt sich dem tatsächlichen Intervall an.",
  "priority": 30,
  "match_conditions": {
    "operator": "AND",
    "conditions": [
      { "field": "ContinuousDrive", "operator": "==", "value": 0 }
    ]
  },
  "grouping": {
    "max_gap_seconds": 15,
    "min_count": 15,
    "adaptive_threshold": true,
    "adaptive_multiplier": 1.5
  }
}
```

| | |
|---|---|
| **Erkennt wenn** | Kamera im Einzelbild-Modus (ContinuousDrive == 0) |
| **Gap** | 15s (deckt Intervalle 1-10s ab mit Puffer) |
| **Min. Fotos** | 15 (weniger ist kein sinnvoller Timelapse) |
| **Adaptive** | Ja – passt sich dem tatsächlichen Intervall an |
| **Typisches Ergebnis** | `Timelapse_180500/` → 720 Fotos (2h bei 10s Intervall) |

**Warum ContinuousDrive == 0?**
Timelapse nutzt den Intervalometer der Kamera (oder einen externen). Jeder Auslösung
ist ein Einzelbild – die Kamera steht NICHT auf Serienbildmodus. Das unterscheidet
Timelapse sauber von Sport (ContinuousDrive ≥ 1).

**Warum nach Astro (Priority 30 > 20)?**
Ein Astro-Timelapse (z.B. 50x 8s Belichtung, Milchstraßen-Rotation) matcht BEIDE
Profile: ContinuousDrive == 0 (Timelapse) UND ExposureTime >= 1.0 (Astro). Weil Astro
höhere Priorität hat (20 < 30), gewinnt Astro. Das ist korrekt – Astro-Stacking braucht
den großen Gap (90s), nicht den Timelapse-Gap (15s).

---

## Prioritäts-Logik

Profile werden in Prioritäts-Reihenfolge geprüft. **Erstes Match gewinnt.**

```
Foto kommt rein
  → Prio 10: Sport-Profil → ContinuousDrive >= 1?
     → Ja → ✅ "Sport" (fertig, nicht weiter prüfen)
     → Nein ↓
  → Prio 20: Astro-Profil → ExposureTime >= 1.0?
     → Ja → ✅ "Astro"
     → Nein ↓
  → Prio 30: Timelapse-Profil → ContinuousDrive == 0?
     → Ja → ✅ "Timelapse"
     → Nein ↓
  → Kein Profil matcht → "Single" (Einzelfoto)
```

**Wichtig:** Die Prioritäts-Reihenfolge bestimmt welches Profil bei Überlappung gewinnt.

### Überlappungs-Matrix

| Foto-Typ | ContinuousDrive | ExposureTime | Matcht Sport? | Matcht Astro? | Matcht TL? | Ergebnis |
|----------|----------------|--------------|---------------|---------------|------------|----------|
| Sport-Burst | 5 | 1/2000s | ✅ | ✗ | ✗ | **Sport** |
| Astro-Stack | 0 | 8s | ✗ | ✅ | ✅ | **Astro** (Prio) |
| Timelapse | 0 | 1/500s | ✗ | ✗ | ✅ | **Timelapse** |
| Astro-TL | 0 | 15s | ✗ | ✅ | ✅ | **Astro** (Prio) |
| Einzelfoto | 0 | 1/250s | ✗ | ✗ | ✅ | ⚠ min_count! |

Das letzte Beispiel zeigt: Ein normales Einzelfoto matcht technisch das Timelapse-Profil
(ContinuousDrive == 0). Aber es wird nur dann als Timelapse-Sequenz gruppiert, wenn
mindestens 15 Fotos in regelmäßigen Abständen (< 15s) aufeinander folgen. Ein einzelnes
Landschaftsfoto oder ein paar zufällige Schnappschüsse erfüllen `min_count: 15` nicht
und bleiben Einzelfotos.

---

## Eigene Profile erstellen

### Beispiel: HighISO-Profil

Für Bilder mit hoher ISO (z.B. Konzert-Fotografie):

```json
{
  "name": "HighISO",
  "description": "Erkennt Serien mit hoher ISO (>= 3200). Für Konzert, Indoor, Low-Light.",
  "priority": 15,
  "match_conditions": {
    "operator": "AND",
    "conditions": [
      { "field": "ISO", "operator": ">=", "value": 3200 },
      { "field": "ContinuousDrive", "operator": ">=", "value": 1 }
    ]
  },
  "grouping": {
    "max_gap_seconds": 5,
    "min_count": 5,
    "adaptive_threshold": true,
    "adaptive_multiplier": 2.0
  }
}
```

### Beispiel: Bracketing-Profil

Für HDR-Belichtungsreihen (3-5 Bilder, schnell hintereinander):

```json
{
  "name": "Bracket",
  "description": "Erkennt Belichtungsreihen (Bracketing). Kurze Serien mit Belichtungsvariation.",
  "priority": 5,
  "match_conditions": {
    "operator": "AND",
    "conditions": [
      { "field": "ContinuousDrive", "operator": ">=", "value": 1 }
    ]
  },
  "grouping": {
    "max_gap_seconds": 1,
    "min_count": 3,
    "adaptive_threshold": false,
    "adaptive_multiplier": 1.0
  }
}
```

> **Tipp:** Nutze den **EXIF-Scanner** in der GUI (Settings → Burst-Profile → EXIF-Felder
> analysieren) um herauszufinden welche EXIF-Felder deine Kamera für bestimmte
> Aufnahmemodi setzt. Scanne einen Ordner mit bekannten Burst-Fotos und übernimm die
> relevanten Felder direkt in den Rule-Builder.

### Schritt-für-Schritt: Neues Profil anlegen

1. **JSON-Datei erstellen:** `Presets/burst/MeinProfil.json`
2. **Name setzen:** Wird als Ordnername verwendet (`MeinProfil_HHMMSS/`)
3. **Priority wählen:** Niedriger = wird zuerst geprüft. Beachte Überlappungen!
4. **Conditions definieren:** Welche EXIF-Werte müssen zutreffen?
5. **Grouping konfigurieren:** Wie eng liegen die Fotos zeitlich zusammen?
6. **In Config aktivieren:** `"active_profiles": ["Sport", "Astro", "MeinProfil"]`
7. **Testen:** `umi import --source R10 -v` (verbose zeigt Matching-Details)

---

## Debugging

Mit `umi import -v` (verbose) zeigt UMI für jedes Foto:
- Welches Profil geprüft wurde
- Welche Conditions evaluiert wurden
- Gap zum vorherigen Foto
- Ob Split oder Zusammen

```bash
$ umi import --source R10 -v

[DBG] Matching IMG_1208.CR3: Sport → ContinuousDrive=5 >= 1 → MATCH
[DBG] Gap IMG_1208→IMG_1209: 0.12s <= 2.0s → ZUSAMMEN
[DBG] Gap IMG_1209→IMG_1210: 0.11s <= 2.0s → ZUSAMMEN
[DBG] Gap IMG_1210→IMG_1211: 45.3s > 2.0s → SPLIT (neue Sequenz)
[INF] Sequenz erkannt: Sport_154111/ (30 Fotos)
```

---

## FAQ

**Q: Meine Kamera ist keine Canon. Kann ich Burst-Profile nutzen?**
A: Ja! Nutze Standard-EXIF-Felder wie ExposureTime, ISO, Aperture. Die sind bei
allen Herstellern verfügbar. ContinuousDrive ist Canon-spezifisch, aber der
EXIF-Scanner hilft dir die richtigen Felder deiner Kamera zu finden.

**Q: Was passiert wenn kein Profil matcht?**
A: Das Foto wird als "Single" klassifiziert und normal importiert (kein Sequenz-Ordner).
Der Fallback nutzt `fallback_max_gap_seconds` und `fallback_min_count` aus der Config
für grundlegende Sequenz-Erkennung.

**Q: Kann ich Profile pro Kamera aktivieren?**
A: Ja! In der `config.json` unter `cameras.R10.burst_detection_config.active_profiles`
kannst du pro Kamera unterschiedliche Profile aktivieren. Die OA5 braucht z.B.
kein Sport-Profil (hat keinen Serienbild-Modus).

**Q: Was wenn zwei Profile gleiche Priority haben?**
A: Nicht empfohlen. Falls es passiert, gewinnt das zuerst in der Liste stehende.
Vergib eindeutige Prioritäten mit Abstand (10, 20, 30, ...).

**Q: Wie teste ich ein neues Profil?**
A: `umi import --source R10 --dry-run -v` – Dry-Run importiert nichts, Verbose zeigt
alle Matching-Entscheidungen. Perfekt zum Debuggen.
