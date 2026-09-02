using UnityEditor;

public static class ReimportHLSL
{
    [MenuItem("Tools/Reimport HLSL")]
    public static void DoIt()
    {
        AssetDatabase.ImportAsset("Assets/Player/Shaders/ElectricalOutlineNode.hlsl", ImportAssetOptions.ForceUpdate);
    }
}
