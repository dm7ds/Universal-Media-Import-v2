## Geräteverwaltung

Unter **Einstellungen** → **Geräte** verwaltest du alle Speicherquellen die UMI kennt. Dein erstes Gerät kannst du auch während der [Einrichtung](chapter:setup) registrieren.

### SD-Karten

SD-Karten werden über ihre Volume Serial Number ([VSN](glossary:VSN)) identifiziert — eine eindeutige Kennung die auch nach dem Umbenennen oder Formatieren (nur Quick-Format) erhalten bleibt.

**Karte registrieren:**

1. **Einstellungen** → **Geräte** → **Gerät hinzufügen**
2. Tab **SD-Karte** → Karte einstecken
3. UMI erkennt die Karte automatisch und zeigt sie an
4. Kamera zuordnen → **Speichern**

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

USB-Kameras und Android-Geräte die per [MTP](glossary:MTP) (Media Transfer Protocol) angeschlossen werden.

1. **Gerät hinzufügen** → Tab **MTP-Gerät**
2. Gerät per USB anschließen
3. UMI zeigt erkannte MTP-Geräte
4. Kamera zuordnen → **Speichern**

MTP-Geräte sind immer fest einer Kamera zugeordnet (kein Floating).

### Feste Pfade

Lokale Ordner oder Netzlaufwerke die als Importquelle dienen — z.B. Dashcam-Ordner auf einem NAS.

1. **Gerät hinzufügen** → Tab **Fester Pfad**
2. Ordner-Pfad eingeben oder durchsuchen
3. Kamera zuordnen → **Speichern**

UMI überwacht den Ordner im automatischen Import und importiert neue Dateien automatisch.

**Online-Status:**

In der Geräte-Übersicht zeigt ein grüner Punkt an, welche Geräte gerade verbunden sind. Graue Punkte = offline/nicht eingesteckt. Der Status wird live aktualisiert.

**Reihenfolge:**

Per Drag & Drop kannst du die Reihenfolge der **SD-Karten** und **MTP-Geräte** innerhalb ihrer Gruppen ändern. Die Reihenfolge bestimmt die Anzeige-Position — hat keinen Einfluss auf die Import-Logik.

> **Hinweis:** Feste Pfade unterstützen kein Drag & Drop zur Umsortierung.
