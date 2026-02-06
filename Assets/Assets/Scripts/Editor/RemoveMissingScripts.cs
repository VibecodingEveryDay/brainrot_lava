using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Утилита для удаления компонентов с отсутствующими скриптами (Missing Script).
/// Меню: Tools -> Remove Missing Scripts
/// </summary>
public static class RemoveMissingScripts
{
    [MenuItem("Tools/Remove Missing Scripts (Current Scene)")]
    public static void RemoveMissingScriptsFromScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded)
        {
            Debug.LogWarning("[RemoveMissingScripts] Сцена не загружена.");
            return;
        }

        int removedCount = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            removedCount += RemoveMissingScriptsRecursive(root);
        }

        if (removedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[RemoveMissingScripts] Удалено {removedCount} компонентов с отсутствующими скриптами в сцене {scene.name}");
        }
        else
        {
            Debug.Log("[RemoveMissingScripts] Компоненты с отсутствующими скриптами не найдены в сцене.");
        }
    }

    [MenuItem("Tools/Remove Missing Scripts (All Prefabs in Project)")]
    public static void RemoveMissingScriptsFromPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int totalRemoved = 0;
        int prefabsModified = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot == null) continue;

            try
            {
                int removed = RemoveMissingScriptsRecursive(prefabRoot);
                if (removed > 0)
                {
                    totalRemoved += removed;
                    prefabsModified++;
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        if (totalRemoved > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RemoveMissingScripts] Удалено {totalRemoved} компонентов в {prefabsModified} префабах.");
        }
        else
        {
            Debug.Log("[RemoveMissingScripts] Компоненты с отсутствующими скриптами не найдены в префабах.");
        }
    }

    [MenuItem("Tools/Remove Missing Scripts (Scene + All Prefabs)")]
    public static void RemoveMissingScriptsEverywhere()
    {
        RemoveMissingScriptsFromScene();
        RemoveMissingScriptsFromPrefabs();
    }

    private static int RemoveMissingScriptsRecursive(GameObject go)
    {
        int count = RemoveMissingScriptsFromGameObject(go);
        foreach (Transform child in go.transform)
        {
            count += RemoveMissingScriptsRecursive(child.gameObject);
        }
        return count;
    }

    private static int RemoveMissingScriptsFromGameObject(GameObject go)
    {
        return GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
    }
}
