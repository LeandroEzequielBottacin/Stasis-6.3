using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Audio.Scripts;
using UnityEngine;
using UnityEngine.Audio;

// AudioSourceFactory

namespace Audio.MusicSystem
{
    /// <summary>
    /// Orquesta cues por capas con PlayScheduled, cuantización, crossfades y stingers (salida/entrada).
    /// Usa pool 2D (AudioSourceFactory.prefab2D) para instancias musicales.
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicDirector : MonoBehaviour
    {
        public static MusicDirector Instance { get; private set; }

        [Header("Graph")] public MusicGraph graph;

        [Header("Runtime Routing")]
        public Transform audioRoot;               // contenedor para sources
        public int voicesPerLayer = 1;
        [Range(0f, 1f)] public float globalVolume = 1f;

        [Header("Debug")] public bool logTransitions;

        // --- Runtime state ---
        private string _currentNodeId;
        private MusicCue _currentCue;
        private double _cueStartDspTime;
        private readonly Dictionary<string, AudioSource> _activeLayerSources = new(); // layerId -> src
        private readonly Dictionary<string, float> _parameters = new();               // nombre->valor
        private readonly HashSet<string> _triggers = new();                           // one-shot triggers

        // Crossfade
        private readonly List<AudioSource> _fadingOut = new();

        // Exposición (debug)
        public string CurrentNodeId => _currentNodeId;
        public MusicCue CurrentCue => _currentCue;
        public IReadOnlyDictionary<string, AudioSource> ActiveLayerSources => _activeLayerSources;
        public IReadOnlyDictionary<string, float> Parameters => _parameters;

        // ---- Unity ----
        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (!audioRoot) audioRoot = transform;
        }

        private void Start()
        {
            if (!graph)
            {
                Debug.LogWarning("[MusicDirector] No graph asignado.");
                return;
            }

            var start = string.IsNullOrEmpty(graph.startNodeId)
                ? graph.nodes.FirstOrDefault()?.id
                : graph.startNodeId;

            if (string.IsNullOrEmpty(start))
            {
                Debug.LogWarning("[MusicDirector] Graph sin nodo de inicio.");
                return;
            }

            // Arranque inmediato sin stinger
            GoToNode(start, MusicGraph.Quantization.Immediate, 0f);
        }

        private void Update()
        {
            // 1) triggers
            if (_triggers.Count > 0)
            {
                foreach (var trg in _triggers.ToArray())
                    TryTransitionByTrigger(trg);
                _triggers.Clear();
            }

            // 2) parámetros
            TryTransitionByParameters();
        }

        // ---------- API pública ----------
        public void SetParameter(string name, float value) => _parameters[name] = value;
        public float GetParameter(string name, float fallback = 0f) => _parameters.TryGetValue(name, out var v) ? v : fallback;
        public void Trigger(string triggerName) => _triggers.Add(triggerName);

        /// <summary>Activa/desactiva una capa del cue actual con fade local.</summary>
        public void SetLayerEnabled(string layerId, bool enabled, float fadeSeconds = 0.5f)
        {
            if (!_currentCue) return;

            var layer = _currentCue.layers.FirstOrDefault(l => l.id == layerId);
            if (layer == null) return;

            if (enabled) EnsureLayerPlaying(layer, fadeSeconds);
            else FadeOutAndStopLayer(layerId, fadeSeconds);
        }

        /// <summary>Ajusta volumen por capa.</summary>
        public void SetLayerVolume(string layerId, float volume, float fadeSeconds = 0.2f)
        {
            if (_activeLayerSources.TryGetValue(layerId, out var src) && src)
                StartCoroutine(FadeVolume(src, Mathf.Clamp01(volume) * globalVolume, fadeSeconds));
        }

        /// <summary>
        /// Cambio manual por nodeId (mantiene compatibilidad). Usa un stinger de entrada opcional.
        /// </summary>
        public void GoToNode(string nodeId, MusicGraph.Quantization q = MusicGraph.Quantization.Bar,
                             float crossfadeSeconds = 1f, bool playStinger = false, string stingerId = null)
        {
            var node = graph.nodes.FirstOrDefault(n => n.id == nodeId);
            if (node == null || !node.cue)
            {
                Debug.LogWarning($"[MusicDirector] Node '{nodeId}' inválido.");
                return;
            }

            var nextCue = node.cue;
            var when = GetNextQuantizedDspTime(_currentCue ? _currentCue : nextCue, q);

            if (logTransitions) Debug.Log($"[MusicDirector] -> {nodeId} @ {q} (when={when:0.000})");

            if (_currentCue) CrossfadeOutCurrent(crossfadeSeconds, when);
            StartCoroutine(PlayCueScheduled(nextCue, when));
            if (playStinger && !string.IsNullOrEmpty(stingerId)) FireStinger(nextCue, stingerId, when);

            _currentNodeId = nodeId;
            _currentCue = nextCue;
            _cueStartDspTime = when;
        }

