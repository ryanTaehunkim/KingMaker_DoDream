using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class NetworkTestController : MonoBehaviour
{
    private void Update()
    {
        if (NetworkManager.Singleton == null)
            return;

        // 이미 Host/Client가 시작된 상태라면 입력하지 않음
        if (NetworkManager.Singleton.IsListening)
            return;

        if (Keyboard.current == null)
            return;

        // H 키 = Host
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            Debug.Log("Starting Host...");
            NetworkManager.Singleton.StartHost();
        }

        // C 키 = Client
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            Debug.Log("Starting Client...");
            NetworkManager.Singleton.StartClient();
        }
    }
}