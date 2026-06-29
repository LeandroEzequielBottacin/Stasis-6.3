using System.Collections;
using System.Collections.Generic;
using Puzzle_Elements.Hedro_conteiner.Scripts;
using UnityEngine;
using UnityEngine.Events;

namespace Environment.Platforms
{
    /// <summary>
    /// Sistema de plataforma + cartel reutilizable.
    /// - Escucha a un HedronContainerIn (opcional) para abrir/cerrar automáticamente.
    /// - Recorre un path simple de waypoints con curvas configurables.
    /// - Muestra cartel "Open"/"Close" y puede cambiar a mitad de camino.
    /// - Permite invertir destino en caliente (RequestOpen/RequestClose/Toggle).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlatformSignSystem : MonoBehaviour
    {
        // ==================== CORE REFERENCES ====================
        [Header("=== Core References ===")]
        [Tooltip("Opcional. Si se asigna, onHedronPlaced cierra; onHedronRemoved abre.")]
        public HedronContainerIn hedronContainer;

        [Tooltip("Transform de la plataforma a animar (si se deja null usa este GameObject).")]
        public Transform platform;

        [Tooltip("Waypoints en orden. Con 2 puntos hace ida/alta; con más, interpolación por tramos.")]
        public List<Transform> waypoints = new List<Transform>();

        [Header("=== Sign / Cartel ===")]
        [Tooltip("Malla/GO para el letrero Open.")]
        public GameObject openSign;
        [Tooltip("Malla/GO para el letrero Close.")]
        public GameObject closeSign;
        [Tooltip("Mostrar/Ocultar cartel durante el movimiento.")]
        public bool showSignDuringMove = true;
        [Tooltip("Cambiar el cartel automáticamente cuando la progresión supera el 50%.")]
        public bool swapSignAtHalf = true;

        // ==================== ANIMATION SETTINGS ====================
        public enum PathMode { Loop, PingPong, Ends } // Ends: extremo 0 = Close, extremo 1 = Open
        [Header("=== Movement & Curves ===")]
        public PathMode pathMode = PathMode.Ends;

        [Tooltip("Duración total (en segundos) para recorrer el path completo (0→1).")]
        [Min(0.05f)] public float duration = 2.5f;

        [Tooltip("Curva de progresión de posición (easing).")]
        public AnimationCurve positionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Curva para rotación (si los wps tienen rotación significativa).")]
        public AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Curva para escala (opcional). Si no se usa, dejar en lineal 0→1 y baseScale=Vector3.one.")]
        public AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Escala a aplicar a lo largo del path (lerp entre baseScale y targetScale).")]
        public Vector3 targetScale = Vector3.one;

        [Header("=== Misc Options ===")]
        [Tooltip("Si está activo, al cambiar de objetivo en caliente, suaviza el viraje sin 'saltos'.")]
        public bool smoothRetarget = true;
        [Tooltip("Blend temporal al retargetear (segundos).")]
        [Min(0f)] public float retargetBlendSeconds = 0.15f;

        [Tooltip("Activar debug gizmos y fracción actual.")]
        public bool debugGizmos = true;
        [Range(0f, 1f)] public float debugT; // sólo lectura visual

        // ==================== EVENTS ====================
        [Header("=== Events ===")]
        public UnityEvent onMoveStart;
        public UnityEvent onReachOpenEnd;
        public UnityEvent onReachCloseEnd;
        public UnityEvent onMoveInterrupted;

        // ==================== RUNTIME STATE ====================
        private Coroutine _moveRoutine;
        private float _t;                 // progreso 0..1 a lo largo del path
        private bool _moving;
        private bool _targetIsOpen;       // destino actual
        private Vector3 _baseScale = Vector3.one;
        private Transform _plat;

        private void Reset()
        {
            var tr = transform;
            waypoints = new List<Transform>();
            waypoints.Add(tr);
        }

        private void Awake()
        {
            _plat = platform != null ? platform : transform;
            _baseScale = _plat.localScale;
        }

        private void OnEnable()
        {
            // Suscribirse a HedronContainerIn si está asignado
            if (hedronContainer != null)
            {
                hedronContainer.onHedronPlaced.AddListener(RequestClose);   // Hedro colocado => cerrar
                hedronContainer.onHedronRemoved.AddListener(RequestOpen);   // Hedro retirado => abrir
            }
            UpdateSignInstant();
        }

        private void OnDisable()
        {
            if (hedronContainer != null)
            {
                hedronContainer.onHedronPlaced.RemoveListener(RequestClose);
                hedronContainer.onHedronRemoved.RemoveListener(RequestOpen);
            }
        }

        // ==================== PUBLIC API ====================
        /// <summary>Ordena abrir (ir hacia t=1).</summary>
        public void RequestOpen()
        {
            SetTarget(true);
        }

        /// <summary>Ordena cerrar (ir hacia t=0).</summary>
        public void RequestClose()
        {
            SetTarget(false);
        }

        /// <summary>Alterna destino de forma inmediata.</summary>
        public void Toggle()
        {
            SetTarget(!_targetIsOpen);
        }

        // ==================== INTERNAL FLOW ====================
        private void SetTarget(bool wantOpen)
        {
            _targetIsOpen = wantOpen;
            // Si ya estamos moviéndonos, reencolar con blend suave.
            if (_moveRoutine != null)
            {
                if (smoothRetarget)
                {
                    // Deja la rutina actual continuar pero cambiamos destino;
                    // el bucle lo detecta y reorienta la marcha sin cortes.
                    return;
                }
                // Si no queremos blending: cortar y relanzar.
                StopCoroutine(_moveRoutine);
                onMoveInterrupted?.Invoke();
                _moveRoutine = null;
            }
            _moveRoutine = StartCoroutine(MoveRoutine());
        }

        private IEnumerator MoveRoutine()
        {
            if (!ValidatePath()) yield break;

            _moving = true;
            onMoveStart?.Invoke();

            // Definir inicio y fin deseado
            float startT = _t;
            float endT = _targetIsOpen ? 1f : 0f;

            // Asegurar cartel visible/oculto
            ApplySignVisibility(showSignDuringMove);
            ApplySignByDirection(startT, endT); // set inicial

            float elapsed = 0f;
            float total = Mathf.Max(0.05f, duration) * Mathf.Abs(endT - startT);

            // Pequeño guard para total≈0
            if (total <= 0.0001f)
            {
                _t = endT;
                EvaluateAndApply(_t, 1f, 1f);
                CompleteByEnd();
                yield break;
            }

            // Variables para retarget suave
            bool lastTargetIsOpen = _targetIsOpen;

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                float lin = Mathf.Clamp01(elapsed / total);

                // ¿Hubo cambio de objetivo?
                if (smoothRetarget && lastTargetIsOpen != _targetIsOpen)
                {
                    // Relanzar una transición corta desde _t actual hacia el nuevo destino.
                    float retStart = _t;
                    float retEnd = _targetIsOpen ? 1f : 0f;
                    float retElapsed = 0f;
                    float retDur = Mathf.Max(0.01f, retargetBlendSeconds);

                    ApplySignByDirection(retStart, retEnd);

                    while (retElapsed < retDur)
                    {
                        retElapsed += Time.deltaTime;
                        float rr = Mathf.Clamp01(retElapsed / retDur);
                        float eased = positionCurve.Evaluate(rr);
                        _t = Mathf.Lerp(retStart, retEnd, eased);
                        EvaluateAndApply(_t, rotationCurve.Evaluate(_t), scaleCurve.Evaluate(_t));
                        UpdateHalfSwap();
                        yield return null;
                    }

                    lastTargetIsOpen = _targetIsOpen;
                    // Recalcular nuevo tramo restante desde _t actual hasta el target actual
                    startT = _t;
                    endT = _targetIsOpen ? 1f : 0f;
                    elapsed = 0f;
                    total = Mathf.Max(0.05f, duration) * Mathf.Abs(endT - startT);
                    continue;
                }

                float easedPos = positionCurve.Evaluate(lin);
                _t = Mathf.Lerp(startT, endT, easedPos);
                EvaluateAndApply(_t, rotationCurve.Evaluate(lin), scaleCurve.Evaluate(lin));
                UpdateHalfSwap();
                yield return null;
            }

            _t = endT;
            EvaluateAndApply(_t, 1f, 1f);
            CompleteByEnd();
        }

