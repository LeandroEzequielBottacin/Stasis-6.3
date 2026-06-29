using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Audio.Scripts
{
    [DefaultExecutionOrder(-50)]
    public class AudioEventHub : MonoBehaviour
    {
        public static AudioEventHub Instance;

        private readonly Dictionary<AudioEventAgent, Dictionary<string, (MonoBehaviour script, EventInfo ei)>> _eventsByKey = new();
        private readonly Dictionary<AudioEventAgent, Dictionary<string, (MonoBehaviour script, FieldInfo fi)>> _fieldsByKey = new();
        private readonly Dictionary<string, Delegate> _handlersByKey = new(StringComparer.Ordinal);

        private readonly Dictionary<string, float> _lastPlayTimeByKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _liveVoicesByKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _lastFrameByKey = new(StringComparer.Ordinal);

        private struct PlayingInstance : IEquatable<PlayingInstance>
        {
            public AudioSource Src;
            public string Tag;
            public AudioEventAgent Agent;
            public bool Is2D;

            public bool Equals(PlayingInstance other)
            {
                return Equals(Src, other.Src) && Tag == other.Tag && Equals(Agent, other.Agent) && Is2D == other.Is2D;
            }

            public override bool Equals(object obj)
            {
                return obj is PlayingInstance other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Src, Tag, Agent, Is2D);
            }
        }

        private readonly List<PlayingInstance> _playing = new();

        private const BindingFlags ScanFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void RegisterAgent(AudioEventAgent agent)
        {
            if (!agent) return;
            UnregisterAgent(agent);

            _eventsByKey[agent] = new Dictionary<string, (MonoBehaviour, EventInfo)>(StringComparer.Ordinal);
            _fieldsByKey[agent] = new Dictionary<string, (MonoBehaviour, FieldInfo)>(StringComparer.Ordinal);

            foreach (var script in agent.TargetScripts.Where(s => s))
                ScanDelegateMembers(agent, script);

            agent.SyncEventConfigListWithReflectedMembers(GetAllKeysFor(agent));

            if (!Application.isPlaying) return;

            foreach (var kv in _eventsByKey[agent])
            {
                string key = kv.Key;
                var (script, ei) = kv.Value;
                var del = CreateZeroParamDelegateFor(ei.EventHandlerType, () => OnReflectedEvent(agent, key));
                if (del != null)
                {
                    ei.AddEventHandler(script, del);
                    _handlersByKey[key] = del;
                }
            }

            foreach (var kv in _fieldsByKey[agent])
            {
                string key = kv.Key;
                var (script, fi) = kv.Value;
                var handlerType = fi.FieldType;
                var current = (Delegate)fi.GetValue(script);
                var addDel = CreateZeroParamDelegateFor(handlerType, () => OnReflectedEvent(agent, key));
                if (addDel != null)
                {
                    var combined = Delegate.Combine(current, addDel);
                    fi.SetValue(script, combined);
                    _handlersByKey[key] = addDel;
                }
            }
        }

        public void UnregisterAgent(AudioEventAgent agent)
        {
            if (!agent) return;

            foreach (var key in GetAllKeysFor(agent))
            {
                if (_handlersByKey.TryGetValue(key, out var del))
                {
                    if (_eventsByKey.TryGetValue(agent, out var eMap) && eMap.TryGetValue(key, out var eInfo))
                    {
                        try { eInfo.ei.RemoveEventHandler(eInfo.script, del); }
                        catch
                        {
                            // ignored
                        }
                    }
                    else if (_fieldsByKey.TryGetValue(agent, out var fMap) && fMap.TryGetValue(key, out var fInfo))
                    {
                        try
                        {
                            var current = (Delegate)fInfo.fi.GetValue(fInfo.script);
                            var removed = Delegate.Remove(current, del);
                            fInfo.fi.SetValue(fInfo.script, removed);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    _handlersByKey.Remove(key);
                }
            }

            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                var p = _playing[i];
                if (p.Agent == agent)
                {
                    if (p.Src) p.Src.Stop();
                    TryReturn(p.Src, p.Is2D);
                    _playing.RemoveAt(i);
                    DecrementLive(p.Tag);
                }
            }

            _eventsByKey.Remove(agent);
            _fieldsByKey.Remove(agent);
        }

        public void RescanAgent(AudioEventAgent agent)
        {
            UnregisterAgent(agent);
            RegisterAgent(agent);
        }

        private void ScanDelegateMembers(AudioEventAgent agent, MonoBehaviour script)
        {
            var type = script.GetType();

            foreach (var ei in type.GetEvents(ScanFlags))
            {
                var handlerType = ei.EventHandlerType;
                if (!IsZeroParamDelegateType(handlerType)) continue;
                string key = MakeKey(script, ei.Name);
                _eventsByKey[agent][key] = (script, ei);
            }

            foreach (var fi in type.GetFields(ScanFlags))
            {
                var fType = fi.FieldType;
                if (!typeof(Delegate).IsAssignableFrom(fType)) continue;
                if (!IsZeroParamDelegateType(fType)) continue;
                if (fi.Name.Contains("k__BackingField")) continue;
                string key = MakeKey(script, fi.Name);
                _fieldsByKey[agent][key] = (script, fi);
            }
        }

        private static bool IsZeroParamDelegateType(Type delegateType)
        {
            if (delegateType == null) return false;
            if (!typeof(Delegate).IsAssignableFrom(delegateType)) return false;
            var invoke = delegateType.GetMethod("Invoke");
            return invoke != null && invoke.GetParameters().Length == 0;
        }

        private static Delegate CreateZeroParamDelegateFor(Type delegateType, Action handler)
        {
            if (!IsZeroParamDelegateType(delegateType)) return null;
            return Delegate.CreateDelegate(delegateType, handler.Target, handler.Method);
        }

        private IEnumerable<string> GetAllKeysFor(AudioEventAgent agent)
        {
            if (_eventsByKey.TryGetValue(agent, out var e)) foreach (var k in e.Keys) yield return k;
            if (_fieldsByKey.TryGetValue(agent, out var f)) foreach (var k in f.Keys) yield return k;
        }

        private static string MakeKey(MonoBehaviour script, string memberName)
            => $"{script.GetType().FullName}::{memberName}";

        private void OnReflectedEvent(AudioEventAgent agent, string eventKey)
        {
            if (!agent) return;

            var cfg = agent.FindConfigByKey(eventKey);
            if (cfg == null || !cfg.enabled) return;

            string key = eventKey;
            float now = Time.time;

            if (cfg.blockSameFrameDuplicates)
            {
                int frame = Time.frameCount;
                if (_lastFrameByKey.TryGetValue(key, out var lastF) && lastF == frame) return;
                _lastFrameByKey[key] = frame;
            }

            if (cfg.coalesceWindow > 0f &&
                _lastPlayTimeByKey.TryGetValue(key, out var lastT) &&
                now - lastT < cfg.coalesceWindow)
            {
                return;
            }

            if (cfg.maxSimultaneous > 0 &&
                _liveVoicesByKey.TryGetValue(key, out var live) &&
                live >= cfg.maxSimultaneous)
            {
                return;
            }

            _lastPlayTimeByKey[key] = now;

            if (cfg.isStopEvent)
            {
                HandleStop(agent, cfg);
                return;
            }

            if (cfg.clips == null || cfg.clips.Count == 0) return;

            if (cfg.randomOne && cfg.clips.Count > 1)
            {
                int idx = NextRandomIndexNoRepeat(cfg);
                var chosen = cfg.clips[idx];
                cfg.LastRandomIndex = idx;
                agent.StartCoroutine(PlayClipRoutine(agent, chosen, cfg, key));
            }
            else
            {
                foreach (var clipCfg in cfg.clips)
                    agent.StartCoroutine(PlayClipRoutine(agent, clipCfg, cfg, key));
            }
        }

        private static int NextRandomIndexNoRepeat(AudioEventAgent.EventConfig cfg)
        {
            int count = cfg.clips.Count;
            if (count <= 1) return 0;
            int last = cfg.LastRandomIndex, idx, attempts = 0;
            do { idx = Random.Range(0, count); attempts++; }
            while (idx == last && attempts < 8);
            if (idx == last) idx = (idx + 1) % count;
            return idx;
        }

        private void HandleStop(AudioEventAgent agent, AudioEventAgent.EventConfig cfg)
        {
            if (cfg.stopMode == AudioEventAgent.StopMode.ByClips && cfg.clips is { Count: > 0 })
            {
                foreach (var p in _playing.ToArray())
                {
                    if (p.Agent != agent || !p.Src) continue;
                    if (cfg.clips.Any(c => c.clip == p.Src.clip))
                    {
                        StopInstance(p, cfg.fadeOutOnStop, cfg.fadeOutTime);
                    }
                }
            }
            else if (cfg.stopMode == AudioEventAgent.StopMode.ByEvent && !string.IsNullOrEmpty(cfg.stopTargetEventKey))
            {
                foreach (var p in _playing.ToArray())
                {
                    if (p.Agent != agent) continue;
                    if (p.Tag == cfg.stopTargetEventKey)
                    {
                        StopInstance(p, cfg.fadeOutOnStop, cfg.fadeOutTime);
                    }
                }
            }
            else if (cfg.stopMode == AudioEventAgent.StopMode.All)
            {
                foreach (var p in _playing.ToArray())
                {
                    if (p.Agent != agent) continue;
                    StopInstance(p, cfg.fadeOutOnStop, cfg.fadeOutTime);
                }
            }
        }

        private void StopInstance(PlayingInstance inst, bool fade, float fadeTime)
        {
            if (!inst.Src) return;

            if (fade && inst.Src.isPlaying && fadeTime > 0f)
                StartCoroutine(FadeOutAndReturn(inst, fadeTime));
            else
            {
                inst.Src.Stop();
                TryReturn(inst.Src, inst.Is2D);
                _playing.Remove(inst);
                DecrementLive(inst.Tag);
            }
        }

        private IEnumerator FadeOutAndReturn(PlayingInstance inst, float time)
        {
            if (!inst.Src) yield break;

            float startVol = inst.Src.volume, t = 0f;
            while (t < time && inst.Src)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / time);
                inst.Src.volume = startVol * k;
                yield return null;
            }
            if (inst.Src) inst.Src.Stop();
            TryReturn(inst.Src, inst.Is2D);
            _playing.Remove(inst);
            DecrementLive(inst.Tag);
        }

        private IEnumerator PlayClipRoutine(
            AudioEventAgent agent,
            AudioEventAgent.ClipConfig clipCfg,
            AudioEventAgent.EventConfig evCfg,
            string tagKey)
        {
            if (clipCfg == null || !clipCfg.clip) yield break;
            if (clipCfg.delay > 0f)
                yield return new WaitForSeconds(clipCfg.delay);

            var factory = AudioSourceFactory.Instance;
            if (!factory) yield break;

            // Determinar emisor
            Transform emitter = evCfg.emitterOverride
                                ? evCfg.emitterOverride
                                : (agent.GlobalEmitterOverride
                                    ? agent.GlobalEmitterOverride
                                    : agent.transform);

            // Determinar si es 3D o 2D basado en la prioridad (Evento > Clip)
            bool is3D = clipCfg.use3D;
            if (evCfg.spatialMode == AudioEventAgent.SpatialMode.Force2D) is3D = false;
            if (evCfg.spatialMode == AudioEventAgent.SpatialMode.Force3D) is3D = true;

            AudioSource src;

            if (is3D)
            {
                src = factory.GetSource();
                if (!src) yield break;

                src.transform.position = emitter.position;

                if (agent.DefaultMixerGroup)
                    src.outputAudioMixerGroup = agent.DefaultMixerGroup;

                var template = agent.SourceTemplate;
                if (template)
                {
                    src.spatialBlend = template.spatialBlend;
                    src.minDistance = template.minDistance;
                    src.dopplerLevel = template.dopplerLevel;
                    src.spread = template.spread;
                    
                    if (evCfg.overrideDistance)
                    {
                        src.rolloffMode = AudioRolloffMode.Linear;
                        src.maxDistance = evCfg.customMaxDistance;
                    }
                    else
                    {
                        src.rolloffMode = template.rolloffMode;
                        src.maxDistance = template.maxDistance;
                    }
                }
                else
                {
                    src.spatialBlend = 1f;
                    if (evCfg.overrideDistance) 
                    {
                        src.rolloffMode = AudioRolloffMode.Linear;
                        src.maxDistance = evCfg.customMaxDistance;
                    }
                    else
                    {
                        src.rolloffMode = AudioRolloffMode.Logarithmic;
                        src.minDistance = 1f;
                        src.maxDistance = 500f;
                    }
                }
            }
            else
            {
                // Es 2D
                src = factory.Get2DSource(parent: null, mixerOverride: agent.DefaultMixerGroup);
                if (!src) yield break;

                // LÓGICA "Si es 2D suena desde el transform del player":
                // Movemos el AudioSource 2D a la posición del emisor (Player).
                src.transform.position = emitter.position;
            }

            src.clip = clipCfg.clip;
            src.loop = clipCfg.loop;
            src.pitch = evCfg.usePitchRandom
                ? Random.Range(evCfg.pitchMin, evCfg.pitchMax)
                : 1f;
            src.volume = Mathf.Clamp01(clipCfg.volume);

            src.Play();

            var inst = new PlayingInstance
            {
                Src = src,
                Tag = tagKey,
                Agent = agent,
                Is2D = !is3D
            };

            _playing.Add(inst);
            IncrementLive(tagKey);

            if (!src.loop)
            {
                yield return new WaitWhile(() => src && src.isPlaying);
                TryReturn(src, !is3D);
                _playing.Remove(inst);
                DecrementLive(tagKey);
            }
        }

        private static void TryReturn(AudioSource src, bool is2D)
        {
            if (!src) return;

            var factory = AudioSourceFactory.Instance;
            if (factory)
            {
                if (is2D)
                    factory.Return2DSource(src);
                else
                    factory.ReturnSource(src);
            }
            else
            {
                src.Stop();
                src.gameObject.SetActive(false);
            }
        }

        private void IncrementLive(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _liveVoicesByKey.TryGetValue(key, out var live);
            _liveVoicesByKey[key] = live + 1;
        }

        private void DecrementLive(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_liveVoicesByKey.TryGetValue(key, out var live))
            {
                live = Mathf.Max(0, live - 1);
                if (live == 0) _liveVoicesByKey.Remove(key);
                else _liveVoicesByKey[key] = live;
            }
        }

        public static IEnumerable<(MonoBehaviour script, string memberName)> EditorScanTargets(IList<MonoBehaviour> scripts)
        {
            foreach (var script in scripts.Where(s => s))
            {
                var type = script.GetType();
                foreach (var ei in type.GetEvents(ScanFlags))
                    if (IsZeroParamDelegateType(ei.EventHandlerType))
                        yield return (script, ei.Name);
                foreach (var fi in type.GetFields(ScanFlags))
                {
                    var fType = fi.FieldType;
                    if (!typeof(Delegate).IsAssignableFrom(fType)) continue;
                    if (!IsZeroParamDelegateType(fType)) continue;
                    if (fi.Name.Contains("k__BackingField")) continue;
                    yield return (script, fi.Name);
                }
            }
        }

        public static string MakeKeyForEditor(MonoBehaviour script, string memberName)
            => $"{script.GetType().FullName}::{memberName}";
    }
}
