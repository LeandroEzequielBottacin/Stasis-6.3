using System;
using UnityEngine;

namespace Audio.Scripts.Player
{
    public class PlayerAnimationAudioEvents : MonoBehaviour, ISoundPlayer
    {
        public event Action OnStepLeft;
        public event Action OnStepRight;

        private global::Player.Scripts.MovementFSM.MVC.Model _playerModel;

        private void Awake()
        {
            _playerModel = GetComponentInParent<global::Player.Scripts.MovementFSM.MVC.Model>();
        }

        [Header("Distance Settings")]
        public float walkStepDistance = 1.6f;
        public float runStepDistance = 2.2f;
        public float minSpeedToStep = 0.5f;

        private float _distanceMoved;
        private bool _isRightFoot;

        private void Update()
        {
            if (!_playerModel) return;

            if (_playerModel.IsGroundedNow())
            {
                Vector3 horizVel = _playerModel.rb.linearVelocity;
                horizVel.y = 0;
                float speed = horizVel.magnitude;

                if (speed > minSpeedToStep)
                {
                    _distanceMoved += speed * Time.deltaTime;

                    float threshold = _playerModel.isRunningRuntime ? runStepDistance : walkStepDistance;

                    if (_distanceMoved >= threshold)
                    {
                        _distanceMoved = 0f;
                        
                        if (_isRightFoot) OnStepRight?.Invoke();
                        else OnStepLeft?.Invoke();

                        _isRightFoot = !_isRightFoot;
                    }
                }
                else
                {
                    // Resetear si se detiene, para que el próximo arranque arranque rápido
                    _distanceMoved = walkStepDistance * 0.5f; 
                }
            }
        }

        // Dejo los métodos de evento de animación vacíos por si el Animator los sigue llamando
        // así no tira error de "Animation Event has no receiver".
        public void Anim_StepLeft()  { }
        public void Anim_StepRight() { }
    }
}