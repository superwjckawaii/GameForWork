# GameForWork

一款面向 Windows PC 的单机挂机构筑游戏。玩家培养一名可深度构筑的主角、运营建立在古代门扉旁的城镇，并通过消耗地图与远征补给持续探索破碎世界。

项目已完成 P0～P5，实现五幕主线、确定性空间群战、单页双队伍远征、地图与 Boss 碎片仓库、集群专精、安全的图连通洗点和金属通货工坊。P6 已完成设计确认，将把固定技能链升级为装备实例孔组，并补齐技能构筑、AI、制作与战斗报告闭环。

## 本地运行与验证

环境要求：Windows 10/11 x64、.NET 8 SDK、Godot 4.7.2 .NET/Mono。

```powershell
.\scripts\verify.ps1
.\scripts\verify.ps1 -Configuration Release
.\scripts\verify.ps1 -Launch
```

验证脚本依次执行依赖恢复、编译、单元测试、Godot 无界面导入和主场景启动检查。若 .NET SDK 或 Godot 不在已约定的本机位置，可分别通过 `DOTNET_BIN` 或 `GODOT_BIN` 指定可执行程序完整路径。

## 当前设计支柱

- 正常窗口用于构筑、城镇管理和策略配置，小窗口用于长期观察与低频交互。
- 每张普通地图约 1～3 分钟，支持最多 48 小时离线结算。
- 构筑深度参考刷宝 ARPG：共享被动树、技能与辅助组合、装备词缀、制作、异界地图和玩法专精。
- 主角是完全透明、由玩家精确配置的构筑；佣兵是拥有自主成长和战斗行为的独立角色。
- 黑暗奇幻为基础风格，后续可以通过世界碎片扩展其他主题。
- 买断制单机、无广告、无内购，首版不依赖联网和玩家交易。

## 文档

- [已确认游戏设计基线](docs/GAME_DESIGN_BASELINE.md)
- [已确认技术架构](docs/TECHNICAL_ARCHITECTURE.md)
- [P0～P6 开发计划与协作流程](docs/DEVELOPMENT_PLAN.md)
- [P0 技术实现规格](docs/P0_SPECIFICATION.md)
- [P1 垂直切片规格](docs/P1_SPECIFICATION.md)
- [P2 纵向切片规格](docs/P2_SPECIFICATION.md)
- [P2 实现与验收说明](docs/P2_IMPLEMENTATION.md)
- [P3 空间场景规格](docs/P3_SPECIFICATION.md)
- [P4 空间群战与角色工坊规格](docs/P4_SPECIFICATION.md)
- [P5 异界派遣与构筑交互规格](docs/P5_SPECIFICATION.md)
- [P6 装备孔组、技能构筑与战斗报告规格](docs/P6_SPECIFICATION.md)
- [待讨论与开发决策](docs/OPEN_QUESTIONS.md)
