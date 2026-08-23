# Prefab Browser

Prefab Browser provides a categorized view of project prefabs and utilities for applying asset labels.

Open the browser from `Window > LoogaSoft > Prefab Browser > Browser Window`. Use the adjacent configuration window to author categories and subcategories.

Mutable configuration and generated database assets are stored in `Assets/Shared/Editor/Prefab Browser`. Package updates therefore do not replace project-specific data.

Prefab filtering uses reusable lists and direct loops, so typing in the search field does not create query iterators. When Toolkit ZLinq support is enabled from `LoogaSoft > Package Support`, drag payload and asset-label result materialization use the optional ZLinq adapter. The normal fallback does not require ZLinq.
