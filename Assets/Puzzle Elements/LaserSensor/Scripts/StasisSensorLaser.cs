using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.LaserSensor.Scripts
{
    public class StasisSensorLaser : MonoBehaviour, IStasis
    {
        private SensorLaser _sensorLaser;
        public bool IsFreezed => isFreezed;
        [SerializeField]private bool isFreezed;
        public StasisEffect StasisEffect { get; private set; }

        public Renderer[] renderers;
        void Start()
        {
            _sensorLaser = GetComponent<SensorLaser>();
            StasisEffect = new StasisEffect(null, renderers);
        }
    
        public void EventSensorLaser()
        {
            if (IsFreezed) StatisEffectDeactivate();
            else StatisEffectActivate();
        }
        public void StatisEffectActivate()
        {
            FreezeObject();
            _sensorLaser.CanShootLasers(false);
        }

        public void StatisEffectDeactivate()
        {
            UnfreezeObject();
            _sensorLaser.CanShootLasers(true);
        }
  

        private void FreezeObject()
        {
            if (!isFreezed)
            {
                isFreezed = true;
                StasisEffect.StasisEffectStart();
            }
        }

        private void UnfreezeObject()
        {
            if (!isFreezed) return;
            isFreezed = false;
            StasisEffect.StasisEffectStop();
        }
    }
}