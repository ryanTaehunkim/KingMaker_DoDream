using UnityEngine;

public interface IItemEffect
{
    void Execute(ItemData item, PlayerCombat combat =null);
}
public class AttackEffect : IItemEffect
{
    

    public void Execute(ItemData item, PlayerCombat combat =null)
    {
        if (combat == null) return;
        combat.ApplyAttackItemRpc(
            item.attackDamage,
            item.attackDistance,
            item.attackDistance
        );
    }

}

public class MissionEffect : IItemEffect
{
    public void Execute(ItemData item, PlayerCombat combat = null)
    {
        Debug.Log($"미션 아이템: {item.itemName}");
    }
}
