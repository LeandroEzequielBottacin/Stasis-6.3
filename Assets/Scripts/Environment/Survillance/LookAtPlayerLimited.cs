using UnityEngine;

namespace Environment.Survillance
{
    /// Sigue a un target con límites de yaw/pitch relativos a la pose neutra del pivot.
    /// Ahora permite elegir cuál eje local del pivot es el "frente" visual (±X, ±Y, ±Z).
    [ExecuteAlways]
    public class LookAtPlayerLimited : MonoBehaviour
    {
        public enum AimAxis { PlusZ, MinusZ, PlusX, MinusX, PlusY, MinusY }

        [Header("Refs")]
        public Transform pivot;            // Único pivot que rota en X/Y
        public Transform target;           
        public string targetTag = "Player";

        [Header("Velocidad y suavizado")]
        public float yawSpeedDeg = 180f;
        public float pitchSpeedDeg = 180f;
        [Range(0f, 2f)] public float deadZoneDeg = 0.25f;

        [Header("Límites relativos al neutro")]
        public float yawLeftLimit  = -80f;
        public float yawRightLimit =  80f;
        public float pitchDownLimit = -25f;
        public float pitchUpLimit   =  25f;

        [Header("Orientación")]
        [Tooltip("Qué eje LOCAL del pivot apunta visualmente hacia el frente del modelo.")]
        public AimAxis forwardLocalAxis = AimAxis.PlusZ;
        [Tooltip("Offset fino adicional en yaw (grados). Usá 0 si elegís bien el eje.")]
        public float extraYawOffsetDeg = 0f;
        [Tooltip("Offset fino adicional en pitch (grados).")]
        public float extraPitchOffsetDeg = 0f;
        public bool lockRoll = true;

        [Header("Debug")]
        public bool drawAxes = false;

        // --- internos ---
        Quaternion _neutralLocalRot, _neutralWithOffset;
        bool _cached;

        void Reset()
        {
            if (!pivot) pivot = transform;
        }

        void OnEnable()
        {
            CacheNeutral();
        }

        void OnValidate()
        {
            if (yawLeftLimit > 0f)  yawLeftLimit  = -Mathf.Abs(yawLeftLimit);
            if (yawRightLimit < 0f) yawRightLimit =  Mathf.Abs(yawRightLimit);
            if (pitchDownLimit > 0f) pitchDownLimit = -Mathf.Abs(pitchDownLimit);
            if (pitchUpLimit   < 0f) pitchUpLimit   =  Mathf.Abs(pitchUpLimit);
        }

