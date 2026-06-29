using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio.Scripts
{
    [DisallowMultipleComponent]
    public class AudioEventAgent : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("Componentes a escanear para detectar eventos/campos delegado sin parámetros.")]
        [SerializeField] private List<MonoBehaviour> targetScripts = new();

        [Header("Mixer / Defaults")]
        [SerializeField] private AudioMixerGroup defaultMixerGroup;

        [Tooltip("Opcional: template de AudioSource para aplicar a las instancias (distancias, rolloff, etc.).")]
        [SerializeField] private AudioSource sourceTemplate;

        [Header("Emitters")]
        [Tooltip("Override general para la posición de emisión. Si está vacío, se usa este transform.")]
        [SerializeField] private Transform globalEmitterOverride;

        [Header("Inspector")]
        [SerializeField] private List<EventConfig> events = new();

        [Header("Debug")]
        [SerializeField] private bool debugScan;

        // API para el Hub
        public IList<MonoBehaviour> TargetScripts => targetScripts;
        public AudioMixerGroup DefaultMixerGroup => defaultMixerGroup;
        public AudioSource SourceTemplate => sourceTemplate;
        public Transform GlobalEmitterOverride => globalEmitterOverride;
        public IReadOnlyList<EventConfig> EventConfigs => events;

        private void OnEnable()
        {
            AudioEventHub.Instance?.RegisterAgent(this);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SyncEventConfigListWithReflectedMembers(EditorDetectedKeys());
            }
#endif
        }

        private void OnDisable()
        {
            AudioEventHub.Instance?.UnregisterAgent(this);
        }

        public EventConfig FindConfigByKey(string eventKey)
        {
            return events.FirstOrDefault(e => e.eventKey == eventKey);
        }
        
        public void SyncEventConfigListWithReflectedMembers(IEnumerable<string> detectedKeys)
        {
            var detectedKeyList = detectedKeys
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct()
                .ToList();

            var remainingExisting = new List<EventConfig>(events);
            var newEventList = new List<EventConfig>(detectedKeyList.Count);

            foreach (var key in detectedKeyList)
            {
                var parts = key.Split(new[] { "::" }, StringSplitOptions.None);
                var member = parts.Length > 1 ? parts[1] : key;

                string display = member;
                if (parts.Length > 1)
                {
                    var typeName = parts[0].Split('.').LastOrDefault() ?? parts[0];
                    display = $"{typeName}.{member}";
                }

                EventConfig cfg = null;

                if (!string.IsNullOrEmpty(key))
                {
                    cfg = remainingExisting.FirstOrDefault(c => c.eventKey == key);
                }

                if (cfg == null)
                {
                    cfg = remainingExisting.FirstOrDefault(c =>
                        (!string.IsNullOrEmpty(c.eventName) && c.eventName == member) ||
                        (!string.IsNullOrEmpty(c.displayName) && c.displayName == display));
                }

                if (cfg == null)
                {
                    cfg = new EventConfig();
                }
                else
                {
                    remainingExisting.Remove(cfg);
                }

                cfg.eventKey = key;
                cfg.displayName = display;
                cfg.eventName = member;

                newEventList.Add(cfg);
            }
            
            foreach (var leftover in remainingExisting)
            {
                leftover.enabled = false;
                newEventList.Add(leftover);
            }
            events.Clear();
            events.AddRange(newEventList);
        }

#if UNITY_EDITOR
        private IEnumerable<string> EditorDetectedKeys()
        {
            foreach (var (script, member) in AudioEventHub.EditorScanTargets(targetScripts))
            {
                yield return AudioEventHub.MakeKeyForEditor(script, member);
            }
        }
#endif

        [Serializable]
        public class EventConfig
        {
            [HideInInspector] public string guid = Guid.NewGuid().ToString();

            [HideInInspector] public string eventKey;
            [HideInInspector] public string displayName;
            public string eventName;

            [Tooltip("Habilitar/Deshabilitar este evento.")]
            public bool enabled = true;

            [Header("Play")]
            [Tooltip("Si está marcado, este evento sirve para detener otros sonidos (no reproduce clips).")]
            public bool isStopEvent;
            [Tooltip("Si hay múltiples clips, elige uno al azar.")]
            public bool randomOne;
            public List<ClipConfig> clips = new();
            
            [Header("Spatial Settings")]
            [Tooltip("Define si forzamos 2D o 3D para todo el evento, ignorando la config individual de los clips.")]
            public SpatialMode spatialMode = SpatialMode.UseClipSettings;

            [Tooltip("Si es true, sobrescribe la Max Distance del AudioSource Template.")]
            public bool overrideDistance;
            [Min(0f)] public float customMaxDistance = 15f;

            [Header("Emitter Override (opcional)")]
            public Transform emitterOverride;

            [Header("Pitch")]
            public bool usePitchRandom = true;
            [Min(0.1f)] public float pitchMin = 1f;
            [Min(0.1f)] public float pitchMax = 1f;

            [Header("Stop")]
            public StopMode stopMode = StopMode.ByClips;

            [Tooltip("Si StopMode = ByEvent, apunta a la 'eventKey' del evento a detener.")]
            public string stopTargetEventKey;
            public bool fadeOutOnStop = true;
            [Min(0f)] public float fadeOutTime = 0.1f;

            [Header("Voice Limiter")]
            [Tooltip("Máximo de instancias simultáneas de este evento (0 = sin límite).")]
            public int maxSimultaneous = 1;

            [Tooltip("Si otro trigger del mismo evento llega dentro de esta ventana (segundos), se ignora.")]
            [Min(0f)] public float coalesceWindow = 0.05f;

            [Tooltip("Bloquear duplicados en el mismo frame (útil con blends de animaciones).")]
            public bool blockSameFrameDuplicates = true;

            [NonSerialized] public int LastRandomIndex = -1;
        }

        public enum StopMode
        {
            ByClips,
            ByEvent,
            All
        }
        
        public enum SpatialMode
        {
            UseClipSettings,
            Force2D,
            Force3D
        }

        [Serializable]
        public class ClipConfig
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Min(0f)] public float delay;
            public bool loop;

            [Header("Spatialization")]
            [Tooltip("Si está activado, este clip se reproduce en 3D (pool 3D). Si está desactivado, se reproduce en 2D (pool 2D).")]
            public bool use3D = true;
        }
    }
}
