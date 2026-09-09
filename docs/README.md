# GameForWork 文档索引

文档按照产品版本保存。跨版本长期有效的设计和技术约束位于 `shared/`；已经交付的版本规格只记录历史，不用新版本决策反向改写。

## 共享文档

- [游戏设计基线](shared/GAME_DESIGN_BASELINE.md)
- [技术架构](shared/TECHNICAL_ARCHITECTURE.md)
- [开发计划与当前版本入口](shared/DEVELOPMENT_PLAN.md)
- [待讨论与开发决策](shared/OPEN_QUESTIONS.md)

## v0.5（当前规划）

- [v0.5 六阶段开发计划：沉金港与全量动画优化](v0.5/V0_5_DEVELOPMENT_PLAN.md)
- [沉金港与统一宝箱：第一阶段确认记录](v0.5/Harbor_REWARD_DECISIONS.md)
- [沉金港装备设计目录](v0.5/Harbor_EQUIPMENT_DESIGN.md)：18 件底材、8 件传奇核心机制与数值范围已确认，基础面板与完整固定词缀待补齐。
- [统一宝箱资源结算分类表](v0.5/Harbor_RESOURCE_SETTLEMENT.md)：分类与特殊来源边界已统一确认，全部写入调用点仍待审计，尚未实现。
- [全量动画审核清单](v0.5/V0_5_ANIMATION_AUDIT.md)与[逐项索引](v0.5/V0_5_ANIMATION_INVENTORY.md)：已枚举 86 主动、98 辅助、80 普通敌人、24 Boss；源码问题已记录，实机画面和性能尚未验收。
- [第一阶段集中收口检查](v0.5/V0_5_STAGE_ONE_CHECKPOINT.md)：第一阶段交付完成，含失败前测证据及第二阶段接续顺序。
- [装备实现参数](v0.5/Harbor_IMPLEMENTATION_PARAMETERS.md)、[流程初始配置](v0.5/Harbor_FLOW_PARAMETERS.md)、[结算调用点审计](v0.5/Harbor_SETTLEMENT_CALLSITE_AUDIT.md)。
- [辅助与Boss子项](v0.5/V0_5_ANIMATION_SUBITEMS.md)、[七组合实机初测](v0.5/V0_5_VISUAL_BASELINE.md)。
- 第一阶段规格、结算审计、动画清单与20组代表场景前测已交付。前测记录长帧及音频设备错误，不代表性能验收通过；第二阶段流程原型已接入，下一步第三阶段完整内容。
- [沉金港第二阶段实现与接续](v0.5/Harbor_STAGE_TWO_CHECKPOINT.md)：旧机制修复延后至v0.5最后、发布验收前。
- 本目录中的装备与实现复核文件属于历史审计，不等同于 v0.5 新增目标。

## v0.1

- [首个完整可玩 Demo 路线图](v0.1/DEMO_ROADMAP.md)
- `v0.1/` 内保存 Foundation～SimulationParity 的阶段规格、实现记录和历史 UI 待办。

## v0.2

- [v0.2 总规格](v0.2/V0_2_SPECIFICATION.md)
- [Inventory 怪物、等级、仓库与过滤器规格](v0.2/Inventory_SPECIFICATION.md)
- [SkillCatalog 技能石底层与内容规格](v0.2/SkillCatalog_SPECIFICATION.md)
- [Ascendancies 铁誓者三升华与资源循环规格](v0.2/Ascendancies_SPECIFICATION.md)
- [EquipmentImport 装备底材与基础词缀库规格](v0.2/EquipmentImport_SPECIFICATION.md)
- [Economy 掉落公式与经济规格](v0.2/Economy_SPECIFICATION.md)
- [Economy 经济蒙特卡洛审计](v0.2/Economy_ECONOMY_AUDIT.md)
- [Economy.5 主天赋树 V2 规格](v0.2/PassiveTree_PASSIVE_TREE_SPECIFICATION.md)
- [Art 像素美术、逐帧动画与稳定图集规格](v0.2/Art_SPECIFICATION.md)
- [Release 整合、平衡与 v0.2 封版规格](v0.2/Release_SPECIFICATION.md)
- [Release.1 v0.2 候选版发布门禁](v0.2/ReleaseGate_RELEASE_GATE.md)
- [Release.2 v0.2 最终界面收口](v0.2/ClientLayout_FINAL_UI.md)
- [Release 经济蒙特卡洛审计](v0.2/Release_ECONOMY_AUDIT.md)
- [Release 六构筑空间战斗审计](v0.2/Release_COMBAT_AUDIT.md)
- [v0.2.0 版本说明](v0.2/V0_2_RELEASE_NOTES.md)

## v0.3

