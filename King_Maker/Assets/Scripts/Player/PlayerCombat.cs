using Unity.Netcode;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [Header("Attack")]
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackRadius = 0.5f;
    [SerializeField] private float attackCoolDown = 0.6f;
    [SerializeField] private LayerMask hitMask;

    [Header("Server Validation")]
    [SerializeField] private float serverMaxDistance = 3.5f;

    [Header("References")]
    [SerializeField] private Transform aimOrigin; //face로 에임 시작점
    [SerializeField] private Transform center;

    private PlayerStateList state;
    private readonly RaycastHit[] hits = new RaycastHit[8];
    private float lastLocalAttackTime = -999f;
    private float lastServerAttackTime = -999f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private float knockbackUpward = 3f;
    private PlayerControlRigid controlRigid;

    // 서버 거리 검증 디버그용
    private Vector3 dbgStart;
    private Vector3 dbgEnd;
    private float dbgDist;
    private bool dbgPassed;
    private bool dbgHasData;

    private void Awake() {
        state = GetComponent<PlayerStateList>();
        controlRigid = GetComponent<PlayerControlRigid>();
    }

    public void TryAttack()
    {
        if(!InputManager.attackPressed) return;
        if(!state.isAlive.Value)     return;
        if (Time.time - lastLocalAttackTime < attackCoolDown) return;

        lastLocalAttackTime = Time.time;

        int count = Physics.SphereCastNonAlloc(aimOrigin.position, attackRadius, aimOrigin.forward, hits, attackRange, hitMask, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        PlayerStateList target = null;
        bool blocked = false;

        for(int i = 0; i<count; i++)
        {
            var rbHit = hits[i].collider.attachedRigidbody;
            if (rbHit != null && rbHit.gameObject == gameObject) continue;
            if (hits[i].distance >= best) continue;
            
            best = hits[i].distance;
            target = rbHit != null ? rbHit.GetComponent<PlayerStateList>() : null;
            blocked = target == null;
        }
        //공격 시 모션과 데미지 등 

        if (target != null && !blocked) AttackServerRpc(target.NetworkObjectId);

    }
    
    [Rpc(SendTo.Server)]
    private void AttackServerRpc(ulong targetId)
    {
        Debug.Log($"[Server] 공격 요청 수신 target={targetId}");

        if (!state.isAlive.Value) { Debug.Log("거부: 공격자 사망"); return; }
        if (Time.time - lastServerAttackTime < attackCoolDown * 0.9f) { Debug.Log("거부: 서버 쿨다운"); return; }

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var targetObj)) { Debug.Log("거부: 대상 없음"); return; }
        if (!targetObj.TryGetComponent(out PlayerStateList target)) { Debug.Log("거부: PlayerStateList 없음"); return; }
        if (!targetObj.TryGetComponent(out PlayerCombat targetCombat)) { Debug.Log("거부: PlayerCombat 없음"); return; }
        if (target == state || !target.isAlive.Value) { Debug.Log("거부: 자기 자신 또는 사망"); return; }

        // 거리 검증: center 간 거리
        float dist = Vector3.Distance(center.position, targetCombat.center.position);
        bool inRange = dist <= serverMaxDistance;

        // 디버그 기록 (통과/거부 모두)
        dbgStart = center.position;
        dbgEnd = targetCombat.center.position;
        dbgDist = dist;
        dbgPassed = inRange;
        dbgHasData = true;

        if (!inRange) { Debug.Log($"거부: 거리 {dist:F2} / 허용 {serverMaxDistance:F2}"); return; }

       

        lastServerAttackTime = Time.time;
        target.TakeDamage(attackDamage);

        Vector3 dir = targetCombat.center.position - center.position;
        dir.y = 0f;
        dir = dir.normalized;

        targetCombat.KnockbackRpc(dir * knockbackForce, knockbackUpward, RpcTarget.Single(target.OwnerClientId, RpcTargetUse.Temp));

    }
    [Rpc(SendTo.SpecifiedInParams)]
    public void KnockbackRpc(Vector3 horizontal, float upward,RpcParams rpcParams = default)
    {
        controlRigid.ApplyKnockback(horizontal, upward);
    }

    private void OnDrawGizmos()
    {
        if (aimOrigin == null) return;

        Vector3 start = aimOrigin.position - aimOrigin.forward * attackRadius;
        Vector3 end = start + aimOrigin.forward * (attackRange + attackRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(start, attackRadius);
        Gizmos.DrawWireSphere(end, attackRadius);
        Gizmos.DrawLine(start, end);

        Vector3 up = aimOrigin.up * attackRadius;
        Vector3 right = aimOrigin.right * attackRadius;
        Gizmos.DrawLine(start + up, end + up);
        Gizmos.DrawLine(start - up, end - up);
        Gizmos.DrawLine(start + right, end + right);
        Gizmos.DrawLine(start - right, end - right);

        // 서버 허용 거리 (center 기준)
        if (center != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
            Gizmos.DrawWireSphere(center.position, serverMaxDistance);
        }

        // 마지막 서버 검증 결과
        if (dbgHasData)
        {
            Gizmos.color = dbgPassed ? Color.green : Color.magenta;
            Gizmos.DrawLine(dbgStart, dbgEnd);
            Gizmos.DrawSphere(dbgEnd, 0.08f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label((dbgStart + dbgEnd) * 0.5f,
                $"{dbgDist:F2}m ({(dbgPassed ? "통과" : "거부")})");
#endif
        }
    }

}
