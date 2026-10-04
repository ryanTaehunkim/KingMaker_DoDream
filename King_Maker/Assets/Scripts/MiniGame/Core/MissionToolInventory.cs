using System.Collections.Generic;
using UnityEngine;

// 미션 도구 보유 여부를 알려주는 인터페이스.
// 도구 획득/상점 파트에서 플레이어 오브젝트에 이 인터페이스를 구현한 컴포넌트를 붙이면 된다.
// 도구 검사는 서버에서만 하므로, 서버에 있는 플레이어 오브젝트의 값만 정확하면 된다.
public interface IMissionToolHolder
{
    bool HasTool(string toolId);
}

// 도구 시스템이 연결되기 전까지 쓰는 기본 구현 (서버 전용 보관).
public class MissionToolInventory : MonoBehaviour, IMissionToolHolder
{
    [SerializeField] private List<string> startingTools = new List<string>();

    private readonly HashSet<string> tools = new HashSet<string>();

    private void Awake()
    {
        foreach (var id in startingTools)
        {
            if (!string.IsNullOrEmpty(id)) tools.Add(id);
        }
    }

    public bool HasTool(string toolId) => tools.Contains(toolId);

    public void AddTool(string toolId)
    {
        if (!string.IsNullOrEmpty(toolId)) tools.Add(toolId);
    }

    public void RemoveTool(string toolId) => tools.Remove(toolId);
}
