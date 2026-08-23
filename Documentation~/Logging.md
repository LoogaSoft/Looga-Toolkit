# Logging

The Logging module provides centralized channels with a replaceable backend.

```csharp
using LoogaSoft.Logging;

private static readonly LoogaLogChannel CombatLog = LoogaLogger.Channel("Combat");

CombatLog.Info("Projectile service initialized.");
CombatLog.Warning("Missing projectile prefab.", this);
```

Add `LoogaLoggerService` to a stable bootstrap object to configure the enabled state, minimum level, backend, and channel overrides. Without a custom backend, logging falls back to the Unity console.

Install a project-specific backend at startup with `LoogaLogger.SetBackend(...)`. Gameplay systems should depend only on the Logging module interfaces.

Use lazy logging when creating the message is expensive:

```csharp
CombatLog.Debug(() => $"Hit audit: {BuildDetailedHitReport()}");
```

Enable ZString under `LoogaSoft > Package Support > Looga Toolkit` to reduce allocations from formatted messages. The ZLogger backend remains isolated in `LoogaSoft.Logger.ZLogger`. It compiles automatically when the `com.cysharp.zlogger` package is installed and otherwise leaves the Unity console fallback available.

## Migrating From Looga Logger

Install Looga Toolkit 2.0.0 or newer, then remove `com.loogasoft.loogalogger` from `Packages/manifest.json`. Do not install both packages together because both contain the same assembly identities. Existing components, namespaces, and asmdef references remain valid.
