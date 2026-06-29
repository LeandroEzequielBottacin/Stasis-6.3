using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    [DefaultExecutionOrder(11000)]
    public class VisualYawFollower : MonoBehaviour
    {
        [Header("Enable/Disable")]
        public bool followEnabled = true;
        public bool snapWhenEnabled;

        [Header("Refs")]
        public Transform yawPivot;     // cam yaw
        public Transform visualRoot;   // graphics root
        public Model model;        // <- asignar en Inspector (o por código)

        [Header("Follow Tuning")]
        [Range(0f, 1f)] public float turnLerp = 0.2f;

        [Header("Turn Trigger (por velocidad angular)")]
        [Tooltip("Umbral para el primer disparo (°/s)")]
        public float startVelDegPerSec = 120f;
        [Tooltip("Umbral para sostener el giro (°/s)")]
        public float sustainVelDegPerSec = 60f;
        [Tooltip("Re-disparo cada X segundos mientras siga girando")]
        public float retriggerPeriod = 0.60f;
        [Tooltip("Anti-spam tras el primer disparo")]
        public float minGapAfterStart = 0.20f;

        [Header("Gating")]
        public bool onlyWhenIdle = true;   // sólo si IsIdleForTurn()
        public bool blockWhenRunning = true;

        private bool _wasFollowing;

        // Estado del detector
        private float _prevCamYawDeg;
        private bool _hasPrev;
        private int _turnDir;                  // 0 = armado, +1 der, -1 izq
        private float _lastFireTime = -999f;

        void Reset() { visualRoot = transform; }

        void OnEnable()
        {
            _hasPrev = false;
            _turnDir = 0;
            _lastFireTime = -999f;
        }

        void LateUpdate()
        {
            if (!yawPivot) return;

            // ------ FOLLOW visual ↦ cámara (como ya lo tenías) ------
            if (!followEnabled)
            {
                _wasFollowing = false;
                // rearmar detector
                _hasPrev = false;
                _turnDir = 0;
                return;
            }

            if (!visualRoot) visualRoot = transform;

            Vector3 f = yawPivot.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return;

            Quaternion target = Quaternion.LookRotation(f.normalized, Vector3.up);

            if (!_wasFollowing && snapWhenEnabled)
            {
                visualRoot.rotation = target;
                _wasFollowing = true;
                return;
            }

            float t = 1f - Mathf.Pow(1f - turnLerp, Time.deltaTime * 60f);
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, target, t);
            _wasFollowing = true;

            // ------ DETECTOR por velocidad angular ------
            DetectAndEmitTurnEvent();
        }

        void DetectAndEmitTurnEvent()
        {
            if (!model) return;

            // Gate: sólo idle / no correr
            if ((onlyWhenIdle && !model.IsIdleForTurn()) ||
                (blockWhenRunning && model.isRunningRuntime))
            {
                _turnDir = 0;
                _hasPrev = false;
                return;
            }

            // Yaw absoluto de la cámara (en grados, -180..180)
            float camYaw = GetYawDeg(yawPivot);
            if (!_hasPrev)
            {
                _prevCamYawDeg = camYaw;
                _hasPrev = true;
                return;
            }

            float dYaw = Mathf.DeltaAngle(_prevCamYawDeg, camYaw);
            float vel = dYaw / Mathf.Max(Time.deltaTime, 1e-4f); // °/s
            _prevCamYawDeg = camYaw;

            float aVel = Mathf.Abs(vel);
            int dirNow = vel > 0f ? +1 : (vel < 0f ? -1 : 0);

            // si frenó por debajo del sustain -> rearmar
            if (aVel < sustainVelDegPerSec || dirNow == 0)
            {
                _turnDir = 0; // armado
                return;
            }

            // cambio de signo de giro -> rearmar
            if (_turnDir != 0 && !Mathf.Approximately(Mathf.Sign(_turnDir), Mathf.Sign(dirNow)))
            {
                _turnDir = 0;
            }

            // 1) primer disparo al superar startVel
            if (_turnDir == 0)
            {
                if (aVel >= startVelDegPerSec &&
                    (Time.time - _lastFireTime) >= minGapAfterStart)
                {
                    Fire(dirNow);
                    _turnDir = dirNow;
                    _lastFireTime = Time.time;
                }
                return;
            }

            // 2) re-disparo periódico si sostiene dirección y velocidad
            if (Mathf.Approximately(Mathf.Sign(_turnDir), Mathf.Sign(dirNow)) &&
                aVel >= sustainVelDegPerSec &&
                (Time.time - _lastFireTime) >= retriggerPeriod)
            {
                Fire(dirNow);
                _lastFireTime = Time.time;
            }
        }

        void Fire(int dir)
        {
            model.RequestTurnYaw(dir);
        }

        static float GetYawDeg(Transform t)
        {
            Vector3 f = t.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return 0f;
            f.Normalize();
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }
    }
}
