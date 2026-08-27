# UI Toolkit Editor Foundation

Looga editor workspaces use retained UI Toolkit controls when they remain open, filter data, or contain complex interactive sections. Property drawers that must participate in Unity's IMGUI property pipeline may remain IMGUI.

## Shared Controls

`LoogaUiToolkitStyle` owns the shared retained-mode geometry and interaction rules:

- Interactive rows and cards.
- Standard foldout headers and vector-drawn foldout triangles.
- Tabs, sections, button rows, and bound property fields.
- Collection views with row-hover suppression.

Do not duplicate these values in individual windows. Extend the shared helper when two or more tools need the same behavior.

## Performance Checks

Use **LoogaSoft > Toolkit > Performance Recorder** before and after a retained-mode migration.

1. Start a capture with the target window visible.
2. Type in each search field, change filters, expand foldouts, and select representative assets.
3. Stop the capture and compare editor-update duration, repaint count, and managed allocations.
4. Repeat with the window hidden to identify work that continues while it is not visible.

The profiler exposes these migration markers:

- `Looga.UI.MenuPreview.Refresh`
- `Looga.Toolkit.PackageWorkspace.Refresh`
- `Looga.UI.DesignSystem.Refresh`

These operations must run only after relevant state changes. They must not run from an unconditional editor update or repaint loop.

A migrated workspace passes when:

- It performs no periodic refresh while idle.
- Hiding the window stops its visual refresh work.
- Filtering, selection, and foldout changes rebuild only the affected retained tree.
- A representative interaction capture has no recurring managed allocation caused by the window.
- Typical state changes finish within one editor frame. Record exceptions with the data size that caused them.

## Migration Status

- Menu Preview is the reference retained-mode implementation.
- Package Support and Package Updates share one retained workspace.
- Design System, Prefab Browser, Asset Labeler, and search-heavy prefab tools are retained.
- Network Player and the primary menu-definition inspectors use retained tabs, foldouts, fields, and embedded inspectors.
