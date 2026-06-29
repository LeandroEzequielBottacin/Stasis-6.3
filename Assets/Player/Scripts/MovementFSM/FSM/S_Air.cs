using System;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Air : IState
    {
        private readonly FSM _fsm;

        private readonly Model _model;

        private readonly Transform _moveBasis;

        private bool _enteredFromGround;

        private float _airTime;

        private float _gravity = 9.81f;

        public S_Air(
            FSM fsm,
            Model model,
            Transform camHolder)
        {
            _fsm = fsm;
            _model = model;
            _moveBasis = camHolder;
        }

        public void OnEnter()
        {
            if (!_model.blockUseGravity)
            {
                _model.rb.useGravity = true;
            }

            _model.OnJump += OnJumpPressed;

            _model.StairStepper?.CancelStep();

            _enteredFromGround =
                _model.airEnteredFromGround;

            _model.airEnteredFromGround = false;

            _airTime = 0f;
        }

        public void OnUpdate()
        {
            _airTime += Time.deltaTime;

            TryConsumeCoyoteOrBuffer();

            var p = _model.probe;

            if (Time.time >= _model.blockWallrunUntil &&
                Time.time >= _model.blockClimbUntil)
            {
                if (p.action == ParkourAction.Climb &&
                    _model.zAxis > 0.1f)
                {
                    _model.ClearJumpBuffer();

                    _fsm.ChangeState(FSM.States.Climb);

                    return;
                }

                bool wantsWall =
                    p.action == ParkourAction.WallrunLeft ||
                    p.action == ParkourAction.WallrunRight;

                if (wantsWall)
                {
                    _model.ClearJumpBuffer();

                    _fsm.ChangeState(FSM.States.Wallrun);

                    return;
                }
            }

            if (_model.IsGroundedNow())
            {
                if (_airTime < _model.minAirTime)
                    return;

                _model.airEnteredFromGround = false;

                _model.landedPending = true;

                _fsm.ChangeState(FSM.States.Grounded);
            }
        }

        public void OnFixedUpdate()
        {
            if (!_model.canMove)
                return;

            Vector2 input =
                new Vector2(
                    _model.xAxis,
                    _model.zAxis
                );

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            Vector3 f =
                (_moveBasis
                    ? _moveBasis.forward
                    : _model.transform.forward);

            f.y = 0f;

            f = f.sqrMagnitude > 0f
                ? f.normalized
                : Vector3.forward;

            Vector3 r =
                (_moveBasis
                    ? _moveBasis.right
                    : _model.transform.right);

            r.y = 0f;

            r = r.sqrMagnitude > 0f
                ? r.normalized
                : Vector3.right;

            Vector3 wishDir =
                (r * input.x + f * input.y);

            // =====================================
            // VELOCIDAD RELATIVA
            // =====================================

            Vector3 external =
                _model.externalVelocity;

            Vector3 horizVel =
                new Vector3(
                    _model.rb.linearVelocity.x,
                    0f,
                    _model.rb.linearVelocity.z
                ) - external;

            Vector3 targetVel =
                wishDir * _model.airMaxSpeed;

            Vector3 delta =
                targetVel - horizVel;

            float maxDelta =
                _model.airAcceleration *
                Time.fixedDeltaTime;

            Vector3 add =
                Vector3.ClampMagnitude(
                    delta,
                    maxDelta
                );

            _model.rb.AddForce(
                new Vector3(add.x, 0f, add.z),
                ForceMode.VelocityChange
            );

            // =====================================
            // CLAMP RELATIVO
            // =====================================

            Vector3 relativeVelocity =
                new Vector3(
                    _model.rb.linearVelocity.x,
                    0f,
                    _model.rb.linearVelocity.z
                ) - external;

            relativeVelocity =
                Vector3.ClampMagnitude(
                    relativeVelocity,
                    _model.airMaxSpeed
                );

            Vector3 finalVelocity =
                relativeVelocity + external;

            _model.rb.linearVelocity =
                new Vector3(
                    finalVelocity.x,
                    _model.rb.linearVelocity.y,
                    finalVelocity.z
                );

            // =====================================
            // GRAVEDAD EXTRA
            // =====================================

            _model.rb.AddForce(
                Vector3.down * _gravity,
                ForceMode.Acceleration
            );
        }

        public void OnExit()
        {
            _model.OnJump -= OnJumpPressed;
        }

        public void OnLateUpdate()
        {
            throw new NotImplementedException();
        }

        private void OnJumpPressed(bool a)
        {
            _model.BufferJumpNow();
        }

        private void TryConsumeCoyoteOrBuffer()
        {
            bool coyoteValid =
                _enteredFromGround &&
                (Time.time - _model.lastLeftGroundTime)
                <= _model.coyoteTime;

            bool pressedNow =
                _model.jumpDownThisFrame;

            bool bufferedNow =
                _model.HasJumpBufferedAfterLeftGround();

            bool inputOk =
                pressedNow || bufferedNow;

            if (!(coyoteValid && inputOk))
                return;

            PerformAirJump();

            _model.ClearJumpBuffer();

            _enteredFromGround = false;
        }

        private void PerformAirJump()
        {
            float g =
                Physics.gravity.y;

            float h =
                Mathf.Max(
                    0.01f,
                    _model.jumpHeight
                );

            float jumpVel =
                Mathf.Sqrt(
                    2f * Mathf.Abs(g) * h
                );

            var vel =
                _model.rb.linearVelocity;

            vel.y = jumpVel;

            _model.rb.linearVelocity = vel;

            _airTime = 0f;
        }
    }
}