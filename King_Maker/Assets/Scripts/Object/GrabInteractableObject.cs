using UnityEngine;
using Unity.Netcode;

public class GrabInteractableObject : MonoBehaviour, IInteractable
{
    private NetworkObject netObj;
    public NetworkVariable<bool> isHeld = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public void Interact(PlayerStateList currentPlayer)
    {
        
        if (!currentPlayer.isHoldingItem.Value)
        {
            GrabServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void GrabServerRpc(RpcParams rpcParams = default)
    {
        ulong senderID = rpcParams.Receive.SenderClientId;
        NetworkObject playerObj = NetworkManager.Singleton.ConnectedClients[senderID].PlayerObject;
        PlayerStateList playerState = playerObj.GetComponent<PlayerStateList>();

        if (playerState == null) return;
        playerState.isHoldingItem.Value = true;
        playerState.heldItemNetworkId.Value = netObj.NetworkObjectId;

        Transform handTransform = playerObj.GetComponent<PlayerControlForNetwork>().GetHandTransform();
        netObj.TrySetParent(handTransform, worldPositionStays: false);


    }

   
}
