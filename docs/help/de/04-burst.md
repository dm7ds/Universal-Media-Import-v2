## Serien-Erkennung — Für Fotografen

UMI erkennt automatisch zusammengehörige Fotoserien und gruppiert sie in Unterordner. Das funktioniert nicht nur für klassische Serienaufnahmen (Burst), sondern für alle Arten von Bildserien. Ideal für:

- Belichtungsreihen ([HDR](glossary:HDR) Brackets)
- Action-Serien (Sport, Wildlife)
- Astro-Stacking-Serien
- Timelapses

### Serien nachträglich erkennen

Du hast bereits Fotos importiert und willst die Serien-Erkennung nachholen? Kein Problem — das geht jederzeit:

1. Wechsle zu **Verarbeitung** → **Foto Tools** → **[Sequence Reviewer](chapter:reviewer)**
2. UMI analysiert automatisch alle Fotos im Arbeitsordner und wendet deine Burst-Profile an
3. Du siehst die erkannten Serien visuell und kannst sie bewerten und taggen

Zum Sortieren in Unterordner nutze **Verarbeitung** → **Nach dem Import** → **Organisieren** mit aktiviertem **Fotoserien erkennen**. UMI gruppiert die erkannten Serien dann automatisch in eigene Ordner.

> **Hinweis:** Die Option **Fotoserien erkennen** in "Organisieren" ist aktuell noch nicht im GUI verfügbar. Sie ist für eine zukünftige Version geplant.

Du brauchst dafür mindestens ein Erkennungs-Profil (siehe unten). Falls du noch keins hast, kann UMI dir eins automatisch generieren.

### So funktioniert's

UMI liest die [EXIF](glossary:EXIF)-Daten jeder Foto-Datei und gruppiert nach zwei Kriterien:

1. **Match Conditions** — Welche Fotos gehören zusammen? Definiert über EXIF-Feld-Regeln.
2. **Grouping** — Wann wird eine Serie unterbrochen? Definiert über den maximalen zeitlichen Abstand.

**Beispiel:** "Alle Fotos mit ISO 100 und Blende f/2.8 die weniger als 3 Sekunden auseinander liegen gehören zu einer Serie."

### Profile

Profile definieren die Erkennungsregeln. Du findest sie unter **Einstellungen** → **Burst** (nur im Advanced-Modus). Siehe auch [Werkzeuge & Optionen](chapter:tools) für Tool-Konfiguration.

Jedes Profil besteht aus:

#### Match Conditions

Eine oder mehrere Bedingungen die ein Foto erfüllen muss:

| Feld | Operator | Wert | Bedeutung |
|------|----------|------|-----------|
| ExposureTime | = | 1/125 | Nur Fotos mit genau dieser Belichtungszeit |
| FNumber | >= | 2.8 | Mindestens Blende 2.8 |
| ISO | < | 400 | ISO unter 400 |
| Model | Contains | "R5" | Kameramodell enthält "R5" |

Verfügbare Operatoren: `=`, `!=`, `>`, `<`, `>=`, `<=`, `Contains`, `StartsWith`, `EndsWith`, `Matches` (Regex)

Mehrere Bedingungen werden mit **AND** (alle müssen zutreffen) oder **OR** (mindestens eine) verknüpft.

#### Gruppierungseinstellungen

| Parameter | Bedeutung |
|-----------|-----------|
| **Max Gap Seconds** | Maximaler zeitlicher Abstand zwischen zwei Fotos einer Serie. Wird der Abstand überschritten, beginnt eine neue Serie. |
| **Adaptive Threshold** | Aktiviert adaptive Schwellwert-Berechnung. UMI analysiert die Zeitabstände und passt den Threshold automatisch an. Der Wert von Max Gap Seconds dient als Mindest-Schwelle. |

**Typische Werte:**
- Belichtungsreihen (HDR): Max Gap 2–5s
- Sport/Action: Max Gap 1–2s
- Astro-Stacking: Max Gap 30–60s
- Timelapse: Max Gap 10–30s

#### Priorität

Profile werden in Reihenfolge abgearbeitet (Drag & Drop zum Umsortieren). Das erste passende Profil gewinnt. Setz spezifische Profile weiter oben, allgemeine weiter unten.

### Profile erstellen

**Manuell:**

1. **Einstellungen** → **Burst** → **Profil hinzufügen**
2. Namen und Beschreibung eingeben
3. Regeleditor aufklappen → Regeln hinzufügen
4. Gruppierung konfigurieren (Max Gap, Adaptive)
5. **Speichern**

**Automatisch (Auto-Preset Generator):**

1. **Einstellungen** → **Burst** → Ordner mit Beispiel-Fotos laden
2. Fotos einer bekannten Serie markieren (Klick + Shift/Ctrl)
3. **Voreinstellung generieren** klicken
4. UMI analysiert die EXIF-Daten der markierten Fotos und generiert passende Regeln
5. Vorschau prüfen → **Übernehmen & Speichern**

**Profil testen:**

Jedes Profil hat einen eingebauten Visualizer:

1. Profil aufklappen → **Ordner laden**
2. UMI scannt die Fotos und zeigt das Ergebnis:
   - Farbig markierte Gruppen = erkannte Serien
   - Grau = nicht zugeordnete Fotos (Orphans)
3. Regeln anpassen bis das Ergebnis passt

### Burst Studio

Das Burst Studio ist der große Visualizer unter den Profil-Karten in **Einstellungen** → **Burst**. Damit testest du deine Profile visuell und kannst neue aus Beispiel-Fotos generieren.

#### Fotos laden

1. Klicke **Durchsuchen** um einen Ordner mit Fotos auszuwählen
2. Klicke **Laden** — UMI liest die [EXIF](glossary:EXIF)-Daten und extrahiert Thumbnails
3. Ein Fortschrittsbalken zeigt den Scan-Status

#### Thumbnail-Raster

Nach dem Laden erscheinen deine Fotos als Thumbnails in einem Raster:

- **Farbige Rahmen** — jede erkannte Serie bekommt eine eigene Farbe passend zum Profil
- **Grau** — nicht zugeordnete Fotos (kein Profil hat gegriffen)
- **Größen-Slider** — ziehen um die Thumbnail-Größe anzupassen (64–256 px)

#### Profile auswerten

Wähle ein Profil aus dem **Profil-Dropdown** oben. UMI wertet die geladenen Fotos sofort gegen das Profil aus und aktualisiert die Farbmarkierung. Die Zusammenfassung zeigt wie viele Serien erkannt und wie viele Fotos zugeordnet wurden.

Wechsle Profile um Ergebnisse zu vergleichen — das Raster aktualisiert sich sofort.

#### Auto-Preset Generator

Keine Lust Regeln von Hand zu schreiben? Lass UMI sie generieren:

1. Klicke den **Auswahl-Modus** Button in der Toolbar
2. Klicke auf Fotos die zur selben Serie gehören — ein Häkchen erscheint auf den gewählten Fotos
3. Klicke **Voreinstellung generieren** — UMI analysiert die [EXIF](glossary:EXIF)-Daten deiner Auswahl
4. UMI findet stabile Felder (Werte die identisch oder sehr ähnlich sind) und erstellt passende Regeln
5. Prüfe die vorgeschlagenen Regeln und Gruppierungseinstellungen
6. Klicke **Übernehmen & Speichern** um ein neues Profil anzulegen, oder **Verwerfen** um neu zu starten

Das generierte Profil erscheint in deiner Profil-Liste und kann manuell nachbearbeitet werden.

#### Tipps

- Lade einen Ordner mit gemischtem Inhalt (Serien + Einzelfotos) für realistische Tests
- Nutze den Auto-Preset Generator zuerst, verfeinere dann die Regeln
- Der [Sequence Reviewer](chapter:reviewer) zeigt Ergebnisse über den gesamten [Workbench](glossary:Workbench)-Ordner — das Burst Studio ist zum Testen mit einem bestimmten Ordner

### Beispiel-Profile

**HDR Brackets (Canon R5):**
```
Conditions: Model Contains "R5" AND DriveMode = "Continuous"
Grouping: Max Gap 3s, Adaptive ON
```

**Astro Stacking (allgemein):**
```
Conditions: ExposureTime >= 10 AND ISO >= 1600
Grouping: Max Gap 45s, Adaptive ON
```

**Sport Action:**
```
Conditions: ShutterSpeed <= 1/500
Grouping: Max Gap 1.5s, Adaptive OFF
```
