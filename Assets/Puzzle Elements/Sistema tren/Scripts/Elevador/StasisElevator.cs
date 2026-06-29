using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Sistema_tren.Scripts.Elevador
{
    public class StasisElevator : MonoBehaviour, IStasis
    {
        private bool _isFreezed;
        public bool IsFreezed => _isFreezed;
        private Renderer _rend;
        private float _saveVelocity;
        private TrainSystem _trainSystem;
        [SerializeField] private StasisPartElevator _stasisPartElevator;
        public StasisEffect StasisEffect { get; private set; }

        private void Awake()
        {
            _rend = GetComponent<Renderer>();
            StasisEffect = new StasisEffect(_rend);
            _trainSystem = GetComponentInParent<TrainSystem>();
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
                _saveVelocity = _trainSystem.elevatorSpeed;
                _trainSystem.elevatorRb.isKinematic = false;
                _trainSystem.elevatorSpeed = 0;
                _trainSystem.elevatorRb.isKinematic = true;

                _isFreezed = true;
                _stasisPartElevator._isFreezed = true;
                //splineAnimate.Pause();
                StasisEffect.StasisEffectStart();
            }
        }

        private void UnfreezeObject()
        {
            if (!_isFreezed) return;
            _isFreezed = false;
            _stasisPartElevator._isFreezed = false;
            _trainSystem.elevatorRb.isKinematic = true;
            _trainSystem.elevatorSpeed = _saveVelocity;
            //splineAnimate.Play();
            StasisEffect.StasisEffectStop();
        }
    }
}


