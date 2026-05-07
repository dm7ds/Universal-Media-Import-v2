## Import

### Where Do My Files Go?

At the top of the Import tab you see the **Workbench** path — this is the folder where UMI stores all imported media. You chose this folder during setup. You can change it anytime via **Browse...** next to the path.

### Starting an Import

Your cameras appear as tiles below the toolbar.

**Option A — Automatic (recommended):**

1. Click **Smart Watch** in the top-left.
2. Insert an SD card.
3. UMI detects the card, starts the import, and shows progress on the camera tile.
4. Card out, next card in — UMI keeps going.
5. Done? Click **Stop Watch**.

**Option B — One-time:**

1. Insert the SD card.
2. Click **Quick Import**.
3. UMI copies all detected media and shows progress.

That's it. Your photos and videos are now sorted by date and camera in your [Workbench](glossary:Workbench) folder.

### Sort Order

The **Sort Order** toggle button switches between two folder layouts inside the Workbench. Click it to alternate between modes:

**Camera First** (default) — groups by date, then camera, then media type:

```
Workbench\
  2026-03-12\
    MyActionCam\
      Video\
        DJI_20260312_143022.mp4
      Photo\
        DJI_20260312_143022.jpg
```

**Type First** — groups by date, then media type, then camera:

```
Workbench\
  2026-03-12\
    Video\
      MyActionCam\
        DJI_20260312_143022.mp4
    Photo\
      MyActionCam\
        DJI_20260312_143022.jpg
```

Pick whichever makes more sense for your workflow. If you only have one camera, both look similar.

### Date Range Filter

Only want to import files from a specific time period? Click the **Date Range** button in the toolbar and set a start and end date. UMI then only imports files whose recording date falls within that range. Click again to remove the filter.

### During Import

While an import is running, the camera tile shows a progress bar. If you have multiple imports running at the same time, you can use **Pause All** and **Cancel All** in the toolbar to control them.

### What's Next?

Your photos are now in the Workbench folder. If you have photo series (HDR brackets, astro stacking, timelapses, etc.), UMI can detect and sort them into subfolders after import — even if the import is already done. See [Photo Series Detection](chapter:burst) for details.

For video post-processing (stabilization, GPS injection), see the [Advanced Guide](chapter:advanced).
