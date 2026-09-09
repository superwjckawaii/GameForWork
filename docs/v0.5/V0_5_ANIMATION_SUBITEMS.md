# 动画子项与辅助可见性分类

状态：第一阶段源码分类与验收项展开完成；这是后续逐项回放的执行清单，不代表已逐项通过动态验收。各项仍需从其正式机制消费者取事件，不能读取分类标签伪造效果。

## 全部 98 个辅助

分类口径：形态/数量跟真实轨迹和实体；状态跟真实状态事件；时序跟真实动作时长；伤害类型跟最终伤害组成；其余数值不附加独立特效，但伤害数字、资源栏、实际暴击等原反馈仍应正常工作。分类不改变辅助本身机制。

| 正式身份 | 名称 | 分类 | 实施/验收要求 |
| --- | --- | --- | --- |
| core.skill_stone.increased_area | 扩大范围 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.attack_speed | 攻击速度 | 动作时序 | 改变实际动作频率或时长，不加额外闪光 |
| core.skill_stone.bleed | 流血 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| core.skill_stone.life_cost | 生命消耗 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.chain | 追加连锁 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.brutality | 残暴 | 伤害组成 | 跟随最终伤害类型与异常结果，混合伤害保留可读性 |
| core.skill_stone.multiple_projectiles | 多重投射 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.faster_projectiles | 极速投射 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.urgent_war_cry | 急促战吼 | 动作时序 | 改变实际动作频率或时长，不加额外闪光 |
| core.skill_stone.life_leech | 血之汲取 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| core.skill_stone.execution | 处决 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.spell_echo | 法术回响 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.elemental_focus | 元素集中 | 伤害组成 | 跟随最终伤害类型与异常结果，混合伤害保留可读性 |
| core.skill_stone.added_fire | 附加火焰 | 伤害组成 | 跟随最终伤害类型与异常结果，混合伤害保留可读性 |
| core.skill_stone.added_cold | 附加寒霜 | 伤害组成 | 跟随最终伤害类型与异常结果，混合伤害保留可读性 |
| core.skill_stone.added_lightning | 附加闪电 | 伤害组成 | 跟随最终伤害类型与异常结果，混合伤害保留可读性 |
| core.skill_stone.critical_strikes | 精准暴击 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.concentrated_effect | 集中效应 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.heavy_momentum | 重势 | 动作时序 | 改变实际动作频率或时长，不加额外闪光 |
| core.skill_stone.triple_impact | 三叠重击 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.tremor_field | 震域 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.shockwave | 余波 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.close_combat | 贴身搏杀 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.armor_shatter | 裂甲 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| core.skill_stone.armor_pierce | 透甲 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.suppression | 镇压 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| core.skill_stone.stun_spread | 震荡蔓延 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.deep_wound | 深创 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| core.skill_stone.swift_bleed | 疾血 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| core.skill_stone.bleed_spread | 血痕播散 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.cruelty | 残酷 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.bloodlust | 嗜血 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.trauma | 创伤积压 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| core.skill_stone.fortification | 坚阵 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| core.skill_stone.vengeance | 复仇增幅 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.block_trigger | 格挡触发 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| core.skill_stone.war_cry_potency | 号令增幅 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.war_cry_echo | 回声战吼 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.banner_potency | 誓旗增幅 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| core.skill_stone.pierce | 贯穿 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.fork | 裂射 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.return | 归返 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| core.skill_stone.faster_casting | 疾咏 | 动作时序 | 改变实际动作频率或时长，不加额外闪光 |
| core.skill_stone.physical_to_lightning | 雷铸转化 | 伤害组成 | 跟随最终伤害类型与异常结果，混合伤害保留可读性 |
| core.skill_stone.lightning_to_cold | 霜流转化 | 伤害组成 | 跟随最终伤害类型与异常结果，混合伤害保留可读性 |
| core.skill_stone.cold_to_fire | 焰化转化 | 伤害组成 | 跟随最终伤害类型与异常结果，混合伤害保留可读性 |
| core.skill_stone.fire_to_void | 虚蚀转化 | 伤害组成 | 跟随最终伤害类型与异常结果，混合伤害保留可读性 |
| core.skill_stone.cast_when_damaged | 受创触发 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.far_shot | 远射 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.precision_pierce | 精准穿透 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.seeking_chain | 追踪连锁 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.mobile_attack | 移动攻击 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.toxin_spread | 毒素扩散 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.multiple_traps | 多重陷阱 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.mark_amplify | 标记增幅 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.backstab_amplify | 背袭增幅 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.minion_amplify | 召唤增幅 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.swift_minions | 迅捷仆从 | 动作时序 | 改变实际动作频率或时长，不加额外闪光 |
| archetypes.skill_stone.support.expanded_army | 扩军 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.bodyguard | 护主 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.aura_amplify | 光环增幅 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.lasting_blessing | 祝福延续 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.hex_spread | 恶咒传播 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.deep_hex | 咒印深化 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.fire_penetration_archetypes | 火焰穿透 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.cold_penetration_archetypes | 寒霜穿透 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.lightning_penetration_archetypes | 闪电穿透 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.elemental_ailment | 元素异常 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.void_duration | 虚蚀延长 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.deep_wither | 深层凋零 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.shield_leech | 护盾汲取 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.shield_casting | 护盾施法 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.unarmed_focus | 徒手专注 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.combo_duration | 连击延续 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.stance_amplify | 姿态增幅 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.movement_echo | 位移回响 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.ferocious_beast | 灵兽凶猛 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.guardian_beast | 灵兽守护 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.phantom_copy | 幻身复制 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.phantom_sacrifice | 幻身献祭 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| archetypes.skill_stone.support.spellblade | 法武交错 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.attack_trigger | 攻击触发 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.imprint_gain | 刻印积累 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.imprint_burst | 刻印爆发 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.spellarmor_fusion | 魔铠融合 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| archetypes.skill_stone.support.shieldbreak_amplify | 破盾增幅 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.construct_amplify | 构装增幅 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| archetypes.skill_stone.support.rapid_rebuild | 快速重铸 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| builds.skill_stone.support.mercy_expansion | 仁心扩界 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| builds.skill_stone.support.temperance_calculus | 持律精算 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| builds.skill_stone.support.humility_guard | 俯身守式 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| builds.skill_stone.support.rage_acceleration | 怒潮催行 | 动作时序 | 改变实际动作频率或时长，不加额外闪光 |
| builds.skill_stone.support.sloth_proliferation | 惰性繁生 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| builds.skill_stone.support.arrogance_critical | 凌峰傲击 | 纯数值 | 不新增独立特效；保留真实资源/命中/暴击等通用反馈 |
| builds.skill_stone.support.lone_focus | 孤锋专注 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| builds.skill_stone.support.kill_spread | 杀势扩散 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |
| builds.skill_stone.support.prolonged_torment | 绵延折磨 | 状态/触发 | 复用真实施加、消失、触发和资源变化反馈；不凭装备辅助常亮 |
| builds.skill_stone.support.overload_supply | 过载供能 | 形态/数量 | 真实范围/目标/单位/重复事件，不能画假连锁或假数量 |

