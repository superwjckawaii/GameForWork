# P2 Generated Asset Manifest

All raster assets in this directory were generated with the built-in OpenAI image generation tool on 2026-08-27 and copied into the Godot project. They have no runtime network dependency.

## Assets

- `characters/p2-character-grid.png`: transparent 5 by 2 source atlas with four hero equipment stages, one mercenary, three regular enemies, one elite and one gate boss. Runtime regions use measured non-overlapping source bounds instead of assuming equal generated spacing.
- `ui/p2-item-grid.png`: transparent 8 by 2 source atlas covering the nine P2 equipment categories and seven future-facing resource icons. Runtime regions use isolated source bounds.
- `town/military-town.png`: wide fixed military frontier town with forge, storage, inn, watchtower and portal.
- `combat/gate-ruins.png`: wide empty gate-ruin combat field designed to leave the central actor lane clear.
- `campaign/five-act-grid.png`: five-panel source atlas for the campaign route: ember camp, famine frontier, drowned city, lightless road and the realm beyond the gate.

## Prompt set and integration constraints

- Built-in mode was used for every asset.
- Visual direction: original dark-fantasy art with the compact readability of a taskbar game; no copied characters, interface or specific assets.
- Coarse hard pixel clusters, strong silhouettes, restrained 10 to 20 color palettes, dark one-pixel outlines, no text, logos or watermarks.
- Characters and icons were requested on transparent backgrounds with generous gutters; the accepted transparent character source is the first generated grid because a later simplification attempt painted a checkerboard instead of preserving alpha.
- Godot uses nearest-neighbor canvas filtering. Character and item atlas regions are explicitly clipped and do not cross the measured alpha gaps between neighboring subjects.

## Final structured prompts

1. Character atlas: exactly ten isolated dark-fantasy sprites in a 5 by 2 grid, four hero stages plus mercenary above and skeleton, plague hound, drowned cultist, elite and gate boss below; transparent background; coarse taskbar-scale pixel art.
2. Item atlas: exactly sixteen isolated icons in an 8 by 2 grid covering equipment, flasks, map, skill stone and town resources; transparent background; coarse silhouette-first pixel art.
3. Town: 16:9 fixed military frontier district at cold dusk, simple building silhouettes, amber windows and a muted cyan portal.
4. Combat: 16:9 empty basalt gate ruin, uncluttered central actor lane, cold navy/cyan palette and restrained corruption marks.
5. Campaign: exactly five equal environment thumbnails in a horizontal row, one landmark per act and a distinct cohesive act palette.
