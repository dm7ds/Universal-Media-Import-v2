## Device Management

Manage all storage sources UMI knows about under **Settings** → **Devices**. You can also register your first device during the [Setup Wizard](chapter:setup).

### SD Cards

SD cards are identified by their Volume Serial Number ([VSN](glossary:VSN)) — a unique identifier that persists even after renaming or formatting (Quick Format only).

**Registering a card:**

1. **Settings** → **Devices** → **Add Device**
2. Tab **SD Card** → insert the card
3. UMI detects the card automatically and displays it
4. Assign a camera → **Register**

**Card assignment:**

| Assignment | Import behavior |
|-----------|----------------|
| Camera assigned (Fixed) | Import starts automatically without prompting |
| No camera (Floating) | UMI asks which camera to use at each import — with a suggestion from history |

To change: simply select a different camera in the dropdown and save. To set as Floating: leave the camera field empty.

**Card history:**

Each SD card has a history icon (clock symbol). Clicking it shows:

- Which cameras have used this card
- How often (import count + percentage)
- When last used

Useful when you swap cards between cameras and want to know where a card has been.

**Card re-recognition:**

UMI recognizes cards even after reformatting, as long as the disk serial and card size match (5% tolerance). The previous assignment is applied automatically.

### MTP Devices

USB cameras and Android devices connected via [MTP](glossary:MTP) (Media Transfer Protocol).

1. **Add Device** → Tab **MTP Device**
2. Connect the device via USB
3. UMI displays detected MTP devices
4. Assign a camera → save

MTP devices are always assigned to a fixed camera (no Floating).

### Fixed Paths

Local folders or network drives used as import sources — for example, a dashcam folder on a NAS.

1. **Add Device** → Tab **Fixed Path**
2. Enter or browse to the folder path
3. Assign a camera → save

UMI monitors the folder in Watch Mode and imports new files automatically.

**Online status:**

In the Devices tab, a green dot indicates which devices are currently connected. Gray dots = offline or not inserted. Status is updated live.

**Order:**

Use drag and drop to change the order of **SD Cards** and **MTP Devices** within their groups. The order determines display position — it has no effect on import logic.

> **Note:** Fixed Paths do not support drag-and-drop reordering.
