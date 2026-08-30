using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.Items;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P6;

public static class P6SocketGroupIds
{
    public static string For(EquipmentSlot slot) => $"p6.socket.{slot.ToString().ToLowerInvariant()}";
}

public static class P6SocketRules
{
    public static bool ProvidesSockets(ItemCategory category) => category is
        ItemCategory.TwoHandWeapon or ItemCategory.OneHandWeapon or ItemCategory.Shield or
        ItemCategory.BodyArmor or ItemCategory.Helmet or
        ItemCategory.Gloves or ItemCategory.Boots;

    public static int Minimum(ItemCategory category) => ProvidesSockets(category) ? 2 : 0;

    public static int EquipmentMaximum(ItemCategory category) => category switch
    {
        ItemCategory.TwoHandWeapon or ItemCategory.BodyArmor => 6,
        ItemCategory.OneHandWeapon or ItemCategory.Shield => 3,
        ItemCategory.Helmet or ItemCategory.Gloves or ItemCategory.Boots => 4,
        _ => 0,
    };

    public static int ItemLevelMaximum(int itemLevel) => itemLevel switch
    {
        <= 3 => 3,
        <= 6 => 4,
        <= 9 => 5,
        _ => 6,
    };

    public static int Maximum(ItemCategory category, int itemLevel) =>
        Math.Min(EquipmentMaximum(category), ItemLevelMaximum(itemLevel));

    public static int Roll(ItemCategory category, int itemLevel, ulong seed)
    {
        int minimum = Minimum(category);
        int maximum = Maximum(category, itemLevel);
        if (maximum == 0)
        {
            return 0;
        }

        var random = new Pcg32(seed ^ 0x6c696e6b65647374UL);
        int highestChance = maximum switch
        {
            3 => 3_000,
            4 => 1_500,
            5 => 500,
            6 => 100,
            _ => 10_000,
        };
        if (maximum == minimum || random.NextBasisPoints() < highestChance)
        {
            return maximum;
        }

        int span = maximum - minimum;
        int weighted = (int)(random.NextUInt() % (uint)(span * (span + 1) / 2));
        for (int sockets = minimum; sockets < maximum; sockets++)
        {
            int weight = maximum - sockets;
            if (weighted < weight)
            {
                return sockets;
            }
            weighted -= weight;
        }
        return minimum;
    }

    public static ItemInstance Ensure(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ItemBaseDefinition canonicalBase = P1ItemBases.Get(item.Base.StableId);
        item = item with { Base = canonicalBase };
        if (!ProvidesSockets(item.Base.Category) || item.LinkedSocketCount > 0)
        {
            return item;
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{item.InstanceId}|{item.ItemLevel}|{item.Base.StableId}"));
        ulong seed = BitConverter.ToUInt64(digest, 0);
        return item with { LinkedSocketCount = Roll(item.Base.Category, item.ItemLevel, seed) };
    }

    public static bool IsValid(ItemInstance item) => !ProvidesSockets(item.Base.Category)
        ? item.LinkedSocketCount == 0
        : item.LinkedSocketCount >= Minimum(item.Base.Category) &&
          item.LinkedSocketCount <= Maximum(item.Base.Category, item.ItemLevel);
}
