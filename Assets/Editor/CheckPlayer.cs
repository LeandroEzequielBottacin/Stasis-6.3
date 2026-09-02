using UnityEngine;
using UnityEditor;
using System.Linq;

public static class CheckPlayer
{
    [MenuItem("Tools/Check Player")]
    public static void Check()
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab");
        Debug.Log(string.Join(", ", p.GetComponents<MonoBehaviour>().Select(x => x.GetType().Name)));
    }
}
