# P31 素材清单

- `vfx/p31-combat-vfx.png`：4×4、每格 64×64 的精细像素战斗特效图集。
- 原始生成母图保存在 `art-source/p31/imagegen/p31-vfx-master.png`，发布时不直接读取。
- `scripts/build_p31_assets.ps1` 对每个源格独立计算透明边界并留出固定内边距，防止相邻素材串入。
- `scripts/verify_p31_assets.ps1` 检查尺寸、16 格非空与 SHA-256，作为构建和发布门禁。

母图由 OpenAI ImageGen 生成，模式为全新生成；运行时图集经过确定性裁切与最近邻缩放。
