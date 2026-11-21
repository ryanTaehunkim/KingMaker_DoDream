using UnityEngine;
using System;

[CreateAssetMenu(fileName = "NewItem", menuName = "KingMaker/Items/Item Data")]
public class ItemData : ScriptableObject
{

    public string itemName;

    [TextArea(2, 4)]

    public string description;



    public GameObject prefab;

    public ItemCategorys category;

    public bool HasCategory(ItemCategorys flag) => (category & flag) == flag;

    
    [HideInInspector] public float attackDamage = 0f;
    [HideInInspector] public float attackDistance = 0f;


    [HideInInspector]public string itemId = "itemid";
    

}

[Flags]
public enum ItemCategorys
{

    Mission = 1 << 0,
    Attack = 1 << 1,
    // 새 종류는 1 << 2, 1 << 3 ... 순서로 추가
}