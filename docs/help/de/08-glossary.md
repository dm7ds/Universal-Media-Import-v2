## Glossar

Kurze Erklärungen zu Begriffen, die dir beim Arbeiten mit UMI begegnen können.

### Burst / Burst Detection

Ein Burst ist eine Serienaufnahme — mehrere Fotos die durch langes Drücken des Auslösers in schneller Folge entstehen. UMI erkennt Bursts automatisch und gruppiert sie in einem eigenen Unterordner, damit sie zusammenbleiben und deine Importordner nicht durcheinanderbringen. HDR-Belichtungsreihen werden auf dieselbe Weise erkannt.

### EIS (Elektronische Bildstabilisierung)

EIS steht für Electronic Image Stabilization — eine Technik bei der die Kamera das Bild digital beschneidet und verschiebt um Verwacklungen auszugleichen. UMI kann erkennen ob EIS während der Aufnahme aktiv war und Videos entsprechend in separate Ordner sortieren. Mehr dazu: [Bildstabilisierung auf Wikipedia](https://de.wikipedia.org/wiki/Bildstabilisierung).

### EXIF (Exchangeable Image File Format)

EXIF ist ein Standard zum Speichern von Metadaten in Foto- und Videodateien. Dazu gehören Aufnahmedatum und -uhrzeit, Kameramodell, Objektivdaten, GPS-Koordinaten und vieles mehr. UMI liest EXIF-Daten zum Organisieren deiner Dateien und kann aktualisierte Metadaten auch wieder zurückschreiben. Mehr dazu: [EXIF auf Wikipedia](https://de.wikipedia.org/wiki/Exchangeable_Image_File_Format).

### ExifTool

ExifTool ist ein Kommandozeilen-Tool zum Lesen und Schreiben von EXIF-Metadaten in nahezu jedem Medienformat. UMI nutzt ExifTool im Hintergrund für GPS-Injection, Metadata-Backup und -Restore. Du musst es nicht direkt bedienen — UMI übernimmt alles automatisch. Mehr Info: [exiftool.org](https://exiftool.org/).

### FFprobe

FFprobe ist Teil des [FFmpeg](https://ffmpeg.org/)-Pakets und analysiert Videodateien auf Codec, Auflösung, Bitrate und Länge. UMI nutzt FFprobe für die detaillierte Videoanalyse beim Import. Es ist optional — einfache Import-Workflows funktionieren auch ohne. Mehr Info: [FFprobe Dokumentation](https://ffmpeg.org/ffprobe.html).

### GPS (Global Positioning System)

GPS ist ein satellitengestütztes Navigationssystem das Standortkoordinaten liefert. UMI kann GPS-Koordinaten aus einer externen GPX-Track-Datei in Videodateien einbetten und so Ortsdaten nachrüsten, die die Kamera selbst nicht aufgezeichnet hat. Mehr dazu: [GPS auf Wikipedia](https://de.wikipedia.org/wiki/Global_Positioning_System).

### GPX (GPS Exchange Format)

GPX ist ein offenes Dateiformat für GPS-Tracks, Wegpunkte und Routen. Jeder Punkt enthält Breiten- und Längengrad sowie einen Zeitstempel. UMI gleicht diese Zeitstempel mit den Aufnahmezeiten deiner Videos ab und bettet die passenden Koordinaten ein. Mehr dazu: [GPX auf Wikipedia](https://de.wikipedia.org/wiki/GPS_Exchange_Format).

### Gyroflow

Gyroflow ist eine Open-Source-Software zur Videostabilisierung mit Gyrosensor-Daten von Kamera oder Action-Cam. UMI kann Gyroflow-Stabilisierung automatisch als Teil der Post-Processing-Pipeline auf deine Videos anwenden. Mehr Info: [gyroflow.xyz](https://gyroflow.xyz/).

### HDR (High Dynamic Range)

HDR-Fotografie kombiniert mehrere unterschiedlich belichtete Aufnahmen derselben Szene um einen größeren Helligkeitsumfang einzufangen. Kameras schießen diese Belichtungsreihen oft als schnelle Burst-Sequenzen — UMI erkennt sie per Burst Detection und hält sie automatisch zusammen. Mehr dazu: [HDR auf Wikipedia](https://de.wikipedia.org/wiki/High_Dynamic_Range_Image).

### Metadata Backup

Bevor UMI eine Datei verändert (zum Beispiel bei der GPS-Injection), speichert es eine Kopie der originalen EXIF-Daten im Unterordner `.umi/metadata/` innerhalb deines Arbeitsordners. So kannst du die ursprünglichen Metadaten jederzeit wiederherstellen, ohne die eigentlichen Mediendateien zu verlieren.

### MTP (Media Transfer Protocol)

MTP ist ein Protokoll über das Kameras und Android-Geräte per USB Dateien übertragen. Anders als ein normaler USB-Stick erscheinen MTP-Geräte nicht als Laufwerksbuchstabe auf dem PC. UMI unterstützt MTP-Verbindungen für Android-Handys und Kameras — Gerät per USB anschließen, dann unter **Einstellungen** → **Geräte** → **MTP-Gerät** registrieren. Mehr dazu: [MTP auf Wikipedia](https://de.wikipedia.org/wiki/Media_Transfer_Protocol).

### VSN (Volume Serial Number)

Die Volume Serial Number ist eine eindeutige Kennung die das Betriebssystem einer SD-Karte oder einem Speichermedium zuweist. UMI nutzt die VSN zur zuverlässigen Wiedererkennung von SD-Karten — auch wenn du die Karte umbenennst oder in einem anderen Slot nutzt — und verknüpft sie automatisch mit dem richtigen Kameraprofil.

### AppMode (Easy / Standard / Advanced)

UMI hat drei Betriebsmodi die über das Dropdown im Header wählbar sind. **Easy** zeigt nur die Import-Ansicht — ideal für Nutzer die einfach nur Dateien kopieren wollen. **Standard** fügt den Verarbeitungs-Tab mit Stabilisierung und GPS-Tools hinzu. **Advanced** schaltet alles frei: Burst-Profile, Geräteverwaltung und alle erweiterten Einstellungen. Der Modus kann jederzeit gewechselt werden.

### Burst Profile

Ein Burst-Profil definiert die Erkennungsregeln für einen Typ von Fotoserien (z.B. HDR-Brackets oder Sport-Sequenzen). Jedes Profil besteht aus Match-Conditions (welche EXIF-Felder übereinstimmen müssen) und Gruppierungseinstellungen (maximaler Zeitabstand zwischen Fotos). Profile werden im Advanced-Modus unter **Einstellungen** → **Burst** verwaltet.

### Fingerprint (SD-Karte)

UMI identifiziert SD-Karten anhand einer Kombination aus Volume Serial Number und Disk-Serial / Kartengröße. Dieser "Fingerabdruck" ermöglicht die zuverlässige Wiedererkennung derselben Karte über verschiedene Kartenleser, Laufwerksbuchstaben und sogar nach Quick-Format hinweg. Nach einmaliger Registrierung wird die Karte beim Einstecken automatisch mit ihrem Kameraprofil verknüpft.

### Sidecar

Eine Sidecar-Datei ist eine kleine Metadaten-Datei die neben deinen Mediendateien gespeichert wird. UMI nutzt `.umi-review.json` für Review-Tags und Bewertungen sowie `.umi-sequences.json` für erkannte Serien-Gruppierungen. Sidecar-Dateien werden unter `.umi/review/` und `.umi/sequences/` im Arbeitsordner gespeichert.

### Workbench

Die Workbench ist der Hauptordner in den UMI alle importierten Medien ablegt und organisiert. Du richtest ihn beim Setup Wizard ein. Alle importierten Dateien landen in Unterordnern hier, nach Kamera und Datum sortiert. Stell dir die Workbench als deine persönliche Mediathek vor.
