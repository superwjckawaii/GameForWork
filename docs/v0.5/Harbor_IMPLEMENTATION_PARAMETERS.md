# 沉金港装备实现参数与接入契约

状态：第一阶段补齐的初始实现规格；第二批已按本表接入正式目录和运行时消费者。核心机制沿用已确认[装备目录](Harbor_EQUIPMENT_DESIGN.md)，以下基础面板与固定属性仍不声称已经平衡验证。后续数值调校须记录差异，不得改掉无上限耗蓝、五词缀容量、多附魔等已确认机制。

## 通用生成规则

- 新身份以 `harbor.base.*`、`harbor.legendary.*` 命名，无阶段开发编号。物品等级初始三档为 100/110/120；需求等级独立于物品等级，不自动提高至物品等级。所有港口底材标记巅峰身份。
- 下列参照底材只复制明确列出的基础数值、属性需求与容量，不复制其基底词缀、特殊掉落来源或专属词缀权限；港口基底完整替换旧基底。未列的基础数值为零。
- 港口箱的底材候选初始为稀有装备，默认品质 0、按[流程参数](Harbor_FLOW_PARAMETERS.md)的概率获得品质20，按物品等级生成自然显式词缀；按正常连接生成规则，不额外保证满连。传奇品质初始 10，固定属性按下表，不再生成随机普通前后缀。
- 普通/魔法/稀有词缀容量：常规底材沿用 0/0、1/1、3/3；逐浪为 0/0、0/3、0/5，守港为 0/0、3/0、5/0。材料只改变稀有度时必须重新验证容量，不能凭空补满词缀。
- 数值放大对本件显式组件计算一次；负数保留符号同倍缩放。布尔、容量、单位数量等规则组件不放大；概率和抗性仍受已有硬上限。多组件词缀逐组件分类，不把整条描述当公式。

## 12 件直接参照的基础面板

伤害是单次基础物理区间；攻击速度为每秒次数，暴击为基础概率；需求顺序为等级/体魄/灵巧/精神/能量；容量为核心/辅助/孔数。防御为原始掷值区间，不含品质和词缀。

| 名称 / 新身份后缀 | 参照正式身份 | 物理/攻速/暴击 | 防御 | 需求 | 容量 | 格挡/移速惩罚 |
| --- | --- | --- | --- | --- | --- | --- |
| 潮行细剑 / tidewalker_rapier | equipment.base.204.e415283651 | 45～78 / 1.75 / 6.25% | 甲0～0 闪0～0 盾0～0 | 80/130/85/0/0 | 1/2/3 | 0% / 0% |
| 断缆手斧 / cablecleaver_axe | equipment.base.208.dcac9f64e5 | 68～108 / 1.35 / 5% | 甲0～0 闪0～0 盾0～0 | 80/130/85/0/0 | 1/2/3 | 0% / 0% |
| 回潮长剑 / returning_tide_sword | equipment.base.216.6a136197b0 | 115～185 / 1.45 / 6% | 甲0～0 闪0～0 盾0～0 | 80/155/100/0/0 | 1/2/6 | 0% / 0% |
| 沉锚重锤 / sunken_anchor_maul | equipment.base.224.a30ef959af | 165～255 / 1 / 5% | 甲0～0 闪0～0 盾0～0 | 80/250/0/0/0 | 1/2/6 | 0% / 0% |
| 掠潮长弓 / tideskimmer_bow | equipment.base.228.1b2753f9a3 | 82～140 / 1.22 / 6% | 甲0～0 闪0～0 盾0～0 | 80/0/130/0/0 | 1/5/6 | 0% / 0% |
| 潮汐法杖 / tidal_wand | equipment.base.236.5b39f77ba1 | 18～32 / 1.42 / 7.25% | 甲0～0 闪0～0 盾0～0 | 80/0/0/85/130 | 1/5/6 | 0% / 0% |
| 顺流箭袋 / downstream_quiver | equipment.base.quiver.5 | 0～0 / 0 / 0% | 甲0～0 闪215～245 盾0～0 | 80/0/120/0/0 | 0/3/4 | 0% / 0% |
| 逐浪指环 / wavechaser_ring | equipment.base.vermillion_ring | 0～0 / 0 / 0% | 甲0～0 闪0～0 盾0～0 | 80/0/0/0/0 | 0/0/0 | 0% / 0% |
| 守港指环 / harbor_guard_ring | equipment.base.vermillion_ring | 0～0 / 0 / 0% | 甲0～0 闪0～0 盾0～0 | 80/0/0/0/0 | 0/0/0 | 0% / 0% |
| 三潮吊坠 / three_tides_amulet | equipment.base.blue_pearl_amulet | 0～0 / 0 / 0% | 甲0～0 闪0～0 盾0～0 | 77/0/0/0/0 | 0/0/0 | 0% / 0% |
| 回流束带 / backflow_belt | equipment.base.crystal_belt | 0～0 / 0 / 0% | 甲0～0 闪0～0 盾0～0 | 79/0/0/0/0 | 0/0/0 | 0% / 0% |
| 定锚重带 / anchored_belt | equipment.base.crystal_belt | 0～0 / 0 / 0% | 甲0～0 闪0～0 盾0～0 | 79/0/0/0/0 | 0/0/0 | 0% / 0% |

