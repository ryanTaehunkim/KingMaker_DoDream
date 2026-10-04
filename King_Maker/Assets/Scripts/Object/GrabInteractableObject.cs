using UnityEngine;
using Unity.Netcode;

public class GrabInteractableObject : MonoBehaviour, IInteractable
{
    private NetworkObject netObj;
    public NetworkVariable<bool> isHeld = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private Vector3 gripOffset;
    [SerializeField] private Vector3 gripEuler;
    [SerializeField] private GameObject heldItemPrefab;

    private void Awake()
    {
        netObj = GetComponent<NetworkObject>();
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void GrabServerRpc(RpcParams rpcParams = default)
    {
      
        ulong senderID = rpcParams.Receive.SenderClientId;
        NetworkObject playerObj = NetworkManager.Singleton.ConnectedClients[senderID].PlayerObject;
        PlayerStateList playerState = playerObj.GetComponent<PlayerStateList>();

        if (playerState == null) return;

        Transform playerTransform = playerObj.transform;


        Vector3 spawnPos = playerTransform.TransformPoint(gripOffset);
        Quaternion spawnRot = playerTransform.rotation * Quaternion.Euler(gripEuler);

        GameObject newItemObj = Instantiate(heldItemPrefab, spawnPos, spawnRot);
        Rigidbody objRigid = newItemObj.GetComponent<Rigidbody>();
        if (objRigid != null)
        {
            objRigid.isKinematic = true;
        }

        NetworkObject newNetObj = newItemObj.GetComponent<NetworkObject>();
        newNetObj.Spawn();
        newNetObj.TrySetParent(playerObj, worldPositionStays: true);
        GrabInteractableObject holdObj = newItemObj.GetComponent<GrabInteractableObject>();
        holdObj.isHeld.Value = true; //손에있는걸 다른 사람이 못잡게

        playerState.isHoldingItem.Value = true;
        playerState.heldItemNetworkId.Value = newNetObj.NetworkObjectId;


        netObj.Despawn(true);
      
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PutDownServerRpc(RpcParams rpcParams = default)
    {
        ulong senderID = rpcParams.Receive.SenderClientId;
        NetworkObject playerObj = NetworkManager.Singleton.ConnectedClients[senderID].PlayerObject;
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
        isHeld.Value = false;
    }

}
