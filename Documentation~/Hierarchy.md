# Looga Toolkit: Hierarchy

Looga Hierarchy adds focused readability and organization tools to Unity's Hierarchy window
without adding components or runtime code to a project.

## Features

### Hierarchy Guides

Hierarchy Guides draw crisp tree connectors between parent GameObjects and their visible child
branches. Guides appear automatically in every Hierarchy window after the package is installed.

Configure the feature under **Project Settings > LoogaSoft > Hierarchy**:

- enable or disable guides for the project;
- use an automatic Editor-theme-aware color or a custom color;
- adjust opacity and physical pixel thickness;
- restore the package defaults.

Settings are stored in `ProjectSettings/LoogaHierarchySettings.asset` and do not affect builds.

### Favorites

Hover a Hierarchy row and click its favorite icon. You can also use **GameObject > Looga Hierarchy >
Toggle Favorite**. Each loaded scene receives a Favorites foldout at the top of its object tree.
Its rows are transient, non-editable navigation proxies. They remain available during Play Mode
transitions but are never serialized or included in builds. Favorites remain personal shortcuts
stored under `UserSettings`, so they do not modify shared scene data.

### Object Colors

Use **GameObject > Looga Hierarchy > Color** to apply a preset or custom accent to ordinary
GameObjects. Colored rows retain Unity's standard hierarchy behavior while gaining a crisp left
accent rail and a low-opacity color wash that fades smoothly across the row. Color metadata is
project-shared and does not add components or runtime state.

Visual metadata is stored in `ProjectSettings/LoogaHierarchyPresentation.asset` without adding
components to scene objects.

### Status Badges

Compact row badges identify missing scripts, prefab overrides, static
objects, and EditorOnly objects. Status is cached and refreshed when hierarchy, project, or Undo
state changes rather than scanning every repaint. The static indicator is a decorative bold `S`.
Hover over it to see `Fully Static` or each enabled static flag on a separate line. Actionable
badges open their relevant actions when clicked:

- select and ping the affected object;
- remove missing script references with confirmation and Undo support;
- open a prefab asset or apply/revert its instance overrides;
- clear the EditorOnly tag.

### Hierarchy Actions

The following commands appear directly under **GameObject > Looga Hierarchy**, separated from
favorites and color commands by a divider:

- moving children up to their parent's level;
- selecting descendants;
- enabling descendants;
- disabling descendants;
- opening the preview-first **Bulk Rename** tool.

Bulk Rename can operate on the current selection or its descendants in deterministic hierarchy
order. It supports a shared base name, find/replace, prefixes, suffixes, configurable sequential
numbering, a complete before/after preview, and one-step Unity Undo.

All mutating actions support Unity Undo and multi-selection.

## Requirements

- Unity 6000.3 or newer.

## Installation

Add the package from disk through Unity Package Manager, or add the following dependency to the
project's `Packages/manifest.json`:

```json
"com.loogasoft.loogatoolkit": "file:<path-to-package>"
```
