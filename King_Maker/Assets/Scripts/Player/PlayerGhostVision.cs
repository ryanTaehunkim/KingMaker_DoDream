using Unity.Netcode;
using UnityEngine;


public class PlayerGhostVision : NetworkBehaviour
{
    private const string GhostLayerName = "Ghost";

    private PlayerStateList state;
    private Camera cam;
    private int ghostLayer = -1;
    private int originalMask;
    private bool subscribed;

    private void Awake()
    {
        state = GetComponent<PlayerStateList>();
        ghostLayer = LayerMask.NameToLayer(GhostLayerName);

        if (ghostLayer < 0)
            Debug.LogError("[PlayerGhostVision] 'Ghost' ");
    }

    public override void OnNetworkSpawn()
    {
   
        if (!IsOwner) return;
        if (state == null || ghostLayer < 0) return;

        cam = Camera.main; 
        if (cam == null)
        {
            Debug.LogWarning("[PlayerGhostVision] MainCamera.");
            return;
        }

        originalMask = cam.cullingMask;

        state.isAlive.OnValueChanged += OnAliveChanged;
        subscribed = true;

       
        ApplyMask(state.isAlive.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (subscribed && state != null)
        {
            state.isAlive.OnValueChanged -= OnAliveChanged;
            subscribed = false;
        }


        if (cam != null)
            cam.cullingMask = originalMask;
    }

    private void OnAliveChanged(bool oldValue, bool newValue)
    {
        ApplyMask(newValue);
    }

    private void ApplyMask(bool alive)
    {
        if (cam == null) return;

        int ghostBit = 1 << ghostLayer;

        if (state.isAlive.Value)
            cam.cullingMask = originalMask & ~ghostBit; 
        else
            cam.cullingMask = originalMask | ghostBit;  
    }
}