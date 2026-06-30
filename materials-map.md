# World Materials Map

Last updated: 2026-06-29. This is the **single canonical reference** for all visual materials, textures, and decoration assets in the 3D world map.

---

## Texture Override System

Drop pixel-art PNG files into `res://assets/texture/world/` and restart. **Zero code changes.**

Every material and decoration checks for an override file at startup. If found → loaded. If not → procedural pixel-art fallback is generated.

Logic in both `WorldMaterials.cs` and `WorldMap3D.cs`:

```
Texture = TryLoadTexture("res://assets/texture/world/xxx.png") ?? ProceduralFallback();
```

---

## Material Slots (23 total, all PerPixel)

### Ground (1)

| Slot | Override File | Fallback | Roughness |
|------|--------------|----------|-----------|
| GrassBase | `grass_base.png` | 64px FastNoiseLite (5-level quantised) | 0.9 |

### Zone Plates (8)

All use Bayer-ordered dither textures (50% checker/stripe/diagonal patterns), `Roughness=0.9`.

| Slot | Override File | Colour |
|------|--------------|--------|
| ZoneForest | `zone_forest.png` | (0.14, 0.33, 0.14) checker |
| ZoneMine | `zone_mine.png` | (0.28, 0.25, 0.22) horizontal stripe |
| ZoneCliff | `zone_cliff.png` | (0.38, 0.33, 0.18) vertical stripe |
| ZoneBattlefield | `zone_battlefield.png` | (0.35, 0.30, 0.22) diagonal |
| ZoneCrystal | `zone_crystal.png` | (0.40, 0.45, 0.55) checker |
| ZoneWasteland | `zone_wasteland.png` | (0.30, 0.20, 0.15) horizontal |
| ZoneTower | `zone_tower.png` | (0.18, 0.16, 0.24) vertical |
| ZoneSpring | `zone_spring.png` | (0.18, 0.38, 0.35) diagonal |

### Paths (3)

All share the same override file. `Roughness=0.9`.

| Slot | Override File | Lighter Edge Colour |
|------|--------------|---------------------|
| Path01 | `path_dirt.png` | (0.42, 0.33, 0.18) / (0.52, 0.42, 0.25) |
| Path12 | `path_dirt.png` | (0.38, 0.30, 0.15) / (0.48, 0.39, 0.22) |
| Path34 | `path_dirt.png` | (0.40, 0.32, 0.20) / (0.50, 0.41, 0.27) |

### Background (4)

Sky/Sun/Mountain have transparent backgrounds for layering. `ShadingMode=PerPixel`.

| Slot | Override File | Fallback Description |
|------|--------------|---------------------|
| Sky | `sky_gradient.png` | 8-band vertical gradient (dark→light blue) |
| Sun | `sun.png` | Pixel-art circle with 8-ray dither edge, 32px |
| Mountain | `mountain.png` | Jagged pixel silhouette with snow caps |
| DragonShadow | `dragon_shadow.png` | Bat-wing silhouette, α=0.35, 64x20px |

### Decorations (3)

These are material definitions used by `WorldMap3D.ScatterDecorations()`. The actual sprites use separate override paths (see Decorations section below). `Roughness=0.9`.

| Slot | Override File | Fallback |
|------|--------------|----------|
| DecoTree | `deco_tree.png` | Diagonal cross-hatch (tree canopy) |
| DecoRock | `deco_rock.png` | 4-level random speckle |
| DecoRuin | `deco_ruin.png` | Offset brick rows with mortar lines |

### Markers (4)

`Roughness=0.5` (slight shine to stand out from terrain).

| Slot | Override File | Fallback Icon |
|------|--------------|---------------|
| EnemyDot | `marker_enemy.png` | Skull (eye sockets + teeth), 16px |
| PlayerBody | `marker_player.png` | Diamond with dithered fill + bright core, 16px |
| CompanionDot | `marker_companion.png` | Heart shape, 16px |
| ShopMarker | `marker_shop.png` | Coin circle with checker-dither + centre band, 16px |

