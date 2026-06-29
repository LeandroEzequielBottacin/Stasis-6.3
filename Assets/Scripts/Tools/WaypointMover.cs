using UnityEngine;

namespace Tools
{
    [DisallowMultipleComponent]
    public class WaypointMoverPro : MonoBehaviour
    {
        public enum PathMode { Loop, PingPong, Once }
        public enum UpdateMode { Transform, Rigidbody }
        public enum TravelMode { BySpeed, ByDuration }
        public enum ProgressCurveMode { ManualCurve, Linear, EaseInOut, ExpoInOut, Elastic, Bounce }

        [Header("Waypoints (A -> B)")]
        public Transform pointA;
        public Transform pointB;

        [Header("Recorrido")]
        public PathMode pathMode = PathMode.Loop;
        public UpdateMode updateMode = UpdateMode.Transform;
        public TravelMode travelMode = TravelMode.BySpeed;

        [Tooltip("Si TravelMode=BySpeed: unidades/seg; si ByDuration: se ignora.")]
        public float speed = 2f;

        [Tooltip("Si TravelMode=ByDuration: segundos totales A→B; si BySpeed: se ignora.")]
        public float durationSeconds = 2f;

        [Tooltip("Multiplicador global para apurar/ralentizar TODO el recorrido.")]
        public float playbackSpeed = 1f;

        [Tooltip("Espera al llegar a A o B.")]
        public float waitAtEnds = 0.25f;

        [Header("Progreso / Easing espacial (A→B)")]
        public ProgressCurveMode progressCurveMode = ProgressCurveMode.EaseInOut;
        [Tooltip("Usada solo si ProgressCurveMode = ManualCurve.")]
        public AnimationCurve progressCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Perfil de velocidad en el tiempo (opcional)")]
        [Tooltip("Curva de multiplicador de tiempo a lo largo del ciclo 0..1 (1 = sin cambio).")]
        public AnimationCurve timeScaleCurve = new AnimationCurve(
            new Keyframe(0, 1f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 1f)
        );
        public bool useTimeScaleCurve = false;

        [Header("Offset lateral opcional")]
        [Tooltip("Amplitud del desplazamiento lateral (perpendicular A→B).")]
        public float lateralOffset = 0f;
        [Tooltip("Curva del offset a lo largo de 0..1 (0 = sin offset).")]
        public AnimationCurve lateralOffsetCurve = new AnimationCurve(
            new Keyframe(0, 0),
            new Keyframe(0.5f, 1f),
            new Keyframe(1, 0)
        );

        [Header("Inicio")]
        [Tooltip("Inicia automáticamente al habilitar.")]
        public bool playOnEnable = true;
        [Range(0, 1)] public float startT = 0f;
        [Tooltip("Dirección inicial (true = A→B).")]
        public bool startForward = true;

        [Header("Rigidbody (opcional)")]
        public Rigidbody rb;

        // Internos
        Vector3 _a, _b, _dir, _right;
        float _length;
        bool _isPlaying;
        bool _forward;
        float _t;               // progreso normalizado 0..1
        float _waitTimer;

        void Reset()
        {
            if (transform.childCount >= 2)
            {
                pointA = transform.GetChild(0);
                pointB = transform.GetChild(1);
            }
            rb = GetComponent<Rigidbody>();
        }

        void Awake()
        {
            if (rb == null && updateMode == UpdateMode.Rigidbody) rb = GetComponent<Rigidbody>();
            CachePath();
            _t = Mathf.Clamp01(startT);
            _forward = startForward;
            ApplyPosition(EvaluatePosition(_t));
        }

        void OnEnable()
        {
            if (playOnEnable) Play();
        }

        void Update()
        {
            if (updateMode == UpdateMode.Transform) Tick(Time.deltaTime);
        }

        void FixedUpdate()
        {
            if (updateMode == UpdateMode.Rigidbody) Tick(Time.fixedDeltaTime);
        }

        void Tick(float dt)
        {
            if (!_isPlaying || pointA == null || pointB == null) return;

            CachePath(); // por si movés los puntos

            // Espera en extremos
            if (_waitTimer > 0f)
            {
                _waitTimer -= dt * Mathf.Max(0.0001f, playbackSpeed);
                return;
            }

            if (_length <= 0.0001f)
            {
                // Nada que recorrer
                return;
            }

            // === Time scaling / velocidad variable ===
            // cycleT = progreso relativo del "tramo actual" (si vamos hacia atrás, invierte)
            float cycleT = _forward ? _t : (1f - _t);
            float timeScale = playbackSpeed;
            if (useTimeScaleCurve && timeScaleCurve != null)
                timeScale *= Mathf.Max(0f, timeScaleCurve.Evaluate(Mathf.Clamp01(cycleT)));

            float dir = _forward ? 1f : -1f;

            float dT;
            if (travelMode == TravelMode.BySpeed)
            {
                // Convertir velocidad lineal (u/s) a deltaT sobre 0..1
                dT = (speed / _length) * dt * timeScale * dir;
            }
            else
            {
                float dur = Mathf.Max(0.0001f, durationSeconds);
                dT = (1f / dur) * dt * timeScale * dir;
            }

            _t += dT;

            // Extremos
            if (_t >= 1f || _t <= 0f)
            {
                _t = Mathf.Clamp01(_t);

                switch (pathMode)
                {
                    case PathMode.Loop:
                        _t = 0f;
                        _forward = true;
                        _waitTimer = waitAtEnds;
                        break;

                    case PathMode.PingPong:
                        _forward = !_forward;
                        _waitTimer = waitAtEnds;
                        break;

                    case PathMode.Once:
                        _isPlaying = false;
                        break;
                }
            }

            ApplyPosition(EvaluatePosition(_t));
        }

