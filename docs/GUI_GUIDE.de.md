# UMI — GUI User Guide

---

## 1. Schnellstart (Easy-Modus)

Für alle, die einfach nur ihre Dateien von der SD-Karte auf den PC kopieren wollen. Kein technisches Vorwissen nötig.

### Erster Start

Beim ersten Start öffnet sich der Setup Wizard automatisch.

1. **Welcome** — Wähle **Easy** als Modus. Damit siehst du nur das Nötigste.
2. **Workbench** — Wähle den Ordner, in den deine Medien importiert werden sollen (z.B. `D:\Medien\Import`).
3. **Detect Camera Source** — Stecke deine SD-Karte ein. UMI erkennt sie automatisch und zeigt sie an.
4. **Name your camera** — Gib deiner Kamera einen Namen (z.B. "Meine Action Cam") und wähle den passenden Kameratyp.
5. **Register SD Cards** — Die erkannte Karte wird deiner Kamera zugeordnet. Optional — du kannst das auch später machen.
6. **Add More Cameras?** — Hast du weitere Kameras? Wenn ja, den Vorgang wiederholen. Wenn nein, weiter.
7. **Tools** — UMI findet ExifTool normalerweise selbst. Einfach weiter klicken.
8. **Summary** — Zusammenfassung prüfen, **Finish** klicken.

### Dein erster Import

Nach der Einrichtung landest du im **Import Tab**. Dort siehst du deine Kamera als Kachel.

**Variante A — Automatisch (empfohlen):**

1. Klicke auf **Smart Watch** oben links.
2. Stecke eine SD-Karte ein.
3. UMI erkennt die Karte, startet den Import und zeigt den Fortschritt auf der Kamera-Kachel.
4. Karte raus, nächste rein — UMI läuft durch.
5. Fertig? **Stop Watch** klicken.

**Variante B — Einmalig:**

1. Stecke die SD-Karte ein.
2. Klicke auf **Quick Import**.
3. UMI kopiert alle erkannten Medien und zeigt den Fortschritt.

Das war's. Deine Fotos und Videos liegen jetzt sortiert nach Datum und Kamera im Workbench-Ordner.

### Ordnerstruktur

Nach dem Import sieht dein Workbench-Ordner so aus:

```
D:\Medien\Import\
  2026-03-12\
    MeineActionCam\
      Video\
        DJI_20260312_143022.mp4
        DJI_20260312_143155.mp4
      Photo\
        DJI_20260312_143022.jpg
```

---

## 2. Erweiterte Anleitung — Drohne, Action Cam & GPS

Für Videografen mit Action Cams oder Drohnen, die Gyroflow-Stabilisierung und GPS-Injection nutzen wollen.

### App-Modi

Oben im Header gibt es ein Dropdown mit drei Modi:

| Modus | Was du siehst |
|-------|--------------|
| **Easy** | Nur Import — keine Nachbearbeitung |
| **Standard** | Import + Process Tab (Stabilisierung, GPS) |
| **Advanced** | Alles — inkl. Profile, Burst Detection, Device Management |

Für diesen Guide: Wähle **Standard** oder **Advanced**.

### Feature-Bubbles

Jede Kamera hat Features die du per **Feature-Bubble** auf der Kamera-Kachel ein- und ausschalten kannst. Farbig = aktiv, dunkel = deaktiviert.

Für eine typische Drohne/Action Cam:

| Feature | Was es tut | Empfehlung |
|---------|-----------|------------|
| **GPS** | UMI baut GPX-Tracks und injiziert GPS-Koordinaten ins Video | An, wenn du einen externen GPS-Tracker nutzt |
| **Gyroflow** | UMI stabilisiert Videos mit EIS-Off via Gyroflow | An bei Action Cams ohne gute interne Stabi |
| **EIS** | Erkennt ob die Kamera-interne Stabilisierung an/aus war | An bei DJI Action Cams |
| **Metadata** | UMI sichert Original-EXIF bevor die Verarbeitung Metadaten verändert | An (empfohlen für alle) |
| **PostProc** | Aktiviert Post-Processing Pipeline (DaVinci Resolve Export-Flow) | An wenn du in DaVinci gradest |
| **Rename** | Benennt Videos mit Timestamp-Prefix um | Optional |
| **GoPro** | Spezielle GoPro-Umbenennung (Chapter-Format) | Nur für GoPro |

