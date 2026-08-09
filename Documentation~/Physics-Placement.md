# Physics Placement

Physics Placement settles scene objects with Unity physics while the Editor remains in Edit Mode.

## Open The Tool

Open `LoogaSoft > Toolkit > Physics Placement`.

You can also select scene objects and use `GameObject > Looga Toolkit > Settle With Physics`.

## Session Workflow

1. Select one or more scene objects.
2. Choose a simulation quality and collider strategy.
3. Select **Start Selected Objects**.
4. Pause, step, reset, or resume the simulation as needed.
5. Select **Apply** to keep the settled transforms.
6. Select **Cancel** to restore the authored transforms.

Apply creates one Undo operation. Cancel does not change the authored scene.

## Simulation Quality

- **Draft** uses a 30 Hz step and reduced solver work.
- **Balanced** uses a 60 Hz step for normal placement.
- **High** uses a 90 Hz step and more solver work.
- **Ultra** uses a 120 Hz step for difficult contact arrangements.

Use the lowest quality that produces a stable result.

## Generated Colliders

The tool uses existing colliders when they are available. It can generate temporary colliders when objects have no collider.

- **Performance** fits a box collider.
- **Balanced** selects a box, sphere, or capsule from the object proportions.
- **Precision** uses one convex source mesh when possible. It falls back to a balanced primitive fit.

Temporary colliders are not saved. Use **Bake Missing Colliders** to add native Unity colliders permanently.

Collider fit data is cached under `Library/LoogaToolkit/PhysicsPlacement`. The cache is local and is not versioned.

## Safety And Recovery

The tool records transforms and Rigidbody settings before simulation.

It restores the session before these events:

- An assembly reload.
- A scene save.
- A Play Mode transition.
- An Editor shutdown.
- An Undo or Redo operation during simulation.

The tool writes an active recovery journal under `Library/LoogaToolkit/PhysicsPlacement`. The next Editor session restores an interrupted placement when its scene opens.

## Current Limits

- Physics Placement simulates the active scene physics world.
- It freezes unrelated active Rigidbodies during the session.
- Precision generation supports a single root-aligned source mesh.
- It does not perform convex decomposition of complex concave meshes.
- Permanent baking adds standard Unity collider components.
