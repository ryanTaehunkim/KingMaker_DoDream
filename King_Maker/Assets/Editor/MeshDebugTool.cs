using UnityEngine;
using UnityEditor;

public class MeshDebugTool
{
    [MenuItem("Tools/Mesh Debug/Log Normal Info")]
    static void LogNormalInfo()
    {
        foreach (var go in Selection.gameObjects)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.Log($"[{go.name}] MeshFilter 없음 or Mesh 없음");
                continue;
            }

            Mesh mesh = mf.sharedMesh;
            Vector3[] normals = mesh.normals;
            Vector3 avgNormal = Vector3.zero;
            foreach (var n in normals) avgNormal += n;
            avgNormal /= normals.Length;

            Debug.Log($"[{go.name}] Mesh: {mesh.name}, VertexCount: {mesh.vertexCount}, " +
                      $"평균 노멀(로컬기준): {avgNormal}, " +
                      $"World Up 기준 내적: {Vector3.Dot(go.transform.TransformDirection(avgNormal.normalized), Vector3.up)}, " +
                      $"Scale: {go.transform.lossyScale}");

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Debug.Log($"[{go.name}] Material: {mr.sharedMaterial?.name}, Shader: {mr.sharedMaterial?.shader.name}, " +
                          $"CastShadows: {mr.shadowCastingMode}, ReceiveShadows: {mr.receiveShadows}");
            }

            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            Debug.Log($"[{go.name}] Static Flags: {flags}");
        }
    }

    [MenuItem("Tools/Mesh Debug/Recalculate Normals (Selected)")]
    static void RecalcNormals()
    {
        foreach (var go in Selection.gameObjects)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            // 공유 메쉬를 건드리면 다른 오브젝트에 영향 줄 수 있으니 복제해서 작업
            Mesh mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + "_Fixed";
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var mc = go.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = mesh;

            EditorUtility.SetDirty(go);
            Debug.Log($"[{go.name}] 노멀 재계산 완료 (새 메쉬: {mesh.name})");
        }
    }

    [MenuItem("Tools/Mesh Debug/Auto Fix Downward Floor Normals (Selected)")]
    static void AutoFixDownwardNormals()
    {
        int fixedCount = 0;
        foreach (var go in Selection.gameObjects)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh mesh = mf.sharedMesh;
            Vector3[] normals = mesh.normals;
            if (normals.Length == 0) continue;

            Vector3 avgLocalNormal = Vector3.zero;
            foreach (var n in normals) avgLocalNormal += n;
            avgLocalNormal /= normals.Length;

            Vector3 avgWorldNormal = go.transform.TransformDirection(avgLocalNormal.normalized);
            float dot = Vector3.Dot(avgWorldNormal, Vector3.up);

            Debug.Log($"[{go.name}] World Normal Dot(Up): {dot}");

            // 아래를 향하고 있으면(위를 향해야 할 바닥인데 반대) 뒤집기
            if (dot < -0.1f)
            {
                Mesh newMesh = Object.Instantiate(mesh);
                newMesh.name = mesh.name + "_Fixed";

                Vector3[] newNormals = newMesh.normals;
                for (int i = 0; i < newNormals.Length; i++)
                    newNormals[i] = -newNormals[i];
                newMesh.normals = newNormals;

                int[] triangles = newMesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int temp = triangles[i];
                    triangles[i] = triangles[i + 1];
                    triangles[i + 1] = temp;
                }
                newMesh.triangles = triangles;

                mf.sharedMesh = newMesh;
                var mc = go.GetComponent<MeshCollider>();
                if (mc != null) mc.sharedMesh = newMesh;

                EditorUtility.SetDirty(go);
                fixedCount++;
                Debug.Log($"[{go.name}] ▶ 노멀 반전 감지 → 자동 수정 완료");
            }
        }
        Debug.Log($"총 {fixedCount}개 오브젝트 수정됨");
    }

    [MenuItem("Tools/Mesh Debug/Scan Entire Scene For Bad Scale")]
    static void ScanSceneForBadScale()
    {
        var allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        int problemCount = 0;
        foreach (var mr in allRenderers)
        {
            var go = mr.gameObject;
            Vector3 scale = go.transform.lossyScale;

            bool hasZero = Mathf.Abs(scale.x) < 0.0001f || Mathf.Abs(scale.y) < 0.0001f || Mathf.Abs(scale.z) < 0.0001f;
            bool hasExtreme = Mathf.Abs(scale.x) > 1000f || Mathf.Abs(scale.y) > 1000f || Mathf.Abs(scale.z) > 1000f;

            if (hasZero || hasExtreme)
            {
                Debug.LogWarning($"[문제 발견] {GetPath(go)} - World Scale: {scale}", go);
                problemCount++;
            }

            // 머티리얼 Metallic/Smoothness 극단값도 같이 체크
            var mat = mr.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty("_Metallic") && mat.HasProperty("_Smoothness"))
                {
                    float metallic = mat.GetFloat("_Metallic");
                    float smoothness = mat.GetFloat("_Smoothness");
                    if (metallic > 0.8f && smoothness > 0.8f)
                    {
                        Debug.LogWarning($"[강한 스펙큘러 재질] {GetPath(go)} - Material: {mat.name}, Metallic: {metallic}, Smoothness: {smoothness}", go);
                    }
                }
            }
        }
        Debug.Log($"스캔 완료. 스케일 문제 오브젝트: {problemCount}개 (위쪽 경고 로그 확인)");
    }

    static string GetPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    [MenuItem("Tools/Mesh Debug/Flip Normals (Selected)")]
    static void FlipNormals()
    {
        foreach (var go in Selection.gameObjects)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + "_Flipped";

            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
                normals[i] = -normals[i];
            mesh.normals = normals;

            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int temp = triangles[i];
                triangles[i] = triangles[i + 1];
                triangles[i + 1] = temp;
            }
            mesh.triangles = triangles;

            mf.sharedMesh = mesh;

            var mc = go.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = mesh;

            EditorUtility.SetDirty(go);
            Debug.Log($"[{go.name}] 노멀+와인딩 반전 완료 (새 메쉬: {mesh.name})");
        }
    }
}