# Looga Toolkit

Looga Toolkit combines Looga Inspector, Looga Hierarchy, Looga Tags, Prefab Browser, and Looga Tools into one modular Unity package. It provides a consistent authoring environment without collapsing the features into one assembly.

## Modules

- **Inspector** provides attribute-driven inspectors, drawers, catalogs, sidebars, reusable GUI controls, and component clipboard tools.
- **Hierarchy** provides hierarchy guides, favorites, presentation colors, status badges, context actions, and bulk rename tools.
- **Tags** provides project-defined, color-coded tags that can be assigned to GameObjects and queried at runtime.
- **Prefab Browser** provides categorized prefab browsing, project-owned configuration, asset labels, and a generated prefab index.
- **Tools** provides Curve Sketcher, assembly-definition helpers, cross-reference authoring, and shared runtime utilities.
- **Physics Placement** settles scene objects with Edit Mode physics and provides reversible collider generation and baking.
- **Transform Authoring** adds local and world editing, vector clipboard actions, real-world size inspection, and Scene view measurements.

## Assemblies

Toolkit exposes these feature assemblies:

- `LoogaSoft.Inspector.Runtime`
- `LoogaSoft.Inspector.Editor`
- `LoogaSoft.Inspector.ZLinq`
- `LoogaSoft.Toolkit.Search.ZLinq`
- `LoogaSoft.Hierarchy.Editor`
- `LoogaSoft.Tags.Runtime`
- `LoogaSoft.Tags.Editor`
- `LoogaSoft.PrefabBrowser.Runtime`
- `LoogaSoft.PrefabBrowser.Editor`
- `LoogaSoft.Tools.Runtime`
- `LoogaSoft.Tools.Editor`
- `LoogaSoft.PhysicsPlacement.Editor`
- `LoogaSoft.TransformAuthoring.Editor`

Looga Tags uses `LoogaSoft.Tags` namespaces and `LoogaTag*` public types. Unity migration metadata preserves existing serialized tag components and databases when upgrading from the retired tag API.

ZLinq support is optional. Enable it from `LoogaSoft > Package Support` to route eligible Inspector, Prefab Browser, asset-label, and Looga Tags collection queries through ZLinq. The normal assemblies keep allocation-conscious loop fallbacks and do not reference ZLinq.

## Documentation

- [Inspector reference](Documentation~/Inspector.md)
- [Hierarchy reference](Documentation~/Hierarchy.md)
- [Looga Tags reference](Documentation~/Tags.md)
- [Prefab Browser reference](Documentation~/Prefab-Browser.md)
- [Tools reference](Documentation~/Tools.md)
- [Physics Placement reference](Documentation~/Physics-Placement.md)
- [Transform Authoring reference](Documentation~/Transform-Authoring.md)
- [Migration guide](Documentation~/Migration.md)

## Installation

Add the package through Unity Package Manager or add this dependency to `Packages/manifest.json`:

```json
"com.loogasoft.loogatoolkit": "https://github.com/LoogaSoft/Looga-Toolkit.git"
```

Remove the old `com.loogasoft.loogainspector`, `com.loogasoft.loogahierarchy`, `com.loogasoft.loogatools`, `com.loogasoft.polytags`, and `com.loogasoft.loogaprefabbrowser` dependencies after all dependent Looga packages reference Looga Toolkit.