        // === Evaluación de posición con curva de progreso + offset lateral ===
        Vector3 EvaluatePosition(float t01)
        {
            float eased = EvaluateProgress(t01);
            Vector3 basePos = Vector3.LerpUnclamped(_a, _b, eased);

            if (lateralOffset != 0f && lateralOffsetCurve != null)
            {
                float off = lateralOffset * lateralOffsetCurve.Evaluate(Mathf.Clamp01(t01));
                basePos += _right * off;
            }

            return basePos;
        }

        float EvaluateProgress(float t)
        {
            t = Mathf.Clamp01(t);
            switch (progressCurveMode)
            {
                case ProgressCurveMode.ManualCurve:
                    return progressCurve != null ? progressCurve.Evaluate(t) : t;

                case ProgressCurveMode.Linear:
                    return t;

                case ProgressCurveMode.EaseInOut:
                    // Suave clásico
                    return Mathf.SmoothStep(0f, 1f, t);

                case ProgressCurveMode.ExpoInOut:
                    // Aprox. exponencial (rápido en centro, lento en extremos)
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    if (t < 0.5f) return 0.5f * Mathf.Pow(2f, (20f * t) - 10f);
                    return 1f - 0.5f * Mathf.Pow(2f, (-20f * t) + 10f);

                case ProgressCurveMode.Elastic:
                    // Aprox. elastic easeInOut
                    if (t == 0f || t == 1f) return t;
                    t = t * 2f - 1f;
                    float s = (2f * Mathf.PI) / 3f;
                    if (t < 0) return -0.5f * Mathf.Pow(2f, 10f * t) * Mathf.Sin((t - 0.075f) * (2f * Mathf.PI) / 0.3f);
                    return 0.5f * Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;

                case ProgressCurveMode.Bounce:
                    // Bounce out-in
                    float outB = BounceOut(t);
                    float inB = 1f - BounceOut(1f - t);
                    return (t < 0.5f) ? (0.5f * inB) : (0.5f * outB + 0.5f);

                default:
                    return t;
            }
        }

        static float BounceOut(float x)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (x < 1f / d1) return n1 * x * x;
            else if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + 0.75f; }
            else if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + 0.9375f; }
            else { x -= 2.625f / d1; return n1 * x * x + 0.984375f; }
        }

        void ApplyPosition(Vector3 pos)
        {
            if (updateMode == UpdateMode.Rigidbody && rb != null)
                rb.MovePosition(pos);
            else
                transform.position = pos;
        }

        void CachePath()
        {
            if (pointA == null || pointB == null) return;
            _a = pointA.position;
            _b = pointB.position;
            _dir = (_b - _a);
            _length = _dir.magnitude;
            if (_length > 0.0001f)
            {
                _dir /= _length;
                // Un vector lateral estable: intenta usar up del mundo; si paralelos, usa forward
                Vector3 up = Vector3.up;
                _right = Vector3.Cross(up, _dir);
                if (_right.sqrMagnitude < 1e-4f)
                    _right = Vector3.Cross(Vector3.forward, _dir);
                _right.Normalize();
            }
            else
            {
                _right = Vector3.right;
            }
        }

        // Controles públicos
        public void Play()
        {
            _isPlaying = true;
            _waitTimer = 0f;
        }

        public void Pause() => _isPlaying = false;

        public void Restart(bool startAtA = true)
        {
            _isPlaying = true;
            _forward = true;
            _t = startAtA ? 0f : 1f;
            _waitTimer = 0f;
            ApplyPosition(EvaluatePosition(_t));
        }

        // Gizmos
        void OnDrawGizmos()
        {
            if (pointA != null && pointB != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pointA.position, pointB.position);
                Gizmos.DrawWireSphere(pointA.position, 0.1f);
                Gizmos.DrawWireSphere(pointB.position, 0.1f);

                // Mostrar offset lateral máximo aproximado
                if (lateralOffset != 0f)
                {
                    CachePath();
                    Vector3 mid = Vector3.Lerp(pointA.position, pointB.position, 0.5f);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(mid - _right * lateralOffset, mid + _right * lateralOffset);
                }
            }
        }
    }
}