        private void CompleteByEnd()
        {
            ApplySignVisibility(true); // dejar cartel visible al final (o cámbialo si lo querés ocultar)
            ForceSignByState();

            _moving = false;
            if (_targetIsOpen) onReachOpenEnd?.Invoke();
            else onReachCloseEnd?.Invoke();
        }

        // ==================== EVALUATION ====================
        private void EvaluateAndApply(float t, float rotAlpha, float scaleAlpha)
        {
            debugT = t;
            Vector3 pos; Quaternion rot;
            EvaluatePath(waypoints, t, out pos, out rot);

            _plat.position = pos;
            _plat.rotation = Quaternion.SlerpUnclamped(waypoints[0].rotation, waypoints[^1].rotation, rotAlpha);

            // Escala opcional
            Vector3 s = Vector3.LerpUnclamped(_baseScale, targetScale, scaleAlpha);
            _plat.localScale = s;
        }

        /// <summary>
        /// Interpola sobre una polilínea de waypoints. t=0 es wps[0], t=1 es wps[last].
        /// </summary>
        private static void EvaluatePath(List<Transform> wps, float t, out Vector3 pos, out Quaternion rot)
        {
            t = Mathf.Clamp01(t);
            int count = wps.Count;
            if (count == 1)
            {
                pos = wps[0].position;
                rot = wps[0].rotation;
                return;
            }

            // Construir distancias acumuladas para mapear t→tramo
            float totalDist = 0f;
            float[] dists = new float[count];
            dists[0] = 0f;
            for (int i = 1; i < count; i++)
            {
                totalDist += Vector3.Distance(wps[i - 1].position, wps[i].position);
                dists[i] = totalDist;
            }

            float targetDist = t * totalDist;

            // Localizar tramo
            int seg = 0;
            for (int i = 1; i < count; i++)
            {
                if (targetDist <= dists[i]) { seg = i - 1; break; }
            }

            float segLength = Mathf.Max(0.0001f, dists[seg + 1] - dists[seg]);
            float segT = (targetDist - dists[seg]) / segLength;

            Transform a = wps[seg];
            Transform b = wps[seg + 1];

            pos = Vector3.LerpUnclamped(a.position, b.position, segT);
            rot = Quaternion.SlerpUnclamped(a.rotation, b.rotation, segT);
        }

