using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Elevador
{
    public class StasisElevatorSingle : MonoBehaviour,IStasis
    {
        private bool _isFreezed;
        public bool IsFreezed => _isFreezed;
    
        private Renderer _rend;
        private float _saveVelocity;
        private ElevatorPlatform _elevatorPlatform;
        public StasisEffect StasisEffect { get; set; }

        private void Awake()
        {
            _rend = GetComponent<Renderer>();
            _elevatorPlatform = GetComponentInParent<ElevatorPlatform>();
            StasisEffect = new StasisEffect(_rend);
        }

        public void StatisEffectActivate()
        {
            FreezeObject();
        }

        public void StatisEffectDeactivate()
        {
            UnfreezeObject();
        }

        private void FreezeObject()
        {
            if (!_isFreezed)
            {
                _saveVelocity = _elevatorPlatform.elevatorSpeed;
                _elevatorPlatform.elevatorSpeed = 0;
            
                _isFreezed = true;
                //splineAnimate.Pause();
                StasisEffect.StasisEffectStart();
            }
        }

        private void UnfreezeObject()
        {
            if (!_isFreezed) return;
            _isFreezed = false;
            _elevatorPlatform.elevatorSpeed = _saveVelocity;
            //splineAnimate.Play();
            StasisEffect.StasisEffectStop();
        }
    }
}
