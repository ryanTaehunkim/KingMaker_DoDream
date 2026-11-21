using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using Unity.VisualScripting;
using UnityEngine;

// Game 씬의 빈 GameObject에 붙인다.
public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }
    //voice준비 됐는지
    public bool IsReady { get; private set; }
    //현재 음성모드
    private enum VoiceMode { None, All, Proximity }
    private VoiceMode currentMode = VoiceMode.None;
    //현재 채널명
    private string currentChannel = "";
    //들어가고 있는중
    private bool isBusy;
    private GameTimeManager gameTimeManager;
    private float nextPosTime;
    [Header("근거리 설정")]
    [SerializeField] private int audibleDistance = 150;
    [SerializeField] private int conversationalDistance = 30;
    [SerializeField] private float fadeIntensity = 1f;
    [SerializeField] private float positionInterval = 0.2f;

    [Header("벽 차폐")]
    [SerializeField] private LayerMask wallMask;              // 벽, 장애물 레이어만 체크
    [SerializeField] private float eyeHeight = 15f;           // 캐릭터 발에서 머리까지 높이 (10유닛=1m 스케일)
    [SerializeField] private float occlusionInterval = 0.2f;
    [SerializeField] private int blockedVolume = -50;         // 벽 너머일 때 음량 (보통 -50~50 범위, 0이 기본)

    private float nextOcclusionTime;

    private void Awake()
    {
        Instance = this;
    }
    private async void Start()
    {

        gameTimeManager = FindFirstObjectByType<GameTimeManager>();

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
            DisplayName = NetworkManager.Singleton.LocalClientId.ToString()
        });


        // 3) 마이크, 스피커 켜기
        VivoxService.Instance.UnmuteInputDevice();
        VivoxService.Instance.UnmuteOutputDevice();
        IsReady = true;
        Debug.Log("[Voice] 마이크/스피커 ON");
    }
    private void Update()
    {
        if (!IsReady || isBusy) return;

        VoiceMode wanted = GetWantedMode();
        if(wanted != currentMode)
        {
            SwitchMode(wanted);
            return;
        }
        if(currentMode == VoiceMode.Proximity)
        {
            UpdatePosition();
            UpdateOcclusion();
        }

        //bool shouldBeIn = ShouldBeInAllChannel();
        //bool isIn = currentChannel != "";

        //if (shouldBeIn && !isInAllChannel) JoinAllChannel();
        //else if (!shouldBeIn && isInAllChannel) LeaveChannel();
    }
    
    private VoiceMode GetWantedMode()
    {
        var flow = GameFlowManager.Instance;
        if (flow == null || !flow.IsSpawned) return VoiceMode.None;
        if (gameTimeManager == null || !gameTimeManager.IsSpawned) return VoiceMode.None;

        //게임 시작 전 로비
        if (!flow.HasGameStarted.Value) return VoiceMode.All;

        switch (gameTimeManager.currentPhase.Value)
        {
            case GameTimeManager.PhaseDay1Drop:
            case GameTimeManager.PhaseDay2Drop:
                return VoiceMode.All;

            case GameTimeManager.PhaseDay1Field:
            case GameTimeManager.PhaseDay2Field:
            case GameTimeManager.PhaseDay3Field:
            case GameTimeManager.PhaseDay4Final:
                return VoiceMode.Proximity;

            default:
                return VoiceMode.None;
        }
    }

    //모드 변경

    private async void SwitchMode(VoiceMode wanted)
    {
        isBusy = true;
        try
        {
            //모드 초기화
            if (currentChannel != "")
            {
                await VivoxService.Instance.LeaveChannelAsync(currentChannel);
                currentChannel = "";
                currentMode = VoiceMode.None;
            }

            //원하는 모드가 전체 방일 경우
            if (wanted == VoiceMode.All)
            {
                string name = GetSessionId() + "_all";
                await VivoxService.Instance.JoinGroupChannelAsync(name, ChatCapability.AudioOnly);
                currentChannel = name;
            }
            //원하는 모드가 개인 거리 기반일 경우
            else if (wanted == VoiceMode.Proximity)
            {
                string name = GetSessionId() + "_field";
                var props = new Channel3DProperties(
                    audibleDistance,
                    conversationalDistance,
                    fadeIntensity,
                    AudioFadeModel.InverseByDistance);

                await VivoxService.Instance.JoinPositionalChannelAsync(name, ChatCapability.AudioOnly, props);
                currentChannel = name;
            }
            currentMode = wanted;
            Debug.Log($"[Voice] 모드 변경: {wanted}");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            //nextRetryTime = Time.time + 2f;    // 실패하면 2초 쉬었다가 다시 시도
        }
        finally
        {
            isBusy = false;
        }
    }

    private void UpdatePosition()
    {
        if (Time.time < nextPosTime) return;
        nextPosTime = Time.time + positionInterval;

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;

        VivoxService.Instance.Set3DPosition(nm.LocalClient.PlayerObject.gameObject, currentChannel);
    }

    private string GetSessionId()
    {
        return MultiplayerSessionManager.Instance.CurrentSession.Id;
    }


    private void UpdateOcclusion()
    {
        if (Time.time < nextOcclusionTime) return;
        nextOcclusionTime = Time.time + occlusionInterval;

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;
        if (!VivoxService.Instance.ActiveChannels.TryGetValue(currentChannel, out var participants)) return;

        Vector3 myEye = nm.LocalClient.PlayerObject.transform.position + Vector3.up * eyeHeight;

        foreach (var p in participants)
        {
            if (p.IsSelf) continue;

            // DisplayName(ClientId) → 그 사람 캐릭터
            if (!ulong.TryParse(p.DisplayName, out ulong clientId)) continue;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) continue;
            if (client.PlayerObject == null) continue;

            Vector3 theirEye = client.PlayerObject.transform.position + Vector3.up * eyeHeight;

            // 두 사람 사이에 벽이 있는가?
            bool blocked = Physics.Linecast(myEye, theirEye, wallMask, QueryTriggerInteraction.Ignore);

            p.SetLocalVolume(blocked ? blockedVolume : 0);
        }
    }
}