        // ==================== SIGN HANDLING ====================
        private void ApplySignVisibility(bool visible)
        {
            if (openSign) openSign.SetActive(visible && _targetIsOpen);
            if (closeSign) closeSign.SetActive(visible && !_targetIsOpen);
        }

        private void ApplySignByDirection(float startT, float endT)
        {
            // Al inicializar un tramo, mostrar el cartel asociado al destino
            bool goingOpen = endT > startT;
            if (openSign) openSign.SetActive(goingOpen && showSignDuringMove);
            if (closeSign) closeSign.SetActive(!goingOpen && showSignDuringMove);
        }

        private void UpdateHalfSwap()
        {
            if (!swapSignAtHalf) return;
            // Cuando cruzamos la mitad del recorrido global, invertimos cartel (solo si está visible por movimiento)
            if (!showSignDuringMove) return;

            if (_t >= 0.5f)
            {
                if (openSign && closeSign)
                {
                    // En la segunda mitad forzamos el cartel del "estado de llegada"
                    openSign.SetActive(_targetIsOpen);
                    closeSign.SetActive(!_targetIsOpen);
                }
            }
        }

        private void ForceSignByState()
        {
            if (openSign) openSign.SetActive(_targetIsOpen);
            if (closeSign) closeSign.SetActive(!_targetIsOpen);
        }

        private void UpdateSignInstant()
        {
            // En reposo (sin moverse) mostrar el estado actual
            ApplySignVisibility(true);
            ForceSignByState();
        }

        // ==================== VALIDATION ====================
        private bool ValidatePath()
        {
            if (_plat == null) _plat = platform != null ? platform : transform;

            if (waypoints == null || waypoints.Count < 2)
            {
                Debug.LogWarning($"[{name}] PlatformSignSystem: Se necesitan al menos 2 waypoints.");
                return false;
            }

            // Clampear t si por alguna razón quedó fuera de rango:
            _t = Mathf.Clamp01(_t);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!debugGizmos || waypoints == null || waypoints.Count < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 1; i < waypoints.Count; i++)
            {
                if (waypoints[i - 1] && waypoints[i])
                    Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
            }

            // Marcador del t actual (en editor)
            if (Application.isPlaying)
            {
                EvaluatePath(waypoints, debugT, out var p, out _);
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(p, 0.05f);
            }
        }
#endif
    }
}