---

## Decorations & Sprites (WorldMap3D.cs)

These are `Sprite3D` billboard objects with procedural textures that also support override files.

### Trees

- **Code**: `MakeTree()` → single `Sprite3D`, 24x32px combined tree texture
- **Override**: `deco_tree.png`
- **Variation**: +/-10 random rotation, scale 0.4-1.3

### Rocks (3 variants)

- **Code**: `MakeRock()` → 16x16px procedural rock (3 grey shades)
- **Overrides**: `deco_rock_0.png`, `deco_rock_1.png`, `deco_rock_2.png`
- **Variation**: 3 shape variants, scale 0.2-0.8

### Ruins (2 variants)

- **Code**: `MakeRuin()` → 16x24px broken pillar/arch with brick lines
- **Overrides**: `deco_ruin_0.png`, `deco_ruin_1.png`

### Bushes

- **Code**: `MakeBush()` → 8x8px round pixel cluster, 2 green shades
- **Override**: `deco_bush.png`

### Grass Tufts

- **Code**: Scattered 120 billboard sprites across the world
- **Override**: `grass_tuft.png`
- **Variation**: random scale 0.7-1.3, colour (0.18, 0.42, 0.12)

---

## Parallax Background Layers (WorldMap3D.BuildParallax)

Multi-layer Sprite3D billboards at varying Z-depths.

| Layer | Z | Override File | Description |
|-------|---|---------------|-------------|
| Sky gradient | -15 | `sky_gradient.png` | 256x128 gradient texture, full-screen |
| Clouds (3-5) | -12 | `cloud.png` | 48x24 white pixel clusters, random positions |
| Far mountains | -8 | `mountain_far.png` | 256x48 jagged silhouette with snow caps |
| Near mountains | -5 | `mountain_near.png` | 256x40 darker silhouette |
| Sun | -13 | `sun.png` | 32x32 pixel-art circle, upper-right |
| Dragon shadow | -4 | `dragon_shadow.png` | 64x20 winged silhouette, tween-animated |

---

## Particle Effects (WorldMap3D.BuildParticles)

GPUParticles3D, zone-specific. Not overridable by files — code-only.

| Zone | Type | Count | Colour | Behaviour |
|------|------|-------|--------|-----------|
| Forest (zone 0) | Falling leaves | 30 | Green (0.22,0.58,0.16,0.85) | Gravity -0.45, wind drift 0.15x |
| Mine (zone 1) | Dust motes | 20 | Warm grey (0.55,0.48,0.38,0.45) | No gravity, slow random movement |
| Crystal (zone 4) | Sparkles | 15 | Blue-white (0.60,0.85,1.0,0.90) | Sphere emission, short lifetime 0.5-1.5s |

---

## Zone Border Blending

Semi-transparent `QuadMesh` strips placed between connected zones. `α=0.06`. Code-only, not overridable.

---

## File Count Summary

| Category | Override Files |
|----------|---------------|
| Ground | 1 |
| Zone plates | 8 |
| Paths | 1 (shared) |
| Background | 4 |
| Decorations (materials) | 3 |
| Markers | 4 |
| Decoration sprites | 7 (1 tree + 3 rock + 2 ruin + 1 bush) |
| Grass tuft | 1 |
| Parallax layers | 5 (cloud + 2 mountain + sun + dragon) |
| **Total unique paths** | **34** |

---

## Quick Start for Artists

1. Create pixel-art PNG (16x16 to 64x64, nearest-neighbour filtered)
2. Save to `res://assets/texture/world/` with the exact filename from the table above
3. Restart the game
4. Missing files silently fall back to procedural textures — no broken visuals

### Which files to replace first (highest visual impact)

1. `grass_base.png` — covers the entire world floor
2. `zone_forest.png` through `zone_spring.png` — 8 zone identity colours
3. `sky_gradient.png` — background atmosphere
4. `deco_tree.png` — trees are the most numerous decoration
5. `cloud.png` — adds depth to the sky
