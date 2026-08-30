# P25 equipment icon generation record

- Mode: built-in image generation with local style reference.
- Style reference: `src/Game.Godot/assets/p21/ui/p21-item-bases.png`.
- Generated master: `p25-equipment-category-master.png`.
- Runtime atlas: `src/Game.Godot/assets/p25/ui/p25-equipment-icons.png`.
- Post-process: `scripts/build_p25_equipment_atlas.ps1` crops ten transparent category masters and creates six deterministic material tiers at 32×32 per cell.

Prompt:

> Create exactly ten distinct dark-fantasy action-RPG equipment icons in the supplied pixel-art style, arranged as a five-column by two-row transparent contact sheet. Top row: longbow, dagger, arcane wand, leather arrow quiver with visible arrows, crystalline caster focus. Bottom row: bone-and-soul summoning focus, cloth unarmed hand wraps, beast-fang talisman necklace, rune-engraved one-handed blade, brass mechanical construct idol. Use crisp black outlines, dark iron, aged brass and restrained jewel colors. Keep one centered isolated icon per equal cell with transparent gutters. No borders, text, watermark, duplicate, shield, boot, helmet, potion, ring or unrelated object.
