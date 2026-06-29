using System.Collections.Generic;
using UnityEngine;

namespace Scenes.Level.Lau.Tutorial_Assets
{
    public class DummyRagdoll : MonoBehaviour
    {
        [Header("🦴 Ragdolls que deben estar ACTIVOS al iniciar")]
        public List<Rigidbody> activeRagdolls = new List<Rigidbody>();

        [Header("Opciones")]
        public bool applyOnStart = true; // Aplica automáticamente al darle Play

        private Rigidbody[] allRagdolls;

        private void Awake()
        {
            // Busca todos los rigidbodies hijos (todos los huesos del ragdoll)
            allRagdolls = GetComponentsInChildren<Rigidbody>(includeInactive: true);
        }

        private void Start()
        {
            if (applyOnStart)
                ApplyRagdollState();
        }

        [ContextMenu("Aplicar configuración de Ragdolls")]
        public void ApplyRagdollState()
        {
            foreach (Rigidbody rb in allRagdolls)
            {
                if (rb == null) continue;
                bool shouldBeActive = activeRagdolls.Contains(rb);

                rb.isKinematic = !shouldBeActive;
                rb.detectCollisions = shouldBeActive;

                Collider col = rb.GetComponent<Collider>();
                if (col != null)
                    col.enabled = shouldBeActive;
            }

            Debug.Log($"🎯 Configuración aplicada: {activeRagdolls.Count} ragdolls activos / {allRagdolls.Length} totales en {gameObject.name}");
        }
    }
}


