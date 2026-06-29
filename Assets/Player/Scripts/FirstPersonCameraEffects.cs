using Player.Scripts.MovementFSM;
using Unity.Cinemachine;
using UnityEngine;

namespace Player.Scripts
{
    [DefaultExecutionOrder(12000)]
    public class FirstPersonCameraEffects : MonoBehaviour
    {
        [Header("Refs")]
        public Transform effectsPivot;
        public UnityEngine.Camera cam;
        [Tooltip("Reference to ProceduralClimbIK for hand-based camera tilt")]
        public ProceduralClimbIK climbIK;

        [Header("FOV")]
        public bool  enableFovKick = true;
        [Tooltip("Si >0, fuerza ese FOV como base (ignora el de la vcam). Si 0, se toma el de la vcam activa cuando aparece.")]
        public float baseFovOverride = 75f;
        public float wallrunFovAdd = 4f;
        public float vaultFovAdd   = 3f;
        public float runFovAdd     = 6f;
        public float climbFovAdd     = 15f;
        public float fovInSpeed    = 8f;
        public float fovOutSpeed   = 6f;

        [Header("Tilt / Roll")]
        [Range(0f, 45f)] public float wallrunTilt = 14f;
        [Range(0f, 45f)] public float vaultTilt  = 8f;
        [Range(0f, 45f)] public float climbTilt  = 8f;
        public float rollInSpeed  = 10f;
        public float rollOutSpeed = 8f;
        public bool  vaultAsPulse = true;
        [Min(0.05f)] public float vaultHoldTime = 0.20f;
        

        [Header("Shake / Bob")]
        public bool enableShake = true;
        public float shakeFrequency = 18f;
        public float shakeDamping   = 6f;
        public bool enableHeadbob;
        public float bobSpeed = 8f;
        public float bobAmount = 0.03f;
        [Range(0f,1f)] public float idleBobWeight = 0.2f;
        
        [Header("Landing Tilt")]
        public bool enableLandTilt = true;
        [Tooltip("Velocidad de caída mínima para activar el tilt.")]
        public float landTiltMinSpeed = 4f;
        [Tooltip("Velocidad de caída para el máximo tilt.")]
        public float landTiltMaxSpeed = 15f;
        [Tooltip("Ángulo de tilt (roll) con velocidad mínima.")]
        public float landTiltMinAngle = 2f;
        [Tooltip("Ángulo de tilt (roll) con velocidad máxima.")]
        public float landTiltMaxAngle = 8f;
        [Tooltip("Duración del 'pulso' de tilt al aterrizar.")]
        public float landTiltHoldTime = 0.18f;
        [Tooltip("Intensidad de shake (vibración) con velocidad mínima.")]
        public float landTiltMinShake = 0.5f;
        [Tooltip("Intensidad de shake (vibración) con velocidad máxima.")]
        public float landTiltMaxShake = 2.0f;
        [Tooltip("Tiempo de suavizado para el 'roll' (tilt). 0.1 es un buen valor.")]
        public float rollSmoothTime = 0.1f;

        [Header("Cinemachine")]
        [Tooltip("Si está activo, el tilt se aplica como Lens.Dutch (roll real en eje Z de la vcam).")]
        public bool useCinemachineDutch = true;

        float _rollTarget, _rollCurrent, _rollPulseTimer, _rollVelocity;
        float _fovCurrent;
        float _extraFovRuntime;
        float _shakeAmpRuntime; Vector2 _shakePhase;
        Vector3 _bobLocalOrigin;
        bool _isClimbing;
        float _climbBobPhase;
        bool _climbFovApplied;
        float _climbTiltCurrent;

        CinemachineBrain _brain;
        CinemachineCamera _activeCamCached;
        float _baseFovLive;
        bool  _haveBaseFov;

        void Awake()
        {
            if (!cam) cam = GetComponentInChildren<UnityEngine.Camera>();
            _brain = cam ? cam.GetComponent<CinemachineBrain>() : null;

            if (!effectsPivot)
            {
                var go = new GameObject("EffectsPivot");
                effectsPivot = go.transform;
                effectsPivot.SetParent(cam ? cam.transform : transform, false);
                effectsPivot.localPosition = Vector3.zero;
                effectsPivot.localRotation = Quaternion.identity;
            }
            _bobLocalOrigin = effectsPivot.localPosition;

            _haveBaseFov = baseFovOverride > 0f;
            _baseFovLive = _haveBaseFov ? baseFovOverride : (cam ? cam.fieldOfView : 60f);
            _fovCurrent  = _baseFovLive;
            ApplyFovImmediate(_fovCurrent);
        }

