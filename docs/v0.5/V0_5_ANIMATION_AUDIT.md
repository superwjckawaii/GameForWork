# v0.5 全量动画审核清单

状态：目录与源码审核完成；[子项](V0_5_ANIMATION_SUBITEMS.md)展开98辅助、150普通敌人技能及74个Boss归属技能。[20组代表场景前测](V0_5_VISUAL_BASELINE.md)已归档，记录长帧和音频设备错误，不代表性能通过。第四阶段逐身份动态验收仍待进行；生产运行时代码未改。

入口：[六阶段计划](V0_5_DEVELOPMENT_PLAN.md)。逐身份追踪：[动画逐项索引](V0_5_ANIMATION_INVENTORY.md)。已确认原则：保留整体画风，合格效果保留，问题效果修复或重做；纯数值辅助不强加特效；任何档位均保留关键危险提示。

## 目录基线

本次重新构建 Game.Core Release 后读取正式静态目录，而非只依据旧文档或资源数量：

| 类别 | 本次数量 | 覆盖与限制 |
| --- | --- | --- |
| 主动技能 | 86 | VisualCatalog.Skills 与 ActiveSkillCatalog；每项前摇、发射、命中、结束及特殊行为 |
| 辅助 | 98 | ActiveSkillCatalog.Supports；逐项区分实际形态变化、状态反馈、纯数值 |
| 普通敌人 | 80 | Enemies.NormalEnemies；150个实际技能子项已展开，共享26个骨架仍需逐身份动态核对 |
| Boss | 24 | 战役5、地图12、战争军官2、统帅1、突破1、天垒阶段3；静态入口已核对，70个目录技能加4个继承子项 |
| 角色图集 | 5 个骨架槽 | 槽位数不是职业覆盖证明；主角、佣兵、幻身等需核对实际映射 |
| 单位图集 | 4 个专用骨架 | 骨卫、魂弓、灵兽、炮台；其余单位由角色绘制分支追踪 |
| 动作 | 6 动作×4 方向 | Idle/Move/Attack/Cast/Hit/Death；复生、替换属于状态切换审核，不假称有独立图集动作 |

ArtContract.SkillStoneIds 当前只覆盖旧核心集合，ArtFeatureTests 对它检查 78 项；不能用它替代完整 184 个技能石的动画覆盖索引。

## 资源、入口与逐项工作

下列资源路径相对 `src/Game.Godot/assets/`；代码路径相对仓库根。已有代表场景前测，但逐身份的最终画面验收未做，不能整表标为保留/通过。

