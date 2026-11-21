#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemData), true)]
public class ItemDataEditor : Editor
{
    private SerializedProperty itemName;
    private SerializedProperty description;
    private SerializedProperty prefab;
    private SerializedProperty category;
    private SerializedProperty attackDamage;
    private SerializedProperty attackDistance;
    private SerializedProperty itemId;

    private void OnEnable()
    {
        itemName = serializedObject.FindProperty("itemName");
        description = serializedObject.FindProperty("description");
        prefab = serializedObject.FindProperty("prefab");
        category = serializedObject.FindProperty("category");
        attackDamage = serializedObject.FindProperty("attackDamage");
        attackDistance = serializedObject.FindProperty("attackDistance");
        itemId = serializedObject.FindProperty("itemId");
        if (itemId == null) Debug.LogError("itemId 프로퍼티를 찾지 못했습니다");
        if (attackDamage == null) Debug.LogError("attackDamage 프로퍼티를 찾지 못했습니다");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("기본 정보", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemName);
        EditorGUILayout.PropertyField(description);
        EditorGUILayout.PropertyField(prefab);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("분류", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(category);

        // 플래그 값은 enum 비트 값으로 직접 확인한다
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        ItemCategorys current = (ItemCategorys)category.intValue;

        if ((current & ItemCategorys.Mission) != 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("미션 아이템", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(itemId);

            if (string.IsNullOrWhiteSpace(itemId.stringValue))
                EditorGUILayout.HelpBox("미션 아이템은 itemId가 필요합니다.", MessageType.Warning);
        }

        if ((current & ItemCategorys.Attack) != 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("공격 아이템", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(attackDamage);
            EditorGUILayout.PropertyField(attackDistance);
        }
        Debug.Log($"category = {category.intValue}");
        serializedObject.ApplyModifiedProperties();
    }
}
#endif