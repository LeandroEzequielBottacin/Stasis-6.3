using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Sistema_tren.Scripts.Tren
{
    public class StasisPartTrain : MonoBehaviour, IStasis
    {
        public bool _isFreezed;
        public bool IsFreezed => _isFreezed;
        public StasisEffect StasisEffect { get; }
        private StasisTrain _train;


        private void Awake()
        {
            _train = GetComponentInParent<StasisTrain>();
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
                _train.StatisEffectActivate();
                _isFreezed = true;
            }
        
        }

        private void UnfreezeObject()
        {
            if (_isFreezed)
            {
                _train.StatisEffectDeactivate();
                _isFreezed = false;
            }
        
        }
    }
}