| 审核身份 | 资源或绘制方式 | 实际入口 | 必查内容 | 当前结论 |
| --- | --- | --- | --- | --- |
| actor.hero / actor.mercenary | art/characters/art-actor-animation.png | WorldView.DrawSpatialBattle / DrawSpatialAlly | 四方向、六动作、武器语义、动作时钟、死亡复生、高攻速 | 待实机；动作时钟需修复 |
| unit.boneguard / unit.soulbow / unit.spirit_beast / unit.turret | art/characters/art-unit-animation.png | DrawCombatUnit / ArtContract.UnitRig | 独立朝向、攻击起点、弹道、死亡、炮台不滑步 | 待实机 |
| unit.character-derived | 角色图集 | TryDrawCharacterUnit / DrawSpatialAlly | 幻身与伙伴实际身份、是否错误继承主角动作、替换残影 | 身份已展开至子项清单；第四阶段动态验收 |
| enemy.* | art/enemies/art-enemy-animation.png | DrawSpatialEnemy / EnemyRig | 80 个身份六动作、体型、精英标记及攻击同步 | 逐项索引待实机 |
| boss.* | art/enemies/art-boss-animation.png | DrawSpatialEnemy / BossRig | 24 个身份各技能前摇、预警、阶段切换、死亡 | 70个技能子项已展开；第四阶段动态验收 |
| skill.* | presentation/vfx/presentation-combat-vfx.png | VisualForEvent / DrawSpatialSkills | 86 技能实际形状、方向、范围、轨迹、持续时间 | 共性绘制问题已确认 |
| support.* | 线段、圆弧、矩形叠层 | ReadSupportLayers / DrawSupportLayers | 98 辅助真实消费者与可见变化；不能伪造连锁或暴击 | 分类契约需修复 |
| danger.ground / danger.boss-warning | 圆与描边 | DrawSpatialSkills | 真实半径、形状、朝向、激活与结束时刻；不得被密度裁剪 | 已确认需修复 |
| feedback.damage / feedback.defense | 文本、线条及提示 | DrawSpatialNumbers / DrawCombatFeedback | 六伤害类型、合并/关闭、格挡闪避护盾、暴击、归属 | 待逐事件实机 |
| status.ailment / status.virtue-vice | 状态图标与绘制 | DrawEnemyStatusIcons / DrawVirtueViceIcons | 异常生效消失、层数、永久与计时状态、重载清理 | 待逐状态实机 |
| camera.shake | 画面位置偏移 | ScreenShakeOffset / VisualPreferences | 开关、强度、连击叠加、暂停及观战切换无残留 | 待实机 |
| scene.town / scene.travel | art/town/art-town-district.png、art/regions/art-region-atlas.png | DrawTown / DrawTravel | 门户脉动、移动观感、场景切换、缩放和资源回退 | 待实机 |
| ui.toast / ui.rewards | 定时显隐、收益条 | ToastOverlay / DrawRewardStrip | 不遮挡、不抢输入、连续消息、未领取与已领取提示 | 待实机及宝箱接入 |
| ui.equipment / ui.skills / ui.tree | equipmentArt/ui/ 图集及天赋背景 | ItemCell / ArtAtlas / PassiveTreeView / TownPanel | 静态图标与交互反馈分开审；悬停、比较、切换无闪烁 | 待实机；ItemCell 每帧检查不等于动画 |
| harbor.* / chest.* | 尚未实现 | 第二、三阶段新入口 | 港区移动/交互/追击/撤离、成功失败、批量开箱和三选一 | 预留，不算现有效果通过 |

## 源码已确认问题

| 稳定问题身份 | 证据 | 影响与修复方向 |
| --- | --- | --- |
| danger.geometry | WorldView.DrawSpatialSkills 的 ground/warning 分支使用场地宽度的 .15 或 1/6 作为半径 | 改读战斗实际几何与方向；不能随窗口比例猜危险范围 |
| skill.cone-geometry | Circle/Cone/GroundArea/MovementCircle 共走 DrawCircle | 锥形必须画扇面，并核对中心与朝向；圆形也要读取真实判定中心 |
| danger.lifetime | CollectRecentEvents 只保留最近 900ms；DrawSpatialSkills 不以 until 判定有效期限 | 风险：长预警或持续地面丢失。建立按事件结束时间维护的活动效果，不靠短窗口重放 |
| danger.density | 所有效果共享 Take(effectLimit)，优先排序仍有数量上限；小窗强制 Low、上限 8 | 关键危险单独保留，预算仅裁剪装饰；多危险压力用例必须验证 |
| projectile.path | DrawSpatialSkills 使用 source.Lerp(target, age*1.25)；部分起终点来自当前实体位置 | 不能证明真实弹道、折返、连锁、穿透。接收真实发射点、轨迹与命中事件，不能追随活目标伪造飞行 |
| support.semantic-mapping | VisualCatalog 要求全部 Supports.Layer 非 None；SupportLayer 用字符串分类与强制回退 | 与纯数值辅助可无特效的确认规则冲突；中文机制键也可能走回退。按正式机制身份明确映射和消费者 |
| actor.action-clock | DrawSpatialBattle 将总 elapsed 传入角色动画，AnimationColumn 直接据此取帧 | 新攻击可能从周期中段开始，非循环死亡可能直接落在末帧；使用动作开始时间和动作归属，不直接复用总战斗时钟 |
| coverage.legacy-contract | ArtFeatureTests 检查 78 个旧核心技能石，但完整技能为 86+98 | 保留必要兼容测试，增加正式目录覆盖与实际画面矩阵，不以旧集合宣布全量覆盖 |
| boss.skill-contract | Bosses.CombatProfile 将多数技能文案映射为三种通用技能；统帅额外继承4个军官技能 | 按实际EffectiveSkills和战斗事件制作表现；不遗漏继承动作，不把文案形状当伤害几何，不擅自新增Boss机制 |
| scene.effect-bounds | 完整客户端正常场景的大窗截帧可见大圆环越过战场边框，覆盖界面文字；WorldView与创建点未设置ClipContents | 将战场效果约束在正确绘制区域，保留危险在场内的真实覆盖；不让几何修复继续污染战场外UI，第四阶段核对所有窗口 |

