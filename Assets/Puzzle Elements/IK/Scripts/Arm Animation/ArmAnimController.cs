using System.Collections;
using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.IK.Scripts.Arm_Animation
{
    public class ArmAnimController : MonoBehaviour,IStasis
    {
        public Animator anim;
        public string animationClipName = "Target_Mover_128";

        [Range(0, 1)]
        public float position;
        [Range(0, 1)]
        public float shake;

        public Renderer[] renderers;

        private bool _isFreezed;
        public bool IsFreezed => _isFreezed;
        public StasisEffect StasisEffect { get; }
    
        private IEnumerator Start()
        {
            float randomDelay = Random.Range(0, 1);
            yield return new WaitForSeconds(randomDelay);
            anim.enabled = true;
        }
        private void Update()
        {
            anim.Play(animationClipName, -1, position);
            anim.SetLayerWeight(1, shake);
        }
        public void EventArmAnimController()
        {
            if (IsFreezed) StatisEffectDeactivate();
            else StatisEffectActivate();
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
            if (_isFreezed) return;
            _isFreezed = true;
            StasisEffect.StasisEffectStart();
        
        }

        private void UnfreezeObject()
        {
            if (!_isFreezed) return;
            _isFreezed = false;
            StasisEffect.StasisEffectStop();
        }
    }
}
