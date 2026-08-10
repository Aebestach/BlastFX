# BlastFX

Shared fireball explosion FX and part-destroy API for KSP 1.12.5. Other mods can call it instead of shipping their own explosion shader.

Works with mods that depend on BlastFX, such as [Thunderbolt](https://github.com/Aebestach/Thunderbolt).

## Install

Copy `GameData/BlastFX` into your KSP `GameData` folder.

Requires **Harmony** (`GameData/000_Harmony`, usually already present via Community Fixes / Harmony).

## Difficulty setting

**Replace stock explosions** (default **off**): when enabled, `Part.explode()` is redirected through the BlastFX fireball. This affects collisions, engine failures, cheats, and other mods — leave it off unless you want that globally.

## API (for modders)

Namespace `BlastFX`, class `Blast`:

```csharp
// Visual only
Blast.Spawn(worldPos, size: 40f);

// Cover part with fireball, then silently destroy it
Blast.DestroyPart(part);

// Spawn at part; destroy == delete behind the fireball
Blast.SpawnAtPart(part, destroy: true);

// Explicit hit point (e.g. lightning strike tip)
Blast.SpawnAtPoint(hitPoint, part, destroy: true, plasma: boltColor);

bool ok = Blast.Ready;
bool hasGpu = Blast.HasShader;
```

Soft dependency: resolve `BlastFX.Blast` by reflection if you do not want a hard compile reference.

## Credits

Fireball look tuned for Thunderbolt lightning strikes; extracted as a shared library so any mod can reuse it.