        /// <summary>
        /// Cambio usando una Transition completa (lee stingers de salida/entrada, cuantización, crossfade).
        /// </summary>
        public void GoToNode(MusicGraph.Transition t)
        {
            if (t == null) return;

            var node = graph.nodes.FirstOrDefault(n => n.id == t.toNodeId);
            if (node == null || !node.cue)
            {
                Debug.LogWarning($"[MusicDirector] Node '{t.toNodeId}' inválido.");
                return;
            }

            var nextCue = node.cue;
            var when = GetNextQuantizedDspTime(_currentCue ? _currentCue : nextCue, t.quantization);

            if (logTransitions) Debug.Log($"[MusicDirector] -> {t.toNodeId} @ {t.quantization} (when={when:0.000})");

            // EXIT stinger (del cue actual)
            if (t.playExitStinger && !string.IsNullOrEmpty(t.exitStingerId))
                FireExitStinger(t, when);

            // Crossfade alineado (fade-out termina en 'when')
            if (_currentCue) CrossfadeOutCurrent(t.crossfadeSeconds, when);

            // Programar nuevo cue
            StartCoroutine(PlayCueScheduled(nextCue, when));

            // ENTRY stinger (del cue destino)
            if (t.playEntryStinger && !string.IsNullOrEmpty(t.entryStingerId))
                FireEntryStinger(nextCue, t, when);

            _currentNodeId = t.toNodeId;
            _currentCue = nextCue;
            _cueStartDspTime = when;
        }

        // ---------- Internals: transitions ----------
        private void TryTransitionByTrigger(string triggerName)
        {
            if (!graph) return;
            foreach (var t in graph.transitions)
            {
                if (t.fromNodeId != _currentNodeId) continue;
                if (!string.IsNullOrEmpty(t.triggerName) && t.triggerName == triggerName)
                {
                    GoToNode(t);
                    return;
                }
            }
        }

        private void TryTransitionByParameters()
        {
            if (!graph) return;
            foreach (var t in graph.transitions)
            {
                if (t.fromNodeId != _currentNodeId) continue;
                if (!string.IsNullOrEmpty(t.triggerName)) continue;

                if (!string.IsNullOrEmpty(t.paramName) && _parameters.TryGetValue(t.paramName, out var val))
                {
                    if (t.Matches(val))
                    {
                        GoToNode(t);
                        return;
                    }
                }
            }
        }

        // ---------- Internals: scheduling ----------
        private IEnumerator PlayCueScheduled(MusicCue cue, double whenDsp)
        {
            foreach (var layer in cue.layers)
            {
                if (!layer.clip) continue;
                if (!layer.isOptional || layer.enabledByDefault)
                    EnsureLayerScheduled(cue, layer, whenDsp);
            }
            yield break;
        }

        private void EnsureLayerScheduled(MusicCue cue, MusicCue.Layer layer, double whenDsp)
        {
            var src = GetOrCreateMusic2DSource(layer.id, cue.outputMixer);

            // 🔎 Diagnóstico: ¿está siendo compartido?
            if (_activeLayerSources.Values.Contains(src))
                Debug.LogWarning($"[MusicDirector] Pool devolvió la MISMA instancia para layer '{layer.id}' (InstanceID={src.GetInstanceID()}). Se forzará instancia nueva.", this);

            // 🔒 Fallback: si ya está en uso por otra layer, fuerza una instancia “fresh”
            if (_activeLayerSources.Values.Contains(src))
                src = ForceFresh2DSource(cue.outputMixer, layer.id);

            // Música: prioridad alta y sin doppler
            src.priority = 0;          // evita voice stealing
            src.dopplerLevel = 0f;
            src.spatialBlend = 0f;
            src.panStereo = 0f;

            src.clip = layer.clip;
            src.volume = Mathf.Clamp01(layer.defaultVolume) * globalVolume;
            src.loop = layer.loop;
            AlignSourceSettingsForCue(src, cue);
            src.PlayScheduled(whenDsp);

            _activeLayerSources[layer.id] = src;
        }

