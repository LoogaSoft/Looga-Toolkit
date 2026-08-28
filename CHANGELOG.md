# Changelog

## Unreleased

- Consolidated Hierarchy component indicators into one hover-revealed summary and removed static indicators.
- Preserved the stock Project Create and search controls around a toolbar-height navigation overlay.
- Removed navigation control dividers and increased the history and arrow icon weight.
- Added bounded back, forward, and direct history navigation bars to Inspector and Project windows.
- Added the Alt-left-click color and semantic-icon palette to Project-window folders with GUID-backed metadata and live previews.
- Replaced the palette clear slash with a pixel-aligned outlined X glyph.
- Restricted Hierarchy palette style and glyph rendering to repaint events.
- Simplified the Hierarchy presentation palette with borderless options, native selection fills, and clear or add glyphs.
- Drew stored and previewed object icons directly in Hierarchy rows instead of relying on Unity's stale row cache.
- Matched colored Hierarchy content to Unity's native row styles and stabilized the custom color picker.
- Replaced Hierarchy row gradients with solid group headers and lighter descendant fills.
- Refreshed Unity's Hierarchy item cache immediately after native object icon changes.
- Stabilized Alt-right-click actions and native icon previews outside Unity's context-menu lifecycle.
- Kept Alt-right-click Hierarchy actions open and added live palette hover previews.
- Added an Alt-left-click Hierarchy palette for row colors and semantic object icons.
- Moved Looga Hierarchy operations to an Alt-right-click menu outside Unity's standard context menu.
- Kept foldout hover fills aligned inside their rounded outlines with DPI-aware bounds and a tighter corner radius.
- Replaced size-specific foldout controls and attributes with one `LoogaFoldout` presentation based on the former large foldout style.
- Renamed the box-only size selector to `LoogaBoxStyle`.
- Kept optional-integration status labels readable across all Unity Editor themes and IMGUI states.

## 2.0.0

- Merged Looga Logger into Looga Toolkit while preserving all `LoogaSoft.Logger.*` assembly names, `LoogaSoft.Logging` namespaces, and Unity asset GUIDs.
- Grouped logging integration controls with the other Looga Toolkit integrations in Package Support.
- Added logging and migration documentation.

## 1.1.0

- Added cached update checks for direct `com.loogasoft.*` Git dependencies.
- Added release, source, local-development, and unavailable package statuses.
- Added safe single-package and ordered all-package update workflows.
- Added direct links that compare installed and available revisions.
- Extended optional ZLinq support to Prefab Browser drag results, asset-label results, and Looga Tags selection filtering.
- Kept allocation-conscious loop fallbacks in all search modules so ZLinq remains optional.
- Removed the remaining ordinary LINQ calls from Prefab Browser and Looga Tags editor workflows.
- Added a Transform inspector with local and world editing modes.
- Added copy, paste, and reset actions for position, rotation, and scale.
- Added calculated world size and Scene view bounds measurements for selected objects.
- Added transactional Edit Mode physics placement with pause, step, reset, apply, cancel, and automatic settling.
- Added temporary collider generation with performance, balanced, and precision strategies.
- Added Undo-aware native collider baking and a project-local collider descriptor cache.
- Added recovery journals that restore interrupted placement sessions after reloads, scene saves, shutdowns, and crashes.
- Renamed the tag assemblies, namespaces, files, types, and database asset from the retired API to Looga Tags.
- Added serialized type and database-path migration for existing projects.
- Restricted Prefab Browser data assemblies to the Editor platform.
- Reduced tag and prefab browser allocations during editor repaint and filtering.
- Added deterministic hierarchy metadata cleanup and scene-aware legacy migration.
- Hardened singleton, extension, and cross-reference runtime APIs.
- Added API documentation and serialization-safe private fields.

## 1.0.0

- Combined Looga Inspector, Looga Hierarchy, and Looga Tools into Looga Toolkit.
- Preserved existing public namespaces, assembly names, and assembly GUIDs.
- Organized each feature as a separate runtime or editor assembly module.
- Updated package-relative editor resources for the Toolkit package layout.
- Added Looga Tags as an isolated module while preserving serialized GUIDs from the previous package.
- Added Prefab Browser as an isolated module with project-owned configuration and database assets.
