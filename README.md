# Looga Toolkit

Looga Toolkit combines Looga Inspector, Looga Hierarchy, Looga Tags, Prefab Browser, and Looga Tools into one modular Unity package. It provides a consistent authoring environment without collapsing the features into one assembly.

## Modules

- **Inspector** provides attribute-driven inspectors, drawers, catalogs, sidebars, reusable GUI controls, and component clipboard tools.
- **Hierarchy** provides hierarchy guides, favorites, presentation colors, status badges, context actions, and bulk rename tools.
- **Tags** provides project-defined, color-coded tags that can be assigned to GameObjects and queried at runtime.
- **Prefab Browser** provides categorized prefab browsing, project-owned configuration, asset labels, and a generated prefab index.
- **Tools** provides Curve Sketcher, assembly-definition helpers, cross-reference authoring, and shared runtime utilities.

## Assemblies

Toolkit exposes these feature assemblies:

- `LoogaSoft.Inspector.Runtime`
- `LoogaSoft.Inspector.Editor`
- `LoogaSoft.Inspector.ZLinq`
- `LoogaSoft.Hierarchy.Editor`
- `LoogaSoft.Tags.Runtime`
- `LoogaSoft.Tags.Editor`
- `LoogaSoft.PrefabBrowser.Runtime`
- `LoogaSoft.PrefabBrowser.Editor`
- `LoogaSoft.Tools.Runtime`
- `LoogaSoft.Tools.Editor`

Looga Tags uses `LoogaSoft.Tags` namespaces and `LoogaTag*` public types. Unity migration metadata preserves existing serialized tag components and databases when upgrading from the retired tag API.

## Documentation

- [Inspector reference](Documentation~/Inspector.md)
- [Hierarchy reference](Documentation~/Hierarchy.md)
- [Looga Tags reference](Documentation~/Tags.md)
- [Prefab Browser reference](Documentation~/Prefab-Browser.md)
- [Migration guide](Documentation~/Migration.md)

## Installation

Add the package through Unity Package Manager or add this dependency to `Packages/manifest.json`:

```json
"com.loogasoft.loogatoolkit": "https://github.com/LoogaSoft/Looga-Toolkit.git"
```

Remove the old `com.loogasoft.loogainspector`, `com.loogasoft.loogahierarchy`, `com.loogasoft.loogatools`, `com.loogasoft.polytags`, and `com.loogasoft.loogaprefabbrowser` dependencies after all dependent Looga packages reference Looga Toolkit.
