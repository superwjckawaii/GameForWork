# GameForWork

一款面向 Windows PC 的单机挂机构筑游戏。玩家培养一名可深度构筑的主角、运营建立在古代门扉旁的城镇，并通过消耗地图持续探索破碎世界；金币是贯穿城镇升级、制作与交易的通用关键资源。

项目已完成 P0～P31 的 `v0.4.0` 候选实现。玩家可以完成五幕主线和七座固定城区建设，进入 T1～T20 异界，体验裂渊、命能花园、赤誓/苍誓祭坛、亡旗战阵、百级突破与固定 Boss 攻坚；构筑系统包含 86 个主动技能、98 个辅助技能、正式装备与词缀库、1475 节点主天赋树、十八升华、珠宝、美德/恶德与 360 节点异界树。任务栏小窗、数据驱动技能表现、分色伤害数字和低密度精细像素素材均已接入。

## 本地运行与验证

当前封版目标为 v0.4.0。P29 完成掉落经济，P30 完成战斗数值、天赋、升华、珠宝和 36 套构筑验证，P31 完成战斗表现、性能门禁与便携发布。沉金港和无光矿脉延期到 v0.5。

环境要求：Windows 10/11 x64、.NET 8 SDK、Godot 4.7.2 .NET/Mono。

```powershell
.\scripts\verify.ps1
.\scripts\verify.ps1 -Configuration Release
.\scripts\verify.ps1 -Launch
.\scripts\stability.ps1 -Mode Offline48h
.\scripts\package-demo.ps1
```

验证脚本依次执行素材审计、依赖恢复、编译、单元测试、Godot 无界面导入和主场景启动检查。Godot 即使返回退出码 0，只要输出 `ERROR:`、脚本错误或结构化 Error 日志，门禁仍会失败。若 .NET SDK 或 Godot 不在已约定的本机位置，可分别通过 `DOTNET_BIN` 或 `GODOT_BIN` 指定可执行程序完整路径。

`stability.ps1` 还支持 `Visible` 与 `Tray` 两种长稳模式，默认运行两小时；`package-demo.ps1` 使用 Godot 的 Windows .NET 导出模板生成自包含 PCK 的便携 ZIP，输出到忽略版本控制的 `artifacts/`。

## 当前设计支柱

- 正常窗口用于构筑、城镇管理和策略配置，小窗口用于长期观察与低频交互。
- 每张普通地图约 1～3 分钟，支持最多 48 小时离线结算。
- 构筑深度参考刷宝 ARPG：共享被动树、技能与辅助组合、装备词缀、制作、异界地图和玩法专精。
- 主角是完全透明、由玩家精确配置的构筑；佣兵是拥有自主成长和战斗行为的独立角色。
- 黑暗奇幻为基础风格，后续可以通过世界碎片扩展其他主题。
- 买断制单机、无广告、无内购，首版不依赖联网和玩家交易。

## 文档

- [文档总索引](docs/README.md)
- [已确认游戏设计基线](docs/shared/GAME_DESIGN_BASELINE.md)
- [P0～P22 开发计划与协作流程](docs/shared/DEVELOPMENT_PLAN.md)
- [v0.1 Demo 路线与历史规格](docs/v0.1/DEMO_ROADMAP.md)
- [v0.2 核心战斗与构筑扩充规格](docs/v0.2/V0_2_SPECIFICATION.md)
- [P16 怪物、等级、仓库与过滤器规格](docs/v0.2/P16_SPECIFICATION.md)
- [P21 像素美术、逐帧动画与稳定图集规格](docs/v0.2/P21_SPECIFICATION.md)
- [P22 整合、平衡与 v0.2 封版规格](docs/v0.2/P22_SPECIFICATION.md)
- [v0.2.0 版本说明](docs/v0.2/V0_2_RELEASE_NOTES.md)
- [v0.4 总规格](docs/v0.4/V0_4_SPECIFICATION.md)
- [P31 表现、性能与发布封版规格](docs/v0.4/P31_SPECIFICATION.md)
- [v0.4.0 版本说明](docs/v0.4/V0_4_RELEASE_NOTES.md)
