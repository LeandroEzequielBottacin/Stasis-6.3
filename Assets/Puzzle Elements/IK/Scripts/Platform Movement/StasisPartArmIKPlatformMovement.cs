using Player.Stasis;
using Puzzle_Elements.IK_OBJECT.Scripts;
using UnityEngine;

namespace Puzzle_Elements.IK.Scripts.Platform_Movement
{
    public class StasisPartArmIKPlatformMovement : MonoBehaviour,IStasis,IStasisPartIK
    {
        public bool IsFreezed => _isFreezed;
        public StasisEffect StasisEffect { get; }
        private bool _isFreezed = false;
        [SerializeField] private StasisTipControllerPlatformMovement stasisTipControllerPlatformMovement;
        [SerializeField] private bool a;

        void Start()
        {
            stasisTipControllerPlatformMovement.OnFreezeEvent += StatisEffectActivate;
            stasisTipControllerPlatformMovement.OnUnFreezeEvent += StatisEffectDeactivate;
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
                stasisTipControllerPlatformMovement.StatisEffectActivate();
            }
            else
            {
                stasisTipControllerPlatformMovement.StatisEffectDeactivate();
            }
        }

        private void Update()
        {
            if (!a) return;

            if (Input.GetKeyDown(KeyCode.J))
            {
                StatisEffectActivate();
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                StatisEffectDeactivate();
            }
        }

        public void SetTipController(Component tipController)
        {
            var a = tipController.GetComponent<StasisTipControllerPlatformMovement>();
            if (a != null)
            {
                stasisTipControllerPlatformMovement = a;
            }
        }
    }
}