        private AudioSource ForceFresh2DSource(AudioMixerGroup mixer, string layerId)
        {
            var factory = AudioSourceFactory.Instance;
            if (factory != null && factory.Has2DPool)
            {
                var fresh = factory.Get2DSource(audioRoot, mixer);
                fresh.name = $"Music2D_{layerId}_fresh";
                return fresh;
            }
            // Fallback no-pooleado
            var go = new GameObject($"Music2D_{layerId}_fresh");
            go.transform.SetParent(audioRoot, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.outputAudioMixerGroup = mixer;
            src.spatialBlend = 0f;
            return src;
        }

        private void EnsureLayerPlaying(MusicCue.Layer layer, float fadeSeconds)
        {
            if (!_currentCue || !layer.clip) return;

            var when = GetNextQuantizedDspTime(_currentCue, MusicGraph.Quantization.Bar);
            var src = GetOrCreateMusic2DSource(layer.id, _currentCue.outputMixer);
            AlignSourceSettingsForCue(src, _currentCue);
            src.clip = layer.clip;
            src.loop = layer.loop;
            src.volume = 0f;
            src.PlayScheduled(when);
            StartCoroutine(FadeVolume(src, layer.defaultVolume * globalVolume, fadeSeconds, when));
            _activeLayerSources[layer.id] = src;
        }

        private void CrossfadeOutCurrent(float fadeSeconds, double anchorWhen)
        {
            foreach (var kv in _activeLayerSources.ToArray())
            {
                var src = kv.Value;
                if (!src) { _activeLayerSources.Remove(kv.Key); continue; }
                _fadingOut.Add(src);
                StartCoroutine(FadeOutAndReturnToPool_Aligned(src, fadeSeconds, anchorWhen));
                _activeLayerSources.Remove(kv.Key);
            }
        }

        private IEnumerator FadeOutAndReturnToPool_Aligned(AudioSource src, float seconds, double anchorWhen)
        {
            if (!src) yield break;

            var now = AudioSettings.dspTime;
            double startAt = Math.Max(now, anchorWhen - Math.Max(0.0, seconds));
            float wait = (float)Math.Max(0, startAt - now);
            if (wait > 0) yield return new WaitForSecondsRealtime(wait);

            float startVol = src.volume;
            float t = 0f;
            float dur = Mathf.Max(0.0001f, seconds);

            while (t < dur && src)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(startVol, 0f, t / dur);
                yield return null;
            }

            if (src)
            {
                src.Stop();
                ReturnMusic2DSource(src);
            }
            _fadingOut.Remove(src);
        }

        private void FadeOutAndStopLayer(string layerId, float fadeSeconds)
        {
            if (_activeLayerSources.TryGetValue(layerId, out var src) && src)
            {
                _fadingOut.Add(src);
                StartCoroutine(FadeOutAndReturnToPool(src, fadeSeconds, AudioSettings.dspTime));
                _activeLayerSources.Remove(layerId);
            }
        }

        // ---------- Internals: sources vía POOL 2D ----------
        private AudioSource GetOrCreateMusic2DSource(string layerId, AudioMixerGroup mixer)
        {
            if (_activeLayerSources.TryGetValue(layerId, out var existing) && existing)
                return existing;

            var factory = AudioSourceFactory.Instance;
            if (!factory || !factory.Has2DPool)
            {
                Debug.LogError("[MusicDirector] No hay AudioSourceFactory con prefab2D asignado.", this);
                // Fallback de emergencia
                var go = new GameObject($"Music2D_{layerId}");
                go.transform.SetParent(audioRoot, false);
                var fallback = go.AddComponent<AudioSource>();
                fallback.playOnAwake = false;
                fallback.outputAudioMixerGroup = mixer;
                fallback.spatialBlend = 0f;
                return fallback;
            }

            var src = factory.Get2DSource(audioRoot, mixer);
            src.name = $"Music2D_{layerId}";
            return src;
        }

        private void ReturnMusic2DSource(AudioSource src)
        {
            var factory = AudioSourceFactory.Instance;
            if (factory && factory.Has2DPool) factory.Return2DSource(src);
            else if (src) Destroy(src.gameObject);
        }

        private void AlignSourceSettingsForCue(AudioSource src, MusicCue cue)
        {
            src.pitch = 1f;
            src.panStereo = 0f;
            src.spatialBlend = 0f;
        }