- [v0.3 六职业、十八升华与主天赋树 V3 总规格](v0.3/V0_3_SPECIFICATION.md)
- [Characters 六职业底层与主天赋树 V3 实现规格](v0.3/Characters_SPECIFICATION.md)
- [Characters.1 十八升华完整实现规格](v0.3/ClassAscendancies_SPECIFICATION.md)
- [Archetypes 五职业配套构筑内容实施规格](v0.3/Archetypes_SPECIFICATION.md)

## v0.4

- [v0.4 玩法与数值打磨总规格](v0.4/V0_4_SPECIFICATION.md)
- [Atlas 地图系统与金币异界天赋实施规格](v0.4/Atlas_SPECIFICATION.md)
- [Monsters 怪物生态、首领与亡旗战阵实施规格](v0.4/Monsters_SPECIFICATION.md)
- [Encounters 五玩法闭环、战斗机制与逐图结算](v0.4/Encounters_SPECIFICATION.md)
- [Resources 掉落池、玩法做装与经济审计](v0.4/Resources_SPECIFICATION.md)
- [Resources 经济蒙特卡洛审计](v0.4/Resources_ECONOMY_AUDIT.md)
- [Builds 战斗公式与构筑平衡规格（开发侧完成，待玩家复核）](v0.4/Builds_SPECIFICATION.md)
- [Builds 设计接续入口：当前进度、剩余内容与下一步](v0.4/Builds_DESIGN_CHECKPOINT.md)
- [Builds 剩余任务与关闭顺序](v0.4/Builds_REMAINING_TASKS.md)
- [Builds 技能基础数据表（86 个主动与 98 个辅助已进入运行时）](v0.4/Builds_SKILL_BASE_DATA.md)
- [Builds 三十六套构筑验证（18 升华开荒/终局全通过）](v0.4/Builds_BUILD_AUDIT.md)
- [Builds UI、美术与性能开发侧验收](v0.4/Builds_UI_ART_PERFORMANCE_ACCEPTANCE.md)
- [Builds 主天赋簇逐类规格（第一组 12 类已确认）](v0.4/Builds_PASSIVE_CLUSTERS.md)
- [Builds 伤害与异常天赋簇（19 类、45 簇已确认，含来源修订）](v0.4/Builds_DAMAGE_AILMENT_CLUSTERS.md)
- [Builds 防御与资源天赋簇（14 类、44 簇全部确认）](v0.4/Builds_DEFENSE_RESOURCE_CLUSTERS.md)
- [Builds 单位天赋簇（5 类、10 簇全部确认）](v0.4/Builds_UNIT_CLUSTERS.md)
- [Builds 技能机制天赋簇（7 类、14 簇全部确认）](v0.4/Builds_SKILL_MECHANISM_CLUSTERS.md)
- [Builds 辅助与通用机制天赋簇（3 类、9 个中型簇全部确认）](v0.4/Builds_AUXILIARY_GENERAL_CLUSTERS.md)
- [Builds 主天赋树拓扑与结构节点（骨架与新增簇已确认）](v0.4/Builds_PASSIVE_TREE_TOPOLOGY.md)
- [Builds 主天赋树 168 个主题簇固定落位（完整确认）](v0.4/Builds_PASSIVE_TREE_PLACEMENT.md)
- [Builds 珠宝、词缀、腐化、掉落与 24 个棱孔布局（完整确认）](v0.4/Builds_JEWELS_AND_SOCKETS.md)
- [Builds 主天赋 149 点经济与洗点（完整确认）](v0.4/Builds_PASSIVE_POINT_ECONOMY.md)
- [Builds 主天赋树最终坐标、稳定数据与像素美术验收（完整确认）](v0.4/Builds_PASSIVE_TREE_FINAL_LAYOUT.md)
- [Builds 十八升华复核第一批（六个完整确认）](v0.4/Builds_ASCENDANCY_REVIEW_1.md)
- [Builds 十八升华复核第二批（六个完整确认）](v0.4/Builds_ASCENDANCY_REVIEW_2.md)
- [Builds 十八升华复核第三批（六个完整确认）](v0.4/Builds_ASCENDANCY_REVIEW_3.md)
- [Builds 装备词缀现状审计与清理清单（第一轮及五项问题全部确认）](v0.4/Builds_EQUIPMENT_AFFIX_AUDIT.md)
- [Builds 装备词缀一次性修改提案（已审核并实施）](v0.4/Builds_EQUIPMENT_AFFIX_FINAL_PROPOSAL.md)
- [Builds 美德、恶德与六个外环大型簇（完整确认）](v0.4/Builds_VIRTUE_VICE_CLUSTERS.md)
- [Builds 怪物等级与数值成长规格](v0.4/Builds_MONSTER_BALANCE.md)
- [Presentation 表现、性能与 v0.4.0 封版规格](v0.4/Presentation_SPECIFICATION.md)
