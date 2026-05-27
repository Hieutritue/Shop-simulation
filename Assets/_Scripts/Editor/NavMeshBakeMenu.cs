#if UNITY_EDITOR
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu utility: bake mọi NavMeshSurface trong scene đang active.
/// Dùng khi không muốn mở Navigation window thủ công.
/// </summary>
public static class NavMeshBakeMenu
{
    [MenuItem("Tools/Bake All NavMesh Surfaces")]
    public static void BakeAll()
    {
        var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        if (surfaces.Length == 0)
        {
            Debug.LogWarning("No NavMeshSurface found in active scene.");
            return;
        }
        foreach (var s in surfaces)
        {
            s.BuildNavMesh();
            EditorUtility.SetDirty(s);
            Debug.Log($"Baked NavMesh on '{s.gameObject.name}'");
        }
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"NavMesh bake complete — {surfaces.Length} surface(s).");
    }
}
#endif