## 6 件补齐面板

这些是匹配既定装备类别的新参数，不伪装为已有同名底材。头盔、手套和鞋按同档防御预算分配，纯护甲胸甲/盾不混入灵障。

| 名称 / 新身份后缀 | 基础面板 | 需求（等级/体/巧/精/能） | 核心/辅助/孔数 | 特殊身份 |
| --- | --- | --- | --- | --- |
| 深汐长杖 / deep_tide_staff | 物理 40～70；攻速 1.10；暴击 6% | 80/0/0/100/180 | 1/5/6 | 新增 Staff 武器族，双手占用；caster/staff/two_hand，不能拥有 wand/one_hand 标签 |
| 返潮重盾 / returning_tide_shield | 护甲 412～474；基础格挡 24%；移速惩罚 3% | 80/159/0/0/0 | 0/0/3 | 真盾；面板参照艾兹麦塔盾，不继承生命基底 |
| 静潮兜帽 / still_tide_hood | 闪避 190～230；护盾 45～60 | 80/0/85/0/85 | 0/1/4 | 头盔、闪避/能量护盾，不是灵障 |
| 压舱重铠 / ballast_plate | 护甲 800～1000；移速惩罚 5% | 80/180/0/0/0 | 1/2/6 | 纯护甲胸甲 |
| 破浪护手 / wavebreaker_gloves | 护甲 110～140；闪避 110～140 | 80/65/65/0/0 | 0/0/4 | 护甲/闪避手套 |
| 涉潮长靴 / tidewading_boots | 闪避 220～280 | 80/0/120/0/0 | 0/0/4 | 纯闪避鞋，无额外基础移速 |

## 8 件传奇的完整固定属性

每件的“完整”指本表固定属性加已确认专属机制、负面机制和对应基底三者合并；不额外补普通随机词缀。范围为实例随机掷值，品质另按既有局部规则计算。

| 名称 / 新身份后缀 | 基底 | 固定属性（不含已确认机制） |
| --- | --- | --- |
| 逆潮之锋 / reverse_tide_edge | tidewalker_rapier | 本地物理伤害提高 100%～130%；本地攻击速度提高 10%～15%；全局命中 +200～300 |
| 灯塔守望 / lighthouse_watch | tideskimmer_bow | 本地物理伤害提高 110%～140%；本地攻击速度提高 8%～12%；投射物速度提高 20%～30% |
| 无眠领航者 / sleepless_navigator | still_tide_hood | 本地闪避与护盾提高 80%～110%；最大法力 +50～80；闪电抗性 +25%～35% |
| 最后一舱 / last_hold | ballast_plate | 本地护甲提高 100%～140%；最大生命 +120～160；火焰抗性 +25%～35% |
| 双潮织手 / twin_tide_weaver | wavebreaker_gloves | 本地护甲与闪避提高 80%～110%；最大生命 +50～70；最大法力 +40～60 |
| 不归航迹 / unreturning_wake | tidewading_boots | 本地闪避提高 80%～110%；移动速度提高 25%～30%；最大生命 +50～70 |
| 三潮共鸣 / three_tides_resonance | three_tides_amulet | 四属性各 +15～25；最大法力 +50～80；所有元素抗性 +12%～18%（火/冰/雷，不包含虚空） |
| 空瓶誓约 / empty_bottle_oath | backflow_belt | 最大生命 +70～100；最大法力 +40～60；冰霜抗性 +25%～35% |

## 三附魔可达性核对

运行时 EquipmentEnchantmentCatalog.Supports 对普通项链给出 24 条适用附魔。可行实例：坚生命纹＋涌泉刻印＋冥想刻印，三条身份不同；不依赖灵兽专属标签或新增附魔。下列是本次实际候选，不将文字里的旧单附魔限制当作三潮特例仍只能一条。

