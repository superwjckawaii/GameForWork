using GameForWork.Core.P1;
using GameForWork.Core.P1.World;

namespace GameForWork.Core.P2;

public enum P2MapContainerKind
{
    Inventory,
    HeroQueue,
    MercenaryQueue,
}

public sealed class P2MapCommandService(P1GameSession session)
{
    public P2ItemCommandResult Move(
        P2MapContainerKind source,
        int sourceIndex,
        P2MapContainerKind target,
        int targetIndex)
    {
        if (!session.IsExpeditionUnlocked)
        {
            return P2ItemCommandResult.Fail("expedition_locked", "完成第五幕前不能配置远征。");
        }

        if (source == target)
        {
            bool moved = source switch
            {
                P2MapContainerKind.Inventory => MoveInventory(sourceIndex, targetIndex),
                P2MapContainerKind.HeroQueue => session.World.Hero.Queue.TryMove(sourceIndex, targetIndex),
                P2MapContainerKind.MercenaryQueue => session.World.Mercenaries.Queue.TryMove(sourceIndex, targetIndex),
                _ => false,
            };
            return moved
                ? P2ItemCommandResult.Ok("地图顺序已更新并立即保存。")
                : P2ItemCommandResult.Fail("map_move_failed", "地图顺序没有变化。");
        }

        P1MapItem? map = Take(source, sourceIndex);
        if (map is null)
        {
            return P2ItemCommandResult.Fail("map_missing", "地图不存在或正在运行。");
        }

        if (!Insert(target, targetIndex, map))
        {
            Insert(source, sourceIndex, map);
            return P2ItemCommandResult.Fail("queue_full", "目标队列已满，操作已回滚。");
        }

        session.Management.AddHistory($"地图 {map.InstanceId} 已移动到 {target}。");
        return P2ItemCommandResult.Ok("地图已移动并立即保存。");
    }

    public P2ItemCommandResult AddToQueue(int inventoryIndex, ExpeditionTeamKind team)
    {
        return Move(
            P2MapContainerKind.Inventory,
            inventoryIndex,
            team == ExpeditionTeamKind.Hero ? P2MapContainerKind.HeroQueue : P2MapContainerKind.MercenaryQueue,
            int.MaxValue);
    }

    private P1MapItem? Take(P2MapContainerKind source, int index) => source switch
    {
        P2MapContainerKind.Inventory when index >= 0 && index < session.World.MapInventory.Count => TakeInventory(index),
        P2MapContainerKind.HeroQueue => session.World.Hero.Queue.TakeAt(index),
        P2MapContainerKind.MercenaryQueue => session.World.Mercenaries.Queue.TakeAt(index),
        _ => null,
    };

    private bool Insert(P2MapContainerKind target, int index, P1MapItem map)
    {
        switch (target)
        {
            case P2MapContainerKind.Inventory:
                session.World.MapInventory.Insert(Math.Clamp(index, 0, session.World.MapInventory.Count), map);
                return true;
            case P2MapContainerKind.HeroQueue:
                return session.World.Hero.Queue.TryInsert(map, index);
            case P2MapContainerKind.MercenaryQueue:
                return session.World.Mercenaries.Queue.TryInsert(map, index);
            default:
                return false;
        }
    }

    private P1MapItem TakeInventory(int index)
    {
        P1MapItem map = session.World.MapInventory[index];
        session.World.MapInventory.RemoveAt(index);
        return map;
    }

    private bool MoveInventory(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= session.World.MapInventory.Count)
        {
            return false;
        }

        P1MapItem map = TakeInventory(sourceIndex);
        session.World.MapInventory.Insert(Math.Clamp(targetIndex, 0, session.World.MapInventory.Count), map);
        return true;
    }
}
