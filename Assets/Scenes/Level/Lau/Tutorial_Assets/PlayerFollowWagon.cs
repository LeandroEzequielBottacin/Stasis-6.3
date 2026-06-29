using UnityEngine;

namespace Scenes.Level.Lau.Tutorial_Assets
{
    public class PlayerFollowWagon : MonoBehaviour
    {
        [Header("Referencia al vagón")]
        public Transform wagon;

        [Header("Velocidad de seguimiento")]
        public float followSpeed = 10f; // velocidad a la que el jugador sigue al vagón

        void LateUpdate()
        {
            if (wagon == null)
                return;

            // Suavizar el movimiento siguiendo exactamente la trayectoria del vagón
            transform.position = Vector3.Lerp(transform.position, wagon.position, followSpeed * Time.deltaTime);

            // Opcional: que el jugador mire hacia la misma dirección que el vagón
            transform.rotation = Quaternion.Lerp(transform.rotation, wagon.rotation, followSpeed * Time.deltaTime);
        }
    }
}

