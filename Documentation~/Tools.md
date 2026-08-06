# Tools

The Tools module provides small runtime utilities and focused editor workflows.

## Runtime Utilities

- `LoogaSingleton<T>` provides one scene-local component instance. A duplicate destroys its GameObject.
- `LoogaPersistentSingleton<T>` provides one optional cross-scene component instance.
- `LoogaExtensions` provides guarded list, transform, distance, and hierarchy helpers.
- `CrossReference` stores an explicit link to another Unity object.

Singleton instances clear their static reference when Unity destroys the active component. Do not use these base classes as a replacement for dependency injection when a service needs an explicit lifetime or interface contract.

## Editor Tools

- **Curve Sketcher** edits animation curves in a dedicated window.
- **Assembly Definition tools** create and inspect assembly definitions.
- **Cross Reference tools** create explicit references through drag-and-drop workflows.

The editor tools do not run in player builds.
