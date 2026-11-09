using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerHitFlash : NetworkBehaviour
{
    [Header("Target")]
    [Tooltip("몸 SkinnedMeshRenderer만 넣을 것. 비우면 자식 전체를 잡음(이름표, 아이템까지 포함되니 비추천)")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Flash")]
    [SerializeField] private Color flashColor = new Color(1f, 0.1f, 0.1f);
    [SerializeField] private float emissionIntensity = 2f;
    [SerializeField] private int flashCount = 3;
    [SerializeField] private float flashOnTime = 0.08f;
    [SerializeField] private float flashOffTime = 0.08f;

    private PlayerStateList state;
    private MaterialPropertyBlock block;
    private Coroutine flashRoutine;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        state = GetComponent<PlayerStateList>();
        block = new MaterialPropertyBlock();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        state.health.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        state.health.OnValueChanged -= OnHealthChanged;
        StopFlash();
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (newValue >= oldValue) return; // 감소할 때만

        StopFlash();                       // 겹치면 이전 깜빡임 정리
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < flashCount; i++)
        {
            SetEmission(flashColor * emissionIntensity);
            yield return new WaitForSeconds(flashOnTime);

            ClearEmission();
            yield return new WaitForSeconds(flashOffTime);
        }
        flashRoutine = null;
    }

    private void StopFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        ClearEmission();
    }

    private void SetEmission(Color color)
    {
        foreach (var r in targetRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, color);
            r.SetPropertyBlock(block);
        }
    }

    private void ClearEmission()
    {
        foreach (var r in targetRenderers)
        {
            if (r == null) continue;
            r.SetPropertyBlock(null); // 오버라이드 제거 → 원본으로 복귀
        }
    }
}