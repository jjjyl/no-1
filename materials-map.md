# World Materials Map

This document maps every material slot in `WorldMaterials.cs` to its visual role, scene location, and planned future texture. Artists use this as a reference when creating replacement textures. No code changes are needed when swapping a solid-color material for a textured one: just place the PNG in `res://assets/texture/world/` and update the material factory call.

---

## Material Slot Table

### Ground

The base terrain plane. Only one material here because the ground uses procedural noise rather than a flat color.

| Slot Name    | Color (R,G,B)    | Intended Visual Role                          | Usage Location in Scene                           | Future Texture                                 |
|-------------|------------------|-----------------------------------------------|---------------------------------------------------|------------------------------------------------|
| GrassBase   | procedural noise | World floor terrain with organic green-brown texture | The main `Ground` mesh covering the entire map area | `res://assets/texture/world/grass_base.png`    |

### Zone Plates

Flat colored plates attached to a `Zone` node. Each zone gets a distinct hue so players can tell regions apart at a glance. Hover/click highlights test against these materials.

| Slot Name         | Color (R,G,B)    | Intended Visual Role                          | Usage Location in Scene                           | Future Texture                                     |
|-------------------|------------------|-----------------------------------------------|---------------------------------------------------|----------------------------------------------------|
| ZoneForest        | (0.14, 0.33, 0.14) | Dense forest region, dark green              | Zone plate mesh attached to the Forest zone node   | `res://assets/texture/world/zone_forest.png`        |
| ZoneMine          | (0.28, 0.25, 0.22) | Underground mine entrance, earthy brown-grey | Zone plate mesh attached to the Mine zone node     | `res://assets/texture/world/zone_mine.png`          |
| ZoneCliff         | (0.38, 0.33, 0.18) | Rocky cliff highlands, warm ochre            | Zone plate mesh attached to the Cliff zone node    | `res://assets/texture/world/zone_cliff.png`         |
| ZoneBattlefield   | (TBD)            | Combat arena, blood-and-iron tones           | Zone plate mesh for the Battlefield zone (Task B)  | `res://assets/texture/world/zone_battlefield.png`   |
| ZoneCrystal       | (TBD)            | Crystal cavern, cool blue-white shimmer      | Zone plate mesh for the Crystal zone (Task B)      | `res://assets/texture/world/zone_crystal.png`       |
| ZoneWasteland     | (TBD)            | Desolate wasteland, sandy grey-brown         | Zone plate mesh for the Wasteland zone (Task B)    | `res://assets/texture/world/zone_wasteland.png`     |
| ZoneTower         | (TBD)            | Mysterious tower, dark stone and shadow      | Zone plate mesh for the Tower zone (Task B)        | `res://assets/texture/world/zone_tower.png`         |
| ZoneSpring        | (TBD)            | Healing spring, soft aqua-green              | Zone plate mesh for the Spring zone (Task B)       | `res://assets/texture/world/zone_spring.png`        |

### Paths

Narrow connecting meshes between zone plates. Each path has its own material so it can darken or lighten independently from the zones it links.

| Slot Name  | Color (R,G,B)    | Intended Visual Role                          | Usage Location in Scene                           | Future Texture                                 |
|-----------|------------------|-----------------------------------------------|---------------------------------------------------|------------------------------------------------|
| Path01    | (0.42, 0.33, 0.18) | Road from Zone 0 to Zone 1, warm tan        | Path mesh spanning the Zone0-Zone1 connection      | `res://assets/texture/world/path01.png`        |
| Path12    | (0.38, 0.30, 0.15) | Road from Zone 1 to Zone 2, darker brown    | Path mesh spanning the Zone1-Zone2 connection      | `res://assets/texture/world/path12.png`        |
| Path34    | (TBD)            | Road from Zone 3 to Zone 4 (Task B)          | Path mesh spanning the Zone3-Zone4 connection      | `res://assets/texture/world/path34.png`        |

### Background

Large backdrop elements behind the play area. These are always visible and create depth. `DragonShadow` uses alpha transparency rather than a solid fill.

| Slot Name     | Color (R,G,B,A)     | Intended Visual Role                          | Usage Location in Scene                           | Future Texture                                     |
|---------------|---------------------|-----------------------------------------------|---------------------------------------------------|----------------------------------------------------|
| Sky           | (0.18, 0.28, 0.45)  | Sky dome or backdrop panel, muted blue       | Backdrop mesh behind the ground plane              | `res://assets/texture/world/sky.png`                |
| Sun           | (1.00, 0.85, 0.50)  | Sun disc or light source, warm gold          | Sun fixture floating above the backdrop            | `res://assets/texture/world/sun.png`                |
| Mountain      | (0.10, 0.12, 0.18)  | Distant mountain silhouette, dark navy-grey  | Mountain mesh placed behind the ground             | `res://assets/texture/world/mountain.png`           |
| DragonShadow  | (0.00, 0.00, 0.00, 0.25) | Flying dragon shadow overlay, semi-transparent black | Large shadow quad above the world map        | `res://assets/texture/world/dragon_shadow.png`      |

### Decorations

Small props scattered across the map for visual variety. They do not affect gameplay. All use solid unshaded color for now.

