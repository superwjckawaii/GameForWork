# GameForWork

一款面向 Windows PC 的单机挂机构筑游戏。玩家培养一名可深度构筑的主角、运营建立在古代门扉旁的城镇，并通过消耗地图持续探索破碎世界；金币是贯穿城镇升级、制作与交易的通用关键资源。

项目已完成 P0～P15 的首个完整可玩 Demo。玩家可以完成五幕主线和七座固定城区建设，进入 T1～T20 异界，体验裂渊、命能花园、赤誓/苍誓祭坛、百级突破与三阶段灰烬天垒攻坚；构筑系统包含 40 种装备底材、12 件规则传奇、1 件神话装备、10 个主动技能、18 个辅助技能、五类药剂、1,624 节点主天赋树和 360 节点异界树。

## 本地运行与验证

环境要求：Windows 10/11 x64、.NET 8 SDK、Godot 4.7.2 .NET/Mono。

```powershell
.\scripts\verify.ps1
.\scripts\verify.ps1 -Configuration Release
.\scripts\verify.ps1 -Launch
.\scripts\stability.ps1 -Mode Offline48h
.\scripts\package-demo.ps1
```

验证脚本依次执行依赖恢复、编译、单元测试、Godot 无界面导入和主场景启动检查。若 .NET SDK 或 Godot 不在已约定的本机位置，可分别通过 `DOTNET_BIN` 或 `GODOT_BIN` 指定可执行程序完整路径。

`stability.ps1` 还支持 `Visible` 与 `Tray` 两种长稳模式，默认运行两小时；`package-demo.ps1` 使用 Godot 的 Windows .NET 导出模板生成自包含 PCK 的便携 ZIP，输出到忽略版本控制的 `artifacts/`。

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
- [P0～P15 开发计划与协作流程](docs/DEVELOPMENT_PLAN.md)
- [首个完整可玩 Demo 路线图](docs/DEMO_ROADMAP.md)
- [P0 技术实现规格](docs/P0_SPECIFICATION.md)
- [P1 垂直切片规格](docs/P1_SPECIFICATION.md)
- [P2 纵向切片规格](docs/P2_SPECIFICATION.md)
- [P2 实现与验收说明](docs/P2_IMPLEMENTATION.md)
- [P3 空间场景规格](docs/P3_SPECIFICATION.md)
- [P4 空间群战与角色工坊规格](docs/P4_SPECIFICATION.md)
- [P5 异界派遣与构筑交互规格](docs/P5_SPECIFICATION.md)
- [P6 装备孔组、技能构筑与战斗报告规格](docs/P6_SPECIFICATION.md)
- [P7 技能交互与实时性能规格](docs/P7_SPECIFICATION.md)
- [P8 Demo 主旅程与首次体验规格](docs/P8_SPECIFICATION.md)
- [P9 固定城区、佣兵编队与金属仓规格](docs/P9_SPECIFICATION.md)
- [P10 异界终局、复杂天赋树与性能修复规格](docs/P10_SPECIFICATION.md)
- [P11 响应式桌面 UI 与交互性能规格](docs/P11_SPECIFICATION.md)
- [P12 正式地图实体、制作与远征方针规格](docs/P12_SPECIFICATION.md)
- [P14 异界机制、突破与攻坚规格](docs/P14_SPECIFICATION.md)
- [P15 Demo 封版与分发规格](docs/P15_SPECIFICATION.md)
- [待讨论与开发决策](docs/OPEN_QUESTIONS.md)
