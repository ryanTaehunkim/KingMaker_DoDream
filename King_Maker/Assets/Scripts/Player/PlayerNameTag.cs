using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Playables;

public class PlayerNameTag : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshPro nameText;     // NameTagText

    [Header("Test")]
    [SerializeField] private string defaultName = "Player"; // 인스펙터에서 입력

    private Transform cam;
    private PlayerStateList playerState;
    private void Awake()
    {
        // 같은 오브젝트에 있든 자식에 있든 찾아짐
        playerState = GetComponentInParent<PlayerStateList>();
    }
    public override void OnNetworkSpawn()
    {
        if (nameText == null) return;

        // 자기 자신에게는 안 보이게
        if (IsOwner)
        {
            nameText.gameObject.SetActive(false);
            return;
        }
        if (playerState == null)
        {
            Debug.LogError("[PlayerNameTag] PlayerStateList를 찾을 수 없습니다.", this);
            nameText.text = defaultName;
            return;
        }

        // 1) 이미 값이 와 있는 경우(늦게 접속한 클라이언트) 즉시 반영
        Apply(playerState.nickname.Value);

        // 2) 이후 서버가 값을 확정/변경하면 갱신
        playerState.nickname.OnValueChanged += OnNicknameChanged;
        
    }
    public override void OnNetworkDespawn()
    {
        if (playerState != null)
            playerState.nickname.OnValueChanged -= OnNicknameChanged;
    }
    private void LateUpdate()
    {
        //이름표가 없거나 이름이 안보이게 되어있으면 return
        if (nameText == null || !nameText.gameObject.activeSelf) return;

        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }

        // 글자가 카메라 쪽을 보게 (뒤에서 봐도 읽히도록)
        nameText.transform.rotation =
            Quaternion.LookRotation(nameText.transform.position - cam.position);
    }

    private void OnNicknameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        Apply(newValue);
    }
    private void Apply(FixedString32Bytes value)
    {
        if (nameText == null) return;
        nameText.text = value.IsEmpty ? defaultName : value.ToString();
    }
}