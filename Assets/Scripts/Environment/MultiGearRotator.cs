using System;
using System.Collections.Generic;
using UnityEngine;

namespace Environment
{
    [AddComponentMenu("Gameplay/MultiGearRotator (rotación de múltiples engranajes)")]
    public class MultiGearRotator : MonoBehaviour
    {
        [Serializable]
        public enum Axis { X, Y, Z, Custom }
        [Serializable]
        public enum SpaceMode { Local, World }
        [Serializable]
        public enum TimeMode { Scaled, Unscaled }

        [Serializable]
        public class RotatorItem
        {
            [Header("Objeto a rotar / mover")]
            public Transform target;

            [Header("Eje de rotación")]
            public Axis axis = Axis.Y;
            public Vector3 customAxis = Vector3.up;

            [Header("Velocidad de rotación"), Range(-720f, 720f)]
            [Tooltip("Velocidad base en grados/segundo. En Ping-Pong se usa el valor absoluto.")]
            public float speed = 90f;

            [Header("Espacio & Tiempo")]
            public SpaceMode space = SpaceMode.Local;
            public TimeMode timeMode = TimeMode.Scaled;

            [Header("Extras")]
            [Tooltip("Randomiza el ángulo inicial entre 0 y 360 grados.")]
            public bool randomizeStartAngle = false;

            [Header("Actualización")]
            [Tooltip("Si está activo, este item se actualizará en FixedUpdate (ideal para cosas que interactúan con física). " +
                     "Si está desactivado, se actualiza en Update (ideal para engranajes puramente visuales).")]
            public bool useFixedUpdate = false;

            [Header("Rotación limitada")]
            [Tooltip("Si está activo, este item usa un ángulo máximo acumulado.")]
            public bool useAngleLimit = false;

            [Tooltip("Ángulo máximo (valor absoluto) en grados. Ej: 90 para un cuarto de vuelta.")]
            [Min(0f)] public float maxAngleAbs = 90f;

            [Tooltip("Si está activo, el engranaje oscilará entre -maxAngleAbs y +maxAngleAbs (modo ping-pong).")]
            public bool usePingPong = false;

            [Header("Easing cerca del límite (rotación)")]
            [Tooltip("Suaviza la velocidad al acercarse al ángulo máximo (aplica a modo limitado y ping-pong).")]
            public bool useEaseNearLimit = false;

            [Tooltip("Fracción del recorrido donde empieza a frenar (0.25 = último 25%).")]
            [Range(0.01f, 1f)] public float easePortion = 0.25f;

            [Tooltip("Factor mínimo de velocidad en el borde (1 = sin freno, 0.1 = muy suave al final).")]
            [Range(0.05f, 1f)] public float minEaseFactor = 0.2f;

            [Header("Movimiento (plataforma entre 2 waypoints)")]
            [Tooltip("Si está activo, el target se moverá entre waypointA y waypointB en ping-pong.")]
            public bool usePositionPingPong = false;

            public Transform waypointA;
            public Transform waypointB;

            [Tooltip("Velocidad de movimiento en unidades/segundo.")]
            [Min(0f)] public float positionSpeed = 2f;

            [Tooltip("Aplica easing al acercarse a los extremos A/B.")]
            public bool positionUseEase = true;

            [Tooltip("Fracción del trayecto donde empieza a frenar (0.25 = último 25%).")]
            [Range(0.01f, 1f)] public float positionEasePortion = 0.25f;

            [Tooltip("Factor mínimo de velocidad en los extremos (0.1 = muy suave, 1 = sin easing).")]
            [Range(0.05f, 1f)] public float positionMinEaseFactor = 0.2f;

            [NonSerialized] internal Vector3 cachedAxis = Vector3.up;
            [NonSerialized] internal bool axisValid = true;
            [NonSerialized] internal bool initialized = false;

            // Rotación limitada / ping-pong (ángulo relativo)
            [NonSerialized] internal float accumulatedAngle = 0f;

            // Ping-pong de rotación
            [NonSerialized] internal float pingPongTime = 0f;
            [NonSerialized] internal Quaternion baseRotation;

            // Movimiento entre waypoints (posición)
            [NonSerialized] internal float positionT = 0f;       // 0..1
            [NonSerialized] internal bool positionForward = true; // true: A→B, false: B→A
        }

        [Header("Lista de engranajes / plataformas a controlar")]
        public List<RotatorItem> items = new List<RotatorItem>();

        [Header("Opciones globales")]
        [Tooltip("Permite que la rotación ocurra también en modo edición (sin Play).")]
        public bool runInEditMode = false;

        [Header("Runtime Control")]
        [Tooltip("Si está activo, pausa TODAS las rotaciones y movimientos gestionados por este componente.")]
        public bool paused = false;

        private readonly HashSet<Transform> _pausedItems = new HashSet<Transform>(); // per-item
        private readonly Dictionary<Transform, int> _indexByTarget = new Dictionary<Transform, int>(128); // fast lookup

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildAxisCache();
            RebuildIndex();
        }
#endif

