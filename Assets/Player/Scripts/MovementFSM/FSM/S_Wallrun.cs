using DG.Tweening;
using Player.Scripts.MovementFSM.MVC;
using Unity.Cinemachine;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Wallrun : IState
    {
        private readonly FSM _fsm;
        private readonly Model _model;
        private Rigidbody _rb;
        private Transform _orient;
        private Vector3 _wallNormal;
        private int _side;
        private float _timer;
        private bool _exiting;
        private float _exitTimer;
        private float _enterTime;
        private Vector3 _lastWallPoint;
        private Vector3 _smoothNormal;
        private float _seamHoldTimer;
        private Tween _reorientTween;
        private string _originalPanTiltInputName;
        private bool _hasAppliedInitialUpBoost;
        private Transform _visualRoot;

        public S_Wallrun(FSM fsm, Model model)
        {
            _fsm = fsm;
            _model = model;
        }

        public void OnEnter()
        {
            _rb = _model.rb;
            _orient = _model.cameraHolderTransform ? _model.cameraHolderTransform : _model.transform;

            var p = _model.probe;
            ReadProbe(p);
            _lastWallPoint = p.wallRunWallPoint;

            _smoothNormal = _wallNormal;
            _seamHoldTimer = 0f;
            _hasAppliedInitialUpBoost = false;

            // Get visual root from VisualYawFollower if available and disable it during wallrun
            var visualYawFollower = _model.GetComponentInChildren<VisualYawFollower>();
            if (visualYawFollower != null)
            {
                if (visualYawFollower.visualRoot != null)
                {
                    _visualRoot = visualYawFollower.visualRoot;
                }

                // Disable VisualYawFollower during wallrun so we can manually control rotation
                visualYawFollower.followEnabled = false;
            }

            _model.WallrunStartEvent(_side);

            _timer = _model.maxWallRunTime;
            _exiting = false;
            _enterTime = Time.time;

            _reorientTween?.Kill();
            AdjustCameraOnEnter();
            ClampInitialWallrunSpeed();

            // Check if at bottom of wall and apply upward boost
            CheckAndApplyInitialUpBoost();
        }


        public void OnUpdate()
        {
            if (_model.IsGroundedNow())
            {
                _fsm.ChangeState(FSM.States.Grounded);
                return;
            }

            if (_model.zAxis < _model.wallInputThreshold)
            {
                _fsm.ChangeState(FSM.States.Air);
                return;
            }

            var p = _model.probe;

            if (p.action == ParkourAction.Climb && _model.jumpDownThisFrame)
            {
                _fsm.ChangeState(FSM.States.Climb);
                return;
            }

            bool hasWall = (p.action == ParkourAction.WallrunLeft || p.action == ParkourAction.WallrunRight);
            if (!hasWall)
            {
                _seamHoldTimer += Time.deltaTime;
                if (_seamHoldTimer > _model.wallSeamHold)
                {
                    _fsm.ChangeState(FSM.States.Air);
                    return;
                }
            }
            else
            {
                Vector3 oldNormal = _wallNormal;

                ReadProbe(p);
                _lastWallPoint = p.wallRunWallPoint;

                if (Vector3.Dot(oldNormal, _wallNormal) < 0.707f)
                {
                    _fsm.ChangeState(FSM.States.Air);
                    return;
                }
            }

            if (!_exiting)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    _exiting = true;
                    _exitTimer = _model.exitWallTime;
                }
            }
            else
            {
                _exitTimer -= Time.deltaTime;
                if (_exitTimer <= 0f) _exiting = false;
            }

            if (_model.jumpDownThisFrame && !_exiting)
            {
                WallJump();
                _fsm.ChangeState(FSM.States.Air);
            }
        }

        public void OnFixedUpdate()
        {
            if (_exiting) return;

            var p = _model.probe;
            bool hasWall = (p.action == ParkourAction.WallrunLeft || p.action == ParkourAction.WallrunRight);

            if (!hasWall && _seamHoldTimer > _model.wallSeamHold)
            {
                _fsm.ChangeState(FSM.States.Air);
                return;
            }

            WallrunForces();
        }


        public void OnExit()
        {
            _reorientTween?.Kill();

            var visualYawFollower = _model.GetComponentInChildren<VisualYawFollower>();
            if (visualYawFollower)
            {
                visualYawFollower.followEnabled = true;
            }

            if (Time.time > _model.wallJustJumpedUntil)
            {
                float until = Time.time + _model.wallPostJumpNoRegrab;
                _model.blockWallrunUntil = until;
            }

            _model.lastWallDetachTime = Time.time;
            _model.WallrunEndEvent();
        }

        public void OnLateUpdate()
        {
            if (_reorientTween != null)
            {
                return;
            }

            AdjustCameraOnUpdate();
        }

        private void ReadProbe(in ParkourProbe p)
        {
            if (p.action is ParkourAction.WallrunRight or ParkourAction.WallrunLeft)
            {
                _wallNormal = p.wallRunNormal;
                _smoothNormal = Vector3.Slerp(
                    _smoothNormal == Vector3.zero ? p.wallRunNormal : _smoothNormal,
                    p.wallRunNormal,
                    1f - Mathf.Exp(-_model.wallNormalSmooth * Time.deltaTime)
                );
                _side = p.wallSide;
                _model.lastWallCollider = p.wallRunCollider;
                _model.lastWallNormal = p.wallRunNormal;

                _seamHoldTimer = 0f;
            }
        }


        float EnterBlend01()
        {
            return Mathf.Clamp01((Time.time - _enterTime) / Mathf.Max(0.01f, _model.wallEnterBlendTime));
        }

        private void WallrunForces()
        {
            // Usar normal suavizada
            Vector3 n = (_smoothNormal == Vector3.zero) ? _wallNormal : _smoothNormal;

            // 1) Tangente
            Vector3 wallForward = Vector3.Cross(n, Vector3.up);
            Vector3 fwd = _orient ? _orient.forward : _model.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = _model.transform.forward;
            fwd.Normalize();
            if ((fwd - wallForward).sqrMagnitude > (fwd + wallForward).sqrMagnitude) wallForward = -wallForward;

            float alignT = 1f - Mathf.Exp(-_model.wallAlignLerp * Time.fixedDeltaTime);

            Vector3 vHoriz = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            if (vHoriz.sqrMagnitude > 1e-6f)
            {
                Vector3 newDir = Vector3.Slerp(vHoriz.normalized, wallForward, alignT);
                vHoriz = newDir * vHoriz.magnitude;
            }

            // --- LÓGICA DE INPUT RESTAURADA ---
            // Comprueba si el jugador está presionando 'W'
            bool pushingForward = _model.zAxis > _model.wallInputThreshold;

            float curAlong = Vector3.Dot(vHoriz, wallForward);

            // Si presionamos 'W', el objetivo es la velocidad máxima.
            // (Como OnUpdate ya nos saca si soltamos W, 'pushingForward' casi siempre será 'true' aquí,
            // pero esta lógica es más robusta si decides cambiarlo a "deslizar" en lugar de "salir")
            float targetAlong = pushingForward ? _model.wallRunMaxSpeed : Mathf.Max(_model.wallCruiseSpeed, curAlong);

            // Si presionamos 'W', usamos la aceleración de 'run'.
            float accel = pushingForward ? _model.wallRunAccel : _model.wallCruiseAccel;

            float delta = Mathf.Clamp(targetAlong - curAlong, -accel * Time.fixedDeltaTime,
                accel * Time.fixedDeltaTime);
            // --- FIN DE LA LÓGICA RESTAURADA ---

            Vector3 addAlong = wallForward * delta;
            _rb.AddForce(addAlong, ForceMode.VelocityChange);

            // Distancia al plano (con normal suavizada)
            Vector3 fromWall = _rb.position - _lastWallPoint;
            float dist = Vector3.Dot(fromWall, -n);
            float err = (_model.wallStandOff - dist);
            float vInto = Vector3.Dot(_rb.linearVelocity, -n);

            float blend = EnterBlend01();
            float stick = blend * _model.wallStickKp * err - _model.wallStickKd * vInto;
            _rb.AddForce(-n * stick, ForceMode.Force);

            float vy = _rb.linearVelocity.y;

            if (_model.wallUseGravity)
            {
                float gravityCounter = _model.gravityCounterForce;
                if (blend < 1f)
                {
                    gravityCounter *= Mathf.Lerp(0.7f, 1f, blend);
                }

                _rb.AddForce(Vector3.up * gravityCounter, ForceMode.Force);
            }

            float targetGlideSpeed = !pushingForward ? -_model.wallGlideDownSpeed : 0f;

            if (blend < 1f && !pushingForward)
            {
                targetGlideSpeed = Mathf.Lerp(vy, -_model.wallGlideDownSpeed, blend);
            }
            else if (blend < 1f && pushingForward)
            {
                targetGlideSpeed = Mathf.Lerp(vy, 0f, blend);
            }

            vy = Mathf.MoveTowards(vy, targetGlideSpeed, 8f * Time.fixedDeltaTime);

            _rb.linearVelocity = new Vector3(vHoriz.x + addAlong.x, vy, vHoriz.z + addAlong.z);

            float into = Vector3.Dot(_rb.linearVelocity, -n);
            if (into > 0f)
            {
                float remove = Mathf.Min(into, _model.wallIntoDamp * Time.fixedDeltaTime);
                _rb.linearVelocity -= n * remove;
            }
        }

        private void WallJump()
        {
            _exiting = true;
            _exitTimer = _model.exitWallTime;

            float until = Time.time + _model.wallPostJumpNoRegrab;
            _model.blockWallrunUntil = until;
            _model.wallJustJumpedUntil = until;
            _model.wallSeamDisableUntil = until;
            _model.lastWallDetachTime = Time.time;

            Vector3 v = _rb.linearVelocity;
            float into = Vector3.Dot(v, -_wallNormal);
            if (into > 0f) v -= _wallNormal * into;
            v.y = 0f;
            _rb.linearVelocity = v;

            Vector3 velChange = Vector3.up * _model.wallJumpUpForce + _wallNormal * _model.wallJumpSideForce;
            _rb.AddForce(velChange, ForceMode.VelocityChange);
        }

        private void AdjustCameraOnEnter()
        {
            Vector3 wallForward = Vector3.Cross(_wallNormal, Vector3.up);
            Vector3 playerLookDirection = new Vector3(_orient.forward.x, 0f, _orient.forward.z).normalized;
            if (playerLookDirection.sqrMagnitude < 0.1f)
            {
                playerLookDirection = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).normalized;
            }

            if (playerLookDirection.sqrMagnitude > 0.1f && Vector3.Dot(playerLookDirection, wallForward) < 0f)
            {
                wallForward = -wallForward;
            }

            float targetYaw = Quaternion.LookRotation(wallForward, Vector3.up).eulerAngles.y;

            if (!_model.cinemachineBrain) return;
            var vcam = _model.cinemachineBrain.ActiveVirtualCamera as CinemachineCamera;
            if (!vcam) return;
            var panTilt = vcam.GetCinemachineComponent(CinemachineCore.Stage.Aim) as CinemachinePanTilt;
            if (!panTilt) return;

            float startYaw = panTilt.PanAxis.Value;
            float percent = 0f;

            _reorientTween?.Kill();

            _reorientTween = DOTween.To(
                    () => percent,
                    x => percent = x,
                    1f,
                    _model.wallReorientDuration)
                .SetEase(Ease.OutCubic)
                .OnUpdate(() =>
                {
                    float currentYaw = Mathf.LerpAngle(startYaw, targetYaw, percent);
                    panTilt.PanAxis.Value = currentYaw;
                })
                .OnComplete(() => { _reorientTween = null; })
                .OnKill(() => { _reorientTween = null; });
        }

        private void AdjustCameraOnUpdate()
        {
            if (!_model.cinemachineBrain) return;
            var vcam = _model.cinemachineBrain.ActiveVirtualCamera as CinemachineCamera;
            if (!vcam) return;
            var panTilt = vcam.GetCinemachineComponent(CinemachineCore.Stage.Aim) as CinemachinePanTilt;
            if (!panTilt) return;


            Vector3 n = (_smoothNormal == Vector3.zero) ? _wallNormal : _smoothNormal;
            if (n.sqrMagnitude < 0.1f) return;

            Vector3 wallForward = Vector3.Cross(n, Vector3.up);

            Vector3 playerLookDirection = new Vector3(_orient.forward.x, 0f, _orient.forward.z).normalized;
            if (playerLookDirection.sqrMagnitude < 0.1f)
            {
                playerLookDirection = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).normalized;
            }

            if (playerLookDirection.sqrMagnitude > 0.1f && Vector3.Dot(playerLookDirection, wallForward) < 0f)
            {
                wallForward = -wallForward;
            }

            float targetYaw = Quaternion.LookRotation(wallForward, Vector3.up).eulerAngles.y;

            float currentYaw = panTilt.PanAxis.Value;

            float blend = 1f - Mathf.Exp(-_model.wallCameraLerpSpeed * Time.deltaTime);

            panTilt.PanAxis.Value = Mathf.LerpAngle(currentYaw, targetYaw, blend);

            // Also rotate the visual model to follow the wall direction
            if (_visualRoot != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(wallForward, Vector3.up);
                float visualBlend = 1f - Mathf.Exp(-_model.wallCameraLerpSpeed * Time.deltaTime);
                _visualRoot.rotation = Quaternion.Slerp(_visualRoot.rotation, targetRotation, visualBlend);
            }
        }

        private void ClampInitialWallrunSpeed()
        {
            Vector3 vHoriz = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

            float vVert = _rb.linearVelocity.y;

            Vector3 wallForward = Vector3.Cross(_wallNormal, Vector3.up);

            if (vHoriz.sqrMagnitude > 0.1f && Vector3.Dot(vHoriz, wallForward) < 0f)
            {
                wallForward = -wallForward;
            }

            float speedAlongWall = Vector3.Dot(vHoriz, wallForward);

            float clampedSpeedAlong = Mathf.Clamp(speedAlongWall, 0f, _model.wallRunMaxSpeed);

            _rb.linearVelocity = (wallForward * clampedSpeedAlong) + (Vector3.up * vVert);
        }

        private void CheckAndApplyInitialUpBoost()
        {
            if (_hasAppliedInitialUpBoost) return;

            // Check if we're at the bottom of the wall (low vertical velocity or near ground)
            float vy = _rb.linearVelocity.y;
            bool isNearBottom = vy < 0.5f; // Low or negative vertical velocity

            // Also check if we're close to ground
            if (_model.IsGroundedNow())
            {
                isNearBottom = false; // Don't boost if already on ground
            }
            else
            {
                // Raycast down to check distance to ground
                if (Physics.Raycast(_rb.position, Vector3.down, out var hit, 2f, _model.groundMask))
                {
                    float distToGround = hit.distance;
                    // If very close to ground, we're at the bottom
                    if (distToGround < 0.5f)
                    {
                        isNearBottom = true;
                    }
                }
            }

            if (isNearBottom)
            {
                // Apply upward boost
                float upBoost = 2.5f; // Upward velocity boost
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, _rb.linearVelocity.y + upBoost, _rb.linearVelocity.z);
                _hasAppliedInitialUpBoost = true;
            }
        }
    }
}