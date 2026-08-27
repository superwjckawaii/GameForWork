using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Combat;

public static class BattleStateCodec
{
    private static readonly byte[] Magic = "GFWB"u8.ToArray();
    public const ushort FormatVersion = 1;

    public static byte[] Serialize(BattleState state)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(state.Tick);
        writer.Write((byte)state.Outcome);
        writer.Write(state.Seed);
        writer.Write(state.Random.State);
        writer.Write(state.Random.Increment);
        writer.Write(state.Actors.Count);
        foreach (ActorState actor in state.Actors.Values.OrderBy(actor => actor.Id))
        {
            writer.Write(actor.Id);
            writer.Write(actor.Name);
            writer.Write((byte)actor.Team);
            writer.Write(actor.XRaw);
            writer.Write(actor.YRaw);
            writer.Write(actor.Life);
            writer.Write(actor.MaxLife);
            writer.Write(actor.SpeedRawPerSecond);
            writer.Write(actor.Damage);
            writer.Write(actor.Armor);
            writer.Write(actor.HitChanceBasisPoints);
            writer.Write(actor.RangeRaw);
            writer.Write(actor.WindupTicks);
            writer.Write(actor.CooldownTicks);
            writer.Write(actor.CooldownRemainingTicks);
            writer.Write(actor.CastResolveTick);
            writer.Write(actor.CastTargetId);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static BattleState Deserialize(ReadOnlySpan<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
        {
            throw new InvalidDataException("Battle snapshot magic is invalid.");
        }

        ushort version = reader.ReadUInt16();
        if (version != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported battle snapshot version: {version}.");
        }

        int tick = reader.ReadInt32();
        BattleOutcome outcome = (BattleOutcome)reader.ReadByte();
        ulong seed = reader.ReadUInt64();
        ulong randomState = reader.ReadUInt64();
        ulong randomIncrement = reader.ReadUInt64();
        int count = reader.ReadInt32();
        if (count is < 0 or > 1_000)
        {
            throw new InvalidDataException("Battle actor count is invalid.");
        }

        var actors = new SortedDictionary<ulong, ActorState>();
        for (int index = 0; index < count; index++)
        {
            var actor = new ActorState
            {
                Id = reader.ReadUInt64(),
                Name = reader.ReadString(),
                Team = (Team)reader.ReadByte(),
                XRaw = reader.ReadInt32(),
                YRaw = reader.ReadInt32(),
                Life = reader.ReadInt32(),
                MaxLife = reader.ReadInt32(),
                SpeedRawPerSecond = reader.ReadInt32(),
                Damage = reader.ReadInt32(),
                Armor = reader.ReadInt32(),
                HitChanceBasisPoints = reader.ReadInt32(),
                RangeRaw = reader.ReadInt32(),
                WindupTicks = reader.ReadInt32(),
                CooldownTicks = reader.ReadInt32(),
                CooldownRemainingTicks = reader.ReadInt32(),
                CastResolveTick = reader.ReadInt32(),
                CastTargetId = reader.ReadUInt64(),
            };
            if (!actors.TryAdd(actor.Id, actor))
            {
                throw new InvalidDataException("Battle actor IDs must be unique.");
            }
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Battle snapshot contains trailing bytes.");
        }

        return new BattleState
        {
            Tick = tick,
            Outcome = outcome,
            Seed = seed,
            Random = Pcg32.Restore(randomState, randomIncrement),
            Actors = actors,
        };
    }

    public static string Hash(BattleState state) =>
        Convert.ToHexString(SHA256.HashData(Serialize(state))).ToLowerInvariant();
}
