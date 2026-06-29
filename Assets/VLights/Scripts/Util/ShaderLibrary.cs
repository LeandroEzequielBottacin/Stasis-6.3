using UnityEngine;

namespace VLights.Scripts.Util
{
    // [CreateAssetMenuAttribute]
    public class ShaderLibrary : ScriptableObject
    {
        [SerializeField]
        private Shader[] _shaders;
    }
}