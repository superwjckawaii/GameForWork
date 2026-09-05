# Monsters monster-family master

Mode: image generation using `actor-master.png` as the visual reference, followed by a transparency-only cleanup pass.

Prompt: Create exactly ten front-facing dark-fantasy pixel-art enemy bodies in a 5 x 2 grid. Columns are Rift Beasts, Life Garden Abominations, Red Oath Penal Legion, Blue Oath Star Retinue, and Fallen Banner Warhost; each column contains two distinct silhouettes. Match the existing low-density crisp pixel style, preserve large gutters and bottom-center anchors, and include no text, grid, border, shadow, or background.

The generated service preview encoded its transparency checker as light pixels. `scripts/prepare_monster_master.ps1` removes only neutral near-white background pixels and writes the production RGBA source used by the atlas builder.
