using UnityEngine;
using Unity.Netcode;

public class GrabInteractableObject : NetworkBehaviour, IInteractable
{
    private NetworkObject netObj;
    public NetworkVariable<bool> isHeld = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] public Vector3 gripOffset;
    [SerializeField] public Vector3 gripEuler;
    [SerializeField] public Vector3 gripScale;
    [SerializeField] private GameObject heldItemPrefab;

    [SerializeField] private ItemData data;

    private void Awake()
    {
        netObj = GetComponent<NetworkObject>();
        if (data != null)
            heldItemPrefab = data.prefab;
    }
    public void Interact(PlayerStateList currentPlayer)
    {
        
        if (!currentPlayer.isHoldingItem.Value&& !isHeld.Value)
        {
            
            GrabServerRpc();
        }
    }

    public void PutDown()
    {
       
        PutDownServerRpc();
    }

    //실제 드는 행위
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void GrabServerRpc(RpcParams rpcParams = default)
    {
      
        ulong senderID = rpcParams.Receive.SenderClientId;
        NetworkObject playerObj = NetworkManager.Singleton.ConnectedClients[senderID].PlayerObject;
        PlayerStateList playerState = playerObj.GetComponent<PlayerStateList>();
        PlayerCombat combat = playerObj.GetComponent<PlayerCombat>();   
        if (playerState == null) return;

        Transform playerTransform = playerObj.transform;

       

        GameObject newItemObj = Instantiate(heldItemPrefab);
        Rigidbody objRigid = newItemObj.GetComponent<Rigidbody>();
        Collider collider = newItemObj.GetComponent<Collider>();
        Collider[] colliders = newItemObj.GetComponentsInChildren<Collider>(true);

        if (collider != null)
        {
            collider.isTrigger = true;
        }
        foreach (Collider col in colliders)
        {
            col.isTrigger = true;
        }
        if (objRigid != null)
        {
            objRigid.isKinematic = true;
        }
        

        NetworkObject newNetObj = newItemObj.GetComponent<NetworkObject>();
        newNetObj.Spawn();

        newItemObj.transform.localScale = gripScale;

        GrabInteractableObject holdObj = newItemObj.GetComponent<GrabInteractableObject>();
        ItemEffectManager.Execute(data,combat);

        holdObj.isHeld.Value = true; //손에있는걸 다른 사람이 못잡게

        playerState.isHoldingItem.Value = true;
        playerState.heldItemNetworkId.Value = newNetObj.NetworkObjectId;


        netObj.Despawn(true);
      
    }
    //실제 내려놓는 행위
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PutDownServerRpc(RpcParams rpcParams = default)
    {
        ulong senderID = rpcParams.Receive.SenderClientId;
        NetworkObject playerObj = NetworkManager.Singleton.ConnectedClients[senderID].PlayerObject;
        PlayerCombat combat = playerObj.GetComponent<PlayerCombat>();
        PlayerStateList playerState = playerObj.GetComponent<PlayerStateList>();

        if (playerState == null) return;

        playerState.isHoldingItem.Value = false;
        playerState.heldItemNetworkId.Value = 0;

        netObj.TrySetParent((NetworkObject)null, worldPositionStays: true);
        Rigidbody objRigid = GetComponent<Rigidbody>();
        if (objRigid != null)
        {
            objRigid.isKinematic = false;
        }
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }
        ItemEffectManager.Deexcute(data, combat);
        isHeld.Value = false;
    }


  
}
