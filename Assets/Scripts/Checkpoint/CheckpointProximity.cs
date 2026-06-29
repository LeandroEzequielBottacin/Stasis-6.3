using UnityEngine;

namespace Checkpoint
{
    [RequireComponent(typeof(Checkpoint))]
    public class CheckpointProximity : MonoBehaviour
    {
        [Header("Detección por distancia")]
        public Transform player;
        [Min(0f)] public float activationDistance = 4f;
        public bool allowMultiplePerFrame = true;

        private Checkpoint _cp;

        void Awake() => _cp = GetComponent<Checkpoint>();

        void Update()
        {
            if (player == null) return;

            var sqr = (player.position - _cp.Spawn.position).sqrMagnitude;
            if (sqr <= activationDistance * activationDistance)
            {
                // Notifica una vez: si el manager lo consume por orden, no vuelve a llegar acá.
                _cp.Reach();
                // Si no querés múltiples activaciones en el mismo frame, deshabilitá este script:
                if (!allowMultiplePerFrame) enabled = false;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var cp = GetComponent<Checkpoint>();
            if (cp == null) return;
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.DrawWireDisc(cp.Spawn.position, Vector3.up, activationDistance);
        }
#endif
    }
}