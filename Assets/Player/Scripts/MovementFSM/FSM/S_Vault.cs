using System;
using Player.Scripts.MovementFSM.MVC;
using Player.Scripts.MovementFSM.Player.Scripts.MovementFSM;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Vault : IState
    {
        private readonly FSM _fsm;
        private readonly Model _m;

        public S_Vault(FSM fsm, Model model)
        {
            _fsm = fsm;
            _m = model;
        }

        Rigidbody _rb;
        CapsuleCollider _capsule;
        Collider _ignoredObstacle;
        ParkourScanner _scanner;

        Vector3 _p0, _p1, _p2;
        Vector3 _enterForward;
        float _t, _dur;
        bool _oldUseGravity, _collisionsOff;
        RigidbodyInterpolation _oldInterp;
        bool _gravityRestored;
        bool _stepUpMode;
        float _entryHorizSpeed, _pathHorizSpeed;
        float _lockedYawDeg;

        // --- NUEVO: cap de altura para “pegarse” a la tapa ---
        float _yTopCap;

        // === Tunables ===
        const float ReleaseBlendStart = 0.60f;   // soltamos desde el 60%

        // Top-hug: cuánto y cuándo pegarnos a la tapa
        const float TopHugEnd = 0.58f;           // hasta ~58% apretamos hacia la tapa
        const float TopHugExtraUp = 0.015f;      // margen por encima de la tapa (m)
        const float TopHugBlendPower = 1.0f;     // 1.0 = mezcla completa hacia el cap

        const float Kp = 80f, Kd = 16f, MaxAccel = 120f;
        const bool UprightWhileVault = true;
        const float UprightSettleDegPerSec = 9999f;

        const float VaultMinTime = 0.22f, VaultMaxTime = 0.55f;
        const float ApexFactor = 0.5f, ApexExtra = 0.25f;
        const float NormalClearance = 0.06f, StartUpNudge = 0.02f;

        // step-up suavizado
        const float StepUpInset   = 0.12f;
        const float StepUpExtraUp = 0.045f;
        const float StepUpApexCap = 0.26f;

        private bool LogOn => !_scanner;

        public void OnEnter()
        {
            _rb = _m.rb;
            _capsule = _m.GetComponent<CapsuleCollider>();
            _scanner = _m.GetComponent<ParkourScanner>();
            var p = _m.probe;

            Vector3 vIn = _rb.linearVelocity; vIn.y = 0f;
            _entryHorizSpeed = vIn.magnitude;

            _oldUseGravity = _rb.useGravity;
            _rb.useGravity = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            _gravityRestored = false;

            _oldInterp = _rb.interpolation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _m.canMove = false;

            _lockedYawDeg = YawDeg(_rb.rotation);

            // Ignorar obstáculo todo el vault
            if (_capsule && p.vaultObstacle)
            {
                Physics.IgnoreCollision(_capsule, p.vaultObstacle, true);
                _ignoredObstacle = p.vaultObstacle;
                if (LogOn) Debug.Log("[VaultState] Ignoring obstacle collider for the whole vault.");
            }
            else
            {
                _rb.detectCollisions = false;
                _collisionsOff = true;
                if (LogOn) Debug.Log("[VaultState] detectCollisions OFF (no obstacle/capsule).");
            }

            BuildVaultCurve(p);

            if (UprightWhileVault)
                _rb.MoveRotation(Quaternion.Euler(0f, _lockedYawDeg, 0f));

            _m.VaultStartEvent();
            _t = 0f;
        }

        public void OnUpdate() { }

        public void OnFixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _t = Mathf.Min(1f, _t + dt / _dur);

            float sNow  = EaseInOutCubic(_t);
            float sNext = EaseInOutCubic(Mathf.Min(1f, _t + dt / _dur));

            Vector3 posNow  = Bezier2(_p0, _p1, _p2, sNow);
            Vector3 posNext = Bezier2(_p0, _p1, _p2, sNext);

            if (_stepUpMode) 
            {
                if (_t <= TopHugEnd)
                {
                    float k = Smooth01(_t / TopHugEnd);
                    float yCap = _yTopCap;

                    float yNowClamped  = Mathf.Min(posNow.y,  yCap);
                    float yNextClamped = Mathf.Min(posNext.y, yCap);

                    posNow.y  = Mathf.Lerp(posNow.y,  yNowClamped,  Mathf.Pow(k, TopHugBlendPower));
                    posNext.y = Mathf.Lerp(posNext.y, yNextClamped, Mathf.Pow(k, TopHugBlendPower));
                }
            }

            Vector3 velDesired = (posNext - posNow) / dt;

            float gain = 1f;
            if (_t >= ReleaseBlendStart)
            {
                if (!_gravityRestored)
                {
                    if (!_m.blockUseGravity)
                    {
                        _rb.useGravity = true;
                    }

                    _gravityRestored = true;
                    if (LogOn) Debug.Log($"[VaultState] Gravity ON at t={_t:F2}");
                }
                float u = Mathf.InverseLerp(ReleaseBlendStart, 1f, _t);
                gain = 1f - (u * u * (3f - 2f * u));
            }

            Vector3 posErr = posNow - _rb.position;
            Vector3 velErr = velDesired - _rb.linearVelocity;

            if (_t >= 0.94f) velErr.y = Mathf.Min(velErr.y, 0f); // sin segunda patada arriba

            Vector3 accel = (Kp * posErr + Kd * velErr) * gain;
            float aMag = accel.magnitude;
            if (aMag > MaxAccel) accel *= (MaxAccel / aMag);

            _rb.AddForce(accel, ForceMode.Acceleration);

            if (UprightWhileVault)
            {
                var curr = _rb.rotation;
                var goal = Quaternion.Euler(0f, _lockedYawDeg, 0f);
                var next = Quaternion.RotateTowards(curr, goal, UprightSettleDegPerSec * dt);
                _rb.MoveRotation(next);
            }

            if (_t >= 1f)
                ExitToGrounded();
        }

        public void OnExit()
        {
            if (_ignoredObstacle && _capsule)
                Physics.IgnoreCollision(_capsule, _ignoredObstacle, false);

            if (_collisionsOff)
                _rb.detectCollisions = true;

            _rb.useGravity = _oldUseGravity;
            _rb.interpolation = _oldInterp;

            _m.canMove = true;
            _m.VaultEndEvent();

            if (LogOn)
                Debug.Log($"[VaultState] EXIT | endPos={_rb.position:F3} vel={_rb.linearVelocity:F2}");
        }

        public void OnLateUpdate()
        {
            throw new NotImplementedException();
        }

        private void BuildVaultCurve(in ParkourProbe p)
        {
            float radius = _capsule ? _capsule.radius : (p.playerRadius > 0 ? p.playerRadius : 0.3f);

            _p0 = _rb.position + Vector3.up * StartUpNudge;
            _p2 = p.vaultLandPoint;

            _stepUpMode = p.vaultLandOnSameCollider ||
                          p.vaultDistance > (_m.Scanner ? _m.Scanner.vaultMaxForward : 2.2f);

            // --- definimos el CAP de Y justo sobre la tapa ---
            _yTopCap = p.vaultTopPoint.y + radius + NormalClearance + TopHugExtraUp;

            if (_stepUpMode)
            {
                Vector3 insetDir =
                    (p.vaultForward.sqrMagnitude > 0f)
                        ? new Vector3(p.vaultForward.x, 0f, p.vaultForward.z).normalized
                        : (_m.transform.forward - Vector3.Project(_m.transform.forward, Vector3.up)).normalized;

                float minY = p.vaultTopPoint.y;
                _p2 = p.vaultTopPoint + insetDir * StepUpInset;
                _p2.y = Mathf.Max(_p2.y, minY);
            }

            Vector3 intended = _p2 - _p0; intended.y = 0f;
            Vector3 fwdHint = p.vaultForward; fwdHint.y = 0f;
            if (intended.sqrMagnitude > 1e-6f && fwdHint.sqrMagnitude > 1e-6f &&
                Vector3.Dot(fwdHint, intended) < 0f)
                fwdHint = -fwdHint;

            Vector3 landDir = intended.sqrMagnitude > 1e-6f
                ? intended.normalized
                : (fwdHint.sqrMagnitude > 1e-6f
                    ? fwdHint.normalized
                    : (_m.cameraHolderTransform ? _m.cameraHolderTransform.forward : _m.transform.forward));
            landDir.y = 0f; landDir.Normalize();
            _enterForward = landDir;

            float dxz = Vector3.Distance(
                new Vector3(_p0.x, 0, _p0.z),
                new Vector3(_p2.x, 0, _p2.z));

            float minVaultSpeed = Mathf.Max(1f, _m.walkingSpeed * 0.85f);
            float maxVaultSpeed = Mathf.Max(minVaultSpeed + 0.1f, _m.runningSpeed * 1.25f);
            _pathHorizSpeed = Mathf.Clamp(_entryHorizSpeed, minVaultSpeed, maxVaultSpeed);

            _dur = Mathf.Clamp(dxz / Mathf.Max(0.1f, _pathHorizSpeed),
                               VaultMinTime,
                               VaultMaxTime);

            // apex base
            float apexY = Mathf.Max(0.15f, p.obstacleHeight * ApexFactor + ApexExtra);
            if (_stepUpMode) apexY = Mathf.Min(apexY, StepUpApexCap);

            float sep = radius + NormalClearance;

            Vector3 midXZ = p.vaultMidXZ; // midXZ.y == top y
            // --- CAP del apex para que nunca supere el top cap + pequeño margen ---
            float apexCapRelative = Mathf.Max(0.02f, (_yTopCap + 0.04f) - midXZ.y);
            apexY = Mathf.Min(apexY, apexCapRelative);

            _p1 = midXZ + p.hitNormal * sep + Vector3.up * apexY;

            // Ajustar tangente inicial
            Vector3 tan0 = Bezier2Tangent(_p0, _p1, _p2, 0.001f);
            tan0.y = 0f;
            if (Vector3.Dot(tan0, landDir) <= 0f)
            {
                float ctrlAhead = Mathf.Max(0.35f, dxz * 0.35f);
                _p1 = _p0 + landDir * ctrlAhead + Vector3.up * apexY;
            }

            if (LogOn)
                Debug.Log($"[VaultState] Curve built | stepUp={_stepUpMode} yCap={_yTopCap:F3} dxz={dxz:F2} dur={_dur:F2} apexY={apexY:F2} p0={_p0:F3} p1={_p1:F3} p2={_p2:F3}");
        }

        private void ExitToGrounded()
        {
            if (!_m.blockUseGravity)
            {
                _rb.useGravity = true;
            }

            var v = _rb.linearVelocity;
            if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) v = Vector3.zero;
            _rb.linearVelocity = v;

            _m.blockVaultUntil = Time.time + _m.vaultRegrabCooldown;
            _fsm.ChangeState(FSM.States.Grounded);
        }

        static float YawDeg(Quaternion q)
        {
            Vector3 f = q * Vector3.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return 0f;
            f.Normalize();
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        static float EaseInOutCubic(float t)
            => (t < 0.5f) ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

        static float Smooth01(float x) // smoothstep 0..1
            => Mathf.Clamp01(x) * Mathf.Clamp01(x) * (3f - 2f * Mathf.Clamp01(x));

        static Vector3 Bezier2(Vector3 p0, Vector3 p1, Vector3 p2, float s)
        {
            float omt = 1f - s;
            return (omt * omt) * p0 + (2f * omt * s) * p1 + (s * s) * p2;
        }

        static Vector3 Bezier2Tangent(Vector3 p0, Vector3 p1, Vector3 p2, float s)
        {
            return 2f * (1f - s) * (p1 - p0) + 2f * s * (p2 - p1);
        }
    }
}
