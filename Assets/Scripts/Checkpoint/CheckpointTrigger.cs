using UnityEngine;

namespace Checkpoint
{
    [RequireComponent(typeof(Checkpoint))]
    [RequireComponent(typeof(Collider))]
    public class CheckpointTrigger : MonoBehaviour
    {
        [Tooltip("Layer del jugador u objeto que debe tocar el trigger.")]
        public LayerMask who;

        private Checkpoint _cp;

        void Awake()
        {
            _cp = GetComponent<Checkpoint>();
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & who) != 0)
                _cp.Reach();
        }
    }
}