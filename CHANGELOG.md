# Changelog

## Unreleased

- Renamed the tag assemblies, namespaces, files, types, and database asset from the retired API to Looga Tags.
- Added serialized type and database-path migration for existing projects.

## 1.0.0

- Combined Looga Inspector, Looga Hierarchy, and Looga Tools into Looga Toolkit.
- Preserved existing public namespaces, assembly names, and assembly GUIDs.
- Organized each feature as a separate runtime or editor assembly module.
- Updated package-relative editor resources for the Toolkit package layout.
- Added Looga Tags as an isolated module while preserving serialized GUIDs from the previous package.
- Added Prefab Browser as an isolated module with project-owned configuration and database assets.
