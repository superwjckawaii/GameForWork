# Art 正式像素资产清单

> 生成模式：OpenAI 内置 imagegen 用于原创视觉语言母版；生产图集由脚本固定尺寸整理。当前三类天赋底图与战斗特效由 Presentation 资产清单管理。

## 生产图集

| 路径 | 网格/规格 | 内容 |
|---|---:|---|
| `characters/art-actor-animation.png` | 31×20，48×64/格 | 5套主角/佣兵、四方向、六类动作 |
| `enemies/art-enemy-animation.png` | 31×64，48×64/格 | 16套怪物主体、四方向、六类动作 |
| `enemies/art-boss-animation.png` | 31×48，72×80/格 | 12套Boss、四方向、六类动作 |
| `ui/art-skill-gems.png` | 10×8，32×32/格 | 30主动与48辅助技能石 |
| `ui/art-metal-atlas.png` | 5×4，32×32/格 | 19种金属 |
| `regions/art-region-atlas.png` | 4×3，256×144/格 | 12个区域 |
| `town/art-building-atlas.png` | 4×2，160×120/格 | 7座固定建筑 |
| `ui/art-ui-skin.png` | 4×2，64×32/格 | 面板、按钮、输入与弹窗九宫格皮肤 |
| `brand/art-app-icon.png` | 256×256 | 暗门与金色余烬正式图标 |
| `brand/art-app-icon.ico` | 16～256 px | Windows 多尺寸程序图标 |
| `brand/art-tray-*.png` | 32×32 | 运行、等待、暂停、异常托盘状态 |

## 内置 imagegen 提示词摘要

- 角色/怪物：原创黑暗奇幻、Taskbar 小窗密度、1 px 强轮廓、透明背景、严格等格、脚底统一、无相邻污染。
- 技能石：主动菱形、辅助圆形、78枚独立中央符文，按伤害与标签统一配色。
- 区域：12个原创黑暗奇幻战场，低细节等距地块，角色与预兆优先。
- 建筑：七座固定城区建筑，低密度等距像素语言。
- 品牌：封闭暗铁门、中央金色余烬、强轮廓、透明背景，缩至16px仍可识别。
- UI：暗铁面板、旧金交互态、钢蓝禁用态、无文字的九宫格组件母版。
Art.1 保留技能石生成中间件、UI 与品牌资源；装备与传奇图集已经迁移到 EquipmentArt，珠宝采用当前运行时绘制，天赋与战斗特效迁移到 Presentation，避免新旧图集并存。

所有母版均无外部游戏素材输入；Taskbar Hero 与 PoE 只用于设计方向说明，不复制其图像、名称或标志。
