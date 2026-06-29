using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Checkpoint
{
    public class CheckpointSequence : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Jugador para teleports/respawn (opcional).")]
        public Transform player;

        [Header("Modo de recolección")]
        [Tooltip("Si está activo, toma todos los Checkpoint hijos en orden de jerarquía.")]
        public bool autoCollectFromChildren = true;
        [Tooltip("Si está desactivado, usará esta lista manual.")]
        public List<Checkpoint> orderedCheckpoints = new();

        [Header("Progreso")]
        [SerializeField, Tooltip("Índice del PRÓXIMO por activar.")]
        private int currentIndex = 0;
        [Tooltip("Permite avanzar varios checkpoints en el mismo frame si se cumplen sus condiciones.")]
        public bool collapseMultiplePerFrame = true;

        [Header("Persistencia (simple)")]
        public bool enablePlayerPrefsSave = true;
        public string saveKey = "CheckpointSequence_CurrentId";

        [Header("Eventos")]
        public UnityEvent<Checkpoint, int> OnCheckpointReached; // (cp, index)
        public UnityEvent<Checkpoint> OnSequenceCompleted;

        public Checkpoint LastActivated
            => Mathf.Clamp(currentIndex - 1, 0, Count - 1) >= 0 && Count > 0
                ? orderedCheckpoints[Mathf.Clamp(currentIndex - 1, 0, Count - 1)]
                : null;

        public int Count => orderedCheckpoints?.Count ?? 0;
        public bool Completed => currentIndex >= Count;

        void Awake()
        {
            CollectIfNeeded();
            Subscribe(true);
            LoadProgress();
            ClampIndex();
            MarkActiveFlags();
        }

        void OnDestroy() => Subscribe(false);

        void Update()
        {
            if (!collapseMultiplePerFrame) return;

            // Si varias condiciones se cumplen al mismo tiempo (por proximidad encadenada),
            // consumimos todos en orden sin esperar al próximo frame.
            bool progressed;
            int safety = 32; // evita bucles.
            do
            {
                progressed = false;
                if (currentIndex < Count && orderedCheckpoints[currentIndex].IsActive)
                {
                    InternalAdvance(orderedCheckpoints[currentIndex]);
                    progressed = true;
                }
            } while (progressed && safety-- > 0);
        }

        void CollectIfNeeded()
        {
            if (autoCollectFromChildren)
                orderedCheckpoints = GetComponentsInChildren<Checkpoint>(true).ToList();
        }

        void Subscribe(bool on)
        {
            if (orderedCheckpoints == null) return;
            foreach (var cp in orderedCheckpoints)
            {
                if (cp == null) continue;
                if (on) cp.OnReached.AddListener(HandleReached);
                else cp.OnReached.RemoveListener(HandleReached);
            }
        }

        void HandleReached(Checkpoint cp)
        {
            // Sólo aceptamos el "próximo" de la secuencia (lineal).
            if (currentIndex < Count && orderedCheckpoints[currentIndex] == cp)
            {
                cp.MarkActive(true); // lo marca como alcanzado
                InternalAdvance(cp);
            }
        }

        void InternalAdvance(Checkpoint cp)
        {
            OnCheckpointReached?.Invoke(cp, currentIndex);
            currentIndex++;
            ClampIndex();
            MarkActiveFlags();
            SaveProgress();
            if (Completed) OnSequenceCompleted?.Invoke(cp);
        }

        void ClampIndex() => currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, Count));

        void MarkActiveFlags()
        {
            for (int i = 0; i < Count; i++)
                orderedCheckpoints[i].MarkActive(i < currentIndex);
        }

        // === API pública ===

        [ContextMenu("Reset to Start")]
        public void ResetProgress()
        {
            currentIndex = 0;
            MarkActiveFlags();
            SaveProgress();
        }

        /// <summary>
        /// Devuelve el Transform de spawn del último checkpoint activado.
        /// Si no hay checkpoints, devuelve el propio transform del objeto que tiene este script.
        /// </summary>
        public Transform GetCurrentSpawn()
        {
            if (Count == 0) return transform;

            var idx = Mathf.Clamp(currentIndex - 1, 0, Count - 1);
            return orderedCheckpoints[idx].Spawn;
        }

        /// <summary>
        /// Versión en Vector3, equivalente a tu LinearCheckpointSystem.CurrentCheckpointPos().
        /// </summary>
        public Vector3 CurrentCheckpointPos()
        {
            return GetCurrentSpawn().position;
        }

        public bool TeleportPlayerToCurrent()
        {
            if (player == null) return false;
            player.position = GetCurrentSpawn().position;
            player.rotation = GetCurrentSpawn().rotation;
            return true;
        }

        public bool JumpToCheckpointId(string id, bool markPreviousAsActive = true)
        {
            int idx = orderedCheckpoints.FindIndex(c => c.Id == id);
            if (idx < 0) return false;

            currentIndex = idx + 1;

            if (!markPreviousAsActive)
                foreach (var cp in orderedCheckpoints)
                    cp.MarkActive(false);

            MarkActiveFlags();
            SaveProgress();
            return true;
        }

        // === Persistencia simple ===
        void SaveProgress()
        {
            if (!enablePlayerPrefsSave || Count == 0) return;

            var last = LastActivated;
            if (last == null)
            {
                PlayerPrefs.DeleteKey(saveKey);
                return;
            }

            PlayerPrefs.SetString(saveKey, last.Id);
        }

        void LoadProgress()
        {
            if (!enablePlayerPrefsSave || Count == 0) return;
            if (!PlayerPrefs.HasKey(saveKey)) return;

            var id = PlayerPrefs.GetString(saveKey);
            JumpToCheckpointId(id, markPreviousAsActive: true);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (autoCollectFromChildren)
                orderedCheckpoints = GetComponentsInChildren<Checkpoint>(true).ToList();

            if (orderedCheckpoints == null || orderedCheckpoints.Count == 0) return;

            for (int i = 0; i < orderedCheckpoints.Count - 1; i++)
            {
                var a = orderedCheckpoints[i]?.Spawn;
                var b = orderedCheckpoints[i + 1]?.Spawn;
                if (a == null || b == null) continue;
                Gizmos.DrawLine(a.position, b.position);
            }
        }
#endif
    }
}
