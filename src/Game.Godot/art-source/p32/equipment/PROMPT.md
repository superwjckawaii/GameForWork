# P32 装备美术母版生成记录

- 生成模式：OpenAI 内置 `imagegen`。
- 风格参考：P25 的武器、防具、配件与特殊职业母版，仅用于像素密度、轮廓、材质和调色方向。
- 输出约束：透明背景、无文字/边框/格线、严格等格、单格单物、32px 可读轮廓；生成服务烘入的浅色中性棋盘格由构建脚本在生产图集中转为 Alpha。

## 母版与格位

- `master-warfront-and-core.png`：4×6；6戒指、6护符、6腰带、三御圣铠、灵障法器。
- `master-spirit-barrier.png`：13×4；灵障衣/头/手/鞋/盾/法器、三类灵障混合防具及盾牌。
- `master-spirit-shields.png`：3×1；补齐三类巅峰灵障混合盾牌。
- `master-weapons.png`：7×4；单手剑/斧/锤、双手剑/斧/锤、弓。
- `master-special-weapons.png`：4×4；匕首、法杖、符刃、徒手拳铠。
- `master-legendary.png`：6×5；第26～55件传奇/神话，按正式目录顺序逐行排列。

核心提示词沿用 P25：dark industrial fantasy action RPG、crisp hard-edged pixel art、strong dark outlines、restrained iron/bronze/leather/cloth/gem palette、one centered isolated icon per occupied cell、genuinely transparent background、no text/grid/border/watermark。

运行 `scripts/build_p25_equipment_assets.ps1` 会按稳定目录顺序生成 13×19 的244底材图集和5×11的55传奇图集，并审计空格、边界接触和重复图标。
