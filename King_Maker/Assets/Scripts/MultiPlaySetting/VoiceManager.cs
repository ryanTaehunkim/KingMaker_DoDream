using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

// Game 씬의 빈 GameObject에 붙인다.
public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }
    public bool IsReady { get; private set; }
    private string currentChannel = "";

    private void Awake()
    {
        Instance = this;
    }
    private void Update()
    {
        if (InputManager.putDownPressed) JoinAllChannel();
        if (InputManager.interactPressed) LeaveChannel();
    }
    private async void Start()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        // 로비에서 MultiplayerSessionManager가 이미 UGS 로그인을 해뒀는지 확인
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // 1) Vivox 초기화
        await VivoxService.Instance.InitializeAsync();

        // 2) Vivox 로그인
        await VivoxService.Instance.LoginAsync(new LoginOptions
        {
            DisplayName = AuthenticationService.Instance.PlayerId.Substring(0, 8)
        });
        

        // 3) 마이크, 스피커 켜기
        VivoxService.Instance.UnmuteInputDevice();
        VivoxService.Instance.UnmuteOutputDevice();
        IsReady = true;
        Debug.Log("[Voice] 마이크/스피커 ON");
    }

    public async void JoinAllChannel()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[Voice] 아직 로그인이 끝나지 않았습니다.");
            return;
        }

        string channelName = GetAllChannelName();

        // 이미 그 채널에 있으면 아무것도 안 함
        if (currentChannel == channelName)
            return;

        // 다른 채널에 있었다면 먼저 나감
        if (currentChannel != "")
            await VivoxService.Instance.LeaveChannelAsync(currentChannel);

        await VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.AudioOnly);
        currentChannel = channelName;

        Debug.Log($"[Voice] 전체 채널 입장: {channelName}");
    }
    private string GetAllChannelName()
    {
        // 세션 ID를 쓰면 다른 방 사람과 섞이지 않는다.
        var session = MultiplayerSessionManager.Instance.CurrentSession;
        return session.Id + "_all";
    }
    public async void LeaveChannel()
    {
        if (currentChannel == "")
            return;

        string leaving = currentChannel;
        currentChannel = "";

        await VivoxService.Instance.LeaveChannelAsync(leaving);
        Debug.Log($"[Voice] 채널 퇴장: {leaving}");
    }
}