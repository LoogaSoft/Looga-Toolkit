# Changelog

## Unreleased

- Drew stored and previewed object icons directly in Hierarchy rows, including objects without custom row colors.
- Matched colored rows to Unity's native icon, label, and foldout geometry and deferred the custom color window until the click completed.
- Replaced the row gradient and accent rail with solid group headers and inherited descendant fills.
- Invalidated Unity's Hierarchy item cache after native object icon changes and previews.
- Replaced the transient Alt-right-click menu with a delayed dropdown outside Unity's context event.
- Kept the last palette preview active across option gaps and used native object icons for replacement.
- Kept Alt-right-click actions open by handling Unity's context-click event.
- Added immediate palette hover feedback and live color or icon previews on target rows.
- Added an Alt-left-click presentation palette for object colors and semantic icons.
- Moved Looga Hierarchy actions to an Alt-right-click menu and removed the standard context submenu.
- Replaced hierarchy status badges with bounded, cached component summaries.
- Aggregated generic MonoBehaviours under one counted script icon while preserving distinct component icons.
- Retained static-state tooltips and added a configurable component-icon limit.
- Moved component counts beside their icons, collapsed MeshFilter and MeshRenderer pairs, and replaced the static `S` with a pushpin.
- Culled hierarchy indicators dynamically when the complete object name needs their row space.

## 0.8.0 - 2026-08-02

- Made hierarchy status badges interactive with context-specific, Undo-safe actions.
- Added a preview-first bulk rename window with find/replace, prefix/suffix, descendant scope, and configurable numbering.

## 0.7.0 - 2026-08-02

- Removed hierarchy folders and migrated existing folder rows back to ordinary colored GameObjects.
- Flattened hierarchy operations into the main Looga Hierarchy context menu.
- Removed child sorting and hierarchy-path copying.

## 0.6.3 - 2026-08-01

- Rebuilt presentation and favorites lookup caches after every editor domain reload.
- Prevented Unity hot-reload state from hiding persisted hierarchy customizations until the next edit.

## 0.6.2 - 2026-08-01

- Matched ordinary colored-row accent spacing to the folder and Favorites header spacing.
- Extended the row gradient beneath the accent rail and content gap for a continuous treatment.

## 0.6.1 - 2026-08-01

- Preserved folder and row-color customizations across script recompiles and domain reloads.
- Added a scene hierarchy locator fallback for objects whose Unity global ID has not stabilized yet.
- Automatically rebound fallback records to their current global IDs after reload or scene save.

## 0.6.0 - 2026-08-01

- Added preset and custom colors for ordinary GameObjects without converting them into folders.
- Replaced the flat ordinary-row tint with a cached low-opacity accent gradient.
- Added clear spacing between the accent rail and Unity's native object icon and label.
- Preserved the existing left accent rail and standard GameObject hierarchy interactions.

## 0.5.2 - 2026-08-01

- Added an editable Folder Name field beneath the protected GameObject header.
- Kept tags, layers, active state, static state, and the organizational Transform read-only.
- Removed the redundant folder rename popup workflow.

## 0.5.1 - 2026-08-01

- Made hierarchy folders read-only in the Inspector and hid their organizational Transform.
- Added a folder rename prompt and context command while preserving native hierarchy parenting.
- Preserved and restored pre-existing object hide flags when folder styling is applied or removed.

## 0.5.0 - 2026-08-01

- Added first-class hierarchy folders with native parenting and folding behavior.
- Matched folder headers to the Favorites visual language while preserving per-folder accent colors.
- Added preset and custom folder colors, empty-folder creation, and selection wrapping.
- Renamed the former section-facing workflow to clearer folder terminology.

## 0.4.4 - 2026-08-01

- Reconciled favorite proxy rows in place instead of recreating them on every hierarchy change.
- Prevented synchronization-generated hierarchy events from causing repeated proxy repaint cycles.

## 0.4.3 - 2026-08-01

- Removed the synthetic `No favorites` child.
- Empty Favorites roots are now childless, so Unity naturally hides their foldout arrow.

## 0.4.2 - 2026-08-01

- Increased Favorites header contrast against the standard Hierarchy background.
- Assigned the empty-state icon directly to its synthetic row to prevent the default GameObject icon from flickering through.

## 0.4.1 - 2026-08-01

- Added distinct Looga styling for per-scene Favorites roots and shortcut rows.
- Preserved native hierarchy folding while preventing ordinary row decorations from affecting synthetic favorites.

## 0.4.0 - 2026-08-01

- Replaced the injected Hierarchy header with native, per-scene Favorites foldouts.
- Added transient non-editable proxy rows that navigate to real favorite objects.
- Kept synthetic favorites out of scene serialization, builds, Play Mode, and hierarchy actions.

## 0.3.0 - 2026-07-31

- Standardized personal navigation terminology around favorites.
- Replaced the separate favorites window with a collapsible section inside each Hierarchy window.
- Removed the redundant disabled-object status badge.

## 0.2.0 - 2026-07-31

- Added personal hierarchy favorites and navigation tooling.
- Added project-shared row labels and section styling.
- Added cached status badges for missing scripts, prefab overrides, static objects, and EditorOnly objects.
- Added multi-selection hierarchy actions for parenting, descendant selection and activation, sorting, path copying, and flattening children.

## 0.1.0 - 2026-07-31

- Added automatic, pixel-aligned Hierarchy Guides.
- Added project settings for guide visibility, color, opacity, and thickness.
