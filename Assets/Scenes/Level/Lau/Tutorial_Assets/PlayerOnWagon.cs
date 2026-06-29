using UnityEngine;

namespace Scenes.Level.Lau.Tutorial_Assets
{
    public class PlayerOnWagon : MonoBehaviour
    {
        [Header("Referencias")]
        public Transform wagon;          // Transform del vagón
        public Transform playerRagdoll;  // Transform raíz del ragdoll del jugador

        [Header("Opciones")]
        public bool followWagon = true;  // Activar/desactivar seguimiento
        public Vector3 offset = Vector3.zero; // Offset relativo al vagón

        private Quaternion initialRotationOffset;

        void Start()
        {
            if (wagon == null || playerRagdoll == null)
            {
                Debug.LogWarning("Faltan referencias en PlayerOnWagon!");
                return;
            }

            // Calcula la rotación inicial relativa como offset
            initialRotationOffset = playerRagdoll.rotation * Quaternion.Inverse(wagon.rotation);

            // Coloca el ragdoll en la posición inicial del vagón con offset
            playerRagdoll.position = wagon.position + offset;
        }

        void Update()
        {
            if (!followWagon || wagon == null || playerRagdoll == null) return;

            // Sincroniza posición y rotación del ragdoll con el vagón, aplicando offset
            playerRagdoll.position = wagon.position + offset;
        }

        // Función para activar/desactivar el seguimiento
        public void SetFollow(bool active)
        {
            followWagon = active;
        }
    }
}





