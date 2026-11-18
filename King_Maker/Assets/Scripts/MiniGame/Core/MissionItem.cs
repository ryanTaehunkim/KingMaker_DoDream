using UnityEngine;

// 미션을 활성화하는 "찾아오는 아이템" 표식. (3차 기획서 8. 미션 표)
// 예: 와인잔 → 바 테이블, 카드 덱 → 포커 테이블, 잭팟 레버 → 레버 빠진 오락 기계
// 줍기/버리기는 GrabInteractableObject가 처리하고, 이 컴포넌트는 "무슨 아이템인지"만 알려준다.
// MissionItemSocket이 acceptedItemId와 같은 itemId를 가진 아이템을 받으면 미션이 활성화된다.
public class MissionItem : MonoBehaviour
{
    [Tooltip("소켓의 acceptedItemId와 같아야 한다. 예: wine_glass, card_deck, jackpot_lever")]
    [SerializeField] private string itemId;
    [Tooltip("안내 문구에 표시할 이름")]
    [SerializeField] private string displayName;

    [Tooltip("숫자 패널 등 값이 있는 아이템 (협동 C2 산수)")]
    [SerializeField] private int value;

    public string ItemId => itemId;
    public int Value => value;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? itemId : displayName;
}