## 全部 Boss 技能子项

每项验收实际战斗可达性、预警开始与结束、形状/方向/范围、伤害时刻和阶段切换；未进入生产调用的目录项标为目录保留，不冒充可玩内容。不会仅因目录里写了预警文本就认定画面实现。

| Boss 正式身份 | 名称 | 技能子项 | 目录预警语义 | 验收状态 |
| --- | --- | --- | --- | --- |
| monsters.boss.campaign.act1 | 余烬守门人 | 阶段重击 | 扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act1 | 余烬守门人 | 幕终异象 | 收缩双环 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act1 | 余烬守门人 | 追猎技 | 闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act2 | 谷仓吞噬者 | 阶段重击 | 扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act2 | 谷仓吞噬者 | 幕终异象 | 收缩双环 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act2 | 谷仓吞噬者 | 追猎技 | 闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act3 | 溺亡圣徒 | 阶段重击 | 扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act3 | 溺亡圣徒 | 幕终异象 | 收缩双环 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act3 | 溺亡圣徒 | 追猎技 | 闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act4 | 无光领路人 | 阶段重击 | 扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act4 | 无光领路人 | 幕终异象 | 收缩双环 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act4 | 无光领路人 | 追猎技 | 闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act5 | 界外之物 | 阶段重击 | 扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act5 | 界外之物 | 幕终异象 | 收缩双环 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.campaign.act5 | 界外之物 | 追猎技 | 闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.01 | 灼痕督军 | 断阵重击 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.01 | 灼痕督军 | 回响投射 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.01 | 灼痕督军 | 区域爆发 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.02 | 沉棺祭司 | 裂界挥扫 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.02 | 沉棺祭司 | 追猎冲锋 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.02 | 沉棺祭司 | 区域爆发 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.03 | 绞枝母体 | 裂界挥扫 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.03 | 绞枝母体 | 回响投射 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.03 | 绞枝母体 | 星骸坠落 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.04 | 无旗将军 | 断阵重击 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.04 | 无旗将军 | 回响投射 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.04 | 无旗将军 | 区域爆发 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.05 | 苔冠巨兽 | 裂界挥扫 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.05 | 苔冠巨兽 | 追猎冲锋 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.05 | 苔冠巨兽 | 区域爆发 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.06 | 碎光监工 | 裂界挥扫 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.06 | 碎光监工 | 回响投射 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.06 | 碎光监工 | 星骸坠落 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.07 | 默祷院长 | 断阵重击 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.07 | 默祷院长 | 回响投射 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.07 | 默祷院长 | 区域爆发 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.08 | 黑帆船长 | 裂界挥扫 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.08 | 黑帆船长 | 追猎冲锋 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.08 | 黑帆船长 | 区域爆发 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.09 | 枯荣园丁 | 裂界挥扫 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.09 | 枯荣园丁 | 回响投射 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.09 | 枯荣园丁 | 星骸坠落 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.10 | 盲眼占星师 | 断阵重击 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.10 | 盲眼占星师 | 回响投射 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.10 | 盲眼占星师 | 区域爆发 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.11 | 赤炉之心 | 裂界挥扫 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.11 | 赤炉之心 | 追猎冲锋 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.11 | 赤炉之心 | 区域爆发 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.12 | 末代誓王 | 裂界挥扫 | 红边扇形蓄力 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.12 | 末代誓王 | 回响投射 | 箭头与闪烁路径 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.map.12 | 末代誓王 | 星骸坠落 | 环形描边收缩 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.warfront.iron_banner | 铁旗校尉 | 盾墙推进 | 长方形推进区 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.warfront.iron_banner | 铁旗校尉 | 猎首号令 | 红色锁定箭头 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.warfront.ember_cannon | 烬炮监军 | 三点炮击 | 三枚橙色落点 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.warfront.ember_cannon | 烬炮监军 | 压制齐射 | 平行箭道 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.warfront.last_marshal | 末旗统帅 | 全军突击 | 多条冲锋箭道 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.warfront.last_marshal | 末旗统帅 | 亡旗炮阵 | 五枚递进落点 | 待动态回放；按实际生产伤害事件核对 |
| monsters.boss.warfront.last_marshal | 末旗统帅 | 战阵处决 | 赤色收缩扇面 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.gate_trial | 百级门扉化身 | 门扉碾压 | 交叉重线 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.gate_trial | 百级门扉化身 | 灵能浪潮 | 蓝色双环 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.gate_trial | 百级门扉化身 | 终末审判 | 全屏倒计时 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.citadel.wall | 活化城墙 | 落石阵 | 方格阴影 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.citadel.wall | 活化城墙 | 城垛齐射 | 平行箭头 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.citadel.wall | 活化城墙 | 熔油 | 橙色地面边框 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.citadel.guards | 灰烬双卫 | 交叉斩 | 交叉亮线 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.citadel.guards | 灰烬双卫 | 誓火链 | 两点连线 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.citadel.guards | 灰烬双卫 | 替身护卫 | 盾形描边 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.citadel.core | 天垒核心 | 核心脉冲 | 三重扩散环 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.citadel.core | 天垒核心 | 灰烬坠落 | 闪烁落点 | 待动态回放；按实际生产伤害事件核对 |
| core.boss.citadel.core | 天垒核心 | 誓约抹除 | 黑白全屏边框 | 待动态回放；按实际生产伤害事件核对 |

