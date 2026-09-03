# P31 素材清单

- `vfx/p31-combat-vfx.png`：4×4、每格 64×64 的精细像素战斗特效图集。
- 原始生成母图保存在 `art-source/p31/imagegen/p31-vfx-master.png`，发布时不直接读取。
- `scripts/build_p31_assets.ps1` 对每个源格独立计算透明边界并留出固定内边距，防止相邻素材串入。
- `scripts/verify_p31_assets.ps1` 检查尺寸、16 格非空与 SHA-256，作为构建和发布门禁。
- `trees/p31-passive-backdrop.png`：2048×2048 主天赋底盘；母图旋转为与节点布局一致的纵向正六边形，底纹之上按当前 1475 节点数据生成低亮度结构轨道，不烘焙会与运行时重复的节点圆。
- `trees/p31-atlas-backdrop.png`：2048×2048 地图天赋底盘；10 条路线按 `P10AtlasTree.LayoutExtent` 与运行时节点使用同一坐标范围。
- `trees/ascendancy/*.png`：18 张独立的 768×768 升华底盘；按 `(0,±190)`、`(±165,±95)` 六个核心节点重建纵向正六边形，以中性暗色表达12节点六向结构，职业色仅由运行时节点表达。
- `P31TreeProjection` 是美术烘焙、运行时绘制和坐标测试共用的唯一投影契约；导出数据同时携带世界坐标与归一化坐标。
- `scripts/build_p31_tree_assets.ps1` 从三张 ImageGen 母图与 `P21TreeExport` 归一化坐标输出全部正式底盘；美术层不再重复计算世界坐标或维护手工位置。

母图由 OpenAI ImageGen 生成，模式为全新生成；运行时图集经过确定性裁切与最近邻缩放。
