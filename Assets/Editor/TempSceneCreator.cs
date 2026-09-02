using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class TempSceneCreator
{
    [MenuItem("Tools/Create Test Sounds Scene")]
    public static void CreateTestSoundsScene()
    {
        string sourcePath = "Assets/Scenes/Level/Gameplay.unity";
        string targetPath = "Assets/Scenes/Test Scenes/TEST_SOUNDS.unity";

        // Open the Gameplay scene
        var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);

        // Analyze prefabs
        var rootObjects = scene.GetRootGameObjects();
        Dictionary<GameObject, List<GameObject>> prefabInstances = new Dictionary<GameObject, List<GameObject>>();
        List<GameObject> toKeep = new List<GameObject>();

        foreach (var root in rootObjects)
        {
            FindPrefabsRecursive(root, prefabInstances, toKeep);
        }

        // Keep the ones we want
        // Sort prefabs by instance count
        var topPrefabs = prefabInstances
            .Where(kv => kv.Value.Count > 1)
            .OrderByDescending(kv => kv.Value.Count)
            .Take(25) // top 25 most repeated prefabs
            .ToList();

        List<GameObject> selectedInstancesToKeep = new List<GameObject>();
        foreach (var kv in topPrefabs)
        {
            if (kv.Value.Count > 0)
                selectedInstancesToKeep.Add(kv.Value[0]); // keep the first one
        }

        // Also keep systems, player, camera, lights
        foreach (var root in rootObjects)
        {
            string lowerName = root.name.ToLower();
            bool isSystem = lowerName.Contains("manager") || lowerName.Contains("system") || 
                            lowerName.Contains("audio") || lowerName.Contains("hub") ||
                            lowerName.Contains("camera") || lowerName.Contains("light") ||
                            lowerName.Contains("player") || lowerName.Contains("ui") ||
                            lowerName.Contains("canvas") || lowerName.Contains("event") ||
                            lowerName.Contains("volume") || lowerName.Contains("postprocess") ||
                            lowerName.Contains("environment"); // careful with environment, it might be huge.
            
            // Wait, environment could be the parent of everything. 
            if (lowerName.Contains("env") || lowerName == "world") continue; // let's not keep full environment

            if (isSystem || root.CompareTag("Player") || root.CompareTag("MainCamera"))
            {
                if (!toKeep.Contains(root))
                {
                    toKeep.Add(root);
                }
            }
        }

        // Now delete everything that is not in toKeep or selectedInstancesToKeep
        // But wait, if we keep a root object, we keep its children.
        // If a prefab instance is a child of something we delete, we should unparent it first!
        
        // Let's create a "TestObjects" root
        GameObject testRoot = new GameObject("TestObjects");
        
        float xOffset = 0;
        foreach (var inst in selectedInstancesToKeep)
        {
            inst.transform.SetParent(testRoot.transform);
            inst.transform.position = new Vector3(xOffset, 0, 5); // put them in a line
            xOffset += 3f;
        }

        // Delete all root objects that are not systems or our new TestRoot
        foreach (var root in rootObjects)
        {
            if (root == testRoot) continue;

            bool shouldKeep = toKeep.Contains(root);
            
            // If it's not a system, destroy it
            if (!shouldKeep)
            {
                Object.DestroyImmediate(root);
            }
        }

        // Save as new scene
        EditorSceneManager.SaveScene(scene, targetPath);
        Debug.Log("TEST_SOUNDS scene created successfully at: " + targetPath);
        
        // Quit editor since this is called from CLI
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    private static void FindPrefabsRecursive(GameObject obj, Dictionary<GameObject, List<GameObject>> prefabInstances, List<GameObject> toKeep)
    {
        // Is it a prefab instance?
        if (PrefabUtility.IsAnyPrefabInstanceRoot(obj))
        {
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(obj) as GameObject;
            if (prefabAsset != null)
            {
                if (!prefabInstances.ContainsKey(prefabAsset))
                {
                    prefabInstances[prefabAsset] = new List<GameObject>();
                }
                prefabInstances[prefabAsset].Add(obj);
            }
            // Do not recurse into prefab children for finding other prefabs to keep it simple and avoid breaking prefab structure
            return;
        }

        // If not a prefab root, check children
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            FindPrefabsRecursive(obj.transform.GetChild(i).gameObject, prefabInstances, toKeep);
        }
    }
}
