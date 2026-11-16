using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

public class VivoxInstallTest : MonoBehaviour
{
    async void Start()
    {
        // MultiplayerSessionManager가 이미 초기화/로그인했다면 이 두 줄은 건너뛰어도 됨
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        await VivoxService.Instance.InitializeAsync();
        await VivoxService.Instance.LoginAsync(new LoginOptions
        {
            DisplayName = AuthenticationService.Instance.PlayerId.Substring(0, 8)
        });
        Debug.Log($"[Vivox] 로그인 성공: {VivoxService.Instance.IsLoggedIn}");

        await VivoxService.Instance.JoinGroupChannelAsync("test_room", ChatCapability.AudioOnly);
        Debug.Log("[Vivox] test_room 입장 완료");
    }
}