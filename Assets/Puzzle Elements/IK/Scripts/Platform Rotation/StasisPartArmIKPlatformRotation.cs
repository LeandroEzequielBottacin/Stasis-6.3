using Player.Stasis;
using Puzzle_Elements.IK_OBJECT.Scripts;
using UnityEngine;

namespace Puzzle_Elements.IK.Scripts.Platform_Rotation
{
    public class StasisPartArmIKPlatformRotation : MonoBehaviour, IStasis, IStasisPartIK
    {
        public bool IsFreezed => _isFreezed;
        private bool _isFreezed = false;
        [SerializeField] private StasisTipControllerPlatformRotation _stasisTipControllerPlatformRotation;
        public StasisEffect StasisEffect { get; }


        // Start is called before the first frame update
        void Start()
        {
            if(_stasisTipControllerPlatformRotation == null)
            {
                _stasisTipControllerPlatformRotation = GetComponentInParent<StasisTipControllerPlatformRotation>();
            }
            _stasisTipControllerPlatformRotation.OnFreezeEvent += StatisEffectActivate;
            _stasisTipControllerPlatformRotation.OnUnFreezeEvent += StatisEffectDeactivate;
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
            if (a)
            {
                _stasisTipControllerPlatformRotation.StatisEffectActivate();
            }
            else
            {
                _stasisTipControllerPlatformRotation.StatisEffectDeactivate();
            }
        }

        public void SetTipController(Component tipController)
        {
            var a = tipController.GetComponent<StasisTipControllerPlatformRotation>();
            if (a != null)
            {
                _stasisTipControllerPlatformRotation = a;
            }
        }
    }
}
