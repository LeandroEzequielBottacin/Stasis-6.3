using System;
using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.PlatformMovement
{
    public class StasisPlatformMovement : MonoBehaviour, IStasis
    {
        public void StatisEffectActivate()
        {
            throw new NotImplementedException();
        }

        public void StatisEffectDeactivate()
        {
            throw new NotImplementedException();
        }

        public bool IsFreezed => _isFreezed;
        private bool _isFreezed = false;
        public StasisEffect StasisEffect { get; }
    
    }
}
