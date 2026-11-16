using Unity.Netcode;
using UnityEngine;

public class PlayerGhostState : NetworkBehaviour
{
    [Header("숨길 대상")]
    [SerializeField] private Renderer[] bodyRenderers;
    private Transform showObj;
    private Transform nameTag;
    [Header("영혼 이동")]
    [SerializeField] private float ghostSpeed = 45f;
    

    private PlayerStateList state;
    private int originalLayer;

    public bool IsGhost => state != null && !state.isAlive.Value;
    public float GhostSpeed => ghostSpeed;


    private void Awake()
    {
        
        state = GetComponent<PlayerStateList>();
        originalLayer = gameObject.layer;
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public override void OnNetworkSpawn()
    {
        state.isAlive.OnValueChanged += OnAliveChanged;
        nameTag = transform.Find("NameTagAnchor");
        showObj = transform.Find("showObj");
        if (!state.isAlive.Value)
            ApplyGhost(true);
    }

    public override void OnNetworkDespawn()
    {
        state.isAlive.OnValueChanged -= OnAliveChanged;
    }

    private void OnAliveChanged(bool oldValue, bool newValue)
    {
        ApplyGhost(!newValue);
    }

    private void ApplyGhost(bool ghost)
    {
        foreach (var r in bodyRenderers)
        {
            if (r != null) r.enabled = !ghost;
        }
        showObj.gameObject.SetActive(ghost);
        if (IsOwner)
        {
            Renderer rd = showObj.GetComponent<Renderer>();
            rd.enabled = false;
        }
        Transform text = transform.Find("NameTagAnchor/Text (TMP)");
        text.gameObject.layer = ghost ? LayerMask.NameToLayer("Ghost") : originalLayer;
        gameObject.layer = ghost ? LayerMask.NameToLayer("Ghost") : originalLayer;
    }
}