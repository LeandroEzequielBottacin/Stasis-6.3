using UnityEngine;

namespace Player.Scripts
{
    public class HeadRotationClamper : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Transform cameraTransform; // Cámara que sigue la cabeza
        [SerializeField] private Transform headBone; // El hueso de la cabeza (constrained object)

        [Header("Clamps")]
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 70f;

        private Quaternion initialHeadLocalRotation;

        void Start()
        {
            // Guardamos la rotación local inicial de la cabeza
            initialHeadLocalRotation = headBone.localRotation;
        }

        void LateUpdate()
        {
            // Obtenemos la rotación X (pitch) relativa de la cámara
            float cameraPitch = cameraTransform.eulerAngles.x;

            // Convertimos de 0-360 a -180 a 180 para que los clamps funcionen bien
            if (cameraPitch > 180f)
                cameraPitch -= 360f;

            // Clampeamos el ángulo
            float clampedPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);

            // Aplicamos la rotación clampeada solo en X
            Quaternion targetRotation = Quaternion.Euler(clampedPitch, headBone.eulerAngles.y, headBone.eulerAngles.z);

            // Aplicamos la rotación directamente
            headBone.rotation = targetRotation;
        }
    }
}

