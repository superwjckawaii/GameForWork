using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Content;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Builds;

namespace GameForWork.Core.Art;

public enum Facing { Down, Left, Right, Up }
public enum SpriteAction { Idle, Move, Attack, Cast, Hit, Death }

public sealed record AnimationRange(SpriteAction Action, int StartColumn, int FrameCount, int FramesPerSecond);

public static class ArtContract
{
    private static IReadOnlyDictionary<string, int>? _enemyIndices;
    private static IReadOnlyDictionary<string, int>? _skillStoneIndices;
    public const int AnimationColumns = 31;
    public const int DirectionCount = 4;
    public const int ActorRigCount = 5;
    public const int EnemyBodyRigCount = 26;
    public const int BossRigCount = 12;
    public const int ActorCellWidth = 48;
    public const int ActorCellHeight = 64;
    public const int BossCellWidth = 72;
    public const int BossCellHeight = 80;
    public const int IconCellSize = 32;

    public static IReadOnlyList<AnimationRange> AnimationRanges { get; } =
    [
        new(SpriteAction.Idle, 0, 4, 8),
        new(SpriteAction.Move, 4, 6, 10),
        new(SpriteAction.Attack, 10, 6, 12),
        new(SpriteAction.Cast, 16, 6, 10),
        new(SpriteAction.Hit, 22, 3, 12),
        new(SpriteAction.Death, 25, 6, 8),
    ];

    public static IReadOnlyList<string> EnemyIds { get; } = Enemies.NormalEnemies
        .Select(enemy => enemy.StableId).ToArray();

    public static IReadOnlyList<string> SkillStoneIds { get; } = CoreSkillDefinitions.Active
        .Select(skill => skill.StoneId).Concat(CoreSkillDefinitions.Supports.Select(skill => skill.StoneId)).ToArray();

    public static int AnimationColumn(SpriteAction action, long elapsedMilliseconds, bool loop = true)
    {
        AnimationRange range = AnimationRanges[(int)action];
        int rawFrame = Math.Abs(checked((int)(elapsedMilliseconds * range.FramesPerSecond / 1_000)));
        int frame = loop ? rawFrame % range.FrameCount : Math.Min(range.FrameCount - 1, rawFrame);
        return range.StartColumn + frame;
    }

    public static int AnimationRow(int rig, Facing facing, int rigCount)
    {
        if (rig < 0 || rig >= rigCount) throw new ArgumentOutOfRangeException(nameof(rig));
        return checked(rig * DirectionCount + (int)facing);
    }

    public static int EnemyRig(string stableId)
    {
        int index = EnemyIndices.TryGetValue(stableId, out int resolved) ? resolved : -1;
        if (index < 0) return StableIndex(stableId, EnemyBodyRigCount);
        if (index < 40) return index % 16;
        if (index < 48) return 16 + (index - 40) % 2 * 5;
        EnemyFamily family = Enemies.NormalEnemies[index].Family;
        int familyOffset = family switch
        {
            EnemyFamily.LifeGarden => 1,
            EnemyFamily.RedOath => 2,
            EnemyFamily.BlueOath => 3,
            EnemyFamily.Warfront => 4,
            _ => 0,
        };
        int withinFamily = Enemies.NormalEnemies.Take(index).Count(enemy => enemy.Family == family);
        return 16 + familyOffset + withinFamily % 2 * 5;
    }

    public static int EnemyVariant(string stableId)
    {
        int index = EnemyIndices.TryGetValue(stableId, out int resolved) ? resolved : -1;
        return index < 0 ? StableIndex(stableId, 3) : index / EnemyBodyRigCount;
    }

    public static int BossRig(string stableId)
    {
        for (int index = 0; index < Bosses.MapBosses.Count; index++)
            if (Bosses.MapBosses[index].StableId == stableId) return index % BossRigCount;
        if (stableId == Bosses.Breakthrough.StableId) return 9;
        if (stableId == Bosses.CitadelStages[0].StableId) return 8;
        if (stableId == Bosses.CitadelStages[1].StableId) return 10;
        if (stableId == Bosses.CitadelStages[2].StableId) return 11;
        return StableIndex(stableId, BossRigCount);
    }

    public static int SkillStoneIndex(string stableId)
    {
        return Equipment.SkillStoneArt.IconIndex(stableId);
    }

    private static IReadOnlyDictionary<string, int> EnemyIndices =>
        _enemyIndices ??= Indexed(EnemyIds);
    private static IReadOnlyDictionary<string, int> SkillStoneIndices =>
        _skillStoneIndices ??= Indexed(SkillStoneIds);

    private static IReadOnlyDictionary<string, int> Indexed(IReadOnlyList<string> values) =>
        values.Select((value, index) => (value, index)).ToDictionary(pair => pair.value, pair => pair.index,
            StringComparer.Ordinal);

    private static int StableIndex(string value, int count)
    {
        uint hash = 2_166_136_261;
        foreach (char character in value) hash = unchecked((hash ^ character) * 16_777_619);
        return (int)(hash % (uint)count);
    }
}
