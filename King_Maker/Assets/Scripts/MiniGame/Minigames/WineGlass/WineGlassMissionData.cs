using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/WineGlass (P1)", fileName = "Mission_WineGlass")]
public class WineGlassMissionData : MissionData
{
    [Header("P1 와인잔 닦기")]
    [Range(1, 8)] public int stainCount = 4;
    [Tooltip("얼룩 크기 범위 (텍스처 대비 반지름 비율)")]
    public Vector2 stainRadius = new Vector2(0.07f, 0.11f);
    [Tooltip("브러시 반지름 (텍스처 대비 비율)")]
    [Range(0.02f, 0.2f)] public float brushRadius = 0.08f;
    [Tooltip("한 번 문지를 때 지워지는 양 (0~1)")]
    [Range(0.05f, 1f)] public float scrubStrength = 0.3f;
    [Tooltip("이 비율 이상 지우면 성공")]
    [Range(0.5f, 1f)] public float requiredCleanRatio = 0.95f;
}