上述是静态实现事实及可推导风险；尚未声称在用户当前实屏复现了每个视觉症状。源码审核不授权本阶段直接修改运行时，按第二阶段事件契约、第四阶段表现优化实施。

## 动态验收矩阵

窗口尺寸从 WindowController 读取：小窗 384×216、标准 960×640、大窗 1920×1280。标准/大窗分别低中高特效。完整客户端的小窗当前只显示状态与操作，不绘制战场，故只采一档UI基线；独立WorldView窄于620时强制低特效，是不同路径，不能混淆。第四阶段另测自定义战场宽度穿越620的边界。

| 场景 | 覆盖 |
| --- | --- |
| 单动作与单技能 | 六动作四方向；每个主动技能单独回放前摇到结束，18 个代表构筑不能替代 86 项全覆盖 |
| 辅助组合 | 每个辅助先单独验证，再覆盖多投射、连锁、穿透、返回、重复、引导、触发及单位组合 |
| Boss/危险 | 单个长持续危险、多预警并存、连续阶段切换；低档不丢关键信息 |
| 极端战斗 | 高攻速/施法、密集敌人、多单位、多投射、多地面、高频受击；录制事件与同场回放 |
| 状态切换 | 暂停/恢复、速度变化、死亡/复生、主角与佣兵切换、换场景、退出重载；无遗留图标或特效 |
| UI与宝箱 | 悬停比较、窗口切换、Toast 堆叠、批量开箱/三选一、跳过动画、中断恢复；领取结果不由动画驱动 |

每条最终记录：稳定身份 → 对应资源/生产入口 → 预期表现 → 当前问题 → 保留/修复/重做/纯数值无独立效果 → 回放输入和时间段 → 截图或录屏路径 → 验收结论。当前索引尚未填入动态证据。

## 性能口径与后续验收

- 已确认目标：固定设备比较前后结果，标准场景稳定 60 FPS，压力场景不低于原基线；不得以平均帧率掩盖明显卡顿。
- 已通过运行中的渲染器确认活动 GPU 为 RTX 3060 Laptop GPU；CPU 为 i7-12700H。引擎、驱动、DPI、供电与逐项数值以[前测报告](V0_5_VISUAL_BASELINE.md)及环境证据为准，不拿系统枚举代替实际绑定。
- 第一阶段采用分层基线：两个场景各覆盖七个窗口/特效组合；正常与高密度场景各做三轮大窗高档长采样（预热30秒、采120秒）。完整42组合长采样及多单位/多投射/多地面极限组合放在第五阶段。该调整减少前测重复时长，不降低性能阈值，不声称所有组合均已三轮长测。
- 正式迷你窗没有战场，只采UI；独立WorldView窄窗渲染另作历史前测，不混用两种结果。计时阶段不录制图像；连续帧画面证据单独采集，避免录制开销污染帧耗时。
- 标准60FPS预算约16.7ms；95分位不超过18ms、99分位不超过25ms作为同机复测目标，保留最大帧40ms、模拟10ms、UI16ms、工作集700MiB的原门槛。失败项必须保留，不以平均值或删除离群值掩盖卡顿；数值达标不代表全量视觉验收通过。

## 验证分工

- `dotnet test src/Game.Tests/Game.Tests.csproj -c Release --filter "FullyQualifiedName~ArtFeatureTests|FullyQualifiedName~EquipmentArtFeatureTests" --no-restore`：16/16 通过。
- `scripts/verify_art_assets.ps1`：通过（尺寸、透明分隔、稳定计数与图标唯一性）。
- `scripts/verify_presentation_assets.ps1`：通过（VFX 图集与背景坐标校验）。
- 最终完整Release、代表场景回放和性能数据见[集中收口检查](V0_5_STAGE_ONE_CHECKPOINT.md)及前测报告；本清单不修改生产代码、不生成或替换正式美术。

角色派生单位、Boss技能/阶段和辅助可见性分类已交付至子项清单。第四阶段按逐身份矩阵填入最终保留/修复/重做证据；第五阶段做极限组合、完整长矩阵和多小时压力验证，不把第一阶段前测当作这些工作的完成证明。
