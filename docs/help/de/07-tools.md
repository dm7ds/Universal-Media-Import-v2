## Werkzeuge & Optionen

Externe Tools und App-Einstellungen findest du unter **Einstellungen** → **Tools**.

### Externe Tools

#### ExifTool (Pflicht)

UMI nutzt [ExifTool](glossary:ExifTool) zum Lesen und Schreiben von [EXIF](glossary:EXIF)-Metadaten. Wird für [GPS](glossary:GPS)-Injection, [Metadata Backup](glossary:Metadata Backup) und -Restore benötigt.

- UMI bringt ExifTool mit und nutzt es automatisch
- **Durchsuchen...** — eigene ExifTool-Datei auswählen
- **Standard** — auf die mitgelieferte Version zurücksetzen
- Status: grüner Haken = gefunden, rotes X = fehlt

#### Gyroflow (Optional)

Nur nötig wenn du [Gyroflow](glossary:Gyroflow)-Stabilisierung nutzt (siehe [Erweiterte Anleitung](chapter:advanced)).

- **Durchsuchen...** — Gyroflow-CLI-Datei auswählen
- **Installieren** — lädt die neueste Version von GitHub
- Nicht nötig wenn du keine Videos stabilisierst

#### FFprobe (Optional)

Für detaillierte Video-Analyse (Codec, Auflösung, Bitrate).

- **Durchsuchen...** — [FFprobe](glossary:FFprobe)-Datei auswählen
- **Installieren** — lädt FFmpeg Essentials (enthält FFprobe)
- Nicht nötig für einfache Import/Export-Workflows

### Optionen

#### GPS Track Ordner

Ordner mit deinen [GPX](glossary:GPX)-Dateien von GPS-Trackern (Handy-Apps, Garmin, etc.).

- **Durchsuchen...** — Ordner auswählen
- UMI durchsucht diesen Ordner bei der [GPS-Injection](chapter:advanced)

#### Sprache

Wechsel zwischen Deutsch und Englisch. Erfordert einen App-Neustart. Die im
Installer gewählte Sprache wird beim ersten Start automatisch übernommen — du
musst hier nur dann anfassen, wenn du sie später wieder umstellen willst.

#### Protokollierung (Logging)

Drei-stufiges Dropdown, das die Log-Datei unter `<Install-Verzeichnis>/logs/`
steuert:

- **Kein Log** (Standard) — es wird gar nichts geschrieben, keine Log-Datei angelegt
- **Info** — übliche Betriebsmeldungen
- **Debug** — ausführliche Ausgabe zur Fehlersuche

Die Änderung greift beim nächsten Start. Schalte nur dann auf *Debug*, wenn du
gezielt ein Problem suchst.

#### Auf Updates beim Start prüfen

Wenn aktiv (Standard) prüft UMI bei jedem Start die GitHub-Release-Seite. Liegt
dort eine neuere Version, wird der Installer leise im Hintergrund geladen und
ein Banner mit **Installieren**-Button erscheint oben im Hauptfenster — ein
Klick startet die Installation.

#### Setup Wizard neu starten

Öffnet den [Setup Wizard](chapter:setup) von vorne. Nützlich wenn du Arbeitsordner, Kameras oder Tools neu konfigurieren möchtest, ohne die Config-Dateien manuell zu bearbeiten.

### Thumbnails generieren

Die Thumbnail-Generierung für RAW-Dateien findest du unter **Verarbeitung** → **Nach dem Import** → **Thumbnails generieren**. Sie ist kein Teil der Tool-Einstellungen — siehe die [Erweiterte Anleitung](chapter:advanced) für Details.