        void OnEnable()
        {
            _rollTarget = _rollCurrent = NormalizeAngle(effectsPivot.localEulerAngles.z);
            _extraFovRuntime = 0f;
            _shakeAmpRuntime = 0f;
            RefreshActiveCamAndMaybeCacheBase();
            _fovCurrent = _baseFovLive;
            ApplyFovImmediate(_fovCurrent);

            if (useCinemachineDutch) ApplyDutch(0f);
        }

        void Update()
        {
            RefreshActiveCamAndMaybeCacheBase();

            if (_rollPulseTimer > 0f)
            {
                _rollPulseTimer -= Time.deltaTime;
                if (_rollPulseTimer <= 0f)
                    _rollTarget = 0f;
            }

            if (enableShake && _shakeAmpRuntime > 0f)
                _shakeAmpRuntime = Mathf.Max(0f, _shakeAmpRuntime - shakeDamping * Time.deltaTime);
        }

        void LateUpdate()
        {
            float maxSpeed = (Mathf.Abs(_rollTarget) > Mathf.Abs(_rollCurrent)) ? rollInSpeed : rollOutSpeed;
            _rollCurrent = Mathf.SmoothDampAngle(
                _rollCurrent,
                _rollTarget,
                ref _rollVelocity,
                rollSmoothTime,
                maxSpeed,
                Time.deltaTime
            );

            if (useCinemachineDutch && HasActiveVcam())
            {
                ApplyDutch(_rollCurrent);
                effectsPivot.localRotation = Quaternion.identity;
            }
            else
            {
                var e = effectsPivot.localEulerAngles;
                effectsPivot.localRotation = Quaternion.Euler(e.x, e.y, _rollCurrent);
            }

            if (enableFovKick)
            {
                float want = _baseFovLive + _extraFovRuntime;
                float speed = (want > _fovCurrent) ? fovInSpeed : fovOutSpeed;
                _fovCurrent = Mathf.MoveTowards(_fovCurrent, want, speed * Time.deltaTime);
                ApplyFovImmediate(_fovCurrent);
            }

            Vector3 pos = _bobLocalOrigin;
            if (enableHeadbob && bobAmount > 0f)
            {
                float w = Mathf.Lerp(idleBobWeight, 1f, 0f);
                float t = Time.time * bobSpeed;
                pos += Vector3.up * (Mathf.Sin(t) * bobAmount * w);
            }
            
            // Climb-specific bob effect (vertical movement during climb)
            if (_isClimbing)
            {
                _climbBobPhase += Time.deltaTime * 2.5f; // Climb bob speed
                float climbBob = Mathf.Sin(_climbBobPhase) * 0.008f; // Subtle vertical bob
                pos += Vector3.up * climbBob;
                
                // Calculate camera tilt based on hand movement
                if (climbIK != null)
                {
                    // Get hand height difference (positive = right hand higher, negative = left hand higher)
                    float handHeightDiff = climbIK.GetHandHeightDifference();
                    
                    // Get climb cycle to determine hand movement phase
                    float cycle = climbIK.GetClimbCycle();
                    
                    // Calculate tilt based on hand height difference and cycle
                    // When one hand is higher, tilt camera in that direction
                    float targetTilt = Mathf.Clamp(handHeightDiff * (climbTilt / 0.5f), -climbTilt, climbTilt);
                    
                    // Also add cycle-based tilt for smoother movement
                    float cycleTilt = Mathf.Sin(cycle) * (climbTilt * 0.3f);
                    targetTilt += cycleTilt;
                    
                    // Smooth the tilt
                    _climbTiltCurrent = Mathf.Lerp(_climbTiltCurrent, targetTilt, Time.deltaTime * 8f);
                    _rollTarget = _climbTiltCurrent;
                }
                else
                {
                    // Fallback to static tilt if no IK reference
                    _rollTarget = climbTilt;
                }
            }
            
            if (enableShake && _shakeAmpRuntime > 0f)
            {
                _shakePhase += new Vector2(shakeFrequency, shakeFrequency * 1.21f) * Time.deltaTime;
                float sx = Mathf.Sin(_shakePhase.x) * (_shakeAmpRuntime * 0.0025f);
                float sy = Mathf.Cos(_shakePhase.y) * (_shakeAmpRuntime * 0.0025f);
                pos += new Vector3(sx, sy, 0f);
            }
            effectsPivot.localPosition = pos;
        }

        public void WallrunStart(int wallSide)
        {
            wallSide = Mathf.Clamp(wallSide, -1, +1);
            // Convención: pared a la derecha ⇒ roll negativo (inclina a la derecha)
            _rollTarget = (wallSide > 0 ? -1f : +1f) * wallrunTilt;
            AddFov(+Mathf.Abs(wallrunFovAdd));
        }
        public void WallrunEnd()
        {
            _rollTarget = 0f;
            AddFov(-Mathf.Abs(wallrunFovAdd));
        }