| Slot Name | Color (R,G,B)    | Intended Visual Role                          | Usage Location in Scene                           | Future Texture                                 |
|----------|------------------|-----------------------------------------------|---------------------------------------------------|------------------------------------------------|
| DecoTree | (0.15, 0.40, 0.10) | Tree prop, bright leaf-green                | Tree decoration instances around zone edges        | `res://assets/texture/world/deco_tree.png`     |
| DecoRock | (0.35, 0.33, 0.30) | Rock prop, neutral grey-brown               | Rock decoration instances scattered on the ground  | `res://assets/texture/world/deco_rock.png`     |
| DecoRuin | (0.28, 0.24, 0.20) | Ruin prop, weathered brown                  | Ruin decoration instances near older zones         | `res://assets/texture/world/deco_ruin.png`     |

### Markers

Small indicator dots or shapes that sit on top of the map to show player position, enemy locations, and other points of interest. These are always rendered above ground geometry.

| Slot Name      | Color (R,G,B)    | Intended Visual Role                          | Usage Location in Scene                           | Future Texture                                       |
|----------------|------------------|-----------------------------------------------|---------------------------------------------------|------------------------------------------------------|
| EnemyDot       | (0.80, 0.30, 0.30) | Enemy location marker, bright red           | Small disc or sphere at each enemy position         | `res://assets/texture/world/enemy_dot.png`            |
| PlayerBody     | (0.30, 0.50, 0.90) | Player character body, cornflower blue      | Player mesh traversing the map                     | `res://assets/texture/world/player_body.png`          |
| CompanionDot   | (TBD)            | Companion location marker (Task B)            | Small disc at companion position                   | `res://assets/texture/world/companion_dot.png`        |
| ShopMarker     | (TBD)            | Shop / vendor location indicator (Task B)     | Small shape at each shop location                  | `res://assets/texture/world/shop_marker.png`          |

---

## New Slots from Task B

These eight slots do not exist in `WorldMaterials.cs` yet. They will be added as companion work (Task B) and share the same factory helpers. Colors are marked TBD; final values depend on the artist's palette direction.

| Slot Name        | Suggested Color         | Section      | Notes                                        |
|------------------|-------------------------|--------------|----------------------------------------------|
| ZoneBattlefield  | reddish-brown           | Zone Plates  | Distinct enough from ZoneMine and ZoneCliff   |
| ZoneCrystal      | cool blue-white         | Zone Plates  | Should read as crystalline at a distance      |
| ZoneWasteland    | sandy grey-brown        | Zone Plates  | Muted, no strong saturation                   |
| ZoneTower        | dark stone-grey         | Zone Plates  | Somber tone, contrasts with bright zones      |
| ZoneSpring       | soft aqua-green         | Zone Plates  | Feels lush and restorative                    |
| Path34           | mid-brown               | Paths        | Sits between Path01 and Path12 in value       |
| CompanionDot     | bright green            | Markers      | Friendly contrast to the red EnemyDot         |
| ShopMarker       | gold/amber              | Markers      | Reads as valuable or interactive              |

---

## Texture Replacement Guide

### Material Factory Helpers

`WorldMaterials.cs` exposes three static factory methods. All return an unshaded `StandardMaterial3D`:

- **`MakeFlat(string name, float r, float g, float b)`**
  Fills the material with a single solid RGB color. Used by every slot except `GrassBase` and `DragonShadow`. This is the starting point: quick to build, easy to identify each material on screen.

- **`MakeTextured(string name, string path)`**
  Loads a `Texture2D` from disk and sets it as the albedo. Call this instead of `MakeFlat` when you have a finished texture ready. The material stays unshaded.

- **`MakeTransparent(string name, float r, float g, float b, float a)`**
  Same as `MakeFlat` but with an alpha channel and transparency enabled. Currently used only by `DragonShadow`.

### Swapping a Solid Color for a Texture

To replace any flat material with a texture, follow these steps inside `BuildAll()`:

1. Drop the texture file into `res://assets/texture/world/`.
2. Find the line that currently calls `MakeFlat` for that slot. For example:
   ```csharp
   ZoneForest = MakeFlat("ZoneForest", 0.14f, 0.33f, 0.14f);
   ```
3. Replace it with a `MakeTextured` call:
   ```csharp
   ZoneForest = MakeTextured("ZoneForest", "res://assets/texture/world/zone_forest.png");
   ```
4. No other file needs to change. Every `Build*` script references `WorldMaterials.Instance.ZoneForest` directly, so the property name stays the same regardless of whether it uses a color or a texture.

### What Does Not Change

- **Property names** (`GrassBase`, `ZoneForest`, `Path01`, etc.) remain the same across both color and texture builds.
- **`Build*` scripts** in `scripts/world/build/` reference these properties by name. They never call `MakeFlat` or `MakeTextured` themselves.
- **Shading mode** stays unshaded for all materials. If lighting or normal maps are added later, that is a shader-level change, not a material-slot change.

### Material Count Summary

| Section      | Existing | New (Task B) | Total |
|-------------|----------|--------------|-------|
| Ground      | 1        | 0            | 1     |
| Zone Plates | 3        | 5            | 8     |
| Paths       | 2        | 1            | 3     |
| Background  | 4        | 0            | 4     |
| Decorations | 3        | 0            | 3     |
| Markers     | 2        | 2            | 4     |
| **Total**   | **15**   | **8**         | **23** |
