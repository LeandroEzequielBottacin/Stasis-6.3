#if UNITY_EDITOR
using System.Linq;
using Audio.MusicSystem;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(MusicDirector))]
    public class MusicDirectorEditor : UnityEditor.Editor
    {
        private SerializedProperty _graph;
        private SerializedProperty _audioRoot;
        private SerializedProperty _voicesPerLayer;
        private SerializedProperty _globalVolume;
        private SerializedProperty _logTransitions;

        private void OnEnable()
        {
            _graph          = serializedObject.FindProperty("graph");
            _audioRoot      = serializedObject.FindProperty("audioRoot");
            _voicesPerLayer = serializedObject.FindProperty("voicesPerLayer");
            _globalVolume   = serializedObject.FindProperty("globalVolume");
            _logTransitions = serializedObject.FindProperty("logTransitions");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_graph);
            EditorGUILayout.PropertyField(_audioRoot);
            EditorGUILayout.PropertyField(_voicesPerLayer);
            EditorGUILayout.Slider(_globalVolume, 0f, 1f);
            EditorGUILayout.PropertyField(_logTransitions);

            var dir = (MusicDirector)target;

            if (!dir || !dir.graph)
            {
                EditorGUILayout.HelpBox("Asigná un MusicGraph para habilitar los controles.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Graph Controls", EditorStyles.boldLabel);

            // Nodos
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Nodos", EditorStyles.boldLabel);
                var cols = Mathf.Clamp(Screen.width / 180, 2, 4);
                int i = 0;
                EditorGUILayout.BeginHorizontal();
                foreach (var node in dir.graph.nodes)
                {
                    if (i > 0 && i % cols == 0) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }

                    var isCurrent = Application.isPlaying && dir.CurrentNodeId == node.id;
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        var style = isCurrent ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                        if (GUILayout.Button(new GUIContent(node.id, node.cue ? node.cue.name : "No Cue"), style, GUILayout.Width(160)))
                        {
                            Undo.RecordObject(dir, "GoToNode");
                            dir.GoToNode(node.id);
                        }
                    }
                    i++;
                }
                EditorGUILayout.EndHorizontal();
            }

            // Triggers
            var uniqueTriggers = dir.graph.transitions
                .Select(t => t.triggerName)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            if (uniqueTriggers.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Triggers", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        foreach (var trg in uniqueTriggers)
                        {
                            if (GUILayout.Button($"Trigger: {trg}", GUILayout.Width(200)))
                                dir.Trigger(trg);
                        }
                    }
                }
            }

            // Parámetros
            var paramNames = dir.graph.transitions
                .Select(t => t.paramName)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            if (paramNames.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Parámetros", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        foreach (var p in paramNames)
                        {
                            float current = dir.GetParameter(p);
                            float newVal = EditorGUILayout.Slider(p, current, 0f, 1f);
                            if (!Mathf.Approximately(newVal, current))
                                dir.SetParameter(p, newVal);
                        }
                    }
                }
            }

            // Runtime info + capas
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Current Node:", dir.CurrentNodeId ?? "(none)");
                    EditorGUILayout.LabelField("Current Cue:", dir.CurrentCue ? dir.CurrentCue.name : "(none)");
                }

                if (dir.CurrentCue)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.LabelField("Capas", EditorStyles.boldLabel);

                        foreach (var layer in dir.CurrentCue.layers)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label(layer.id, GUILayout.Width(120));

                                bool isActive = dir.ActiveLayerSources.ContainsKey(layer.id) && dir.ActiveLayerSources[layer.id];
                                string btn = isActive ? "Disable" : "Enable";
                                if (GUILayout.Button(btn, GUILayout.Width(80)))
                                {
                                    dir.SetLayerEnabled(layer.id, !isActive, 0.4f);
                                }

                                using (new EditorGUI.DisabledScope(!isActive))
                                {
                                    float currentVol = isActive ? dir.ActiveLayerSources[layer.id].volume : layer.defaultVolume;
                                    float targetVol = EditorGUILayout.Slider(currentVol, 0f, 1f);
                                    if (isActive && !Mathf.Approximately(targetVol, currentVol))
                                        dir.SetLayerVolume(layer.id, targetVol);
                                }
                            }
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif