#!/usr/bin/env python3
"""Build the deterministic P19 equipment snapshot from PoB Community Lua data."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path


TARGETS = {
    "TwoHandWeapon": ("MainHand", 12, ("sword.lua", "axe.lua", "mace.lua", "staff.lua")),
    "OneHandWeapon": ("MainHand", 8, ("sword.lua", "axe.lua", "mace.lua")),
    "Shield": ("OffHand", 8, ("shield.lua",)),
    "BodyArmor": ("Chest", 9, ("body.lua",)),
    "Helmet": ("Helmet", 8, ("helmet.lua",)),
    "Gloves": ("Gloves", 6, ("gloves.lua",)),
    "Boots": ("Boots", 6, ("boots.lua",)),
    "Belt": ("Belt", 5, ("belt.lua",)),
    "Amulet": ("Amulet", 6, ("amulet.lua",)),
    "Ring": ("RingLeft", 7, ("ring.lua",)),
    "LifeFlask": ("Flask1", 5, ("flask.lua",)),
}

LEGACY = {
    "TwoHandWeapon": [
        ("core.base.rusted_greatsword", "生锈巨剑"), ("core.base.heavy_battleaxe", "沉重战斧"),
        ("core.base.pole_warhammer", "长柄战锤"), ("core.base.ash_glaive", "烬锋长刃"),
        ("core.base.warden_maul", "监守重槌"), ("core.base.blood_halberd", "血痕战戟"),
        ("core.base.glass_greatblade", "琉璃巨刃"), ("core.base.oathbreaker_axe", "破誓巨斧"),
    ],
    "OneHandWeapon": [("core.base.rusted_warhammer", "锈蚀战锤")],
    "Shield": [("core.base.ash_iron_shield", "灰铁塔盾")],
    "BodyArmor": [
        ("core.base.crude_chainmail", "粗制链甲"), ("core.base.hide_coat", "兽皮外衣"),
        ("core.base.runed_robe", "符文长袍"), ("core.base.bastion_plate", "堡垒板甲"),
        ("core.base.gloom_raiment", "幽影战衣"), ("core.base.starweave_robe", "星织法袍"),
        ("core.base.triune_carapace", "三相甲壳"),
    ],
    "Helmet": [
        ("core.base.iron_helmet", "铁制盔"), ("core.base.hunter_hood", "猎手兜帽"),
        ("core.base.ash_circlet", "灰纹头冠"), ("core.base.warlord_helm", "军阀重盔"),
        ("core.base.raven_mask", "鸦影面具"), ("core.base.oracle_crown", "先知冠冕"),
    ],
    "Gloves": [("core.base.iron_gauntlets", "铁鳞护手"), ("core.base.ritual_gloves", "仪式手套")],
    "Boots": [("core.base.march_boots", "行军铁靴"), ("core.base.shadow_treads", "影行短靴")],
    "Belt": [("core.base.chain_belt", "锁链腰带"), ("core.base.ration_belt", "补给腰带")],
    "Amulet": [("core.base.ember_amulet", "余烬护符"), ("core.base.spirit_amulet", "祷灵护符")],
    "Ring": [
        ("core.base.iron_ring", "铁环"), ("core.base.life_ring", "生命戒"),
        ("core.base.focus_ring", "专注戒"), ("core.base.ember_ring", "余火戒"),
        ("core.base.guard_ring", "壁垒戒"), ("core.base.quicksilver_ring", "迅银戒"),
    ],
    "LifeFlask": [
        ("core.base.life_flask", "生命药剂"), ("core.base.mana_flask", "法力药剂"),
        ("core.base.armor_flask", "玄铁药剂"), ("core.base.movement_flask", "疾行药剂"),
        ("core.base.resistance_flask", "棱彩药剂"),
    ],
}

DISPLAY_PREFIX = {
    "TwoHandWeapon": "远古重武", "OneHandWeapon": "远古单手武器", "Shield": "远古盾牌",
    "BodyArmor": "远古胸甲", "Helmet": "远古头盔", "Gloves": "远古手套", "Boots": "远古战靴",
    "Belt": "远古腰带", "Amulet": "远古护符", "Ring": "远古戒指", "LifeFlask": "远古药剂",
}

FIXED_SELECTION = {
    "TwoHandWeapon": ["Driftwood Maul"],
    "Ring": ["Iron Ring", "Coral Ring", "Paua Ring", "Ruby Ring", "Sapphire Ring", "Topaz Ring", "Vermillion Ring"],
    "LifeFlask": ["Small Life Flask", "Small Mana Flask", "Granite Flask", "Quicksilver Flask", "Bismuth Flask"],
}


def field(text: str, name: str, default: int = 0) -> int:
    match = re.search(rf"\b{re.escape(name)}\s*=\s*([-\d.]+)", text)
    return int(float(match.group(1))) if match else default


def decimal_field(text: str, name: str, default: float = 0.0) -> float:
    match = re.search(rf"\b{re.escape(name)}\s*=\s*([-\d.]+)", text)
    return float(match.group(1)) if match else default


def text_field(text: str, name: str) -> str:
    match = re.search(rf'\b{re.escape(name)}\s*=\s*"([^"]*)"', text)
    return match.group(1) if match else ""


def lua_blocks(path: Path):
    source = path.read_text(encoding="utf-8")
    marker = re.compile(r'itemBases\["([^"]+)"\]\s*=\s*\{')
    for match in marker.finditer(source):
        depth = 1
        cursor = match.end()
        while cursor < len(source) and depth:
            depth += (source[cursor] == "{") - (source[cursor] == "}")
            cursor += 1
        yield match.group(1), source[match.start():cursor]


def tags(block: str) -> list[str]:
    match = re.search(r"\btags\s*=\s*\{([^}]*)\}", block)
    if not match:
        return []
    return sorted(re.findall(r"([A-Za-z0-9_]+)\s*=\s*true", match.group(1)))


def eligible(category: str, block: str) -> bool:
    item_type = text_field(block, "type")
    item_tags = tags(block)
    if "demigods" in item_tags or ("not_for_sale" in item_tags and "atlas_base_type" not in item_tags):
        return False
    if category == "TwoHandWeapon":
        return "Two Hand" in item_type or item_type in {"Staff", "Warstaff"}
    if category == "OneHandWeapon":
        return "One Hand" in item_type
    if category == "LifeFlask":
        return item_type == "Flask"
    return True


def spread(items: list[dict], count: int) -> list[dict]:
    items = sorted(items, key=lambda item: (item["requiredLevel"], item["sourceId"]))
    if len(items) < count:
        raise ValueError(f"Need {count} bases but only found {len(items)}")
    if count == 1:
        return [items[0]]
    indexes = [round(index * (len(items) - 1) / (count - 1)) for index in range(count)]
    return [items[index] for index in indexes]


def implicit(text: str) -> tuple[str, int, int]:
    if not text:
        return "None", 0, 0
    values = [int(value) for value in re.findall(r"\d+", text)]
    lo = values[0] if values else 0
    hi = values[1] if len(values) > 1 else lo
    if "to maximum Life" in text: return "FlatMaximumLife", lo, hi
    if "to maximum Mana" in text: return "FlatMaximumMana", lo, hi
    if "Fire Resistance" in text and "Cold Resistance" not in text and "Lightning Resistance" not in text:
        return "FireResistanceBasisPoints", lo * 100, hi * 100
    if "Cold Resistance" in text and "Fire Resistance" not in text and "Lightning Resistance" not in text:
        return "ColdResistanceBasisPoints", lo * 100, hi * 100
    if "Lightning Resistance" in text and "Fire Resistance" not in text and "Cold Resistance" not in text:
        return "LightningResistanceBasisPoints", lo * 100, hi * 100
    if "Chaos Resistance" in text or "Void Resistance" in text:
        return "VoidResistanceBasisPoints", lo * 100, hi * 100
    if "Strength" in text: return "Physique", lo, hi
    if "Dexterity" in text: return "Dexterity", lo, hi
    if "Intelligence" in text: return "Spirit", lo, hi
    if "increased" in text and "Physical Damage" in text: return "IncreasedPhysicalDamageBasisPoints", lo * 100, hi * 100
    if "Physical Damage" in text: return "AddedPhysicalDamage", lo, hi
    if "Attack Speed" in text: return "IncreasedAttackSpeedBasisPoints", lo * 100, hi * 100
    if "Armour" in text: return "IncreasedArmorBasisPoints", lo * 100, hi * 100
    return "None", 0, 0


def base_record(category: str, slot: str, source_id: str, block: str) -> dict:
    item_tags = tags(block)
    armour_min, armour_max = field(block, "ArmourBaseMin"), field(block, "ArmourBaseMax")
    evasion_min, evasion_max = field(block, "EvasionBaseMin"), field(block, "EvasionBaseMax")
    shield_min, shield_max = field(block, "EnergyShieldBaseMin"), field(block, "EnergyShieldBaseMax")
    implicit_text = text_field(block, "implicit").replace("Chaos", "Void")
    implicit_kind, implicit_min, implicit_max = implicit(implicit_text)
    return {
        "stableId": "", "displayName": "", "sourceId": source_id, "category": category,
        "primarySlot": slot, "requiredLevel": field(block, "level", 1),
        "requiredPhysique": field(block, "str"), "requiredDexterity": field(block, "dex"),
        "requiredSpirit": field(block, "int"), "requiredEnergy": 0, "tags": item_tags,
        "minimumPhysicalDamage": field(block, "PhysicalMin"), "maximumPhysicalDamage": field(block, "PhysicalMax"),
        "attacksPerSecondMilli": round(decimal_field(block, "AttackRateBase") * 1000),
        "criticalChanceBasisPoints": round(decimal_field(block, "CritChanceBase") * 100),
        "armorMinimum": armour_min, "armorMaximum": armour_max,
        "evasionMinimum": evasion_min, "evasionMaximum": evasion_max,
        "shieldMinimum": shield_min, "shieldMaximum": shield_max,
        "blockChanceBasisPoints": field(block, "BlockChance") * 100,
        "movementPenaltyBasisPoints": field(block, "MovementPenalty") * 100,
        "socketLimit": field(block, "socketLimit"), "implicitText": implicit_text,
        "implicitModifier": implicit_kind, "implicitMinimumValue": implicit_min,
        "implicitMaximumValue": implicit_max,
    }


def load_bases(root: Path) -> tuple[list[dict], list[Path]]:
    result: list[dict] = []
    paths: set[Path] = set()
    for category, (slot, count, names) in TARGETS.items():
        candidates: list[dict] = []
        for name in names:
            path = root / "Data" / "Bases" / name
            paths.add(path)
            for source_id, block in lua_blocks(path):
                if eligible(category, block):
                    candidates.append(base_record(category, slot, source_id, block))
        candidates = list({item["sourceId"]: item for item in candidates}.values())
        if category in FIXED_SELECTION:
            by_source = {item["sourceId"]: item for item in candidates}
            fixed = [by_source[source] for source in FIXED_SELECTION[category]]
            remaining = [item for item in candidates if item["sourceId"] not in FIXED_SELECTION[category]]
            selected = fixed + spread(remaining, count - len(fixed)) if len(fixed) < count else fixed
        else:
            selected = spread(candidates, count)
        legacy = LEGACY[category]
        for index, item in enumerate(selected):
            if item["requiredSpirit"] > 0 and index % 2 == 1:
                item["requiredEnergy"] = item["requiredSpirit"]
                item["requiredSpirit"] = 0
            if index < len(legacy):
                item["stableId"], item["displayName"] = legacy[index]
            else:
                slug = re.sub(r"[^a-z0-9]+", "_", item["sourceId"].lower()).strip("_")
                item["stableId"] = f"p19.base.{slug}"
                item["displayName"] = f"{DISPLAY_PREFIX[category]}·{index + 1}"
            item["coreSkillCapacity"] = 1 if category in {"TwoHandWeapon", "OneHandWeapon", "BodyArmor"} else 0
            item["supportLinkCapacity"] = {
                "TwoHandWeapon": 2, "OneHandWeapon": 2, "BodyArmor": 2, "Helmet": 1,
            }.get(category, 0)
        result.extend(selected)
    return result, sorted(paths)


MOD_PATTERNS = [
    (r"to Strength", "Physique", "体魄"), (r"to Dexterity", "Dexterity", "灵巧"),
    (r"to Intelligence", "Spirit", "精神"), (r"maximum Life", "FlatMaximumLife", "最大生命"),
    (r"maximum Mana", "FlatMaximumMana", "最大法力"),
    (r"to Fire Resistance", "FireResistanceBasisPoints", "火焰抗性"),
    (r"to Cold Resistance", "ColdResistanceBasisPoints", "冰霜抗性"),
    (r"to Lightning Resistance", "LightningResistanceBasisPoints", "闪电抗性"),
    (r"to Chaos Resistance", "VoidResistanceBasisPoints", "虚空抗性"),
    (r"increased Attack Speed", "IncreasedAttackSpeedBasisPoints", "攻击速度增加"),
    (r"increased Critical Strike Chance", "IncreasedCriticalChanceBasisPoints", "暴击率增加"),
    (r"increased Physical Damage", "IncreasedPhysicalDamageBasisPoints", "物理伤害增加"),
    (r"to Accuracy Rating", "FlatAccuracy", "命中值"),
    (r"increased Armour", "IncreasedArmorBasisPoints", "护甲增加"),
    (r"increased Evasion Rating", "IncreasedEvasionBasisPoints", "闪避增加"),
    (r"increased Energy Shield", "IncreasedShieldBasisPoints", "护盾增加"),
    (r"increased Movement Speed", "IncreasedMovementSpeedBasisPoints", "移动速度增加"),
    (r"increased Mana Regeneration Rate", "IncreasedManaRegenerationBasisPoints", "法力恢复增加"),
    (r"increased Recovery rate", "IncreasedLifeFlaskEffectBasisPoints", "药剂恢复速度增加"),
    (r"increased Amount Recovered", "IncreasedLifeFlaskEffectBasisPoints", "药剂恢复量增加"),
    (r"increased (?:Life|Mana) Recovered", "IncreasedLifeFlaskEffectBasisPoints", "药剂恢复量增加"),
    (r"Chance to Block", "BlockChanceBasisPoints", "格挡概率"),
    (r"chance to Suppress Spell Damage", "SpellSuppressionBasisPoints", "法术压制概率"),
    (r"Life per second", "FlatLifeRegeneration", "每秒生命恢复"),
]


def list_field(line: str, name: str) -> list[str]:
    match = re.search(rf"\b{name}\s*=\s*\{{([^}}]*)\}}", line)
    return re.findall(r'"([^"]+)"', match.group(1)) if match else []


def classify(raw: str):
    if any(word in raw for word in ("Minion", "Totem", "Trap", "Mine", "Poison", "Bow", "Wand")):
        return None
    for pattern, kind, display in MOD_PATTERNS:
        if re.search(pattern, raw, re.IGNORECASE):
            values = [round(float(value)) for value in re.findall(r"\d+(?:\.\d+)?", raw)]
            if not values:
                return None
            lo, hi = values[0], values[1] if len(values) > 1 else values[0]
            if "BasisPoints" in kind:
                lo, hi = lo * 100, hi * 100
            return kind, display, lo, hi
    physical = re.search(r"Adds \((\d+)-(\d+)\) to \((\d+)-(\d+)\) Physical Damage", raw)
    if physical:
        return "AddedPhysicalDamage", "附加物理伤害", int(physical.group(1)), int(physical.group(4))
    return None


def load_affixes(root: Path, base_tags: set[str]) -> tuple[list[dict], list[Path]]:
    source_paths = [root / "Data" / "ModExplicit.lua", root / "Data" / "ModFlask.lua"]
    records: list[dict] = []
    for path in source_paths:
        flask_mods = path.name == "ModFlask.lua"
        for line in path.read_text(encoding="utf-8").splitlines():
            head = re.match(r'\s*\["([^"]+)"\]\s*=\s*\{\s*type\s*=\s*"(Prefix|Suffix)"', line)
            if not head:
                continue
            source_id, position = head.groups()
            group = text_field(line, "group")
            level = field(line, "level", 1)
            weight_keys = list_field(line, "weightKey")
            weight_values = [int(value) for value in re.findall(r"-?\d+", re.search(r"\bweightVal\s*=\s*\{([^}]*)\}", line).group(1))] if "weightVal" in line else []
            raw_weights = {key: value for key, value in zip(weight_keys, weight_values) if value > 0}
            weights = {key: value for key, value in raw_weights.items() if key != "default"}
            if flask_mods and raw_weights.get("default", 0) > 0:
                weights["flask"] = raw_weights["default"]
            if not weights or not base_tags.intersection(weights):
                continue
            affix_pos = line.find('affix = "')
            order_pos = line.find("statOrder", affix_pos)
            quoted = re.findall(r'"([^"]+)"', line[affix_pos:order_pos])
            raw_parts = quoted[1:] if len(quoted) > 1 else []
            if not raw_parts:
                continue
            raw = " / ".join(raw_parts).replace("Chaos", "Void")
            mapped = classify(raw.replace("Void", "Chaos"))
            if mapped is None:
                continue
            kind, display, minimum, maximum = mapped
            if kind == "VoidResistanceBasisPoints":
                display = "虚空抗性"
            stable_group = re.sub(r"[^a-z0-9]+", "_", group.lower()).strip("_")
            records.append({
                "stableFamilyId": f"p19.affix.{stable_group}", "sourceId": source_id,
                "displayName": display, "rawText": raw, "position": position, "groupId": group,
                "tier": 0, "minimumItemLevel": level, "minimumValue": minimum, "maximumValue": maximum,
                "weight": max(weights.values()), "modifierKind": kind, "tagWeights": dict(sorted(weights.items())),
                "modTags": sorted(list_field(line, "modTags")), "local": "Local" in source_id,
                "source": "Natural",
            })

    grouped: dict[tuple[str, str], list[dict]] = defaultdict(list)
    for record in records:
        grouped[(record["groupId"], record["position"])].append(record)
    for family in grouped.values():
        for tier, record in enumerate(sorted(family, key=lambda value: (-value["minimumItemLevel"], value["sourceId"])), 1):
            record["tier"] = tier

    # Keep complete families. Common baseline families come first, then deterministic source order.
    priority = ("Strength", "Dexterity", "Intelligence", "Life", "Mana", "Resistance", "Physical", "Armour", "Evasion", "EnergyShield", "Accuracy", "AttackSpeed", "Critical", "Movement")
    families = sorted(grouped.values(), key=lambda family: (0 if any(token in family[0]["groupId"] for token in priority) else 1, family[0]["groupId"], family[0]["position"]))
    chosen: list[dict] = []
    for family in families:
        if chosen and len(chosen) + len(family) > 410:
            continue
        chosen.extend(family)

    # Energy is a first-class fourth attribute and deliberately mirrors Spirit.
    energy: list[dict] = []
    for record in chosen:
        if record["modifierKind"] != "Spirit":
            continue
        clone = dict(record)
        clone["sourceId"] = f'{record["sourceId"]}:EnergyMirror'
        clone["stableFamilyId"] = record["stableFamilyId"].replace("intelligence", "energy")
        clone["displayName"] = "能量"
        clone["rawText"] = record["rawText"].replace("Intelligence", "Energy")
        clone["groupId"] = f'{record["groupId"]}:EnergyMirror'
        clone["modifierKind"] = "Energy"
        energy.append(clone)
    chosen.extend(energy)
    return sorted(chosen, key=lambda value: (value["stableFamilyId"], value["tier"], value["sourceId"])), source_paths


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pob-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    bases, base_paths = load_bases(args.pob_root)
    affixes, affix_paths = load_affixes(args.pob_root, {tag for item in bases for tag in item["tags"]})
    files = []
    for path in sorted(set(base_paths + affix_paths)):
        files.append({"path": str(path.relative_to(args.pob_root)).replace("\\", "/"), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    payload = {
        "schemaVersion": 1,
        "source": {
            "snapshot": "PathOfBuildingCommunity-local-2026-08-29",
            "sourceRoot": args.pob_root.name,
            "files": files,
        },
        "bases": bases,
        "affixes": affixes,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"wrote {len(bases)} bases and {len(affixes)} affix tiers to {args.output}")


if __name__ == "__main__":
    main()
