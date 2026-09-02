using UnityEditor;
using UnityEngine;

public static class FixMissingScripts
{
    [MenuItem("Tools/Fix Hedro Missing Scripts")]
    public static void FixHedro()
    {
        string path = "Assets/Puzzle Elements/Hedron/Hedro.prefab";
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        
        if (prefabRoot != null)
        {
            // Load it properly to edit
            using (var editingScope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var prefabContentsRoot = editingScope.prefabContentsRoot;
                int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefabContentsRoot);
                
                // Recurse to children
                Transform[] allChildren = prefabContentsRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in allChildren)
                {
                    removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }

                Debug.Log($"Se eliminaron {removedCount} scripts faltantes del prefab Hedro.");
            } // Automatically saves the prefab when the scope is disposed
        }
        else
        {
            Debug.LogError("No se pudo cargar el prefab Hedro en la ruta: " + path);
        }
    }
}
