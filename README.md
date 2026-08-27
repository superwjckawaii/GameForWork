# GameForWork

一款面向 Windows PC 的单机挂机构筑游戏。玩家培养一名可深度构筑的主角、运营建立在古代门扉旁的城镇，并通过消耗地图与远征补给持续探索破碎世界。

项目已完成 P0 技术原型实现，正在等待统一人工验收。P0 用确定性自动战斗验证权威模拟、回放哈希、SQLite 存档、48 小时离线结算、窗口/托盘和结构化日志；正式玩法与精细像素素材从 P1 开始接入。

## 本地运行与验证

环境要求：Windows 10/11 x64、.NET 8 SDK、Godot 4.7.2 .NET/Mono。

```powershell
.\scripts\verify.ps1
.\scripts\verify.ps1 -Configuration Release
.\scripts\verify.ps1 -Launch
```

验证脚本依次执行依赖恢复、编译、单元测试、Godot 无界面导入和主场景启动检查。若 Godot 不在已约定的本机位置，可通过 `GODOT_BIN` 指定控制台程序完整路径。

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
- [P0/P1 开发计划与协作流程](docs/DEVELOPMENT_PLAN.md)
- [P0 技术实现规格](docs/P0_SPECIFICATION.md)
- [P1 垂直切片规格](docs/P1_SPECIFICATION.md)
- [待讨论与开发决策](docs/OPEN_QUESTIONS.md)