        public void VaultStart(int sideSign = 0)
        {
            sideSign = Mathf.Clamp(sideSign, -1, 1);
            _rollTarget = (sideSign==0? 1: sideSign) * vaultTilt;
            if (vaultAsPulse) _rollPulseTimer = vaultHoldTime;
            AddFov(+Mathf.Abs(vaultFovAdd));
            AddImpulseShake(1.4f);
        }
        public void VaultEnd()
        {
            _rollTarget = 0f;
            AddFov(-Mathf.Abs(vaultFovAdd));
        }
        
        public void ClimbStart()
        {
            _isClimbing = true;
            _climbBobPhase = 0f;
            _climbFovApplied = true;
            _climbTiltCurrent = 0f;
            AddFov(+Mathf.Abs(climbFovAdd));
            // Add subtle shake during climb
            if (enableShake) _shakeAmpRuntime = Mathf.Max(_shakeAmpRuntime, 0.8f);
        }
        public void ClimbEnd()
        {
            _isClimbing = false;
            // Only remove FOV if it was actually applied to prevent double removal
            if (_climbFovApplied)
            {
                AddFov(-Mathf.Abs(climbFovAdd));
                _climbFovApplied = false;
            }
            // Reset tilt
            _climbTiltCurrent = 0f;
            _rollTarget = 0f;
        }

        public void OnRunStart() => AddFov(+Mathf.Abs(runFovAdd));
        public void OnRunEnd()   => AddFov(-Mathf.Abs(runFovAdd));

        private void AddImpulseShake(float deg)
        {
            if (!enableShake) return;
            _shakeAmpRuntime = Mathf.Max(_shakeAmpRuntime, Mathf.Abs(deg));
        }
        
        public void TriggerLandTilt(float impactSpeed, float airTime)
        {
            if (!enableLandTilt || impactSpeed < landTiltMinSpeed || airTime < 0.1f)
                return;
        
            if (_rollPulseTimer > 0f) return;

            float t = Mathf.InverseLerp(landTiltMinSpeed, landTiltMaxSpeed, impactSpeed);
            float angle = Mathf.Lerp(landTiltMinAngle, landTiltMaxAngle, t);
            
            float shake = Mathf.Lerp(landTiltMinShake, landTiltMaxShake, t);
            AddImpulseShake(shake);
        
            _rollTarget = (Random.value > 0.5f ? 1f : -1f) * angle;

            _rollPulseTimer = landTiltHoldTime;
        }

        public void ClearAll()
        {
            _rollTarget = 0f; _rollPulseTimer = 0f; _extraFovRuntime = 0f; _shakeAmpRuntime = 0f;
            _isClimbing = false;
            _climbBobPhase = 0f;
            _climbFovApplied = false;
            _climbTiltCurrent = 0f;
            effectsPivot.localPosition = _bobLocalOrigin;
            effectsPivot.localRotation = Quaternion.identity;
            RefreshActiveCamAndMaybeCacheBase(force:true);
            _fovCurrent = _baseFovLive;
            ApplyFovImmediate(_fovCurrent);
            if (useCinemachineDutch) ApplyDutch(0f);
        }

        void AddFov(float delta) { if (!enableFovKick) return; _extraFovRuntime += delta; }

        void RefreshActiveCamAndMaybeCacheBase(bool force=false)
        {
            if (!_brain) return;
            var active = _brain.ActiveVirtualCamera as CinemachineCamera; // CM3
            if (active != _activeCamCached || force)
            {
                _activeCamCached = active;
                if (!_haveBaseFov)
                {
                    if (_activeCamCached) _baseFovLive = _activeCamCached.Lens.FieldOfView;
                    else if (cam)         _baseFovLive = cam.fieldOfView;
                }
            }
        }

        bool HasActiveVcam()
        {
            if (!_brain) return false;
            return (_brain.ActiveVirtualCamera as CinemachineCamera);
        }

        void ApplyDutch(float deg)
        {
            if (!_brain) return;
            var vcam = _brain.ActiveVirtualCamera as CinemachineCamera;
            if (!vcam) return;
            var lens = vcam.Lens;   // CM3 LensSettings
            lens.Dutch = deg;       // roll sobre Z (tilt real)
            vcam.Lens = lens;
        }

        void ApplyFovImmediate(float v)
        {
            if (_brain)
            {
                var vcam = _brain.ActiveVirtualCamera as CinemachineCamera;
                if (vcam)
                {
                    var lens = vcam.Lens;
                    lens.FieldOfView = v;
                    vcam.Lens = lens;
                    return;
                }
            }
            if (cam) cam.fieldOfView = v; // fallback cuando aún no hay vcam live
        }

        static float NormalizeAngle(float deg) { deg %= 360f; if (deg > 180f) deg -= 360f; return deg; }
    }
}