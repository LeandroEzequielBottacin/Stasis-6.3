using Player.Scripts.MovementFSM.MVC;
using Player.Scripts.MovementFSM.Player.Scripts.MovementFSM;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Climb : IState
    {
        private readonly FSM _fsm;
        private readonly Model _m;

        Rigidbody _rb;
        Transform _orient;
        CapsuleCollider _capsule;

        bool _climbing;
        bool _hasFront;
        RaycastHit _frontHit;

        private Vector3 _wallNormal;
        Vector3 _planarNormal;
        Vector3 _lastHitPoint;


        readonly float _climbDetectLength = 0.9f;
        readonly float _climbSphereRadius = 0.35f;
        [Range(0, 85)] readonly float _climbMaxLookAngle = 180f;
        [Range(0, 89)] readonly float _climbMinWallSlopeDeg = 70f;

        readonly float _upSpeed = 3.2f;
        readonly float _downSpeed = 3.0f;
        readonly float _slideSpeed = 0.01f;

        readonly float _maxUpAccel = 30f;
        readonly float _maxDownAccel = 30f;

        readonly float _standOff = 0.18f;
        readonly float _stickKp = 90f;
        readonly float _stickKd = 12f;
        readonly float _maxStickAccel = 80f;

        readonly float _intoCancelMaxAccel = 120f;

        readonly float _mantleSpeed = 4.6f;
        readonly float _mantleAssistAccel = 24f;
        readonly float _mantleAssistTime = 0.22f;

        readonly float _maxOutSpeedBeforeMantle = 1.6f;
        readonly float _maxMantleSpeed = 5.0f;

        readonly float _mantleDragAdd = 0.6f;
        float _prevDrag;
        
        private float _enterTime;
        private Vector3 _entryVelocity;

        readonly bool _drawGizmos = false;

        bool _mantling;
        float _mantleUntil;
        Vector3 _mantleDirCached;
        Vector3 _mantleStandPoint;

        private ParkourScanner _scan;

        public S_Climb(FSM fsm, Model model)
        {
            _fsm = fsm;
            _m = model;
        }

        public void OnEnter()
        {
            _rb = _m.rb;
            _orient = _m.cameraHolderTransform ? _m.cameraHolderTransform : _m.transform;
            _capsule = _m.GetComponent<CapsuleCollider>();
            _scan = _m.GetComponent<ParkourScanner>();

            _climbing = true;
            _mantling = false;
            _m.canMove = false;
            _rb.useGravity = false;
            _enterTime = Time.time;
            _entryVelocity = _rb.linearVelocity;
            
            _m.isClimbingState = true;
            _m.isMantlingState = false;
            _m.isAtLedge = false;
            _m.didClimbJump = false;

            WallCheck(true);

            if (_hasFront)
            {
                float vInto = Vector3.Dot(_rb.linearVelocity, -_planarNormal);
                if (vInto > 0f)
                {
                    _rb.AddForce(_planarNormal * vInto, ForceMode.VelocityChange);
                }
            }
            
            Vector3 forward = -_planarNormal;
            
            _m.ClimbStartEvent(forward);
            
        }

        public void OnUpdate()
        {
            if (_m.jumpDownThisFrame)
            {
                Vector3 camForward = _orient.forward;
                float lookPitch = camForward.y;
                float upThreshold = 0.3f;
                
                if (!_mantling || lookPitch > upThreshold)
                {
                    ClimbJump(); 
                    return;
                }
            }
            
            WallCheck(false);
            if (!_hasFront && !_mantling)
            {
                _m.isClimbingState = false;
            }
            
            if (!_hasFront && !_mantling)
            {
                ExitToNext();
            }
        }


        public void OnFixedUpdate()
        {
            if (!_climbing) return;

            if (_mantling)
            {
                if (Time.time <= _mantleUntil)
                    _rb.AddForce(_mantleDirCached * _mantleAssistAccel, ForceMode.Acceleration);

                Vector3 toStand = _mantleStandPoint - _rb.position;
                Vector3 plan = Vector3.ProjectOnPlane(toStand, Vector3.up);
                float distPlan = plan.magnitude;
                if (distPlan > 0.01f)
                {
                    Vector3 guideDir = plan.normalized;
                    _rb.AddForce(guideDir * 20f, ForceMode.Acceleration);
                    _rb.AddForce((-_planarNormal) * 6f, ForceMode.Acceleration);
                }

                float spd = _rb.linearVelocity.magnitude;
                if (spd > _maxMantleSpeed)
                    _rb.AddForce(-_rb.linearVelocity.normalized * (spd - _maxMantleSpeed), ForceMode.VelocityChange);

                if (_rb.position.y >= _mantleStandPoint.y - 0.02f && distPlan < 0.12f)
                    _rb.AddForce(Vector3.down * 1.5f, ForceMode.VelocityChange);

                if (_m.IsGroundedNow())
                {
                    ExitToNext();
                }

                return;
            }
            
            if (!_hasFront) return;
            
            _m.isClimbingState = true;
            _m.climbWallPoint = _lastHitPoint;
            _m.climbWallNormal = _planarNormal;
            
            float blend = EnterBlend01();
            
            float inputZ = Mathf.Abs(_m.rawZ) > 0.01f ? _m.rawZ : _m.zAxis;
            bool wantUp = (inputZ > 0.1f);
            bool wantDown = (inputZ < -0.1f);
            
            bool ledgeFound = false;
            Vector3 foundLedgePoint = Vector3.zero;

            var p = _scan.Probe;
            if (p.action == ParkourAction.Climb && p.climbStandPoint != Vector3.zero)
            {
                ledgeFound = true;
                foundLedgePoint = p.climbLedgePoint;
                
                if (wantUp)
                {
                    StartMantleFromStand(p.climbStandPoint, p.climbLedgePoint);
                    return;
                }
            }
            else if (TryGetLedgeLocal(out var ledge, out var stand))
            {
                ledgeFound = true;
                foundLedgePoint = ledge;

                if (wantUp)
                {
                    StartMantleFromStand(stand, ledge);
                    return;
                }
            }
            
            if (ledgeFound)
            {
                _m.isAtLedge = true;
                _m.isClimbingState = false;
                _m.mantleLedgePoint = foundLedgePoint;
                
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z); 
                
                return; 
            }
            
            _m.isAtLedge = false;
            _m.isClimbingState = true;
            
            if (wantDown && _m.IsGroundedNow())
            {
                ExitToNext();
                return;
            }

            float vClimbTarget = wantUp ? _upSpeed : wantDown ? -_downSpeed : -_slideSpeed;

            float vEntryTarget = _entryVelocity.y;

            float vTargetY = Mathf.Lerp(vEntryTarget, vClimbTarget, blend);

            float vY = Vector3.Dot(_rb.linearVelocity, Vector3.up);
            float dv = vTargetY - vY;
            float reqAy = dv / Time.fixedDeltaTime;
            float ay = Mathf.Clamp(reqAy, -_maxDownAccel, _maxUpAccel);
            
            _rb.AddForce(Vector3.up * ay, ForceMode.Acceleration);
    
            Vector3 vEntryHorizontal = Vector3.ProjectOnPlane(_entryVelocity, Vector3.up);
            if (vEntryHorizontal.sqrMagnitude > 1e-4f)
            {
                Vector3 vHorizontal = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);

                Vector3 vTargetHorizontal = Vector3.Lerp(vEntryHorizontal, Vector3.zero, blend);
        
                Vector3 dvHorizontal = vTargetHorizontal - vHorizontal;
                Vector3 aHorizontal = dvHorizontal / Time.fixedDeltaTime;

                _rb.AddForce(aHorizontal, ForceMode.Acceleration);
            }

            float vInto = Vector3.Dot(_rb.linearVelocity, -_planarNormal);
            if (vInto > 0f)
            {
                float aCancel = Mathf.Clamp(-vInto / Time.fixedDeltaTime, -_intoCancelMaxAccel, 0f);
                _rb.AddForce(_planarNormal * aCancel, ForceMode.Acceleration);
            }

            Vector3 pos = _rb.position;
            Vector3 target = _lastHitPoint + _planarNormal * _standOff;

            Vector3 delta = target - pos;
            delta.y = 0f;
            float errAlongN = Vector3.Dot(delta, _planarNormal);
            float vAlongN = Vector3.Dot(_rb.linearVelocity, _planarNormal);

            float aStick = _stickKp * errAlongN - _stickKd * vAlongN;
            aStick = Mathf.Clamp(aStick, -_maxStickAccel, _maxStickAccel);
            _rb.AddForce(_planarNormal * (aStick * blend), ForceMode.Acceleration);
        }
        
        public void OnExit()
        {
            _climbing = false;
            _mantling = false;
            _m.canMove = true;
            _rb.useGravity = true;
            _rb.linearDamping = _prevDrag;
            
            _m.isClimbingState = false;
            _m.isMantlingState = false;
            _m.isAtLedge = false;

            _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
            _m.ClimbEndEvent();
        }

        void ClimbJump()
        {
            _m.lastWallDetachTime = Time.time;
            _m.didClimbJump = true;
            
            float timeSinceEnter = Time.time - _enterTime;
            bool isEarlyJump = timeSinceEnter < 0.1f;
            
            _climbing = false;
            _mantling = false;
            _m.isClimbingState = false;
            _m.isMantlingState = false;
            _m.isAtLedge = false;
            
            _m.JumpSucceed();
            
            _m.ClimbEndEvent();
            
            Vector3 camForward = _orient.forward;
            
            Vector3 lookDirH = camForward;
            lookDirH.y = 0f;

            if (lookDirH.sqrMagnitude < 0.001f)
            {
                lookDirH = _planarNormal; 
            }
            else
            {
                lookDirH.Normalize();
            }
            
            float lookPitch = camForward.y;

            Vector3 velChange;

            float upThreshold = 0.5f;
            float downThreshold = -0.5f;

            if (lookPitch > upThreshold)
            {
                velChange = Vector3.up * _m.climbLeapUpForce + _planarNormal * _m.climbLeapSideForce;
                
                _m.blockClimbUntil = Time.time + _m.climbDynoRegrabCooldown;
            }
            else if (lookPitch < downThreshold)
            {
                velChange = Vector3.up * _m.wallJumpUpForce + _planarNormal * _m.wallJumpSideForce;
                
                _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
            }
            else
            {
                Vector3 awayForce = _planarNormal * _m.climbLeapSideForce;
                Vector3 fwdForce = lookDirH * _m.wallJumpSideForce;
                
                velChange = Vector3.up * _m.wallJumpUpForce + awayForce + fwdForce;
                
                _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
            }

            Vector3 vCleaned = _rb.linearVelocity;
            
            if (isEarlyJump)
            {
                vCleaned = Vector3.zero;
                vCleaned.y = 0f; 
            }
            else
            {
                float vInto = Vector3.Dot(vCleaned, -_planarNormal);
                if (vInto > 0f)
                {
                    vCleaned += _planarNormal * vInto; 
                }
                vCleaned.y = 0f;
            }

            _rb.linearVelocity = vCleaned;
            _rb.AddForce(velChange, ForceMode.VelocityChange);

            // Restore gravity and movement
            if (!_m.blockUseGravity)
            {
                _rb.useGravity = true;
            }
           
            _m.canMove = true;
            _rb.linearDamping = _prevDrag;

            ExitToNext();
        }

        public void OnLateUpdate()
        {
            
        }

        bool TryGetLedgeLocal(out Vector3 ledgePoint, out Vector3 standPoint)
        {
            ledgePoint = Vector3.zero;
            standPoint = Vector3.zero;
            if (!_capsule) return false;

            float h = _capsule.height;
            float r = _capsule.radius;
            Vector3 pos = _m.transform.position;

            // “cabeza” + un toque hacia adentro de la pared
            float headY = Mathf.Clamp(h * 0.92f, 1.3f, 1.9f);
            Vector3 headOrigin = pos + Vector3.up * headY;
            Vector3 inward = (-_planarNormal).sqrMagnitude > 1e-6f ? (-_planarNormal).normalized : Vector3.forward;

            // si a nivel cabeza ya NO hay pared, puede haber borde
            bool headHits = Physics.SphereCast(headOrigin, r * 0.95f, _orient.forward, out _, _climbDetectLength * 0.9f,
                ~0, QueryTriggerInteraction.Ignore);
            if (headHits) return false;

            // downcast desde un poco adentro y arriba para hallar la tapa
            Vector3 downFrom = headOrigin + inward * (r + 0.08f) + Vector3.up * 0.40f;
            if (Physics.Raycast(downFrom, Vector3.down, out var topHit, 2.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                // debe ser razonablemente horizontal
                if (Vector3.Dot(topHit.normal.normalized, Vector3.up) >= Mathf.Cos(32f * Mathf.Deg2Rad))
                {
                    ledgePoint = topHit.point;
                    // stand un pelín adentro y elevado al radio
                    Vector3 stand = topHit.point + inward * (r + 0.06f) + Vector3.up * (r + 0.06f);

                    // clearance real del cápsule en destino
                    float heightSeg = h - 2f * r;
                    Vector3 p1 = stand + Vector3.up * (r + 0.02f);
                    Vector3 p2 = p1 + Vector3.up * heightSeg;
                    bool blocked = Physics.CheckCapsule(p1, p2, Mathf.Max(0.05f, r * 0.95f), ~0,
                        QueryTriggerInteraction.Ignore);
                    if (!blocked)
                    {
                        standPoint = stand;
                        return true;
                    }
                }
            }

            return false;
        }
        
        float EnterBlend01()
        {
            return Mathf.Clamp01((Time.time - _enterTime) / Mathf.Max(0.01f, 0.2f));
        }
        
        void StartMantleFromStand(Vector3 standP, Vector3 ledgeP)
        {
            // Ensure we're not in a buggy state from previous leap
            if (_m.didClimbJump)
            {
                // Reset climb jump flag to allow proper mantle animation
                _m.didClimbJump = false;
            }
            
            _mantling = true;
            _mantleStandPoint = standP;
            
            _m.isClimbingState = false;
            _m.isMantlingState = true;
            _m.isAtLedge = false;
            _m.mantleLedgePoint = ledgeP;
            _m.climbWallNormal = _planarNormal;

            if (!_m.blockUseGravity)
            {
                _rb.useGravity = true;
            }

            Vector3 vPlanar = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
            if (vPlanar.sqrMagnitude > 1e-6f)
                _rb.AddForce(-vPlanar, ForceMode.VelocityChange);

            // limitar salida hacia afuera de la pared
            float vOut = Vector3.Dot(_rb.linearVelocity, _planarNormal);
            if (vOut > _maxOutSpeedBeforeMantle)
                _rb.AddForce(-_planarNormal * (vOut - _maxOutSpeedBeforeMantle), ForceMode.VelocityChange);

            // dirección principal: hacia stand + up
            Vector3 toStand = (_mantleStandPoint - _rb.position);
            Vector3 planToStand = Vector3.ProjectOnPlane(toStand, Vector3.up);
            Vector3 dir = (planToStand.normalized * 0.7f + Vector3.up * 0.9f).normalized;
            _mantleDirCached = dir;

            // impulso Δv ≈ _mantleSpeed
            _rb.AddForce(dir * (_rb.mass * _mantleSpeed), ForceMode.Impulse);

            // drag temporal para amortiguar
            _prevDrag = _rb.linearDamping;
            _rb.linearDamping = _prevDrag + _mantleDragAdd;

            _mantleUntil = Time.time + _mantleAssistTime;
        }


        void WallCheck(bool forceFirst)
        {
            _hasFront = false;

            float h = _capsule ? _capsule.height : 1.8f;
            float chest = Mathf.Clamp(h * 0.55f, 0.8f, 1.1f);
            Vector3 origin = _m.transform.position + Vector3.up * chest;

            Vector3 fwd = _orient ? _orient.forward : _m.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = _m.transform.forward;
            fwd.Normalize();

            // SphereCast adelante
            if (Physics.SphereCast(origin, _climbSphereRadius, fwd, out _frontHit,
                    _climbDetectLength, ~0, QueryTriggerInteraction.Ignore))
            {
                if (_frontHit.collider && _frontHit.collider.CompareTag(_m.tagClimb))
                {
                    float slopeDeg = Vector3.Angle(_frontHit.normal, Vector3.up);
                    if (slopeDeg >= _climbMinWallSlopeDeg)
                    {
                        float lookAng = Vector3.Angle(fwd, -_frontHit.normal);
                        if (lookAng <= _climbMaxLookAngle)
                        {
                            _hasFront = true;
                            _wallNormal = _frontHit.normal.normalized;
                            _planarNormal = Vector3.ProjectOnPlane(_wallNormal, Vector3.up);
                            if (_planarNormal.sqrMagnitude < 1e-6f)
                                _planarNormal = new Vector3(_wallNormal.x, 0f, _wallNormal.z);
                            _planarNormal.Normalize();
                            _lastHitPoint = _frontHit.point;

                            if (_drawGizmos)
                            {
                                Debug.DrawRay(_frontHit.point, _wallNormal * 0.4f, Color.cyan, 0.02f, false);
                                Debug.DrawRay(_frontHit.point, _planarNormal * 0.4f, Color.yellow, 0.02f, false);
                            }
                        }
                    }
                }
            }

            // Pequeño “grace” si perdimos la pared un frame
            if (!_hasFront && !forceFirst)
            {
                Vector3 ahead = origin + fwd * Mathf.Max(0.05f, _climbSphereRadius * 0.5f);
                var cols = Physics.OverlapSphere(ahead, _climbSphereRadius * 0.9f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var c in cols)
                {
                    if (!c || !c.CompareTag(_m.tagClimb)) continue;
                    Vector3 p = c.ClosestPoint(origin);
                    Vector3 n = (origin - p).sqrMagnitude > 1e-6f ? (origin - p).normalized : -fwd;

                    float slopeDeg = Vector3.Angle(n, Vector3.up);
                    float lookAng = Vector3.Angle(fwd, -n);

                    if (slopeDeg >= _climbMinWallSlopeDeg && lookAng <= _climbMaxLookAngle)
                    {
                        _hasFront = true;
                        _wallNormal = n;
                        _planarNormal = Vector3.ProjectOnPlane(n, Vector3.up).normalized;
                        _lastHitPoint = p;
                        break;
                    }
                }
            }
        }

        void ExitToNext()
        {
            _fsm.ChangeState(_m.IsGroundedNow() ? FSM.States.Grounded : FSM.States.Air);
        }
    }
}