        void LateUpdate()
        {
            if (!pivot) return;
            if (!_cached) CacheNeutral();

            if (!target)
            {
                var go = GameObject.FindGameObjectWithTag(targetTag);
                if (go) target = go.transform;
                if (!target) return;
            }

            Vector3 dirWorld = target.position - pivot.position;
            if (dirWorld.sqrMagnitude < 1e-6f) return;

            var parent = pivot.parent;
            Vector3 dirLocal = parent ? parent.InverseTransformDirection(dirWorld.normalized)
                : dirWorld.normalized;

            // Deseados desde el neutro con offset y eje de mira
            GetYawPitchFromDirLocal(dirLocal, out float desiredYaw, out float desiredPitch);

            desiredYaw   = Mathf.Clamp(desiredYaw,   yawLeftLimit,  yawRightLimit);
            desiredPitch = Mathf.Clamp(desiredPitch, pitchDownLimit, pitchUpLimit);

            GetCurrentYawPitch(out float currentYaw, out float currentPitch);

            float dt = Application.isPlaying ? Time.deltaTime : 1f/60f;

            if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, desiredYaw)) > deadZoneDeg)
                currentYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, yawSpeedDeg * dt);

            if (Mathf.Abs(Mathf.DeltaAngle(currentPitch, desiredPitch)) > deadZoneDeg)
                currentPitch = Mathf.MoveTowardsAngle(currentPitch, desiredPitch, pitchSpeedDeg * dt);

            Quaternion local =
                _neutralWithOffset
                * Quaternion.Euler(0f, currentYaw, 0f)
                * Quaternion.Euler(currentPitch, 0f, 0f);

            if (lockRoll)
            {
                Vector3 e = (Quaternion.Inverse(_neutralWithOffset) * local).eulerAngles;
                e.z = 0f;
                local = _neutralWithOffset * Quaternion.Euler(e);
            }

            pivot.localRotation = local;
        }

        void CacheNeutral()
        {
            _cached = true;
            if (!pivot) pivot = transform;
            _neutralLocalRot = pivot.localRotation;

            // Construimos un “neutro con offset” alineando el forward elegido
            // 1) base neutra
            _neutralWithOffset = _neutralLocalRot;

            // 2) rotamos para que el "forward visual" coincida con +Z lógico
            Quaternion align = GetAxisAlignmentToPlusZ(forwardLocalAxis);
            _neutralWithOffset = _neutralWithOffset * align;

            // 3) offsets finos opcionales
            _neutralWithOffset = _neutralWithOffset
                                 * Quaternion.Euler(0f, extraYawOffsetDeg, 0f)
                                 * Quaternion.Euler(extraPitchOffsetDeg, 0f, 0f);
        }

        // Devuelve la rotación que lleva el eje elegido a +Z
        static Quaternion GetAxisAlignmentToPlusZ(AimAxis axis)
        {
            switch (axis)
            {
                case AimAxis.PlusZ:  return Quaternion.identity;
                case AimAxis.MinusZ: return Quaternion.Euler(0f, 180f, 0f);
                case AimAxis.PlusX:  return Quaternion.Euler(0f, -90f, 0f);
                case AimAxis.MinusX: return Quaternion.Euler(0f, 90f, 0f);
                case AimAxis.PlusY:  return Quaternion.Euler(90f, 0f, 0f);
                case AimAxis.MinusY: return Quaternion.Euler(-90f, 0f, 0f);
                default:             return Quaternion.identity;
            }
        }

        void GetYawPitchFromDirLocal(Vector3 dirLocal, out float yawDeg, out float pitchDeg)
        {
            dirLocal.Normalize();

            Vector3 neutralFwdLocal   = (_neutralWithOffset * Vector3.forward);
            Vector3 neutralRightLocal = (_neutralWithOffset * Vector3.right);
            Vector3 neutralUpLocal    = (_neutralWithOffset * Vector3.up);

            yawDeg = SignedAngleOnPlane(neutralFwdLocal, dirLocal, neutralUpLocal);

            Vector3 dirOnPitchPlane = Vector3.ProjectOnPlane(dirLocal, neutralRightLocal).normalized;
            pitchDeg = SignedAngleOnPlane(neutralFwdLocal, dirOnPitchPlane, neutralRightLocal);
        }

        void GetCurrentYawPitch(out float yawDeg, out float pitchDeg)
        {
            Quaternion rel = Quaternion.Inverse(_neutralWithOffset) * pivot.localRotation;
            Vector3 e = rel.eulerAngles;

            float norm(float a) => (a > 180f) ? a - 360f : a;
            yawDeg   = norm(e.y);
            pitchDeg = norm(e.x);
        }

        static float SignedAngleOnPlane(Vector3 from, Vector3 to, Vector3 planeNormal)
        {
            from = Vector3.ProjectOnPlane(from, planeNormal).normalized;
            to   = Vector3.ProjectOnPlane(to,   planeNormal).normalized;
            return Vector3.SignedAngle(from, to, planeNormal);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawAxes || !pivot) return;

            // Dibuja los ejes del "neutro con offset" para diagnosticar
            if (!_cached) CacheNeutral();

            Vector3 p = pivot.position;
            float L = 0.5f;

            Vector3 f = _neutralWithOffset * Vector3.forward;
            Vector3 r = _neutralWithOffset * Vector3.right;
            Vector3 u = _neutralWithOffset * Vector3.up;

            Gizmos.color = Color.green;  Gizmos.DrawLine(p, p + f * L); // forward
            Gizmos.color = Color.red;    Gizmos.DrawLine(p, p + r * L); // right
            Gizmos.color = Color.blue;   Gizmos.DrawLine(p, p + u * L); // up
        }
    }
}
