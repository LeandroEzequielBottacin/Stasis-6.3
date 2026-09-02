using UnityEditor;
using UnityEngine;

public static class AddTestPrefabs
{
    [MenuItem("Tools/Add Test Prefabs to Scene")]
    public static void AddPrefabs()
    {
        string[] prefabPaths = new string[]
        {
            "Assets/Puzzle Elements/Hedron/Hedro.prefab",
            "Assets/Puzzle Elements/IK/Prefabs/Platform Distance/IK by Distance.prefab",
            "Assets/-Ian/Old Folder/Fbx/Basura_O_No_Hay_Que_Ver/Mod_Hedro_Spot.prefab",
            "Assets/Puzzle Elements/LaunchPlate/Pad.prefab"
        };

        float xOffset = 0f;
        foreach (var path in prefabPaths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                if (instance != null)
                {
                    instance.transform.position = new Vector3(xOffset, 0, 0); // Spaced out along X on the ground
                    instance.name = asset.name + " (Test)";
                    Undo.RegisterCreatedObjectUndo(instance, "Add Test Prefab");
                    xOffset += 3f;
                }
            }
            else
            {
                Debug.LogWarning("No se encontró el prefab en: " + path);
            }
        }
    }
}
