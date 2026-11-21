using NUnit.Framework.Internal;
using System.Collections.Generic;

public static class ItemEffectManager
{
    private static readonly Dictionary<ItemCategorys, IItemEffect> effects =
        new()
        {
            
            { ItemCategorys.Attack, new AttackEffect() },
            { ItemCategorys.Mission, new MissionEffect() }
        };

    public static void Execute(ItemData item, PlayerCombat combat)
    {
        foreach (var effect in effects)
        {
            if ((item.category & effect.Key) != 0)
            {
                effect.Value.Execute(item,combat);
            }
        }
    }
    public static void Deexcute(ItemData item,PlayerCombat combat=null)
    {
        foreach (var effect in effects)
        {
            if ((item.category & effect.Key) != 0)
            {
                effect.Value.Deexecute(item,combat);
            }
        }
    }
}