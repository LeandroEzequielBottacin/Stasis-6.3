using System;
using System.Collections.Generic;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Grounded : IState
    {
        private readonly FSM _fsm;
        private readonly Model _model;
        private readonly Transform _moveBasis;

        private readonly List<Func<float>> _speedOverrides = new();

        private Ray _moveCheckRay;

        private int _lastJumpFrame = -1000;
        private readonly float _moveCheckDist = 0.75f;
        private LayerMask _moveCheckMask;
        private bool _wasGrounded;

        private bool _isRunning, _isStopping, _isStunned;

        public S_Grounded(FSM fsm, Model model, Transform camHolder)
        {
            _fsm = fsm;
            _model = model;
            _moveBasis = camHolder;
        }

        public void OnEnter()
        {
            _model.OnJump += OnJumpPressed;

            _moveCheckMask = _model.wallMask;

            if (_model.HasJumpBuffered())
            {
                _model.ClearJumpBuffer();
                PerformJumpAndGoAir();
                return;
            }

            if (_model.landedPending)
            {
                _model.lastLandingTime = Time.time;
                _model.landedPending = false;
            }

            _wasGrounded = _model.IsGroundedNow();
        }

        public void OnUpdate()
        {
            HandleStoppingLogic();

            bool grounded = _model.IsGroundedNow();

            if (!grounded && _wasGrounded &&
                (!_model.StairStepper || !_model.StairStepper.IsStepping))
            {
                _model.lastLeftGroundTime = Time.time;
                _model.airEnteredFromGround = true;
                _fsm.ChangeState(FSM.States.Air);
                _wasGrounded = false;
                return;
            }

            _wasGrounded = grounded;
        }

        public void OnFixedUpdate()
        {
            if (_model.StairStepper && _model.StairStepper.IsStepping)
                return;

            // =====================================================
            //  FIX MOVIMIENTO DEL TREN (SIN PATINAJE)
            // =====================================================

            Vector3 trainVel = Vector3.zero;

            if (_model.trainSystem != null)
            {
                trainVel = _model.trainSystem.GetTrainVelocity();
                trainVel.y = 0f; // CLAVE: solo plano
            }

            _model.externalVelocity = trainVel;

            ApplyGroundStickAndSnap();

            if (_model.canMove)
            {
                HandleRunning();
                HandleMovement();
            }
            else
            {
                Vector3 ext = _model.externalVelocity;

                _model.rb.linearVelocity = new Vector3(
                    ext.x,
                    _model.rb.linearVelocity.y,
                    ext.z
                );
            }
        }

        public void OnExit()
        {
            _model.OnJump -= OnJumpPressed;

            if (_model.rb && !_model.blockUseGravity)
                _model.rb.useGravity = true;
        }

        public void OnLateUpdate() { }

        // ========================= MOVEMENT =========================

        private void HandleMovement()
        {
            _isStunned = Time.time < _model.speedCapUntil;

            float targetSpeed;

            if (_isStunned)
                targetSpeed = _model.hardLandSpeedCap;
            else
                targetSpeed = _model.runningKeyPressed
                    ? _model.runningSpeed
                    : _model.walkingSpeed;

            if (_speedOverrides.Count > 0)
                targetSpeed = _speedOverrides[^1]();

            Vector2 input = new Vector2(_model.xAxis, _model.zAxis);

            if (input.magnitude > 1f)
                input.Normalize();

            GetPlanarBasis(out var f, out var r);

            Vector3 moveDir = (r * input.x + f * input.y);

            if (input.magnitude > 0 && !IsBlocked(moveDir))
                ApplyAcceleration(moveDir, targetSpeed);
            else
                ApplyDeceleration();

            ClampVelocity(targetSpeed);
        }

        private void HandleRunning()
        {
            _isStunned = Time.time < _model.speedCapUntil;

            _isRunning =
                _model.canRun &&
                _model.runningKeyPressed &&
                (Mathf.Abs(_model.xAxis) > 0.1f ||
                 Mathf.Abs(_model.zAxis) > 0.1f) &&
                !_isStunned;

            _model.UpdateIsRunning(_isRunning);
        }

        // ========================= PHYSICS =========================

        private void ApplyAcceleration(Vector3 direction, float targetSpeed)
        {
            Vector3 external = _model.externalVelocity;

            Vector3 current =
                new Vector3(_model.rb.linearVelocity.x, 0, _model.rb.linearVelocity.z) - external;

            Vector3 target = direction * targetSpeed;

            Vector3 delta = target - current;

            delta = Vector3.ClampMagnitude(
                delta,
                _model.acceleration * Time.fixedDeltaTime
            );

            _model.rb.AddForce(delta, ForceMode.VelocityChange);
        }

        private void ApplyDeceleration()
        {
            Vector3 external = _model.externalVelocity;

            Vector3 relative =
                new Vector3(_model.rb.linearVelocity.x, 0, _model.rb.linearVelocity.z) - external;

            Vector3 decel =
                -relative * (_model.deceleration * Time.fixedDeltaTime);

            _model.rb.AddForce(decel, ForceMode.VelocityChange);

            if (relative.magnitude < 0.1f)
            {
                _model.rb.linearVelocity = new Vector3(
                    external.x,
                    _model.rb.linearVelocity.y,
                    external.z
                );
            }
        }

        private void ClampVelocity(float maxSpeed)
        {
            Vector3 external = _model.externalVelocity;

            Vector3 relative =
                new Vector3(_model.rb.linearVelocity.x, 0, _model.rb.linearVelocity.z) - external;

            relative = Vector3.ClampMagnitude(relative, maxSpeed);

            Vector3 final = relative + external;

            _model.rb.linearVelocity = new Vector3(
                final.x,
                _model.rb.linearVelocity.y,
                final.z
            );
        }

        // ========================= INPUT =========================

        private void OnJumpPressed(bool b)
        {
            _model.BufferJumpNow();

            if (!_model.canMove)
                return;

            PerformJumpAndGoAir();
        }

        private void PerformJumpAndGoAir()
        {
            if (Time.frameCount == _lastJumpFrame)
                return;

            _lastJumpFrame = Time.frameCount;

            _model.StairStepper?.CancelStep();

            float g = Physics.gravity.y;
            float h = Mathf.Max(0.01f, _model.jumpHeight);

            float jumpVel = Mathf.Sqrt(2f * Mathf.Abs(g) * h);

            var vel = _model.rb.linearVelocity;
            vel.y = jumpVel;

            _model.rb.linearVelocity = vel;

            _model.groundedIgnoreUntil = Time.time + _model.groundedIgnoreAfterJump;

            _model.lastLeftGroundTime = Time.time;
            _model.airEnteredFromGround = false;

            _fsm.ChangeState(FSM.States.Air);

            _model.JumpSucceed();
        }

        // ========================= UTILS =========================

        private void GetPlanarBasis(out Vector3 f, out Vector3 r)
        {
            Transform basis = _moveBasis ? _moveBasis : _model.transform;

            f = basis.forward;
            f.y = 0f;
            f = f.sqrMagnitude > 0f ? f.normalized : Vector3.forward;

            r = basis.right;
            r.y = 0f;
            r = r.sqrMagnitude > 0f ? r.normalized : Vector3.right;
        }

        private bool IsBlocked(Vector3 dir)
        {
            Vector3 origin = _model.transform.position + Vector3.up * 0.1f;

            _moveCheckRay = new Ray(origin, dir);

            return Physics.Raycast(_moveCheckRay, _moveCheckDist, _moveCheckMask);
        }

        private void HandleStoppingLogic()
        {
            float raw = new Vector2(_model.rawX, _model.rawZ).magnitude;

            bool zero = raw < _model.stopThreshold;
            bool had = _model.wasMovingByInput;
            bool now = raw > _model.moveThreshold;

            if (had && zero && _isStopping)
            {
                _isStopping = true;
                _model.stopTimer = _model.stopCooldown;
            }

            if (_isStopping)
            {
                _model.stopTimer -= Time.deltaTime;

                if (_model.stopTimer <= 0f)
                    _isStopping = false;
            }

            _model.wasMovingByInput = now;
            _model.UpdateStopping(_isStopping);
        }

        private void ApplyGroundStickAndSnap()
        {
            var scanner = _model.Scanner;

            if (!scanner ||
                !_model.IsGroundedNow() ||
                Time.time < _model.groundedIgnoreUntil)
            {
                if (!_model.rb.useGravity && !_model.blockUseGravity)
                    _model.rb.useGravity = true;

                return;
            }

            float slope = scanner.CurrentGroundSlopeDeg;

            bool allowSlide = slope >= _model.slideFromSlopeDeg;

            if (!allowSlide)
            {
                if (_model.rb.useGravity)
                    _model.rb.useGravity = false;

                if (_model.rb.linearVelocity.y < 0.1f)
                {
                    var v = _model.rb.linearVelocity;
                    v.y = 0;
                    _model.rb.linearVelocity = v;
                }
            }
            else
            {
                if (!_model.rb.useGravity && !_model.blockUseGravity)
                    _model.rb.useGravity = true;
            }
        }
    }
}