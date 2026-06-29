using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Sistema_tren.Scripts.Elevador
{
    public class StasisPartElevator : MonoBehaviour, IStasis
    {
        public bool _isFreezed;
        public bool IsFreezed => _isFreezed;
        public StasisEffect StasisEffect { get; }

        private StasisElevator _elevator;


        private void Awake()
        {
            _elevator = GetComponentInParent<StasisElevator>();
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
                _elevator.StatisEffectActivate();
            }
        }

        private void UnfreezeObject()
        {

            if (_isFreezed)
            {
                _elevator.StatisEffectDeactivate();
                _isFreezed = false;
            }
        }
    }
}


