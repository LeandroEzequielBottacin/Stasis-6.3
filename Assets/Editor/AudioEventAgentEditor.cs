#if UNITY_EDITOR
using System.Linq;
using Audio.Scripts;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(AudioEventAgent))]
    public class AudioEventAgentEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetScripts;
        private SerializedProperty _defaultMixerGroup;
        private SerializedProperty _sourceTemplate;
        private SerializedProperty _globalEmitterOverride;
        private SerializedProperty _events;

        private ReorderableList _scriptsList;

        private void OnEnable()
        {
            _targetScripts        = serializedObject.FindProperty("targetScripts");
            _defaultMixerGroup    = serializedObject.FindProperty("defaultMixerGroup");
            _sourceTemplate       = serializedObject.FindProperty("sourceTemplate");
            _globalEmitterOverride= serializedObject.FindProperty("globalEmitterOverride");
            _events               = serializedObject.FindProperty("events");

            _scriptsList = new ReorderableList(serializedObject, _targetScripts, true, true, true, true)
                {
                    DrawHeaderCallback = rect => EditorGUI.LabelField(rect, "Target Scripts"),
                    DrawElementCallback = (rect, index, active, focused) =>
                    {
                        var el = _targetScripts.GetArrayElementAtIndex(index);
                        EditorGUI.PropertyField(rect, el, GUIContent.none);
                    }
                };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _scriptsList.DoLayoutList();

            EditorGUILayout.PropertyField(_defaultMixerGroup);
            EditorGUILayout.PropertyField(_sourceTemplate);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Emitters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_globalEmitterOverride, new GUIContent("Global Emitter Override"));

            var agent = (AudioEventAgent)target;

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Eventos detectados", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refrescar", GUILayout.Width(100)))
                {
                    // Solo sincroniza la lista en el inspector (no suscribe)
                    Undo.RecordObject(agent, "Refresh Audio Events");
                    var keys = AudioEventHub.EditorScanTargets(agent.TargetScripts)
                                             .Select(p => AudioEventHub.MakeKeyForEditor(p.script, p.memberName));
                    agent.SyncEventConfigListWithReflectedMembers(keys);
                    EditorUtility.SetDirty(agent);
                    GUI.FocusControl(null);
                }
            }

            if (_events is { isArray: true })
            {
                for (int i = 0; i < _events.arraySize; i++)
                {
                    var ev = _events.GetArrayElementAtIndex(i);
                    DrawEventConfig(ev, agent);
                    EditorGUILayout.Space(6);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEventConfig(SerializedProperty ev, AudioEventAgent agent)
        {
            var eventKey            = ev.FindPropertyRelative("eventKey");
            var displayName         = ev.FindPropertyRelative("displayName");
            var eventName           = ev.FindPropertyRelative("eventName");

            var enabled             = ev.FindPropertyRelative("enabled");
            var isStopEvent         = ev.FindPropertyRelative("isStopEvent");
            var randomOne           = ev.FindPropertyRelative("randomOne");
            var clips               = ev.FindPropertyRelative("clips");

            var emitterOverride     = ev.FindPropertyRelative("emitterOverride");

            var usePitchRandom      = ev.FindPropertyRelative("usePitchRandom");
            var pitchMin            = ev.FindPropertyRelative("pitchMin");
            var pitchMax            = ev.FindPropertyRelative("pitchMax");

            var stopMode            = ev.FindPropertyRelative("stopMode");
            var stopTargetEventKey  = ev.FindPropertyRelative("stopTargetEventKey");
            var fadeOutOnStop       = ev.FindPropertyRelative("fadeOutOnStop");
            var fadeOutTime         = ev.FindPropertyRelative("fadeOutTime");

            var maxSimultaneous     = ev.FindPropertyRelative("maxSimultaneous");
            var coalesceWindow      = ev.FindPropertyRelative("coalesceWindow");
            var blockSameFrameDupes = ev.FindPropertyRelative("blockSameFrameDuplicates");
            var spatialMode = ev.FindPropertyRelative("spatialMode");
            var overrideDistance = ev.FindPropertyRelative("overrideDistance");
            var customMaxDistance = ev.FindPropertyRelative("customMaxDistance");

            EditorGUILayout.BeginVertical("HelpBox");

            using (new EditorGUILayout.HorizontalScope())
            {
                enabled.boolValue = EditorGUILayout.ToggleLeft(
                    $" {(displayName.stringValue ?? eventName.stringValue)}",
                    enabled.boolValue,
                    EditorStyles.boldLabel
                );
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField(eventKey.stringValue, GUILayout.MaxWidth(240));
                isStopEvent.boolValue = GUILayout.Toggle(isStopEvent.boolValue, "Stop Event", "Button", GUILayout.Width(100));
            }

            EditorGUI.indentLevel++;

            if (!isStopEvent.boolValue)
            {
                EditorGUILayout.PropertyField(emitterOverride, new GUIContent("Emitter Override (evento)"));
                randomOne.boolValue = EditorGUILayout.ToggleLeft("Usar Random Sound (sin repetir el último)", randomOne.boolValue);

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Spatial & 3D Settings", EditorStyles.boldLabel);
                
                EditorGUILayout.PropertyField(spatialMode, new GUIContent("Modo Espacial"));

                var currentMode = (AudioEventAgent.SpatialMode)spatialMode.enumValueIndex;
                bool showDistance = currentMode == AudioEventAgent.SpatialMode.Force3D || 
                                    (currentMode == AudioEventAgent.SpatialMode.UseClipSettings); // Asumimos que clips pueden ser 3D

                if (showDistance)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(overrideDistance, new GUIContent("Override Radio 3D"));
                    if (overrideDistance.boolValue)
                    {
                        EditorGUILayout.PropertyField(customMaxDistance, GUIContent.none);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
                randomOne.boolValue = EditorGUILayout.ToggleLeft("Randomize Clip Selection", randomOne.boolValue);
                EditorGUILayout.PropertyField(maxSimultaneous, new GUIContent("Max Voices"));
                EditorGUILayout.PropertyField(coalesceWindow, new GUIContent("Coalesce (s)"));
                blockSameFrameDupes.boolValue = EditorGUILayout.ToggleLeft("Block Same Frame", blockSameFrameDupes.boolValue);

                EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);
                // Si forzamos 2D o 3D, ocultamos la opción 'Use 3D' individual del clip para no confundir
                bool hideClip3D = currentMode != AudioEventAgent.SpatialMode.UseClipSettings;
                DrawClipList(clips, showLoop: true, hideUse3D: hideClip3D);
            }
            else
            {
                EditorGUILayout.PropertyField(stopMode);

                if ((AudioEventAgent.StopMode)stopMode.enumValueIndex == AudioEventAgent.StopMode.ByEvent)
                {
                    // opciones = todos los eventos detectados (display -> key) — como estamos en un Agent,
                    // ofrecemos solo los del mismo agent:
                    var pairs = agent.EventConfigs.Select(e => (e.eventKey, e.displayName ?? e.eventName)).ToList();
                    var displays = pairs.Select(p => p.Item2).ToArray();
                    var keys     = pairs.Select(p => p.eventKey).ToArray();
                    int idx = System.Array.IndexOf(keys, stopTargetEventKey.stringValue);
                    int newIdx = EditorGUILayout.Popup(new GUIContent("Stop Target (evento)"), Mathf.Max(0, idx), displays);
                    if (newIdx >= 0 && newIdx < keys.Length)
                        stopTargetEventKey.stringValue = keys[newIdx];
                }
                else if ((AudioEventAgent.StopMode)stopMode.enumValueIndex == AudioEventAgent.StopMode.ByClips)
                {
                    EditorGUILayout.HelpBox("Agregá aquí los clips a detener. Si la lista está vacía no se detendrá nada.", MessageType.Info);
                    EditorGUILayout.LabelField("Clips a detener", EditorStyles.boldLabel);
                    DrawClipList(clips, showLoop:false);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(fadeOutOnStop, new GUIContent("Fade Out"));
                if (fadeOutOnStop.boolValue)
                    EditorGUILayout.PropertyField(fadeOutTime, new GUIContent("Tiempo"));
                EditorGUILayout.EndHorizontal();
            }

            // Pitch
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Pitch", EditorStyles.boldLabel);
            usePitchRandom.boolValue = EditorGUILayout.ToggleLeft("Usar Pitch Aleatorio", usePitchRandom.boolValue);
            using (new EditorGUI.DisabledScope(!usePitchRandom.boolValue))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(4);
                    GUILayout.Label("Min", GUILayout.Width(28));
                    pitchMin.floatValue = EditorGUILayout.FloatField(pitchMin.floatValue, GUILayout.Width(80));
                    GUILayout.Space(12);
                    GUILayout.Label("Max", GUILayout.Width(30));
                    pitchMax.floatValue = EditorGUILayout.FloatField(pitchMax.floatValue, GUILayout.Width(80));
                    GUILayout.FlexibleSpace();
                }
                if (pitchMin.floatValue > pitchMax.floatValue)
                    pitchMax.floatValue = pitchMin.floatValue;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawClipList(SerializedProperty listProp, bool showLoop = true, bool hideUse3D = false)
        {
            int removeAt = -1;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var el = listProp.GetArrayElementAtIndex(i);
                var clip = el.FindPropertyRelative("clip");
                var volume = el.FindPropertyRelative("volume");
                var delay = el.FindPropertyRelative("delay");
                var loop = el.FindPropertyRelative("loop");
                var use3D = el.FindPropertyRelative("use3D");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(clip, GUIContent.none);
                if (GUILayout.Button("X", GUILayout.Width(20))) removeAt = i;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Vol", GUILayout.Width(25));
                EditorGUILayout.Slider(volume, 0f, 1f, GUIContent.none);
                EditorGUILayout.LabelField("Dly", GUILayout.Width(25));
                EditorGUILayout.PropertyField(delay, GUIContent.none, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                if (showLoop)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(loop, new GUIContent("Loop"));
                    if (!hideUse3D)
                    {
                        EditorGUILayout.PropertyField(use3D, new GUIContent("3D"));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            if (removeAt >= 0) listProp.DeleteArrayElementAtIndex(removeAt);
            if (GUILayout.Button("+ Clip")) listProp.InsertArrayElementAtIndex(Mathf.Max(0, listProp.arraySize));
        }
        
        // ReorderableList mínima
        private class ReorderableList
        {
            public delegate void DrawElement(Rect rect, int index, bool isActive, bool isFocused);
            public delegate void DrawHeader(Rect rect);

            private readonly SerializedObject _so;
            private readonly SerializedProperty _prop;
            private readonly bool _displayHeader, _displayAddButton, _displayRemoveButton;

            public DrawHeader DrawHeaderCallback;
            public DrawElement DrawElementCallback;

            public ReorderableList(SerializedObject so, SerializedProperty prop, bool draggable, bool displayHeader, bool displayAdd, bool displayRemove)
            {
                _so = so; _prop = prop;
                _displayHeader = displayHeader; _displayAddButton = displayAdd; _displayRemoveButton = displayRemove;
            }

            public void DoLayoutList()
            {
                if (_displayHeader)
                {
                    var r = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                    DrawHeaderCallback?.Invoke(r);
                }

                int removeIndex = -1;
                for (int i = 0; i < _prop.arraySize; i++)
                {
                    var r = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                    DrawElementCallback?.Invoke(r, i, false, false);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (_displayRemoveButton && GUILayout.Button("Remove", GUILayout.Width(70)))
                            removeIndex = i;
                    }
                }

                if (removeIndex >= 0) _prop.DeleteArrayElementAtIndex(removeIndex);
                if (_displayAddButton && GUILayout.Button("Add"))
                    _prop.InsertArrayElementAtIndex(Mathf.Max(0, _prop.arraySize));
            }
        }
    }
}
#endif