## 角色与单位展开

每个身份均覆盖 Idle/Move/Attack/Cast/Hit/Death × Down/Left/Right/Up；复生、替换、离场另查状态清理。图集支持动作并不代表每单位都会生产全部动作；不可达组合须记录原因而非补假动作。

| 身份 | 生产/播放入口 | 重点 |
| --- | --- | --- |
| 主角 | DrawSpatialBattle | 武器方向、移动与攻击状态切换、从动作起点计时、死亡非循环 |
| 同行佣兵 / 佣兵远征主体 | DrawSpatialAlly / DrawSpatialBattle | 不盲目继承主角朝向/动作；独立受击死亡 |
| phantom:* 幻身 | BattleArmy / TryDrawCharacterUnit | 半透明角色映射、复制/到期/献祭、实体归属，不重复伤害反馈 |
| mercenary 实体 | TryDrawCharacterUnit | 角色骨架1；换队伍/观战后无残影 |
| summon_boneguard 骨卫 | BattleArmy / UnitRig 0 | 护主、近战盾击、受击与死亡 |
| summon_soulbow 魂弓 | BattleArmy / UnitRig 1 | 远程弹道、退避、目标选择 |
| summon_spirit_beast 灵兽 | BattleArmy / UnitRig 2 | 跟随/冲锋/分担/首次致死保护；不把保护当复生 |
| forge_turret 炮台 | BattleArmy / UnitRig 3 | 固定站位，禁止表现层假步伐；射击轨迹、重铸与自爆 |
| 陷阱/符阵等地面实体 | 技能事件与持续区域分支 | 布置位置、激活、触发、结束、数量上限；不默认当作角色骨架 |

## 源码分类完成后的动态验收安排

- 第二阶段：产出真实几何、轨迹、动作与结束事件；第一阶段不提前修这些运行时缺口。
- 第四阶段：按本表和主索引逐项回放并填写保留/修复/重做及证据，完成所有存量动画最终验收。
- 第五阶段：代表构筑和高密度压力测量；第一阶段采样作为前测样本，不冒充极限场景已通过。

## 普通敌人的 150 个实际技能子项

从当前 Release 程序集的 Enemies.NormalEnemies → EffectiveSkills 读取，覆盖80个普通敌人及其全部150个技能条目，而非只查看主技能。当前技能尚无独立永久身份，以所属敌人身份和技能名定位本轮快照；第二阶段事件契约需补稳定动作身份，不将列表序号当永久身份。

生产入口为 SpatialCombatRunner 的敌人动作轮转。RangeRaw 是生产原始范围参数，不等于屏幕半径；实际作用形状、倍率、目标和结束时间仍须读取运行时事件。Area/可避是当前数据标志，治疗、链接等非伤害行为不能只凭这两个标志画伤害预警。以下均是第四阶段逐项动态验收任务，尚未宣布通过。

| 敌人身份（名称） | 技能 | 生产类型 | RangeRaw | Area / 可避 | 预警文案（非几何契约） |
| --- | --- | --- | --- | --- | --- |
| core.enemy.corrupted_worker（腐化工役） | 基础攻击 | BasicStrike | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.corrupted_worker（腐化工役） | 余烬燃地 | GroundHazard | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.gate_hound（门扉猎犬） | 冲锋 | Charge | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.gate_hound（门扉猎犬） | 余烬燃地 | GroundHazard | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.oathless_guard（失誓守卫） | 蓄力重击 | HeavySlam | 1200 | 是 / 是 | 扇形蓄力 |
| core.enemy.oathless_guard（失誓守卫） | 余烬燃地 | GroundHazard | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.ash_bone_archer（烬骨弓手） | 齐射 | Volley | 6000 | 否 / 是 | 无独立文案 |
| core.enemy.ash_bone_archer（烬骨弓手） | 余烬燃地 | GroundHazard | 6000 | 是 / 是 | 无独立文案 |
| core.enemy.cinder_confessor（余烬告解者） | 秘术投射 | ArcaneBolt | 7000 | 否 / 是 | 无独立文案 |
| core.enemy.cinder_confessor（余烬告解者） | 余烬燃地 | GroundHazard | 7000 | 是 / 是 | 无独立文案 |
| core.enemy.charred_banner（焦旗侍从） | 战斗光环 | WarAura | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.charred_banner（焦旗侍从） | 余烬燃地 | GroundHazard | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.ember_brute（烬壳蛮卒） | 蓄力重击 | HeavySlam | 1200 | 是 / 是 | 扇形蓄力 |
| core.enemy.ember_brute（烬壳蛮卒） | 余烬燃地 | GroundHazard | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.ash_crow（灰烬鸦） | 冲锋 | Charge | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.ash_crow（灰烬鸦） | 余烬燃地 | GroundHazard | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.frostfang_hound（霜牙猎犬） | 冲锋 | Charge | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.frostfang_hound（霜牙猎犬） | 霜原冰缚 | RootSnare | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.rime_archer（霜痕弓手） | 齐射 | Volley | 6000 | 否 / 是 | 无独立文案 |
| core.enemy.rime_archer（霜痕弓手） | 霜原冰缚 | RootSnare | 6000 | 是 / 是 | 无独立文案 |
| core.enemy.winter_shaman（寒原萨满） | 持续危险地面 | GroundHazard | 7000 | 是 / 是 | 地面描边 |
| core.enemy.winter_shaman（寒原萨满） | 霜原冰缚 | RootSnare | 7000 | 是 / 是 | 无独立文案 |
| core.enemy.icehide_aurochs（冰皮原牛） | 蓄力重击 | HeavySlam | 1200 | 是 / 是 | 扇形蓄力 |
| core.enemy.icehide_aurochs（冰皮原牛） | 霜原冰缚 | RootSnare | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.hunger_matron（饥群母兽） | 召唤兽群 | SummonSwarm | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.hunger_matron（饥群母兽） | 霜原冰缚 | RootSnare | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.snow_stalker（雪幕潜猎者） | 基础攻击 | BasicStrike | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.snow_stalker（雪幕潜猎者） | 霜原冰缚 | RootSnare | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.rimehorn_charger（霜角冲兽） | 冲锋 | Charge | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.rimehorn_charger（霜角冲兽） | 霜原冰缚 | RootSnare | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.frost_totem_keeper（冻柱守卫） | 战斗光环 | WarAura | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.frost_totem_keeper（冻柱守卫） | 霜原冰缚 | RootSnare | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.drowned_corpse（溺尸） | 基础攻击 | BasicStrike | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.drowned_corpse（溺尸） | 墓潮尸爆 | CorpseBurst | 4000 | 是 / 是 | 无独立文案 |
| core.enemy.crypt_beetle（墓穴甲虫） | 蓄力重击 | HeavySlam | 1200 | 是 / 是 | 扇形蓄力 |
| core.enemy.crypt_beetle（墓穴甲虫） | 墓潮尸爆 | CorpseBurst | 4000 | 是 / 是 | 无独立文案 |
| core.enemy.salt_corpse（盐尸） | 尸体爆发 | CorpseBurst | 1200 | 是 / 是 | 尸体红环 |
| core.enemy.bell_wraith（钟灵） | 秘术投射 | ArcaneBolt | 7000 | 否 / 是 | 无独立文案 |
| core.enemy.bell_wraith（钟灵） | 墓潮尸爆 | CorpseBurst | 7000 | 是 / 是 | 无独立文案 |
| core.enemy.tide_raider（潮盗） | 冲锋 | Charge | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.tide_raider（潮盗） | 墓潮尸爆 | CorpseBurst | 4000 | 是 / 是 | 无独立文案 |
| core.enemy.crypt_cantor（墓潮咏者） | 战斗光环 | WarAura | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.crypt_cantor（墓潮咏者） | 墓潮尸爆 | CorpseBurst | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.bone_tide_archer（骨潮射手） | 齐射 | Volley | 6000 | 否 / 是 | 无独立文案 |
| core.enemy.bone_tide_archer（骨潮射手） | 墓潮尸爆 | CorpseBurst | 6000 | 是 / 是 | 无独立文案 |
| core.enemy.grave_broodmother（墓穴育母） | 召唤兽群 | SummonSwarm | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.grave_broodmother（墓穴育母） | 墓潮尸爆 | CorpseBurst | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.mine_thrall（矿奴） | 基础攻击 | BasicStrike | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.mine_thrall（矿奴） | 熔炉地带 | GroundHazard | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.chain_hammer（链锤工） | 蓄力重击 | HeavySlam | 1200 | 是 / 是 | 扇形蓄力 |
| core.enemy.chain_hammer（链锤工） | 熔炉地带 | GroundHazard | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.furnace_sentry（熔炉哨机） | 齐射 | Volley | 6000 | 否 / 是 | 无独立文案 |
| core.enemy.furnace_sentry（熔炉哨机） | 熔炉地带 | GroundHazard | 6000 | 是 / 是 | 无独立文案 |
| core.enemy.slag_caster（炉渣咒机） | 持续危险地面 | GroundHazard | 7000 | 是 / 是 | 地面描边 |
| core.enemy.blood_press（血压机偶） | 冲锋 | Charge | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.blood_press（血压机偶） | 熔炉地带 | GroundHazard | 4500 | 是 / 是 | 无独立文案 |
| core.enemy.gear_marshal（齿轮监军） | 战斗光环 | WarAura | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.gear_marshal（齿轮监军） | 血炉修复 | RepairPulse | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.spark_drone（火花浮械） | 秘术投射 | ArcaneBolt | 7000 | 否 / 是 | 无独立文案 |
| core.enemy.spark_drone（火花浮械） | 熔炉地带 | GroundHazard | 7000 | 是 / 是 | 无独立文案 |
| core.enemy.foundry_brood（铸巢母机） | 召唤兽群 | SummonSwarm | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.foundry_brood（铸巢母机） | 血炉修复 | RepairPulse | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.penitent（赎罪者） | 基础攻击 | BasicStrike | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.penitent（赎罪者） | 虚空回响 | DelayedNova | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.void_zealot（虚空狂信徒） | 冲锋 | Charge | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.void_zealot（虚空狂信徒） | 虚空回响 | DelayedNova | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.rift_oracle（裂隙谕者） | 持续危险地面 | GroundHazard | 7000 | 是 / 是 | 地面描边 |
| core.enemy.rift_oracle（裂隙谕者） | 虚空回响 | DelayedNova | 7000 | 是 / 是 | 无独立文案 |
| core.enemy.oathless_crossbow（失誓弩手） | 齐射 | Volley | 6000 | 否 / 是 | 无独立文案 |
| core.enemy.oathless_crossbow（失誓弩手） | 虚空回响 | DelayedNova | 6000 | 是 / 是 | 无独立文案 |
| core.enemy.night_deacon（无光执事） | 战斗光环 | WarAura | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.night_deacon（无光执事） | 虚空回响 | DelayedNova | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.shard_summoner（碎界唤徒） | 召唤兽群 | SummonSwarm | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.shard_summoner（碎界唤徒） | 虚空回响 | DelayedNova | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.black_sun_guard（黑日禁卫） | 蓄力重击 | HeavySlam | 1200 | 是 / 是 | 扇形蓄力 |
| core.enemy.black_sun_guard（黑日禁卫） | 虚空回响 | DelayedNova | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.void_lance（虚矛祭兵） | 秘术投射 | ArcaneBolt | 7000 | 否 / 是 | 无独立文案 |
| core.enemy.void_lance（虚矛祭兵） | 虚空回响 | DelayedNova | 7000 | 是 / 是 | 无独立文案 |
| core.enemy.thorn_beast（棘兽） | 冲锋 | Charge | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.thorn_beast（棘兽） | 裂渊钻袭 | Burrow | 4000 | 是 / 是 | 无独立文案 |
| core.enemy.iron_dryad（铁皮树妖） | 战斗光环 | WarAura | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.iron_dryad（铁皮树妖） | 裂渊钻袭 | Burrow | 5500 | 是 / 是 | 无独立文案 |
| core.enemy.bog_beast（泥沼兽） | 蓄力重击 | HeavySlam | 1200 | 是 / 是 | 扇形蓄力 |
| core.enemy.bog_beast（泥沼兽） | 裂渊钻袭 | Burrow | 4000 | 是 / 是 | 无独立文案 |
| core.enemy.blood_leech（血蛭） | 尸体爆发 | CorpseBurst | 1200 | 是 / 是 | 尸体红环 |
| core.enemy.blood_leech（血蛭） | 裂渊钻袭 | Burrow | 4000 | 是 / 是 | 无独立文案 |
| core.enemy.crystal_scarab（晶壳虫） | 基础攻击 | BasicStrike | 1200 | 否 / 是 | 无独立文案 |
| core.enemy.crystal_scarab（晶壳虫） | 裂渊钻袭 | Burrow | 4000 | 是 / 是 | 无独立文案 |
| core.enemy.cinder_raven（烟羽鸦） | 齐射 | Volley | 6000 | 否 / 是 | 无独立文案 |
| core.enemy.cinder_raven（烟羽鸦） | 裂渊钻袭 | Burrow | 6000 | 是 / 是 | 无独立文案 |
| core.enemy.starved_aberration（饥星畸兽） | 持续危险地面 | GroundHazard | 7000 | 是 / 是 | 地面描边 |
| core.enemy.starved_aberration（饥星畸兽） | 裂渊钻袭 | Burrow | 7000 | 是 / 是 | 无独立文案 |
| core.enemy.rift_broodmother（裂界育母） | 召唤兽群 | SummonSwarm | 5500 | 否 / 是 | 无独立文案 |
| core.enemy.rift_broodmother（裂界育母） | 裂渊钻袭 | Burrow | 5500 | 是 / 是 | 无独立文案 |
| monsters.enemy.life_spore（愈生孢子） | 命能绽放 | HealingBloom | 5500 | 是 / 是 | 绿色花环 |
| monsters.enemy.life_spore（愈生孢子） | 孢子弹 | ArcaneBolt | 6500 | 否 / 是 | 无独立文案 |
| monsters.enemy.root_mauler（缠根掠兽） | 根须突袭 | Charge | 2000 | 否 / 是 | 无独立文案 |
| monsters.enemy.root_mauler（缠根掠兽） | 绞足藤 | RootSnare | 4500 | 是 / 是 | 蔓藤圆环 |
| monsters.enemy.carapace_bloomguard（甲壳花卫） | 花甲震击 | HeavySlam | 1800 | 是 / 是 | 无独立文案 |
| monsters.enemy.spore_spitter（命能喷吐者） | 腐育孢雨 | Volley | 6500 | 否 / 是 | 无独立文案 |
| monsters.enemy.spore_spitter（命能喷吐者） | 菌毯 | GroundHazard | 6000 | 是 / 是 | 青绿落点 |
| monsters.enemy.brood_vine（育种母株） | 播种幼体 | SummonSwarm | 5000 | 否 / 是 | 无独立文案 |
| monsters.enemy.brood_vine（育种母株） | 母株回春 | HealingBloom | 5500 | 是 / 是 | 无独立文案 |
| monsters.enemy.thorn_crown_keeper（荆冠看守） | 荆冠共生 | WarAura | 5500 | 否 / 是 | 无独立文案 |
| monsters.enemy.thorn_crown_keeper（荆冠看守） | 荆棘禁足 | RootSnare | 4500 | 是 / 是 | 无独立文案 |
| monsters.enemy.grafted_aberration（嫁接畸兽） | 嫁接重砸 | HeavySlam | 2000 | 是 / 是 | 无独立文案 |
| monsters.enemy.grafted_aberration（嫁接畸兽） | 命能溢流 | GroundHazard | 4000 | 是 / 是 | 无独立文案 |
| monsters.enemy.harvest_avatar（丰收化身） | 丰收轮转 | DelayedNova | 6000 | 是 / 是 | 三层花瓣环 |
| monsters.enemy.harvest_avatar（丰收化身） | 收割回春 | HealingBloom | 6000 | 是 / 是 | 无独立文案 |
| monsters.enemy.red_thrall（赤誓奴兵） | 血刃 | BasicStrike | 0 | 否 / 是 | 无独立文案 |
| monsters.enemy.bloodhound（放血猎犬） | 放血扑咬 | Charge | 2000 | 否 / 是 | 无独立文案 |
| monsters.enemy.blood_banner（血旗侍从） | 赤誓战旗 | WarAura | 5500 | 否 / 是 | 无独立文案 |
| monsters.enemy.blood_banner（血旗侍从） | 献血号令 | Sacrifice | 5500 | 是 / 是 | 无独立文案 |
| monsters.enemy.pyre_crossbow（火刑弩手） | 火刑齐射 | SuppressingVolley | 7000 | 否 / 是 | 无独立文案 |
| monsters.enemy.pyre_crossbow（火刑弩手） | 焚刑地带 | GroundHazard | 6000 | 是 / 是 | 无独立文案 |
| monsters.enemy.sacrifice_magus（献祭术士） | 血焰献祭 | Sacrifice | 6000 | 是 / 是 | 血色收缩环 |
| monsters.enemy.sacrifice_magus（献祭术士） | 誓火 | ArcaneBolt | 7000 | 否 / 是 | 无独立文案 |
| monsters.enemy.armor_executioner（裂甲处刑者） | 断首处决 | Execution | 1900 | 是 / 是 | 赤色扇面 |
| monsters.enemy.armor_executioner（裂甲处刑者） | 裂甲横扫 | HeavySlam | 2100 | 是 / 是 | 无独立文案 |
| monsters.enemy.oathblood_rider（誓血骑士） | 誓血冲阵 | Charge | 2200 | 否 / 是 | 无独立文案 |
| monsters.enemy.oathblood_rider（誓血骑士） | 燃命 | Sacrifice | 3500 | 是 / 是 | 无独立文案 |
| monsters.enemy.red_crown_arbiter（赤冠裁决官） | 赤冠裁决 | DelayedNova | 6500 | 是 / 是 | 赤金审判环 |
| monsters.enemy.red_crown_arbiter（赤冠裁决官） | 终刑 | Execution | 4000 | 是 / 是 | 无独立文案 |
| monsters.enemy.blue_acolyte（苍誓卫徒） | 星钢斩 | BasicStrike | 0 | 否 / 是 | 无独立文案 |
| monsters.enemy.star_arrow（星矢射手） | 星矢齐射 | Volley | 7000 | 否 / 是 | 无独立文案 |
| monsters.enemy.star_arrow（星矢射手） | 坠星标记 | DelayedNova | 7000 | 是 / 是 | 蓝色落星圈 |
| monsters.enemy.time_frozen_deacon（冻时执事） | 冻时祷文 | RootSnare | 5500 | 是 / 是 | 无独立文案 |
| monsters.enemy.time_frozen_deacon（冻时执事） | 苍誓护链 | ShieldLink | 6000 | 否 / 是 | 无独立文案 |
| monsters.enemy.storm_ring_magus（雷环术士） | 雷环连锁 | ChainLightning | 7000 | 否 / 是 | 无独立文案 |
| monsters.enemy.storm_ring_magus（雷环术士） | 延时雷暴 | DelayedNova | 6000 | 是 / 是 | 闪烁双环 |
| monsters.enemy.mirror_shield（镜盾侍卫） | 镜盾链接 | ShieldLink | 5000 | 否 / 是 | 无独立文案 |
| monsters.enemy.mirror_shield（镜盾侍卫） | 盾镜冲击 | HeavySlam | 1700 | 是 / 是 | 无独立文案 |
| monsters.enemy.delayed_oracle（延时预言者） | 预言回响 | DelayedNova | 6500 | 是 / 是 | 三段倒计时环 |
| monsters.enemy.delayed_oracle（延时预言者） | 苍星碎片 | ArcaneBolt | 7000 | 否 / 是 | 无独立文案 |
| monsters.enemy.star_gate_caller（星门召集者） | 开启星门 | SummonSwarm | 5500 | 否 / 是 | 无独立文案 |
| monsters.enemy.star_gate_caller（星门召集者） | 星门屏障 | ShieldLink | 6000 | 否 / 是 | 无独立文案 |
| monsters.enemy.sky_arbiter（苍穹审判官） | 苍穹审判 | DelayedNova | 7000 | 是 / 是 | 苍白全环 |
| monsters.enemy.sky_arbiter（苍穹审判官） | 群星连裁 | ChainLightning | 7000 | 否 / 是 | 无独立文案 |
| monsters.enemy.fallen_spearman（亡旗枪兵） | 列阵突刺 | BasicStrike | 1700 | 否 / 是 | 无独立文案 |
| monsters.enemy.breach_axeman（破阵斧手） | 破阵斩 | HeavySlam | 2000 | 是 / 是 | 无独立文案 |
| monsters.enemy.trench_crossbow（战壕弩兵） | 压制齐射 | SuppressingVolley | 7500 | 否 / 是 | 无独立文案 |
| monsters.enemy.firepot_thrower（火罐投手） | 火罐抛击 | Artillery | 8000 | 是 / 是 | 橙色落点 |
| monsters.enemy.war_drummer（鼓令官） | 战鼓号令 | WarAura | 6000 | 否 / 是 | 无独立文案 |
| monsters.enemy.war_drummer（鼓令官） | 鼓槌 | BasicStrike | 0 | 否 / 是 | 无独立文案 |
| monsters.enemy.shieldwall_guard（盾墙卫士） | 盾墙 | ShieldLink | 5500 | 否 / 是 | 无独立文案 |
| monsters.enemy.shieldwall_guard（盾墙卫士） | 盾墙推进 | HeavySlam | 1600 | 是 / 是 | 无独立文案 |
| monsters.enemy.headhunt_officer（猎首军官） | 猎首突进 | Charge | 2200 | 否 / 是 | 无独立文案 |
| monsters.enemy.headhunt_officer（猎首军官） | 军官处决 | Execution | 2000 | 是 / 是 | 无独立文案 |
| monsters.enemy.siege_engineer（攻城术师） | 炮击标记 | Artillery | 9000 | 是 / 是 | 三枚炮击落点 |
| monsters.enemy.siege_engineer（攻城术师） | 战地修复 | RepairPulse | 6000 | 是 / 是 | 无独立文案 |

