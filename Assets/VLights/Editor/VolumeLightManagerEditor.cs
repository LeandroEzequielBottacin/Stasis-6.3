using UnityEditor;
using VLights.Scripts.VLight;

namespace VLights.Editor
{
    [CustomEditor(typeof(VLightManager))]
    public class VolumeLightManagerEditor : UnityEditor.Editor
    {
        public VLightManager Manager => target as VLightManager;
    }
}