        private void Awake()
        {
            RebuildAxisCache();
            InitializeRandomStarts();
            RebuildIndex();
        }

        // ===================== API pública per-item =====================

        /// <summary>
        /// Pausa la rotación/movimiento de un item específico.
        /// </summary>
        public bool PauseItem(Transform t)
        {
            if (t == null) return false;
            if (!_indexByTarget.ContainsKey(t))
            {
                Debug.LogWarning($"[MultiGearRotator] PauseItem: '{t.name}' no está en items.", this);
                return false;
            }
            _pausedItems.Add(t);
            return true;
        }

        /// <summary>
        /// Reanuda la rotación/movimiento de un item específico.
        /// </summary>
        public bool ResumeItem(Transform t)
        {
            if (t == null) return false;
            return _pausedItems.Remove(t);
        }

        /// <summary>
        /// Resetea el ángulo relativo de un engranaje (útil para modo limitado/ping-pong de rotación).
        /// No toca la posición entre waypoints.
        /// </summary>
        public bool ResetItemAngle(Transform t)
        {
            if (t == null) return false;
            if (!_indexByTarget.TryGetValue(t, out int index)) return false;

            var it = items[index];
            if (it == null || it.target == null) return false;

            it.accumulatedAngle = 0f;
            it.pingPongTime = 0f;

            if (it.space == SpaceMode.Local)
                it.target.localRotation = it.baseRotation;
            else
                it.target.rotation = it.baseRotation;

            return true;
        }

        public void PauseAll() => paused = true;
        public void ResumeAll() => paused = false;

        // ===================== Ciclos de actualización =====================

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !runInEditMode) return;
#endif
            if (paused) return;

            RotateAndMoveItems(isFixedStep: false);
        }

        private void FixedUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !runInEditMode) return;