Zum Bearbeiten: Klicke auf die Kamera-Kachel, ändere Features, klicke **Save**.

### Gyroflow-Stabilisierung

Kameras mit aktiviertem **Gyroflow**-Feature brauchen ein Lens-Preset:

1. Kamera-Kachel aufklappen (klicken)
2. **Gyroflow Preset** Feld → Browse → `.gyroflow` Preset-Datei auswählen
3. Save

UMI verwendet dieses Preset automatisch bei der Stabilisierung.

### GPS-Injection

UMI injiziert GPS-Daten aus GPX-Dateien in deine Videos. Dafür brauchst du einen GPS-Tracker (Handy-App, Garmin, etc.).

1. **Settings** → **Tools** → **GPS Track Folder** → Ordner mit deinen GPX-Dateien auswählen
2. Feature **GPS** auf der Kamera-Kachel aktivieren

UMI matched Videos automatisch per Timestamp mit dem passenden GPX-Track.

### Process Tab

Nach dem Import wechselst du zum **Process Tab**. Er ist in drei Sub-Tabs unterteilt:

#### Sub-Tab: Video

Enthält die Aktionen für die Video-Pipeline:

##### Videos stabilisieren

Gyroflow-Stabilisierung für Videos ohne interne Stabilisierung (EIS-Off).

- Toggle **Detect EIS** aktivieren → UMI findet automatisch Videos ohne EIS und verschiebt sie in den Gyroflow-Ordner
- Klicke **Run** → Videos werden über die GPU-Queue stabilisiert
- Fortschritt pro Video (Frame-Count + ETA)
- Ergebnis: Stabilisierte Videos in `Video/Stabilized/`

##### GPS injizieren

Baut optimierte GPX-Tracks und injiziert GPS-Koordinaten.

- Toggle **Inject into video** → Ein: GPS direkt ins Video schreiben. Aus: Nur GPX-Dateien vorbereiten.
- Nur für Kameras bei denen du das **GPS**-Feature in UMI aktiviert hast
- Ergebnis aufklappbar: Pro Video ob GPS injiziert, GPX gebaut, oder übersprungen

##### Metadaten wiederherstellen

Stellt EXIF-Metadaten aus den `.umi/metadata/`-Backups wieder her.

- Nützlich wenn Gyroflow oder andere Tools Metadaten beschädigt haben
- Toggle **Force overwrite** → Metadaten auch überschreiben wenn sie OK aussehen
- Ergebnis: "Metadata restored" / "Not needed" / "No backup available"

##### Exports finalisieren

Wenn du Videos in DaVinci Resolve gegradet hast und die Exports in `Video/postprocess/exported/` liegen:

- Injiziert GPS-Daten in die Exports
- Stellt Metadaten wieder her
- Verschiebt fertige Videos nach `Video/`
- Räumt den postprocess-Ordner auf

#### Sub-Tab: Photo

Enthält Aktionen für die Foto-Überprüfung und Burst-Verwaltung:

##### Sequence Reviewer

Öffnet den Sequence Reviewer zum Bewerten und Taggen von Burst-Sequenzen.

- Klicke **Run** um den Sequence Reviewer zu öffnen
- UMI lädt automatisch alle Fotos aus deinem Workbench-Ordner und wendet die Burst-Profile an

