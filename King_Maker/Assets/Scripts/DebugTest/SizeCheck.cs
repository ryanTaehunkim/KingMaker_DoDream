using UnityEngine;

public class SizeCheck : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private Collider meshCollider;

    public enum checkType
    {
        meshRendereType = 0,
        colliderType = 1,
    }

    public checkType objType;

    private void Start()
    {
        if (objType == checkType.meshRendereType)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
        else if (objType == checkType.colliderType)
        {
            meshCollider = GetComponent<Collider>();
        }
    }

    private void Update()
    {
        if (objType == checkType.meshRendereType)
        {
            if (meshRenderer != null)
            {
                Bounds bound = meshRenderer.bounds;
                Debug.Log($"size = {bound.size} x= {bound.size.x:F2} y= {bound.size.y:F2}  z = {bound.size.z:F2}");

            }
        }
        else if (objType == checkType.colliderType)
        {
            Bounds bound = meshCollider.bounds;
            Debug.Log($"size = {bound.size} x= {bound.size.x:F2}, y= {bound.size.y:F2}, z= {bound.size.z:F2}");
        }
        

    }
}