## 末旗统帅的 4 个继承技能子项

Bosses.CombatProfile 在统帅的3个目录技能后追加两位军官的4个技能；SpatialCombatRunner 按 EffectiveSkills 轮转。因此Boss目录仍是70条，但实际动画核对需覆盖74个所属Boss/技能组合，不可遗漏继承动作。

| 所属Boss | 来源 | 继承技能 | 生产类型 | 第四阶段核对 |
| --- | --- | --- | --- | --- |
| monsters.boss.warfront.last_marshal | iron_banner | 盾墙推进 | ShieldLink | 链接对象、有效范围、解除与原军官一致 |
| monsters.boss.warfront.last_marshal | iron_banner | 猎首号令 | Charge | 实际目标、起止位置、方向与结束事件 |
| monsters.boss.warfront.last_marshal | ember_cannon | 三点炮击 | Artillery | 真实落点和次数，不按技能名凭空画三处 |
| monsters.boss.warfront.last_marshal | ember_cannon | 压制齐射 | Charge | 当前类型是Charge而非独立Volley；按真实行为审计，不能只凭文案假画箭雨 |

## 27 个Boss的静态可达路径

| 所属目录 / 数量 | 已核对的生产路径 |
| --- | --- |
| 战役 / 5 | SceneTimelineBuilder.BuildCampaign 按幕数选 CampaignBosses，并传入 NodeCombatRequest.BossStableId |
| 地图 / 12 | MapCatalog.Areas 与 MapBosses 同源；Gameplay.Build → Bosses.ForArea(map.AreaId) → Boss节点 |
| 军官 / 2 | Warfront路线的 Gameplay.Build 从 WarfrontOfficers 中取样，写入 WarfrontOfficer 节点 |
| 统帅 / 1 | Warfront路线末节点固定 WarfrontCommander；包含上表4个继承动作 |
| 突破 / 1 | GameSession.AssignBossChallenge → BreakthroughMapPrefix → MapPlanner.Build 的突破分支 |
| 天垒 / 3 | GameSession.AssignBossChallenge → CitadelMapPrefix / 演练前缀 → MapPlanner.Build 的三个阶段节点 |
| 港区 / 3 | HarborRunner.Run → NodeCombatRequest.BossStableId → SpatialCombatRunner.CreateEnemies → Bosses.CombatProfile；按区域绑定断缆船长、沉仓守卫、沉金典狱长 |

共同链路为 SceneTimelineBuilder → NodeCombatRequest → SpatialCombatRunner.CreateEnemies → Bosses.CombatProfile；生命阈值和狂暴计时由敌人动作循环生成 BossPhaseChanged。以上确认的是静态入口与消费者，不声称逐Boss动态通关已验收。

Bosses.CombatProfile 将大部分目录技能按位置映射为 HeavySlam/Charge/DelayedNova，另有军官特例。目录中的扇形、箭道、双环等文案不是实际几何证明。第四阶段以真实命中/运动/持续区域契约为准：修正误导表现和文案，不为凑动画而擅自新增Boss战斗机制。
