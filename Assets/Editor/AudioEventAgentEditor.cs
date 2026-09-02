#if UNITY_EDITOR
using System.Linq;
using System.Collections.Generic;
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

        private Dictionary<string, bool> _scriptFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> _eventFoldouts = new Dictionary<string, bool>();

        private void OnEnable()
        {
            _defaultMixerGroup    = serializedObject.FindProperty("defaultMixerGroup");
            _sourceTemplate       = serializedObject.FindProperty("sourceTemplate");
            _globalEmitterOverride= serializedObject.FindProperty("globalEmitterOverride");
            _events               = serializedObject.FindProperty("events");

            if (_defaultMixerGroup.objectReferenceValue == null)
            {
                var guids = AssetDatabase.FindAssets("MainMixer t:AudioMixer");
                if (guids.Length > 0)
                {
                    var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    if (mixer != null)
                    {
                        var groups = mixer.FindMatchingGroups("Sound FX");
                        if (groups.Length == 0) groups = mixer.FindMatchingGroups("SFX");
                        if (groups.Length > 0)
                        {
                            _defaultMixerGroup.objectReferenceValue = groups[0];
                            serializedObject.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }

            if (_sourceTemplate.objectReferenceValue == null)
            {
                var guids = AssetDatabase.FindAssets("DefaultAudioSource t:GameObject");
                if (guids.Length > 0)
                {
                    var sourceGo = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    if (sourceGo != null)
                    {
                        _sourceTemplate.objectReferenceValue = sourceGo.GetComponent<AudioSource>();
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Configuración General", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_defaultMixerGroup, new GUIContent("Canal de Audio (Mixer)"));
            EditorGUILayout.PropertyField(_sourceTemplate, new GUIContent("Plantilla (Opcional)"));
            EditorGUILayout.PropertyField(_globalEmitterOverride, new GUIContent("Emisor Fijo para todo el agente (Opcional)"));

            var agent = (AudioEventAgent)target;

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Eventos detectados", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Añadir Evento...", GUILayout.Width(120)))
                {
                    ShowAddEventMenu(agent);
                }
            }

            if (_events is { isArray: true })
            {
                var groups = new Dictionary<MonoBehaviour, List<int>>();
                for (int i = 0; i < _events.arraySize; i++)
                {
                    var ev = _events.GetArrayElementAtIndex(i);
                    var scriptRef = ev.FindPropertyRelative("targetScript").objectReferenceValue as MonoBehaviour;
                    if (!groups.ContainsKey(scriptRef)) groups[scriptRef] = new List<int>();
                    groups[scriptRef].Add(i);
                }

                foreach (var kvp in groups)
                {
                    var script = kvp.Key;
                    string scriptName = script != null ? script.GetType().Name : "Desconocido (Falta el Script)";
                    if (!_scriptFoldouts.ContainsKey(scriptName)) _scriptFoldouts[scriptName] = true;

                    GUI.backgroundColor = new Color(0.85f, 0.9f, 1f);
                    EditorGUILayout.BeginVertical("box");
                    GUI.backgroundColor = Color.white;

                    _scriptFoldouts[scriptName] = EditorGUILayout.Foldout(_scriptFoldouts[scriptName], $"➔ Script: {scriptName}", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });

                    if (_scriptFoldouts[scriptName])
                    {
                        EditorGUI.indentLevel++;
                        foreach (int i in kvp.Value)
                        {
                            var ev = _events.GetArrayElementAtIndex(i);
                            var evtName = ev.FindPropertyRelative("eventName").stringValue;
                            var guid = ev.FindPropertyRelative("guid").stringValue;

                            if (!_eventFoldouts.ContainsKey(guid)) _eventFoldouts[guid] = false;

                            EditorGUILayout.BeginVertical("box");
                            EditorGUILayout.BeginHorizontal();
                            _eventFoldouts[guid] = EditorGUILayout.Foldout(_eventFoldouts[guid], $"Sonido para: {evtName}", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
                            
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Quitar", GUILayout.Width(60)))
                            {
                                _events.DeleteArrayElementAtIndex(i);
                                serializedObject.ApplyModifiedProperties();
                                GUIUtility.ExitGUI(); // Prevenir errores de GUI tras borrar
                            }
                            EditorGUILayout.EndHorizontal();

                            if (_eventFoldouts[guid])
                            {
                                DrawEventConfig(ev, agent);
                            }
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.Space(2);
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(6);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEventConfig(SerializedProperty ev, AudioEventAgent agent)
        {
            var guid                = ev.FindPropertyRelative("guid");
            var displayName         = ev.FindPropertyRelative("displayName");
            var eventName           = ev.FindPropertyRelative("eventName");
            var targetScript        = ev.FindPropertyRelative("targetScript");

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
                    EditorGUILayout.TextField(guid.stringValue, GUILayout.MaxWidth(240));
                isStopEvent.boolValue = GUILayout.Toggle(isStopEvent.boolValue, "Apagar Sonidos", "Button", GUILayout.Width(110));
            }

            EditorGUI.indentLevel++;

            if (!isStopEvent.boolValue)
            {
                EditorGUILayout.PropertyField(emitterOverride, new GUIContent("Emisor Específico (Opcional)"));

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Audio Espacial (3D vs 2D)", EditorStyles.boldLabel);
                
                // Mostrar Spatial Mode de forma más limpia
                var spatialNames = new[] { "Usar Configuración del Sonido original", "Forzar a que suene en 3D (Espacial)", "Forzar a que suene en 2D (Plano)" };
                int currentModeIndex = spatialMode.enumValueIndex;
                spatialMode.enumValueIndex = EditorGUILayout.Popup(new GUIContent("Modo de Audio"), currentModeIndex, spatialNames);

                var currentMode = (AudioEventAgent.SpatialMode)spatialMode.enumValueIndex;
                bool showDistance = currentMode == AudioEventAgent.SpatialMode.Force3D || 
                                    (currentMode == AudioEventAgent.SpatialMode.UseClipSettings);

                if (showDistance)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(overrideDistance, new GUIContent("Personalizar Distancia de Escucha"));
                    if (overrideDistance.boolValue)
                    {
                        EditorGUILayout.PropertyField(customMaxDistance, GUIContent.none);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Comportamiento de Reproducción", EditorStyles.boldLabel);
                randomOne.boolValue = EditorGUILayout.ToggleLeft("Elegir un sonido al azar (sin repetir el anterior)", randomOne.boolValue);
                EditorGUILayout.PropertyField(maxSimultaneous, new GUIContent("Límite de sonidos al mismo tiempo"));
                EditorGUILayout.PropertyField(coalesceWindow, new GUIContent("Agrupar sonidos rápidos (Segundos)", "Si el sonido se pide muchas veces seguidas en este tiempo, se agrupan en uno solo."));
                blockSameFrameDupes.boolValue = EditorGUILayout.ToggleLeft("Evitar que suene 2 veces en el mismo instante", blockSameFrameDupes.boolValue);

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Lista de Sonidos (Clips)", EditorStyles.boldLabel);
                DrawClipList(clips, showLoop: true);
            }
            else
            {
                var stopModeNames = new[] { "Detener sonidos de un Evento Específico", "Detener Clips Específicos", "Detener TODOS los sonidos de este objeto" };
                stopMode.enumValueIndex = EditorGUILayout.Popup(new GUIContent("¿Qué sonidos detener?"), stopMode.enumValueIndex, stopModeNames);

                if ((AudioEventAgent.StopMode)stopMode.enumValueIndex == AudioEventAgent.StopMode.ByEvent)
                {
                    var pairs = agent.EventConfigs.Select(e => (e.guid, e.displayName ?? e.eventName)).ToList();
                    var displays = pairs.Select(p => p.Item2).ToArray();
                    var keys     = pairs.Select(p => p.guid).ToArray();
                    int idx = System.Array.IndexOf(keys, stopTargetEventKey.stringValue);
                    int newIdx = EditorGUILayout.Popup(new GUIContent("Elegí el Evento a detener:"), Mathf.Max(0, idx), displays);
                    if (newIdx >= 0 && newIdx < keys.Length)
                        stopTargetEventKey.stringValue = keys[newIdx];
                }
                else if ((AudioEventAgent.StopMode)stopMode.enumValueIndex == AudioEventAgent.StopMode.ByClips)
                {
                    EditorGUILayout.HelpBox("Agregá aquí los sonidos que querés detener. Si la lista está vacía no se detendrá nada.", MessageType.Info);
                    EditorGUILayout.LabelField("Sonidos a detener", EditorStyles.boldLabel);
                    DrawClipList(clips, showLoop:false);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(fadeOutOnStop, new GUIContent("Apagar con transición suave (Fade Out)"));
                if (fadeOutOnStop.boolValue)
                    EditorGUILayout.PropertyField(fadeOutTime, new GUIContent("Duración (s)"));
                EditorGUILayout.EndHorizontal();
            }

            // Pitch
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Tono (Pitch)", EditorStyles.boldLabel);
            usePitchRandom.boolValue = EditorGUILayout.ToggleLeft("Variar el Tono aleatoriamente (Para que no suene repetitivo)", usePitchRandom.boolValue);
            using (new EditorGUI.DisabledScope(!usePitchRandom.boolValue))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    float oldLabel = EditorGUIUtility.labelWidth;
                    
                    EditorGUIUtility.labelWidth = 100;
                    EditorGUILayout.PropertyField(pitchMin, new GUIContent("Más Grave (Min)"), GUILayout.MinWidth(120));
                    
                    GUILayout.Space(10);
                    
                    EditorGUIUtility.labelWidth = 100;
                    EditorGUILayout.PropertyField(pitchMax, new GUIContent("Más Agudo (Max)"), GUILayout.MinWidth(120));
                    
                    EditorGUIUtility.labelWidth = oldLabel;
                }
                if (pitchMin.floatValue > pitchMax.floatValue)
                    pitchMax.floatValue = pitchMin.floatValue;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawClipList(SerializedProperty listProp, bool showLoop = true)
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
                float oldLabel = EditorGUIUtility.labelWidth;
                
                EditorGUIUtility.labelWidth = 55;
                EditorGUILayout.Slider(volume, 0f, 1f, new GUIContent("Volumen"), GUILayout.MinWidth(100));
                
                GUILayout.Space(10);
                
                EditorGUIUtility.labelWidth = 55;
                EditorGUILayout.PropertyField(delay, new GUIContent("Delay (s)"), GUILayout.Width(100));
                
                EditorGUIUtility.labelWidth = oldLabel;
                EditorGUILayout.EndHorizontal();

                if (showLoop)
                {
                    EditorGUILayout.PropertyField(loop, new GUIContent("Repetir en Bucle (Loop)"));
                }
                EditorGUILayout.EndVertical();
            }
            if (removeAt >= 0) listProp.DeleteArrayElementAtIndex(removeAt);
            if (GUILayout.Button("Añadir Sonido"))
            {
                listProp.InsertArrayElementAtIndex(Mathf.Max(0, listProp.arraySize));
            }
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
        private void ShowAddEventMenu(AudioEventAgent agent)
        {
            var menu = new GenericMenu();

            // Buscar en toda la jerarquía
            var scripts = agent.GetComponentsInParent<MonoBehaviour>(true).ToList();
            scripts.AddRange(agent.GetComponentsInChildren<MonoBehaviour>(true));
            scripts = scripts.Distinct().Where(s => s != null && s != agent).ToList();

            if (scripts.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No se encontraron scripts en la jerarquía"));
                menu.ShowAsContext();
                return;
            }

            bool foundAny = false;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            foreach (var script in scripts)
            {
                var type = script.GetType();
                var menuPath = $"{script.gameObject.name}/{type.Name}";

                // Buscar eventos
                foreach (var ei in type.GetEvents(flags))
                {
                    if (IsZeroParamDelegateType(ei.EventHandlerType))
                    {
                        foundAny = true;
                        string evtName = ei.Name;
                        menu.AddItem(new GUIContent($"{menuPath}/{evtName}"), false, () => AddEventToAgent(agent, script, evtName));
                    }
                }

                // Buscar campos Action/Delegate
                foreach (var fi in type.GetFields(flags))
                {
                    if (typeof(System.Delegate).IsAssignableFrom(fi.FieldType) && IsZeroParamDelegateType(fi.FieldType))
                    {
                        if (fi.Name.Contains("k__BackingField")) continue;
                        foundAny = true;
                        string evtName = fi.Name;
                        menu.AddItem(new GUIContent($"{menuPath}/{evtName}"), false, () => AddEventToAgent(agent, script, evtName));
                    }
                }
            }

            if (!foundAny)
            {
                menu.AddDisabledItem(new GUIContent("No hay eventos disponibles (sin parámetros) en la jerarquía"));
            }

            menu.ShowAsContext();
        }

        private void AddEventToAgent(AudioEventAgent agent, MonoBehaviour targetScript, string eventName)
        {
            Undo.RecordObject(agent, "Añadir Evento de Audio");

            var newConfig = new AudioEventAgent.EventConfig
            {
                targetScript = targetScript,
                eventName = eventName,
                displayName = $"{targetScript.GetType().Name}.{eventName}",
                guid = System.Guid.NewGuid().ToString()
            };

            agent.AddEventConfig(newConfig);
            EditorUtility.SetDirty(agent);
        }

        private static bool IsZeroParamDelegateType(System.Type delegateType)
        {
            if (delegateType == null) return false;
            if (!typeof(System.Delegate).IsAssignableFrom(delegateType)) return false;
            var invoke = delegateType.GetMethod("Invoke");
            return invoke != null && invoke.GetParameters().Length == 0;
        }
    }
}
#endif