#endif
            if (paused) return;

            RotateAndMoveItems(isFixedStep: true);
        }

        /// <summary>
        /// Aplica rotación y movimiento a todos los items que correspondan a este tipo de paso.
        /// </summary>
        private void RotateAndMoveItems(bool isFixedStep)
        {
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var it = items[i];
                if (it == null || it.target == null) continue;
                if (_pausedItems.Contains(it.target)) continue;
                if (!it.axisValid && !it.usePositionPingPong) continue; // si no tiene eje válido y tampoco usa movimiento, skip

                if (it.useFixedUpdate != isFixedStep) continue;

                // Delta de tiempo según modo
                float dt;
                if (it.timeMode == TimeMode.Scaled)
                    dt = isFixedStep ? Time.fixedDeltaTime : Time.deltaTime;
                else
                    dt = isFixedStep ? Time.fixedUnscaledDeltaTime : Time.unscaledDeltaTime;

                // --- ROTACIÓN ---
                if (it.axisValid && Mathf.Abs(it.speed) > 0.0001f)
                {
                    ApplyRotation(it, dt);
                }

                // --- MOVIMIENTO ENTRE WAYPOINTS ---
                if (it.usePositionPingPong && it.waypointA != null && it.waypointB != null && it.positionSpeed > 0f)
                {
                    ApplyPositionPingPong(it, dt);
                }
            }
        }

        // ===================== Lógica de Rotación =====================

        private void ApplyRotation(RotatorItem it, float dt)
        {
            // ----- MODO PING-PONG DE ROTACIÓN -----
            if (it.useAngleLimit && it.usePingPong && it.maxAngleAbs > 0f)
            {
                it.pingPongTime += dt;

                float absSpeed = Mathf.Abs(it.speed);
                float range = it.maxAngleAbs * 2f;

                float raw = Mathf.PingPong(it.pingPongTime * absSpeed, range);  // 0..2*max
                float norm = (range > 0f) ? (raw / range) : 0f;                  // 0..1

                float easedNorm = it.useEaseNearLimit
                    ? Mathf.SmoothStep(0f, 1f, norm)
                    : norm;

                float targetAngle = Mathf.Lerp(-it.maxAngleAbs, it.maxAngleAbs, easedNorm);

                it.accumulatedAngle = targetAngle;

                Quaternion rotOffset = Quaternion.AngleAxis(targetAngle, it.cachedAxis);

                if (it.space == SpaceMode.Local)
                    it.target.localRotation = it.baseRotation * rotOffset;
                else
                    it.target.rotation = it.baseRotation * rotOffset;

                return;
            }

            // ----- RESTO DE MODOS (LIBRE / LIMITADO UNA VEZ) -----
            float angleStep = it.speed * dt;

            // Modo libre (sin límite)
            if (!it.useAngleLimit || it.maxAngleAbs <= 0f)
            {
                if (Mathf.Approximately(angleStep, 0f)) return;

                if (it.space == SpaceMode.Local)
                    it.target.Rotate(it.cachedAxis, angleStep, Space.Self);
                else
                    it.target.Rotate(it.cachedAxis, angleStep, Space.World);

                return;
            }

            // Modo limitado una sola vez
            float usedAbs = Mathf.Abs(it.accumulatedAngle);
            float remainingAbs = it.maxAngleAbs - usedAbs;
            if (remainingAbs <= 0f) return;

            float stepAbs = Mathf.Abs(angleStep);

            // Easing cerca del límite
            if (it.useEaseNearLimit && it.maxAngleAbs > 0f)
            {
                float progress = usedAbs / it.maxAngleAbs;      // 0..1
                float easeStart = 1f - it.easePortion;          // p.ej. 0.75

                float tEase = Mathf.InverseLerp(easeStart, 1f, progress);
                float easeFactor = 1f;

                if (tEase > 0f)
                {
                    float smooth = Mathf.SmoothStep(0f, 1f, tEase);
                    easeFactor = Mathf.Lerp(1f, it.minEaseFactor, smooth);
                }

                stepAbs *= easeFactor;
                angleStep = Mathf.Sign(angleStep) * stepAbs;
            }

            // No pasar el límite en este frame
            stepAbs = Mathf.Abs(angleStep);
            if (stepAbs > remainingAbs)
                angleStep = Mathf.Sign(angleStep) * remainingAbs;

            if (Mathf.Approximately(angleStep, 0f)) return;

            if (it.space == SpaceMode.Local)
                it.target.Rotate(it.cachedAxis, angleStep, Space.Self);
            else
                it.target.Rotate(it.cachedAxis, angleStep, Space.World);

            it.accumulatedAngle += angleStep;
        }

        // ===================== Lógica de Movimiento entre Waypoints =====================

        private void ApplyPositionPingPong(RotatorItem it, float dt)
        {
            Vector3 a = it.waypointA.position;
            Vector3 b = it.waypointB.position;
            float dist = Vector3.Distance(a, b);
            if (dist < 1e-4f) return;

            // Paso base en términos de t (0..1)
            float baseStep = (it.positionSpeed * dt) / dist;
            if (baseStep <= 0f) return;

            // Easing cerca de los extremos
            if (it.positionUseEase)
            {
                float edgeProgress = it.positionForward ? it.positionT : 1f - it.positionT; // qué tan cerca del extremo hacia el que va
                float easeStart = 1f - it.positionEasePortion;                              // p.ej. 0.75

                float tEase = Mathf.InverseLerp(easeStart, 1f, edgeProgress);               // 0..1
                if (tEase > 0f)
                {
                    float smooth = Mathf.SmoothStep(0f, 1f, tEase);
                    float easeFactor = Mathf.Lerp(1f, it.positionMinEaseFactor, smooth);
                    baseStep *= easeFactor;
                }
            }

            // Actualizamos T según la dirección
            if (it.positionForward)
                it.positionT += baseStep;
            else
                it.positionT -= baseStep;

            // Manejo de límites y cambio de dirección (ping-pong)
            if (it.positionT >= 1f)
            {
                it.positionT = 1f;
                it.positionForward = false;
            }
            else if (it.positionT <= 0f)
            {
                it.positionT = 0f;
                it.positionForward = true;
            }

            // Aplicamos posición interpolada
            it.target.position = Vector3.Lerp(a, b, it.positionT);
        }

        // ===================== Helpers =====================

        private void RebuildAxisCache()
        {
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var it = items[i];
                if (it == null) continue;

                Vector3 axisVec = it.axis switch
                {
                    Axis.X => Vector3.right,
                    Axis.Y => Vector3.up,
                    Axis.Z => Vector3.forward,
                    Axis.Custom => it.customAxis,
                    _ => Vector3.up
                };

                if (axisVec.magnitude > 1e-5f)
                {
                    it.cachedAxis = axisVec.normalized;
                    it.axisValid = true;
                }
                else
                {
                    it.cachedAxis = Vector3.up;
                    it.axisValid = false;
                }
            }
        }

        private void InitializeRandomStarts()
        {
            var count = items?.Count ?? 0;
            for (var i = 0; i < count; i++)
            {
                var it = items?[i];
                if (it == null || it.target == null) continue;
                if (it.initialized) continue;

                // Rotación: reset de ángulos relativos
                it.accumulatedAngle = 0f;
                it.pingPongTime = 0f;

                // Random start de rotación si corresponde
                if (it.randomizeStartAngle && it.axisValid)
                {
                    float startAngle = UnityEngine.Random.Range(0f, 360f);
                    if (it.space == SpaceMode.Local)
                        it.target.Rotate(it.cachedAxis, startAngle, Space.Self);
                    else
                        it.target.Rotate(it.cachedAxis, startAngle, Space.World);
                }

                // Guardamos la rotación base
                it.baseRotation = it.space == SpaceMode.Local ? it.target.localRotation : it.target.rotation;

                // Movimiento: estado inicial
                it.positionT = 0f;
                it.positionForward = true;

                it.initialized = true;
            }
        }

        private void RebuildIndex()
        {
            _indexByTarget.Clear();
            var count = items?.Count ?? 0;
            for (var i = 0; i < count; i++)
            {
                if (items == null) continue;
                var it = items[i];
                if (it?.target == null) continue;

                _indexByTarget.TryAdd(it.target, i);
            }
        }
    }
}
