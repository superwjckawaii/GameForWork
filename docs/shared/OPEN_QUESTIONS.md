# 待讨论与开发决策

> 更新日期：2026-09-01
> 本文只列出尚未确定的内容。已确认设计以 `GAME_DESIGN_BASELINE.md` 为准。

## 1. 窗口与桌面体验

- 窗口尺寸、比例、缩放、边框和快捷隐藏行为已经确定。
- 后续只需在原型阶段验证 `384×216` 小窗中文字和危险提示的实际可读性。

## 2. 像素美术后续细化

- Management 的低密度像素方向、战斗角色包围盒、物品图标尺寸和资产范围已经确定。
- 后续只需在资产制作时确定每个角色的最终色板和具体动作帧。
- v0.2 固定制作四方向逐帧动画；是否扩展八方向随未来直接控制方案再讨论。

## 3. 技术栈

- 已验证 Godot 4.7.2 .NET 实际路径；创建工程时固定合适的 .NET SDK `global.json`。
- 首个代码工程需要同时创建 GitHub Actions：编译、`dotnet test` 和 Godot 无界面启动检查。
- SQLite 的具体 C# 驱动与版本。
- Steam Cloud 等云存档是否接入。
- 最终性能目标、内存目标、最小硬件要求与耗电指标。

## 4. 数值与公式

- Builds 已固化四种基础属性第一批收益、装备局部计算、合法伤害转化图、侵蚀/凋零、偷取上限和天赋簇结构，见 `../v0.4/Builds_SPECIFICATION.md`。
- Builds 前六组共 60 类、158 簇、1103 个主题节点完整确认并已生成运行时主树；第六组光环/保留、诅咒、战吼/增益的完整节点与共享专精见 [辅助与通用机制正式表](../v0.4/Builds_AUXILIARY_GENERAL_CLUSTERS.md)。
- 浴火回生、雷息护体、沉稳持械、战意回流的来源替换已确认；精神减益时长与能量充能的基础属性固有收益确认保留。普通天赋不直接提供已移出的专门属性。
- 辅助与通用组、主树骨架与坐标、168 个主题簇、24 个棱孔、149 点经济、十八升华和装备词缀第一阶段均已进入运行时。Builds 只剩 [技能基础表、36 套构筑验证、UI/美术/性能验收](../v0.4/Builds_REMAINING_TASKS.md)。
- 佣兵成长潜力的档位和精确差距。
- 多人佣兵队的敌群和奖励缩放公式。
- v0.2 已固定地图等级、词缀结构和掉落目标；具体数值表由 Inventory、EquipmentImport、Economy 的模拟结果确定，不再作为产品方向问题。

## 5. 内容细化

- Encounters 已确认五玩法闭环：实际击杀收益保留、未完成奖励不发放、苍誓承诺完成后兑现；不新增沉金港、无光矿脉。详见 `../v0.4/Encounters_SPECIFICATION.md`。
- Resources 各玩法专属传奇、底材、技能石与材料池已固化并实施，见 `../v0.4/Resources_SPECIFICATION.md`。
- Builds 的 36 套基准构筑实测数值、流程平衡与封版阈值。
- 主角出身切换的世界观解释与具体成本。
- “断界之夜”的开发内部唯一真相。

## 6. 产品与发布

- 正式名称与商标检查。
- 是否首先发布本地独立版本或接入 Steam。
- 买断价格、Demo、抢先体验和 DLC 原则。
- 成就、手柄、语言和无障碍支持范围。
- Foundation、Campaign 与 Management 范围已经确定；后续仍需确定首个公开测试版和 1.0 的内容与时间范围。

## 7. 首个 Demo 路线

Playback～SimulationParity 已在 `DEMO_ROADMAP.md` 封版。首个 Demo 之前不再讨论第二主角职业、永久死亡、第三远征队、音频、手柄、Steam 或联网功能；这些内容在 SimulationParity 之后重新评估。

## 8. v0.2 路线

Inventory～Release 的怪物、技能、升华、装备、掉落、美术和封版范围已在 [`V0_2_SPECIFICATION.md`](../v0.2/V0_2_SPECIFICATION.md) 固化，Inventory 细节见 [`Inventory_SPECIFICATION.md`](../v0.2/Inventory_SPECIFICATION.md)。v0.3 转为六职业、十八升华与主天赋树 V3 版本，规格见 [`V0_3_SPECIFICATION.md`](../v0.3/V0_3_SPECIFICATION.md)；亡旗战场、沉金港和无光矿脉顺延到 v0.4。
