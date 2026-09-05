using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.World;

namespace GameForWork.Core.Management;

public enum MapContainerKind
{
    Inventory,
    HeroQueue,
    MercenaryQueue,
}

public sealed class MapCommandService(GameSession session)
{
    public ItemCommandResult Move(
        MapContainerKind source,
        int sourceIndex,
        MapContainerKind target,
        int targetIndex)
    {
        if (!session.IsExpeditionUnlocked)
        {
            return ItemCommandResult.Fail("expedition_locked", "完成第五幕前不能配置远征。");
        }

        if (source == target)
        {
            bool moved = source switch
            {
                MapContainerKind.Inventory => MoveInventory(sourceIndex, targetIndex),
                MapContainerKind.HeroQueue => session.World.Hero.Queue.TryMove(sourceIndex, targetIndex),
                MapContainerKind.MercenaryQueue => session.World.Mercenaries.Queue.TryMove(sourceIndex, targetIndex),
                _ => false,
            };
            return moved
                ? ItemCommandResult.Ok("地图顺序已更新并立即保存。")
                : ItemCommandResult.Fail("map_move_failed", "地图顺序没有变化。");
        }

        MapItem? map = Take(source, sourceIndex);
        if (map is null)
        {
            return ItemCommandResult.Fail("map_missing", "地图不存在或正在运行。");
        }

        if (!Insert(target, targetIndex, map))
        {
            Insert(source, sourceIndex, map);
            return ItemCommandResult.Fail("queue_full", "目标队列已满，操作已回滚。");
        }

        session.Management.AddHistory($"地图 {map.InstanceId} 已移动到 {target}。");
        return ItemCommandResult.Ok("地图已移动并立即保存。");
    }

    public ItemCommandResult AddToQueue(int inventoryIndex, ExpeditionTeamKind team)
    {
        return Move(
            MapContainerKind.Inventory,
            inventoryIndex,
            team == ExpeditionTeamKind.Hero ? MapContainerKind.HeroQueue : MapContainerKind.MercenaryQueue,
            int.MaxValue);
    }

    private MapItem? Take(MapContainerKind source, int index) => source switch
    {
        MapContainerKind.Inventory when index >= 0 && index < session.World.MapInventory.Count => TakeInventory(index),
        MapContainerKind.HeroQueue => session.World.Hero.Queue.TakeAt(index),
        MapContainerKind.MercenaryQueue => session.World.Mercenaries.Queue.TakeAt(index),
        _ => null,
    };

    private bool Insert(MapContainerKind target, int index, MapItem map)
    {
        switch (target)
        {
            case MapContainerKind.Inventory:
                session.World.MapInventory.Insert(Math.Clamp(index, 0, session.World.MapInventory.Count), map);
                return true;
            case MapContainerKind.HeroQueue:
                return session.World.Hero.Queue.TryInsert(map, index);
            case MapContainerKind.MercenaryQueue:
                return session.World.Mercenaries.Queue.TryInsert(map, index);
            default:
                return false;
        }
    }

    private MapItem TakeInventory(int index)
    {
        MapItem map = session.World.MapInventory[index];
        session.World.MapInventory.RemoveAt(index);
        return map;
    }

    private bool MoveInventory(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= session.World.MapInventory.Count)
        {
            return false;
        }

        MapItem map = TakeInventory(sourceIndex);
        session.World.MapInventory.Insert(Math.Clamp(targetIndex, 0, session.World.MapInventory.Count), map);
        return true;
    }
}
