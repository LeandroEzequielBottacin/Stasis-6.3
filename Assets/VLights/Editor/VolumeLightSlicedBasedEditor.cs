using UnityEditor;
using UnityEngine;
using VLights.Scripts.VLight;

namespace VLights.Editor
{
    [CustomEditor(typeof(VLight))]
    [CanEditMultipleObjects]
    public class VolumeLightSlicedBasedEditor : UnityEditor.Editor
    {
        // Toggle global para wireframe (solo para vista en el editor)
        private static bool _renderWireframe = false;

        private VLight Light => (VLight)target;

        public override void OnInspectorGUI()
        {
            // Actualizamos el objeto serializado por si hay propiedades
            serializedObject.Update();

            // Dibuja el inspector por defecto del VLight
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug / Tools", EditorStyles.boldLabel);

            // Aseguramos que el MeshRenderer sea visible (si existe)
            if (Light != null && Light.MeshRender != null)
            {
                Light.MeshRender.hideFlags = HideFlags.None;

                // Toggle de wireframe
                _renderWireframe = EditorGUILayout.Toggle("Render wireframe", _renderWireframe);

                var editorSelectedRenderState = _renderWireframe
                    ? EditorSelectedRenderState.Wireframe
                    : EditorSelectedRenderState.Hidden;

                EditorUtility.SetSelectedRenderState(Light.MeshRender, editorSelectedRenderState);
            }
            else
            {
                EditorGUILayout.HelpBox("MeshRender no está asignado en VLight.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shadow Map", EditorStyles.boldLabel);

            if (GUILayout.Button("Bake shadow map", GUILayout.Width(200)))
            {
                if (Light != null)
                {
                    Light.RenderBakedShadowMap();
                }
                else
                {
                    Debug.LogWarning("No se pudo acceder a VLight para hornear el shadow map.");
                }
            }

            // Aplicamos cambios si hubiera propiedades serializadas
            serializedObject.ApplyModifiedProperties();
        }
    }
}
