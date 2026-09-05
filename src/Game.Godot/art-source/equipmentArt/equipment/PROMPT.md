# EquipmentArt 统一装备母版生成记录

- 生成模式：OpenAI 内置 `imagegen`。
- 风格参考：原 Art `equipment-master.png`，仅用于像素密度、轮廓、材质和调色方向；EquipmentArt 所有装备均为重新生成的新图形。
- 输出约束：透明背景、无文字、无边框、固定等格、单格单物、图标不得跨格、低中高阶按材质与装饰递进。

## 母版分组

- `master-weapons.png`：3 列×8 行；剑、斧、锤/战锤。
- `master-armor.png`：4 列×9 行；胸甲、头盔、手套、鞋。
- `master-accessories.png`：5 列×8 行；盾牌、腰带、护符、戒指、药剂。
- `master-special-ranged.png`：5 列×6 行；弓、匕首、法杖、箭袋、法器。
- `master-special-class.png`：5 列×6 行；召唤法器、拳套、灵兽护符、符刃、构装核心。

## 核心提示词

> Use the reference only as the exact visual style guide. Create a brand-new transparent-background pixel-art equipment sprite master for a dark industrial fantasy action RPG. Use a strict equal-cell production grid, with no borders, grid lines, labels, text, numbers, or shadows outside cells. Put one centered isolated icon in every occupied cell with generous transparent gutter. Use crisp hard pixels, dark outlines, compact 32px-readable silhouettes, restrained iron, bronze, leather, cloth and gem palettes, and subtle higher-tier accents. Every occupied icon must be visibly unique, category-correct at a glance, and increasingly elaborate down each category column. Do not use smooth vector art or painterly blur.

首次生成的武器与护甲母版错误地烘入浅色棋盘格；在入库前仅对与单元格边界连通的浅色中性背景做了 Alpha 清理，没有重绘、缩放或移动图标。配件母版通过 `imagegen` 编辑直接获得真实 Alpha。运行时图集由 `scripts/build_equipment_assets.ps1` 按透明包围盒和类别占位规则确定性生成。
