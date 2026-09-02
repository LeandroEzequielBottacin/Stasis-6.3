using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stasis.Rendering
{
    /// <summary>
    /// Makes objects held in stasis tremble in place, like a zero point energy field.
    ///
    /// The tremble is purely visual: transforms are nudged in beginCameraRendering and put
    /// back in endCameraRendering, so nothing outside the render ever sees the offset.
    /// Physics, IK, colliders and gameplay all keep reading the object's real resting
    /// transform, which matters here because frozen objects are platforms the player
    /// stands on and arms the player rides.
    ///
    /// Registration is driven from StasisRenderingLayers.SetOutline, so every system that
    /// freezes something (StasisEffect, the IK tip controllers, the container arms, the
    /// gears) gets this without having to know about it.
    ///
    /// Drop this component on a GameObject in the scene to tune the numbers in the
    /// Inspector; if none exists, one is spawned automatically with the defaults below.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Stasis/Stasis Vibration")]
    public class StasisVibration : MonoBehaviour
    {
        [Header("Traslacion")]
        [Tooltip("Amplitud del temblor, en metros.")]
        [SerializeField, Min(0f)] private float amplitude = 0.005f;

        [Tooltip("Que tan rapido tiembla. Valores altos dan el zumbido electrico.")]
        [SerializeField, Min(0f)] private float frequency = 26f;

        [Tooltip("Capa fina de alta frecuencia encima, para que no se lea como un vaiven suave.")]
        [SerializeField, Range(0f, 1f)] private float buzz = 0.45f;

        [Header("Rotacion")]
        [Tooltip("Amplitud del temblor angular, en grados.")]
        [SerializeField, Min(0f)] private float rotationAmplitude = 0.22f;

        [Header("Sacudones")]
        [Tooltip("Cada cuantos segundos el objeto pega un tiron mas grande. 0 = nunca.")]
        [SerializeField, Min(0f)] private float joltInterval = 0.9f;

        [Tooltip("Cuanto multiplica la amplitud durante un tiron.")]
        [SerializeField, Min(1f)] private float joltScale = 2.4f;

        [Header("Escala")]
        [Tooltip("Escalar la amplitud con el tamano del objeto, para que un vagon no tiemble igual que una caja.")]
        [SerializeField] private bool scaleWithSize = true;

        [SerializeField, Min(0.01f)] private float referenceSize = 2f;
        [SerializeField] private Vector2 sizeMultiplierRange = new Vector2(0.5f, 3f);

        private static StasisVibration _instance;
        private static bool _spawning;

        private readonly HashSet<Transform> _registered = new HashSet<Transform>();
        private readonly List<Transform> _movers = new List<Transform>();
        private readonly List<Vector3> _savedPositions = new List<Vector3>();
        private readonly List<Quaternion> _savedRotations = new List<Quaternion>();
        private readonly Dictionary<Transform, Vector3> _groupOffset = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Quaternion> _groupTwist = new Dictionary<Transform, Quaternion>();

        private bool _moversDirty;
        private bool _applied;

        /// <summary>
        /// Starts or stops the tremble for one transform. Safe to call every time an
        /// object freezes or unfreezes, and safe to call redundantly.
        /// </summary>
        public static void Set(Transform target, bool vibrating)
        {
            if (target == null) return;

            if (!vibrating)
            {
                if (_instance == null) return;
                if (_instance._registered.Remove(target)) _instance._moversDirty = true;
                return;
            }

            EnsureInstance();
            if (_instance == null) return;
            if (_instance._registered.Add(target)) _instance._moversDirty = true;
        }

        private static void EnsureInstance()
        {
            if (_instance != null || _spawning || !Application.isPlaying) return;

            _instance = FindFirstObjectByType<StasisVibration>();
            if (_instance != null) return;

            _spawning = true;
            var go = new GameObject("~StasisVibration") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StasisVibration>();
            _spawning = false;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError($"{nameof(StasisVibration)}: ya hay una instancia en '{_instance.name}'. " +
                               $"Se desactiva la de '{name}' para no aplicar el temblor dos veces.");
                enabled = false;
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            Restore();
            if (_instance == this) _instance = null;
        }

        // If a camera is torn down between the two callbacks the offset would otherwise
        // survive into the next frame, where gameplay would read it as the real position.
        private void LateUpdate()
        {
            if (_applied) Restore();
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam) => Apply();

        private void OnEndCamera(ScriptableRenderContext context, Camera cam) => Restore();

        private void RebuildMovers()
        {
            _moversDirty = false;
            _movers.Clear();

            foreach (var t in _registered)
            {
                if (t == null) continue;

                // Only move the topmost registered transform of each chain: moving a
                // parent already carries its children, and offsetting both would double it.
                bool hasRegisteredAncestor = false;
                for (var p = t.parent; p != null; p = p.parent)
                {
                    if (!_registered.Contains(p)) continue;
                    hasRegisteredAncestor = true;
                    break;
                }

                if (!hasRegisteredAncestor) _movers.Add(t);
            }
        }

        private static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) - 0.5f;

        private Vector3 OffsetFor(Transform group, float time)
        {
            float seed = (group.GetInstanceID() & 0xFFFF) * 0.017f;
            float t = time * frequency;

            var slow = new Vector3(Noise(t, seed), Noise(t, seed + 13.7f), Noise(t, seed + 27.1f));

            // A second, much faster layer is what reads as an electrical buzz instead of
            // a gentle sway.
            float f = t * 3.7f;
            var fast = new Vector3(Noise(f, seed + 5.3f), Noise(f, seed + 41.9f), Noise(f, seed + 63.2f));

            var v = Vector3.Lerp(slow, fast, buzz) * 2f;

            float amp = amplitude;
            if (joltInterval > 0f)
            {
                // One short spike per interval, phase-shifted per object so they don't all
                // jolt on the same beat.
                float phase = Mathf.Repeat(time + seed, joltInterval) / joltInterval;
                float spike = Mathf.Clamp01(1f - phase * 8f);
                amp *= Mathf.Lerp(1f, joltScale, spike * spike);
            }

            return v * amp;
        }

        private float SizeMultiplier(Transform group)
        {
            if (!scaleWithSize) return 1f;

            var rend = group.GetComponentInChildren<Renderer>();
            if (rend == null) return 1f;

            float size = rend.bounds.size.magnitude;
            return Mathf.Clamp(size / referenceSize, sizeMultiplierRange.x, sizeMultiplierRange.y);
        }

        private void Apply()
        {
            if (_applied) return;
            if (_moversDirty) RebuildMovers();
            if (_movers.Count == 0) return;

            float time = Time.time;
            _groupOffset.Clear();
            _groupTwist.Clear();
            _savedPositions.Clear();
            _savedRotations.Clear();

            for (int i = 0; i < _movers.Count; i++)
            {
                var t = _movers[i];
                if (t == null)
                {
                    _savedPositions.Add(Vector3.zero);
                    _savedRotations.Add(Quaternion.identity);
                    continue;
                }

                // Everything under one object shares an offset, so the whole thing trembles
                // as one piece instead of its parts drifting apart.
                var group = t.root;
                if (!_groupOffset.TryGetValue(group, out var offset))
                {
                    offset = OffsetFor(group, time) * SizeMultiplier(group);
                    _groupOffset[group] = offset;

                    var axis = OffsetFor(group, time + 91.3f);
                    float angle = rotationAmplitude *
                                  Mathf.Sin(time * frequency * 1.3f + (group.GetInstanceID() & 0xFF) * 0.05f);
                    _groupTwist[group] = axis.sqrMagnitude > 1e-12f
                        ? Quaternion.AngleAxis(angle, axis.normalized)
                        : Quaternion.identity;
                }

                _savedPositions.Add(t.position);
                _savedRotations.Add(t.rotation);

                t.position += offset;
                if (rotationAmplitude > 0f) t.rotation = _groupTwist[group] * t.rotation;
            }

            _applied = true;
        }

        private void Restore()
        {
            if (!_applied) return;
            _applied = false;

            int count = Mathf.Min(_movers.Count, _savedPositions.Count);
            for (int i = 0; i < count; i++)
            {
                var t = _movers[i];
                if (t == null) continue;
                t.position = _savedPositions[i];
                t.rotation = _savedRotations[i];
            }
        }
    }
}