        // ---------- Internals: timing ----------
        private double GetNextQuantizedDspTime(MusicCue cue, MusicGraph.Quantization q)
        {
            var dspNow = AudioSettings.dspTime;
            if (q == MusicGraph.Quantization.Immediate || cue == null) return dspNow + 0.02;

            double grid = q switch
            {
                MusicGraph.Quantization.Beat     => 60.0 / cue.bpm,
                MusicGraph.Quantization.Bar      => (60.0 / cue.bpm) * cue.beatsPerBar,
                MusicGraph.Quantization.TwoBars  => (60.0 / cue.bpm) * cue.beatsPerBar * 2,
                MusicGraph.Quantization.FourBars => (60.0 / cue.bpm) * cue.beatsPerBar * 4,
                MusicGraph.Quantization.Loop     => cue.LoopDuration,
                _ => (60.0 / cue.bpm) * cue.beatsPerBar
            };

            var anchor = _currentCue ? _cueStartDspTime : dspNow;
            var t = dspNow - anchor;
            var n = Math.Ceiling(t / grid);
            return anchor + Math.Max(1, n) * grid;
        }

        // ---------- Internals: stingers ----------
        // Compatibilidad: stinger simple (entrada) por id
        private void FireStinger(MusicCue targetCue, string stingerId, double whenDsp)
        {
            var st = targetCue.stingers.FirstOrDefault(s => s.id == stingerId);
            if (st?.clip == null) return;
            FireStinger(targetCue, st, whenDsp);
        }

        // Exit (del cue actual)
        private void FireExitStinger(MusicGraph.Transition t, double whenDsp)
        {
            if (_currentCue == null) return;

            var st = _currentCue.stingers.FirstOrDefault(s => s.id == t.exitStingerId);
            if (st?.clip == null) return;

            double dspNow = AudioSettings.dspTime;
            double at = t.quantizeExitStinger ? (whenDsp + t.exitOffsetSeconds)
                                              : (dspNow + t.exitOffsetSeconds);
            at = Math.Max(dspNow + 0.02, at);

            FireStinger(_currentCue, st, at);
        }

        // Entry (del cue destino)
        private void FireEntryStinger(MusicCue targetCue, MusicGraph.Transition t, double whenDsp)
        {
            if (!targetCue) return;

            var st = targetCue.stingers.FirstOrDefault(s => s.id == t.entryStingerId);
            if (st?.clip == null) return;

            double dspNow = AudioSettings.dspTime;
            double at = t.quantizeEntryStinger ? (whenDsp + t.entryOffsetSeconds)
                                               : (dspNow + t.entryOffsetSeconds);
            at = Math.Max(dspNow + 0.02, at);

            FireStinger(targetCue, st, at);
        }

        private void FireStinger(MusicCue cue, MusicCue.Stinger st, double whenDsp)
        {
            var factory = AudioSourceFactory.Instance;
            AudioSource src;

            if (factory && factory.Has2DPool)
            {
                src = factory.Get2DSource(audioRoot, cue.outputMixer);
            }
            else
            {
                var go = new GameObject($"Stinger_{st.id}");
                go.transform.SetParent(audioRoot, false);
                src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.outputAudioMixerGroup = cue.outputMixer;
            }

            src.clip = st.clip;
            src.volume = st.volume * globalVolume;
            src.loop = false;
            src.PlayScheduled(whenDsp);

            StartCoroutine(ReturnStingerWhenDone(src, whenDsp, st.clip.length));
        }

        private IEnumerator ReturnStingerWhenDone(AudioSource src, double when, float len)
        {
            float wait = (float)Math.Max(0, when - AudioSettings.dspTime) + len + 0.05f;
            if (wait > 0) yield return new WaitForSecondsRealtime(wait);
            ReturnMusic2DSource(src);
        }

        // ---------- Coroutines: fades ----------
        private IEnumerator FadeOutAndReturnToPool(AudioSource src, float seconds, double _)
        {
            if (!src) yield break;
            float start = src.volume;
            float t = 0f;
            while (t < seconds && src)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, 0f, t / seconds);
                yield return null;
            }
            if (src)
            {
                src.Stop();
                ReturnMusic2DSource(src);
            }
            _fadingOut.Remove(src);
        }

        private IEnumerator FadeVolume(AudioSource src, float target, float seconds, double playAnchor = -1)
        {
            if (!src) yield break;

            if (playAnchor > 0)
            {
                var wait = (float)Math.Max(0, playAnchor - AudioSettings.dspTime);
                if (wait > 0) yield return new WaitForSecondsRealtime(wait);
            }

            float start = src.volume;
            float t = 0f;
            while (t < seconds && src)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            if (src) src.volume = target;
        }
    }
}
