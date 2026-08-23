# Changelog

## Unreleased

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
