using Player.Stasis;
using Puzzle_Elements.IK_OBJECT.Scripts;
using UnityEngine;

namespace Puzzle_Elements.IK.Scripts
{
    public class StasisPartArmIK : MonoBehaviour,IStasis,IStasisPartIK
    {
        public bool IsFreezed => _isFreezed;
        public StasisEffect StasisEffect { get; }
        [SerializeField]private bool _isFreezed;
        [SerializeField] private StasisTipController _stasisTipController;
    
        void Start()
        {
            //if(_stasisTipController )
            //{
            //    _stasisTipController.OnFreezeEvent += StatisEffectActivate;
            //    _stasisTipController.OnUnFreezeEvent += StatisEffectDeactivate;
            //}
        }

        public void StatisEffectActivate()
        {
            NotifyTipController(true);
        }

        public void StatisEffectDeactivate()
        {
            NotifyTipController(false);
        }

        public void NotifyTipController(bool a)
        {
            if(a)
            {
                _stasisTipController.StatisEffectActivate();
            }
            else
            {
                _stasisTipController.StatisEffectDeactivate();
            }
        }

        public void SetTipController(Component tipController)
        {
            var a = tipController.GetComponent<StasisTipController>();
            if(a!= null)
            {
                _stasisTipController = a;
            }
        
        }
    }
}
