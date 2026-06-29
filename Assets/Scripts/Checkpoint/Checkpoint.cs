using UnityEngine;
using UnityEngine.Events;

namespace Checkpoint
{
    public interface ICheckpoint
    {
        string Id { get; }
        Transform Spawn { get; }
        bool IsActive { get; }
    }

    [DisallowMultipleComponent]
    public class Checkpoint : MonoBehaviour, ICheckpoint
    {
        [Header("ID y Spawn")]
        [Tooltip("Identificador único (para save/load y teleports).")]
        public string id = "CP_01";
        [Tooltip("Punto exacto de respawn (si es null usa este transform).")]
        public Transform spawn;

        [Header("Estado")]
        [SerializeField] private bool isActive;

        [Header("Eventos")]
        public UnityEvent<Checkpoint> OnReached;

        public string Id => id;
        public Transform Spawn => spawn != null ? spawn : transform;
        public bool IsActive => isActive;

        public void MarkActive(bool active) => isActive = active;

        // Permite triggerear por código o desde otros scripts/condiciones.
        public void Reach()
        {
            OnReached?.Invoke(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(Spawn.position, 0.25f);
        }
#endif
    }
}