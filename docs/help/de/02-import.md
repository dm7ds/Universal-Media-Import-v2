## Import

### Wo landen meine Dateien?

Oben im Import-Tab siehst du den **Arbeitsordner** — das ist der Ordner, in dem UMI alle importierten Medien ablegt. Du hast ihn bei der Einrichtung festgelegt. Du kannst ihn jederzeit über **Durchsuchen...** daneben ändern.

### Import starten

Deine Kameras erscheinen als Kacheln unterhalb der Toolbar.

**Variante A — Automatisch (empfohlen):**

1. Klicke auf **Automatischer Import** oben links.
2. Stecke eine SD-Karte ein.
3. UMI erkennt die Karte, startet den Import und zeigt den Fortschritt auf der Kamera-Kachel.
4. Karte raus, nächste rein — UMI läuft durch.
5. Fertig? **Import stoppen** klicken.

**Variante B — Einmalig:**

1. Stecke die SD-Karte ein.
2. Klicke auf **Schnell-Import**.
3. UMI kopiert alle erkannten Medien und zeigt den Fortschritt.

Das war's. Deine Fotos und Videos liegen jetzt sortiert nach Datum und Kamera im [Workbench](glossary:Workbench)-Ordner.

### Sortierung

Der **Sortierung**-Toggle-Button wechselt zwischen zwei Ordner-Layouts im Arbeitsordner. Klicken zum Umschalten:

**Kamera zuerst** (Standard) — sortiert nach Datum, dann Kamera, dann Medientyp:

```
Arbeitsordner\
  2026-03-12\
    MeineActionCam\
      Video\
        DJI_20260312_143022.mp4
      Photo\
        DJI_20260312_143022.jpg
```

**Typ zuerst** — sortiert nach Datum, dann Medientyp, dann Kamera:

```
Arbeitsordner\
  2026-03-12\
    Video\
      MeineActionCam\
        DJI_20260312_143022.mp4
    Photo\
      MeineActionCam\
        DJI_20260312_143022.jpg
```

Wähle was für deinen Workflow besser passt. Wenn du nur eine Kamera hast, sehen beide ähnlich aus.

### Datumsfilter

Du willst nur Dateien aus einem bestimmten Zeitraum importieren? Klicke auf **Datumsbereich** in der Toolbar und lege ein Start- und Enddatum fest. UMI importiert dann nur Dateien, deren Aufnahmedatum in diesen Zeitraum fällt. Nochmal klicken entfernt den Filter.

### Während des Imports

Während ein Import läuft, zeigt die Kamera-Kachel einen Fortschrittsbalken. Wenn mehrere Imports gleichzeitig laufen, kannst du über die Toolbar alle auf einmal pausieren oder abbrechen.

### Und danach?

Deine Fotos liegen jetzt im Arbeitsordner. Wenn du Fotoserien hast (HDR-Brackets, Astro-Stacking, Timelapses etc.), kann UMI diese nachträglich erkennen und in Unterordner sortieren — auch wenn der Import schon abgeschlossen ist. Siehe dazu [Serien-Erkennung](chapter:burst).

Für Video-Nachbearbeitung (Stabilisierung, GPS-Injection) siehe die [Erweiterte Anleitung](chapter:advanced).
