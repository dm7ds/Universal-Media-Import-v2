## Erweiterte Anleitung — Drohne, Action Cam & GPS

Für Videografen mit Action Cams oder Drohnen, die [Gyroflow](glossary:Gyroflow)-Stabilisierung und [GPS](glossary:GPS)-Injection nutzen wollen.

### App-Modi

Oben im Header gibt es ein Dropdown mit drei Modi:

| Modus | Was du siehst |
|-------|--------------|
| **Easy** | Nur Import — keine Nachbearbeitung |
| **Standard** | Import + Process-Tab (Stabilisierung, GPS) |
| **Advanced** | Alles — inkl. Profile, [Serien-Erkennung](chapter:burst), [Geräteverwaltung](chapter:devices) |

Für diesen Guide: Wähle **Standard** oder **Advanced**.

### Feature-Bubbles

Jede Kamera hat Features die du per **Feature-Bubble** auf der Kamera-Kachel ein- und ausschalten kannst. Farbig = aktiv, dunkel = deaktiviert.

Für eine typische Drohne/Action Cam:

| Feature | Was UMI damit tut | Empfehlung |
|---------|-----------|------------|
| **GPS** | Baut GPX-Tracks und injiziert GPS-Koordinaten ins Video | An, wenn du einen externen GPS-Tracker nutzt |
| **Gyroflow** | Stabilisiert Videos mit [EIS](glossary:EIS)-Off via Gyroflow | An bei Action Cams ohne gute interne Stabi |
| **Burst** | Aktiviert Burst-/Fotoserien-Erkennung für diese Kamera | An bei Kameras die Bildserien schießen |
| **EIS** | Erkennt ob die Kamera-interne Stabilisierung an/aus war | An bei DJI Action Cams |
| **Metadata** | Sichert Original-[EXIF](glossary:EXIF) bevor die Verarbeitung Metadaten verändert | An (empfohlen für alle) |
| **Lens** | Wendet Objektivkorrektur an (Stub — noch nicht aktiv) | Experimentell |
| **PostProc** | Aktiviert Post-Processing Pipeline (DaVinci Resolve Export-Flow) | An wenn du in DaVinci gradest |
| **Rename** | Benennt Videos mit Timestamp-Prefix um | Optional |
| **GoPro** | Spezielle GoPro-Umbenennung (Chapter-Format) | Nur für GoPro |

Zum Bearbeiten: Klicke auf die Kamera-Kachel, ändere Features, klicke **Speichern**.

### Gyroflow-Stabilisierung

Kameras mit aktiviertem **Gyroflow**-Feature brauchen ein Lens-Preset:

1. Kamera-Kachel aufklappen (klicken)
2. **Gyroflow Preset** Feld → Durchsuchen → `.gyroflow` Preset-Datei auswählen
3. Speichern

UMI verwendet dieses Preset automatisch bei der Stabilisierung.

### GPS-Injection

UMI injiziert GPS-Daten aus [GPX](glossary:GPX)-Dateien in deine Videos. Dafür brauchst du einen GPS-Tracker (Handy-App, Garmin, etc.).

1. **Einstellungen** → **Tools** → **GPS Track Folder** → Ordner mit deinen GPX-Dateien auswählen
2. Feature **GPS** auf der Kamera-Kachel aktivieren

UMI gleicht Videos automatisch per Zeitstempel mit dem passenden GPX-Track ab.

### Verarbeitung

Nach dem Import wechselst du zur **Verarbeitung**. Es gibt drei Gruppen:

#### Video Tools

Action-Karten für die Video-Nachbearbeitung, von oben nach unten abarbeiten:

##### Videos stabilisieren

Gyroflow-Stabilisierung für Videos ohne interne Stabilisierung (EIS-Off).

- **EIS erkennen** aktivieren → UMI findet automatisch Videos ohne EIS und verschiebt sie in den Gyroflow-Ordner
- Klicke **Ausführen** → Videos werden über die GPU-Queue stabilisiert
- Fortschritt pro Video (Frame-Count + ETA)
- Ergebnis: Stabilisierte Videos in `Video/Stabilized/`

##### GPS injizieren

UMI baut optimierte GPX-Tracks und injiziert GPS-Koordinaten.

- **In Video injizieren** → Ein: GPS direkt ins Video schreiben. Aus: Nur GPX-Dateien vorbereiten.
- Nur für Kameras bei denen du das **GPS**-Feature in UMI aktiviert hast
- Ergebnis aufklappbar: Pro Video ob GPS injiziert, GPX gebaut, oder übersprungen

##### Metadaten wiederherstellen

Stellt EXIF-Metadaten aus den `.umi/metadata/`-Backups wieder her.

- Nützlich wenn Gyroflow oder andere Tools Metadaten beschädigt haben
- **Überschreiben erzwingen** → Metadaten auch überschreiben wenn sie OK aussehen
- Ergebnis: "Metadaten wiederhergestellt" / "Nicht nötig" / "Kein Backup vorhanden"

##### Exports finalisieren

Wenn du Videos in DaVinci Resolve gegradet hast und die Exports in `Video/postprocess/exported/` liegen:

- UMI injiziert GPS-Daten in die Exports
- Stellt Metadaten wieder her
- Verschiebt fertige Videos nach `Video/`
- Räumt den postprocess-Ordner auf

#### Foto Tools

##### Sequence Reviewer

Öffnet den [Sequence Reviewer](chapter:reviewer) zur visuellen Durchsicht und Bewertung erkannter Fotoserien.

#### Nach dem Import

##### Organisieren

Sortiert Dateien nach EXIF-Datum und erkennt [Fotoserien](chapter:burst). (**Fotoserien erkennen** ist aktuell noch nicht im GUI verfügbar — kommt bald.)

##### Thumbnails generieren

Generiert Vorschau-Thumbnails für RAW-Dateien im Arbeitsordner. Thumbnails werden unter `.umi/thumbnails/` zwischengespeichert und vom [Sequence Reviewer](chapter:reviewer) genutzt.

##### Workbench-Statistiken

Übersicht über alle Videos im [Workbench](glossary:Workbench).

Gelbe Warnung bei: Status-Widerspruch (History vs. Ordner), fehlende Backups, Integritätsprobleme.

### Pipeline-Übersicht

So sieht der Lebenszyklus eines Videos aus:

```
SD-Karte → Import (EIS-Erkennung, Metadata Backup)
  → EIS Off?  → Gyroflow/ → Stabilisieren → Stabilized/
  → EIS On?   → Video/ (direkt nutzbar)
  → GPS?      → GPX bauen → GPS injizieren
  → DaVinci?  → postprocess/ → Grading → exported/ → Finalisieren → Video/
```
