using UnityEngine;

namespace Player.Camera
{
    public class MoveCamera : MonoBehaviour
    {
        // Asigna en el Inspector el transform objetivo (por ejemplo, un empty child del jugador)
        public Transform cameraPosition;

        private void Update()
        {
            // Sin rotación ni input: solo fijar posición
            transform.position = cameraPosition.position;
        }
    }
}
