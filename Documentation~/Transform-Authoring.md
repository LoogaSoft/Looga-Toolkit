# Transform Authoring

Looga Toolkit replaces the standard Transform inspector with authoring controls that preserve Unity's normal transform data.

## Inspector controls

- Use **Local** to edit values relative to the parent.
- Use **World** to edit world position, world rotation, and calculated world scale.
- Use the row buttons to copy, paste, or reset one vector.
- Read **Size** to inspect the world-space size of rendered geometry under the transform.
- When rendered geometry does not exist, Size uses enabled collider bounds.

World scale is not a stored Unity property. Toolkit converts the requested world scale to local scale. A rotated hierarchy with non-uniform scale can contain shear that a Transform cannot represent exactly.

## Scene measurements

Select a GameObject with rendered geometry or enabled colliders. Toolkit draws an oriented bounds box in the Scene view. It also labels the X, Y, and Z dimensions. One Unity unit is displayed as one meter.
