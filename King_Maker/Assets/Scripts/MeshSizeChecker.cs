using UnityEngine;

public class MeshSizeChecker : MonoBehaviour
{
    private void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            Debug.Log($"Mesh Size (world): {rend.bounds.size}");
            Debug.Log($"Mesh Center (world): {rend.bounds.center}");
        }
        else
        {
            Debug.Log("Renderer 컴포넌트를 못 찾았습니다.");
        }
    }
}