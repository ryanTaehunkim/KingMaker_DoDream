using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum Role
{
    None,
    Citizen,
    Joker
}

public class RoleManager : NetworkBehaviour
{
    [Header("Role Settings")]
    [SerializeField] private int minJokerCount = 1;
    [SerializeField] private bool allowAllJoker = true;

    public Role myRole { get; private set; } = Role.None;
    //서버 호스트에 전체 정체 저장
    private readonly Dictionary<ulong, Role> serverRoles = new Dictionary<ulong, Role>();

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private float fadeoutTime = 1f;
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float holdDuration = 2f;

    private Coroutine revealRoutine;
    private void Awake()
    {
        
        roleText.alpha = 0f;
        roleText.text = "";
    }
 
    private void Update()
    {
        //서버장만 게임 시작
        if (!IsServer) return;
        if (Keyboard.current.zKey.wasPressedThisFrame)
        {
            AssignRoles();
        }
    }


    private void AssignRoles()
    {

        //1. 접속 중인 클라이언트 ID목록 자신포함
        List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        int total = clientIds.Count;
        if (total == 0) return;

        //3. Fisher-Yates 셔플
        for (int i = clientIds.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (clientIds[i], clientIds[j]) = (clientIds[j], clientIds[i]);
        }

        //4. 셔플된 결과에서 앞에 인원을 조커로 지정 나머지를 시민으로 지정
        for (int i = 0; i < clientIds.Count; i++)
        {
            serverRoles[clientIds[i]] = i < minJokerCount ? Role.Joker : Role.Citizen;
        }

        foreach (var pair in serverRoles)
        {
            Debug.Log($"[RoleManager] ClientId {pair.Key} = {pair.Value}");
        }

        foreach (var pair in serverRoles)
        {
            //pair Value = 역할 , pair.key = 클라이언트 아이디
            //RpcTarget은 전송하는 보낼 대상 묶음, Single = 한명만, RpcTargetUse.Temp 타겟 객체의 수명 관리가 한 번 쓰고 버리는 방식
            AssignRoleRpc(pair.Value, RpcTarget.Single(pair.Key, RpcTargetUse.Temp));
        }

    }
    [Rpc (SendTo.SpecifiedInParams)]
    private void AssignRoleRpc(Role role, RpcParams rpcParams = default)
    {
        myRole = role;
        ShowRoleUI(role);
    }

    private void ShowRoleUI(Role role)
    {
        
        string label = role == Role.Joker ? "JOKER" : "CITIZEN";
        if(revealRoutine  != null) StopCoroutine(revealRoutine);

        revealRoutine = StartCoroutine(RevealRoutine(label));
    }

    private IEnumerator RevealRoutine(string label)
    {
        //1 화면 어두워지기 FadeManager구현 후 fadeoutTime넘겨서 화면 아웃
        yield return new WaitForSecondsRealtime(fadeoutTime);
        
        roleText.text = label;

        yield return FadeTextRoutine(0f, 1f, fadeDuration);
        yield return new WaitForSecondsRealtime(holdDuration);
        yield return FadeTextRoutine(1f, 0f, fadeDuration);

    }

    private IEnumerator FadeTextRoutine(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            roleText.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        roleText.alpha = to;
    }
}