| 正式身份 | 名称 |
| --- | --- |
| equipment.enchantment.01.a8e8bab605 | 精准刻印 |
| equipment.enchantment.02.3bddd61e27 | 坚生命纹 |
| equipment.enchantment.12.e2b595989a | 秘法铭文 |
| equipment.enchantment.13.c76598fb2f | 三相铭文 |
| equipment.enchantment.14.26e0f4135f | 虚蚀铭文 |
| equipment.enchantment.15.7edfeb779e | 锐目刻印 |
| equipment.enchantment.16.9b6aa7503a | 毁伤铭文 |
| equipment.enchantment.17.3951e7d02f | 涌泉刻印 |
| equipment.enchantment.19.76ed6148fe | 冥想刻印 |
| equipment.enchantment.23.f45f336e95 | 虹彩王印 |
| equipment.enchantment.27.9a3e1e6238 | 双咒王印 |
| equipment.enchantment.28.0d4f2faf08 | 不离王印 |
| equipment.enchantment.42.6a0cb0bd4d | 蛮力刻印 |
| equipment.enchantment.43.fb0f1fa365 | 灵风刻印 |
| equipment.enchantment.44.4eff18a7e5 | 明识刻印 |
| equipment.enchantment.45.8ab6e62b88 | 聚能刻印 |
| equipment.enchantment.46.f6f8ef1b19 | 巨灵铭文 |
| equipment.enchantment.47.013ae3c5b4 | 疾影铭文 |
| equipment.enchantment.48.649b864f53 | 睿思铭文 |
| equipment.enchantment.49.84a357b273 | 星能铭文 |
| equipment.enchantment.50.1b3cbf0759 | 泰坦王印 |
| equipment.enchantment.51.8b4eacbb2f | 逐风王印 |
| equipment.enchantment.52.4eb4b6011f | 万象王印 |
| equipment.enchantment.53.a7b056cfa8 | 星海王印 |

数值放大白名单为伤害、速度、固定属性/资源、防御、恢复等数值组件；双咒容量、不离复生次数等不放大。当前未发现独立多附魔互斥模型：实现时继续限制适用装备、同身份重复，并保留已有规则互斥；不能默默额外禁止三条不同合法附魔。选择替换前应显示完整三条结果，支付与替换原子提交。

## 实际消费者与必须新增的契约

| 机制 | 既有接入点 | 实施要求与验收 |
| --- | --- | --- |
| 美德/恶德上限与定时慈悲 | CharacterBuildAssembler.VirtueVice / VirtueViceState | 上限只加一次，定时获取在战斗时钟执行，暂停不累计、离线一致；致死保护必须实际 Consume 成功 |
| 五秒耗蓝增伤 | ResourceState.TryPaySkillCost / LastSkillManaPaid / EquipmentCombatRuntime | 保存实际支付时间序列，超过五秒移出；本次成功支付计入后续该次伤害，免费施放为零；精度按基点，不擅自封顶；新战斗清零 |
| 移动条件、近距/残血 | SpatialCombatRunner / EquipmentCombatRuntime | 从实际事件与位置读取，命中前快照判距判血；移动不能用动画位移判定 |
| 充能延迟/不被击中打断 | ResourceState 与实际受伤/护盾充能路径 | 独立延迟、速率和中断原因，不以通用恢复冒充充能；只豁免敌方击中中断 |
| 异常免疫 | 异常施加与周期伤害入口 | 流血/点燃阻止施加；不顺带免疫其他火焰/物理持续伤害；加载已有异常时同步免疫结果 |
| 单侧五词缀及双倍效果 | ItemGenerator / EquipmentCraftingService / ItemRebinder / EquipmentLoadout | 统一容量提供者覆盖生成、增删重铸、破裂/保护、迁移与UI；拒绝非法组件，不改普通装备容量 |
| 三附魔 | ItemInstance.Enchantment / ApplyEnchantment / 聚合与存档 | 单值迁移到列表，普通装备容量1、三潮容量3，旧条目无损；固定身份、替换和扣费防重 |
| 药剂通用效果与瞬时恢复 | FlaskRack / 生命法力恢复入口 | 通用效果和生命专用效果分别相加后各走真实消费者；即时化总量不重复逐跳恢复，不延长功能增益 |
| 蓄势、交替、致死保护 | BattleArmy / SpatialCombatRunner / EquipmentCombatRuntime | 使用唯一动作ID、命中与消耗顺序，重复/触发不重复领效果；多段致死按每段与冷却处理 |

阶段二先验证港口与事件契约，阶段三实现装备；本文件不要求在第一阶段提前添加运行时玩法。
