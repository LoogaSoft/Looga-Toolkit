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

Hover a Hierarchy row and click its favorite icon. You can also use **Alt + Right Click** and select
the favorite action. Favorites remain personal shortcuts stored under `UserSettings`, so they do
not modify shared scene data.

### Object Presentation

Use **Alt + Left Click** on a Hierarchy row to open the object presentation palette. The palette
provides preset and custom row colors plus semantic object icons. The clear control at the start of
each row removes that presentation value. A palette change applies to the complete selection when
the clicked object is already selected. Hovering a palette option previews it on every target.
Moving across the space between options keeps the last preview stable.

A colored object uses a uniform solid header fill. Its visible descendants inherit a lighter solid
fill aligned to the colored object's hierarchy depth. Custom icons replace the ordinary object icon
without adding a component.

Visual metadata is stored in `ProjectSettings/LoogaHierarchyPresentation.asset` without adding
components to scene objects.

### Component Summary

One compact row control represents the components on an object without duplicating Transform or
RectTransform. Hover over the summary to reveal the detailed component icons to its left. The
reveal temporarily takes the required row space and clips a long object name cleanly. Moving away
collapses the details back to the single summary control.

Components with distinct Unity icons keep those icons. Repeated component types use one icon with
a compact count beside it, so the icon remains unobstructed. Generic MonoBehaviours share a C#
script icon with a total count. Missing scripts are included in that count and tint the script icon
orange. Tooltips identify each component type and report missing scripts. A MeshFilter is omitted
when the same object has a MeshRenderer because the renderer already represents the mesh pair.

The **Maximum Component Icons** project setting limits each row to between one and eight component
indicators in the expanded group. The default is five. An overflow indicator replaces component
types that do not fit. Static state is intentionally not represented in Hierarchy rows. Component
summaries are cached and refresh after hierarchy, project, or Undo state changes rather than
scanning components on every repaint.

### Hierarchy Actions

Use **Alt + Right Click** on a Hierarchy row to open Looga Hierarchy actions. These actions no
longer occupy Unity's standard GameObject context menu:

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
