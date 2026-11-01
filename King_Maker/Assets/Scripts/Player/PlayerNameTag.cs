using Unity.Netcode;
using UnityEngine;
using TMPro;

public class PlayerNameTag : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshPro nameText;     // NameTagText

    [Header("Test")]
    [SerializeField] private string nickname = "Player"; // 인스펙터에서 입력

    private Transform cam;

    public override void OnNetworkSpawn()
    {
        if (nameText == null) return;

        // 자기 자신에게는 안 보이게
        if (IsOwner)
        {
            nameText.gameObject.SetActive(false);
            return;
        }

        nameText.text = nickname;
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
}