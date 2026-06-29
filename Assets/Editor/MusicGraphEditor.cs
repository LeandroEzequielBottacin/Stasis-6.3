#if UNITY_EDITOR
using System;
using System.Linq;
using Audio.MusicSystem;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(MusicGraph))]
    public class MusicGraphEditor : UnityEditor.Editor
    {
        private SerializedProperty _nodesProp;
        private SerializedProperty _transitionsProp;
        private SerializedProperty _startNodeIdProp;

        private void OnEnable()
        {
            _nodesProp        = serializedObject.FindProperty("nodes");
            _transitionsProp  = serializedObject.FindProperty("transitions");
            _startNodeIdProp  = serializedObject.FindProperty("startNodeId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(6);
            DrawNodesSection();

            EditorGUILayout.Space(8);
            DrawStartNodePopup();

            EditorGUILayout.Space(8);
            DrawTransitionsSection();

            serializedObject.ApplyModifiedProperties();
        }

        // ---------- Sections ----------
        private void DrawNodesSection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Nodos", EditorStyles.boldLabel);

                for (int i = 0; i < _nodesProp.arraySize; i++)
                {
                    var nodeProp = _nodesProp.GetArrayElementAtIndex(i);
                    DrawSingleNode(nodeProp, i);
                    EditorGUILayout.Space(4);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+ Agregar Nodo", GUILayout.Width(140)))
                    {
                        _nodesProp.InsertArrayElementAtIndex(Mathf.Max(0, _nodesProp.arraySize));
                        var n = _nodesProp.GetArrayElementAtIndex(_nodesProp.arraySize - 1);
                        n.FindPropertyRelative("id").stringValue = $"Node_{_nodesProp.arraySize}";
                        n.FindPropertyRelative("cue").objectReferenceValue = null;
                    }
                }
            }
        }

        private void DrawSingleNode(SerializedProperty nodeProp, int index)
        {
            var idProp  = nodeProp.FindPropertyRelative("id");
            var cueProp = nodeProp.FindPropertyRelative("cue");

            using (new EditorGUILayout.VerticalScope("helpBox"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"#{index + 1}", GUILayout.Width(28));
                    idProp.stringValue = EditorGUILayout.TextField("Id", idProp.stringValue);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("X", GUILayout.Width(22)))
                    {
                        _nodesProp.DeleteArrayElementAtIndex(index);
                        return;
                    }
                }

                EditorGUILayout.PropertyField(cueProp, new GUIContent("Cue"));
            }
        }

        private void DrawStartNodePopup()
        {
            var ids = GetNodeIds();
            int idx = Mathf.Max(0, Array.IndexOf(ids, _startNodeIdProp.stringValue));
            idx = EditorGUILayout.Popup(new GUIContent("Start Node"), idx, ids);
            if (ids.Length > 0) _startNodeIdProp.stringValue = ids[Mathf.Clamp(idx, 0, ids.Length - 1)];
        }

        private void DrawTransitionsSection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Transiciones", EditorStyles.boldLabel);

                for (int i = 0; i < _transitionsProp.arraySize; i++)
                {
                    var tProp = _transitionsProp.GetArrayElementAtIndex(i);
                    DrawSingleTransition(tProp, i);
                    EditorGUILayout.Space(6);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+ Agregar Transición", GUILayout.Width(180)))
                    {
                        _transitionsProp.InsertArrayElementAtIndex(Mathf.Max(0, _transitionsProp.arraySize));
                        var t = _transitionsProp.GetArrayElementAtIndex(_transitionsProp.arraySize - 1);
                        t.FindPropertyRelative("fromNodeId").stringValue = GetNodeIds().FirstOrDefault() ?? "";
                        t.FindPropertyRelative("toNodeId").stringValue   = GetNodeIds().Skip(1).FirstOrDefault() ?? "";
                        t.FindPropertyRelative("quantization").enumValueIndex = (int)MusicGraph.Quantization.Bar;
                        t.FindPropertyRelative("crossfadeSeconds").floatValue = 1.5f;
                        t.FindPropertyRelative("paramName").stringValue = "";
                        t.FindPropertyRelative("triggerName").stringValue = "";
                        // stingers default
                        t.FindPropertyRelative("playEntryStinger").boolValue = false;
                        t.FindPropertyRelative("entryStingerId").stringValue = "";
                        t.FindPropertyRelative("quantizeEntryStinger").boolValue = true;
                        t.FindPropertyRelative("entryOffsetSeconds").floatValue = 0f;

                        t.FindPropertyRelative("playExitStinger").boolValue = false;
                        t.FindPropertyRelative("exitStingerId").stringValue = "";
                        t.FindPropertyRelative("quantizeExitStinger").boolValue = false;
                        t.FindPropertyRelative("exitOffsetSeconds").floatValue = 0f;
                    }
                }
            }
        }

        private void DrawSingleTransition(SerializedProperty tProp, int index)
        {
            var fromProp  = tProp.FindPropertyRelative("fromNodeId");
            var toProp    = tProp.FindPropertyRelative("toNodeId");

            var triggerProp   = tProp.FindPropertyRelative("triggerName");
            var paramNameProp = tProp.FindPropertyRelative("paramName");
            var compareProp   = tProp.FindPropertyRelative("compare");
            var thresholdProp = tProp.FindPropertyRelative("paramThreshold");

            var quantProp     = tProp.FindPropertyRelative("quantization");
            var xfProp        = tProp.FindPropertyRelative("crossfadeSeconds");

            var playEntryProp     = tProp.FindPropertyRelative("playEntryStinger");
            var entryIdProp       = tProp.FindPropertyRelative("entryStingerId");
            var quantEntryProp    = tProp.FindPropertyRelative("quantizeEntryStinger");
            var entryOffsetProp   = tProp.FindPropertyRelative("entryOffsetSeconds");

            var playExitProp      = tProp.FindPropertyRelative("playExitStinger");
            var exitIdProp        = tProp.FindPropertyRelative("exitStingerId");
            var quantExitProp     = tProp.FindPropertyRelative("quantizeExitStinger");
            var exitOffsetProp    = tProp.FindPropertyRelative("exitOffsetSeconds");

            using (new EditorGUILayout.VerticalScope("helpBox"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Transition #{index + 1}", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("X", GUILayout.Width(22)))
                    {
                        _transitionsProp.DeleteArrayElementAtIndex(index);
                        return;
                    }
                }

                // From / To (dropdowns)
                var ids = GetNodeIds();
                int fromIdx = Mathf.Max(0, Array.IndexOf(ids, fromProp.stringValue));
                int toIdx   = Mathf.Max(0, Array.IndexOf(ids, toProp.stringValue));

                fromIdx = EditorGUILayout.Popup(new GUIContent("From"), fromIdx, ids);
                toIdx   = EditorGUILayout.Popup(new GUIContent("To"),   toIdx,   ids);

                if (ids.Length > 0)
                {
                    fromProp.stringValue = ids[Mathf.Clamp(fromIdx, 0, ids.Length - 1)];
                    toProp.stringValue   = ids[Mathf.Clamp(toIdx,   0, ids.Length - 1)];
                }

                EditorGUILayout.Space(4);

                // Trigger / Parámetro
                EditorGUILayout.LabelField("Condición", EditorStyles.boldLabel);
                triggerProp.stringValue = EditorGUILayout.TextField("Trigger (opcional)", triggerProp.stringValue);

                using (new EditorGUILayout.HorizontalScope())
                {
                    paramNameProp.stringValue = EditorGUILayout.TextField("Param (opcional)", paramNameProp.stringValue);
                    EditorGUILayout.PropertyField(compareProp, GUIContent.none, GUILayout.MaxWidth(140));
                    thresholdProp.floatValue = EditorGUILayout.FloatField("Threshold", thresholdProp.floatValue);
                }

                EditorGUILayout.Space(4);

                // Blend
                EditorGUILayout.LabelField("Blend", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(quantProp, new GUIContent("Quantization"));
                xfProp.floatValue = EditorGUILayout.FloatField(new GUIContent("Crossfade (s)"), xfProp.floatValue);

                EditorGUILayout.Space(4);

                // STINGERS
                EditorGUILayout.LabelField("Stingers", EditorStyles.boldLabel);

                // EXIT (del cue FROM)
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    playExitProp.boolValue = EditorGUILayout.ToggleLeft("Exit Stinger (tapa fade-out del cue actual)", playExitProp.boolValue);
                    using (new EditorGUI.DisabledScope(!playExitProp.boolValue))
                    {
                        var fromCue = GetCueByNodeId(fromProp.stringValue);
                        var exitIds = (fromCue?.stingers != null)
                            ? fromCue.stingers.Select(s => s.id).ToArray()
                            : Array.Empty<string>();

                        int exIdx = Mathf.Max(0, Array.IndexOf(exitIds, exitIdProp.stringValue));
                        exIdx = EditorGUILayout.Popup("Stinger (from cue)", exIdx, exitIds);
                        exitIdProp.stringValue = exitIds.Length > 0 ? exitIds[Mathf.Clamp(exIdx, 0, exitIds.Length - 1)] : "";

                        EditorGUILayout.PropertyField(quantExitProp, new GUIContent("Quantize"));
                        exitOffsetProp.floatValue = EditorGUILayout.FloatField(new GUIContent("Offset (s)"), exitOffsetProp.floatValue);
                    }
                }

                // ENTRY (del cue TO)
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    playEntryProp.boolValue = EditorGUILayout.ToggleLeft("Entry Stinger (marca entrada del nuevo cue)", playEntryProp.boolValue);
                    using (new EditorGUI.DisabledScope(!playEntryProp.boolValue))
                    {
                        var toCue = GetCueByNodeId(toProp.stringValue);
                        var entryIds = (toCue?.stingers != null)
                            ? toCue.stingers.Select(s => s.id).ToArray()
                            : Array.Empty<string>();

                        int enIdx = Mathf.Max(0, Array.IndexOf(entryIds, entryIdProp.stringValue));
                        enIdx = EditorGUILayout.Popup("Stinger (to cue)", enIdx, entryIds);
                        entryIdProp.stringValue = entryIds.Length > 0 ? entryIds[Mathf.Clamp(enIdx, 0, entryIds.Length - 1)] : "";

                        EditorGUILayout.PropertyField(quantEntryProp, new GUIContent("Quantize"));
                        entryOffsetProp.floatValue = EditorGUILayout.FloatField(new GUIContent("Offset (s)"), entryOffsetProp.floatValue);
                    }
                }
            }
        }

        // ---------- Helpers ----------
        private string[] GetNodeIds()
        {
            var graph = (MusicGraph)target;
            return graph.nodes.Select(n => n.id ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }

        private MusicCue GetCueByNodeId(string nodeId)
        {
            var graph = (MusicGraph)target;
            var n = graph.nodes.FirstOrDefault(x => x.id == nodeId);
            return n?.cue;
        }
    }
}
#endif