Vollständige Dokumentation in [Abschnitt 5](#5-sequence-reviewer).

#### Sub-Tab: Tools

Enthält Hilfs-Aktionen:

##### Nach Datum sortieren

Sortiert Dateien nach EXIF-Datum in Ordner. (Aktuell noch nicht im GUI verfügbar — kommt bald.)

##### Workbench-Statistiken

Übersicht über alle Videos im Workbench:

| Spalte | Zeigt |
|--------|-------|
| Date | Aufnahmedatum |
| Source | Kamera-ID |
| File | Dateiname |
| Status | Pipeline-Status (Imported → Stabilized → GPS injected → Graded → Ready) |
| EIS | ON / OFF / N/A |
| GPS | Yes / Built / No |
| Backup | OK / Missing / N/A |
| Integrity | OK / Size=0 |

Gelbe Warnung bei: Status-Widerspruch (History vs. Ordner), fehlende Backups, Integritätsprobleme.

##### Thumbnails generieren

Generiert den Thumbnail-Cache für alle Fotos im Workbench vorab. Beschleunigt den ersten Ladevorgang im Sequence Reviewer.

- Klicke **Run** → UMI scannt alle Foto-Ordner und generiert fehlende Thumbnails
- Optionaler Datumsfilter in der Process-Tab-Kopfleiste um nur bestimmte Ordner zu verarbeiten

### Pipeline-Übersicht

So sieht der Lebenszyklus eines Videos aus:

```
SD-Karte → Import (EIS-Erkennung, Metadata Backup)
  → EIS Off?  → Gyroflow/ → Stabilize → Stabilized/
  → EIS On?   → Video/ (direkt nutzbar)
  → GPS?      → GPX bauen → GPS injizieren
  → DaVinci?  → postprocess/ → Grading → exported/ → Finalize → Video/
```

---

## 3. Burst Detection — Für Fotografen

Burst Detection erkennt Serienaufnahmen in deinen Fotos und gruppiert sie in Unterordner. Ideal für:

- Belichtungsreihen (HDR Brackets)
- Action-Serien (Sport, Wildlife)
- Astro-Stacking-Serien
- Timelapses

### So funktioniert's

UMI liest die EXIF-Daten jeder Foto-Datei und gruppiert nach zwei Kriterien:

1. **Match Conditions** — Welche Fotos gehören zusammen? Definiert über EXIF-Feld-Regeln.
2. **Grouping** — Wann wird eine Serie unterbrochen? Definiert über den maximalen zeitlichen Abstand.

**Beispiel:** "Alle Fotos mit ISO 100 und Blende f/2.8 die weniger als 3 Sekunden auseinander liegen gehören zu einer Serie."

### Profile

Profile definieren die Erkennungsregeln. Du findest sie unter **Settings** → **Burst** (nur im Advanced Modus).

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

1. **Settings** → **Burst** → **Add Profile**
2. Namen und Beschreibung eingeben
3. **Rules Editor** aufklappen → Regeln hinzufügen
4. **Grouping** konfigurieren (Max Gap, Adaptive)
5. **Save**

**Automatisch (Auto-Preset Generator):**

1. **Settings** → **Burst** → Ordner mit Beispiel-Fotos laden
2. Fotos einer bekannten Serie markieren (Klick + Shift/Ctrl)
3. **Generate Preset** klicken
4. UMI analysiert die EXIF-Daten der markierten Fotos und generiert passende Regeln
5. Vorschau prüfen → **Accept & Save**

**Profil testen:**

Jedes Profil hat einen eingebauten Visualizer:

1. Profil aufklappen → **Ordner laden**
2. UMI scannt die Fotos und zeigt das Ergebnis:
   - Farbig markierte Gruppen = erkannte Serien
   - Grau = nicht zugeordnete Fotos (Orphans)
3. Regeln anpassen bis das Ergebnis passt

Im **Burst Studio** (großer Visualizer unter den Profil-Karten):

- Thumbnail-Raster mit farblicher Sequenz-Zuordnung
- Größen-Slider (64–256 px)
- Profil-Dropdown zum schnellen Wechsel
- Selection Mode für den Auto-Preset Generator

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

---

## 4. Geräteverwaltung

Unter **Settings** → **Devices** verwaltest du alle Speicherquellen die UMI kennt.

### SD-Karten

SD-Karten werden über ihre Volume Serial Number (VSN) identifiziert — eine eindeutige Kennung die auch nach dem Umbenennen oder Formatieren (nur Quick-Format) erhalten bleibt.

**Karte registrieren:**

1. **Settings** → **Devices** → **Add Device**
2. Tab **SD Card** → Karte einstecken
3. UMI erkennt die Karte automatisch und zeigt sie an
4. Kamera zuordnen → **Register**

**Karten-Zuordnung:**

| Zuordnung | Verhalten beim Import |
|-----------|----------------------|
| Kamera zugewiesen (Fixed) | Import startet automatisch ohne Rückfrage |
| Keine Kamera (Floating) | UMI fragt bei jedem Import welche Kamera — mit Vorschlag aus Historie |

Zum Wechseln: Einfach die Kamera im Dropdown ändern und speichern. Zum Floating-Setzen: Kamera-Feld leer lassen.

**Karten-Historie:**

Jede SD-Karte hat ein History-Icon (Uhr-Symbol). Klick darauf zeigt:

- Welche Kameras diese Karte benutzt haben
- Wie oft (Import-Count + Prozent)
- Wann zuletzt

Nützlich wenn du Karten zwischen Kameras tauschst und wissen willst, welche Karte wo war.

**Karten-Wiedererkennung:**

UMI erkennt Karten auch nach dem Neuformatieren, wenn das Disk-Serial und die Kartengröße übereinstimmen (5% Toleranz). Die alte Zuordnung wird automatisch übernommen.

### MTP-Geräte

USB-Kameras und Android-Geräte die per MTP (Media Transfer Protocol) angeschlossen werden.

1. **Add Device** → Tab **MTP Device**
2. Gerät per USB anschließen
3. UMI zeigt erkannte MTP-Geräte
4. Kamera zuordnen → speichern

MTP-Geräte sind immer fest einer Kamera zugeordnet (kein Floating).

### Feste Pfade

Lokale Ordner oder Netzlaufwerke die als Importquelle dienen — z.B. Dashcam-Ordner auf einem NAS.

1. **Add Device** → Tab **Fixed Path**
2. Ordner-Pfad eingeben oder browsen
3. Kamera zuordnen → speichern

UMI überwacht den Ordner im Watch Mode und importiert neue Dateien automatisch.

**Online-Status:**

Im Devices Tab zeigt ein grüner Punkt an, welche Geräte gerade verbunden sind. Graue Punkte = offline/nicht eingesteckt. Der Status wird live aktualisiert.

**Reihenfolge:**

Per Drag & Drop kannst du die Reihenfolge der Geräte innerhalb jeder Gruppe ändern. Die Reihenfolge bestimmt die Anzeige-Position — hat keinen Einfluss auf die Import-Logik.

---

## 5. Sequence Reviewer

Der Sequence Reviewer zeigt dir deine Burst-Sequenzen visuell — eine Sequenz, ein Foto nach dem anderen. Du kannst Fotos bewerten, taggen und filtern.

### Reviewer öffnen

Öffne den Sequence Reviewer über den **Process Tab** → **Photo** (Sub-Tab) → **Sequence Reviewer** (Action Card) → **Run**. UMI lädt automatisch alle Fotos aus deinem Workbench-Ordner und wendet die Burst-Profile an.

### Navigation

| Taste | Aktion |
|-------|--------|
| **← / →** | Vorheriges / nächstes Foto in der aktuellen Sequenz |
| **↑ / ↓** | Vorherige / nächste Sequenz (beachtet Filter) |
| **Mausrad** | Durch Fotos scrollen |

Der **Filmstrip** unten zeigt alle Fotos der aktuellen Sequenz. Klicke auf ein Thumbnail um direkt dorthin zu springen. Du kannst den Filmstrip auch per Drag scrollen.

### Bewerten und Taggen

| Taste | Aktion |
|-------|--------|
| **Leertaste** | Favorit umschalten |
| **X** | Papierkorb umschalten |
| **1–5** | Sternebewertung setzen. Dieselbe Taste nochmal drücken um die Bewertung zu löschen. |

Tags und Bewertungen werden sofort gespeichert und bleiben über Sessions hinweg erhalten.

### Filter

In der Kopfleiste gibt es Filter-Buttons:

| Filter | Zeigt |
|--------|-------|
| **Alle** | Alle Fotos der Sequenz |
| **Favoriten** | Nur als Favorit getaggte Fotos |
| **Papierkorb** | Nur als Papierkorb getaggte Fotos |

Wenn ein Filter aktiv ist und keine Fotos passen, wird eine Meldung angezeigt.

### Sequenz-Übersicht

Drücke **G** um die Sequenz-Übersicht umzuschalten — ein Raster aller Sequenzen mit dem ersten Foto als Thumbnail.

Jede Karte zeigt:
- Sequenzname und Fotoanzahl
- Aufnahmedatum (vom ersten Foto)
- Status-Badges (Sterne, Favoriten, Papierkorb)
- Profilfarbe als Streifen am oberen Rand

**Sequenz-Filter:**

| Filter | Zeigt |
|--------|-------|
| **Alle** | Alle Sequenzen |
| **Bewertet** | Sequenzen mit mindestens einem bewerteten/getaggten Foto |
| **Unbewertet** | Sequenzen ohne Bewertungen oder Tags |

Klicke auf eine Karte um in die Sequenz zu springen. Navigation mit ↑/↓ beachtet den aktiven Filter.

### Profile und Farben

Das Profil-Dropdown in der Kopfleiste wechselt zwischen Burst-Profilen. Spezialeinträge:

| Eintrag | Verhalten |
|---------|-----------|
| **Alle Sequenzen** | Vereint Sequenzen aller Profile. Jede Karte zeigt welches Profil gegriffen hat. |
| **Nicht zugeordnet** | Zeigt Fotos die von keinem Profil erfasst werden. |

Jedes Profil hat eine Farbe (konfigurierbar unter **Einstellungen** → **Burst** → Profil bearbeiten). Die Farbe erscheint auf den Sequenzkarten, beim Profilnamen in der Kopfleiste und in der Übersicht.

### Tastenkürzel

| Taste | Aktion |
|-------|--------|
| **← →** | Fotos navigieren |
| **↑ ↓** | Sequenzen navigieren |
| **Leertaste** | Favorit umschalten |
| **X** | Papierkorb umschalten |
| **1–5** | Bewertung setzen (dieselbe Taste nochmal = löschen) |
| **G** | Übersicht umschalten |
| **F1** | Hilfe öffnen |
| **Escape** | Reviewer schließen |

---

## 6. Einstellungen

Öffne die Einstellungen über **Settings** in der Hauptnavigation. Der Settings Tab hat fünf Sub-Tabs:

### Cameras

Alle Kameras die UMI kennt verwalten. Jede Kamera hat einen Namen, einen Typ und eine Feature-Konfiguration.

- **Add Camera** — neue Kamera manuell registrieren
- **Restart Setup Wizard** — Setup-Wizard erneut starten um Kameras hinzuzufügen, Tools zu konfigurieren oder Einstellungen zu ändern

### Profiles (nur Advanced-Modus)

Profile für Kamera-Feature-Sets importieren und exportieren. Nur im Advanced-Modus sichtbar.

### Devices

SD-Karten, MTP-Geräte und feste Ordner-Pfade verwalten. Details in [Abschnitt 4](#4-geräteverwaltung).

### Tools

Externe Tools und Pfade konfigurieren:

- **ExifTool-Pfad** — Pfad zu `exiftool.exe`
- **Gyroflow-Pfad** — Pfad zur Gyroflow CLI
- **GPS Track Folder** — Ordner in dem UMI nach GPX-Dateien sucht
- **Sprache** — zwischen Englisch und Deutsch wechseln (Neustart erforderlich)

### Burst (nur Advanced-Modus)

Burst-Detection-Profile verwalten. Nur im Advanced-Modus sichtbar. Details in [Abschnitt 3](#3-burst-detection--für-fotografen).

---

## 7. Hilfe-System

UMI hat ein eingebautes Hilfe-System, das jederzeit abrufbar ist:

- **F1** — öffnet die kontextbezogene Hilfe für den aktuell aktiven Tab oder das aktive Fenster
- **Hilfe-Button** — der `?`-Button in der Toolbar öffnet das Hilfe-Fenster für den aktuellen Kontext
- Das **Hilfe-Fenster** zeigt Markdown-Dokumentation mit einer Sidebar zur Navigation zwischen den Kapiteln und einem Suchfeld um Themen schnell zu finden
- Jeder Tab hat außerdem einen **Mehr erfahren**-Link der direkt zum entsprechenden Abschnitt in der Hilfe springt
