using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stasis.Rendering.EditorTools
{
    /// <summary>
    /// Strips the runtime stasis bit from every renderer just before a scene is saved.
    ///
    /// StasisRenderingLayers.StasisMask is transient state that StasisEffect toggles while
    /// an object is frozen, but it lives in Renderer.renderingLayerMask, which is a
    /// serialized field. Setting it from an editor script (or leaving it set after a test)
    /// bakes "this object is frozen" into the scene, and every later play session starts
    /// with those objects outlined for no visible reason.
    ///
    /// The eligible bit is authoring data and is deliberately left alone.
    /// </summary>
    [InitializeOnLoad]
    public static class StasisOutlineBitGuard
    {
        static StasisOutlineBitGuard()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            int cleared = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
                {
                    if ((rend.renderingLayerMask & StasisRenderingLayers.StasisMask) == 0) continue;
                    rend.renderingLayerMask &= ~StasisRenderingLayers.StasisMask;
                    cleared++;
                }
            }

            if (cleared > 0)
                Debug.Log($"[Stasis] Se limpiaron {cleared} renderers con el bit de stasis " +
                          $"antes de guardar '{scene.name}'. Ese bit es estado de runtime y no debe persistirse.");
        }
    }
}
