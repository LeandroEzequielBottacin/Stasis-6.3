using System;
using UnityEngine;

namespace Audio.Scripts.Player
{
    public class PlayerAnimationAudioEvents : MonoBehaviour, ISoundPlayer
    {
        public event Action OnStepLeft;
        public event Action OnStepRight;

        public void Anim_StepLeft()    => OnStepLeft?.Invoke();
        public void Anim_StepRight()   => OnStepRight?.Invoke();
    }
}