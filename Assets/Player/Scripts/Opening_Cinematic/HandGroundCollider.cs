using UnityEngine;

namespace Player.Scripts.Opening_Cinematic
{
    public class HandGroundCollider : MonoBehaviour
    {
        [Header("Configuración del Raycast")]
        [Tooltip("Distancia máxima a la que el raycast busca el suelo")]
        public float rayDistance = 0.3f;

        [Tooltip("Altura mínima a mantener sobre el suelo")]
        public float offsetAboveGround = 0.01f;

        [Tooltip("Capas que se consideran suelo")]
        public LayerMask groundLayer;

        [Header("Suavizado del ajuste")]
        [Tooltip("Qué tan rápido la mano se ajusta a la posición del suelo")]
        public float smoothSpeed = 15f;

        private Vector3 targetPosition;
        private Vector3 originalLocalPos;
        private RaycastHit hitInfo;
        private bool isHittingGround;

        void Start()
        {
            // Guardamos la posición local inicial (respecto al hueso padre)
            originalLocalPos = transform.localPosition;
        }

        void LateUpdate()
        {
            // Verificamos colisión con el suelo
            isHittingGround = Physics.Raycast(transform.position, Vector3.down, out hitInfo, rayDistance, groundLayer);

            if (isHittingGround)
            {
                float groundY = hitInfo.point.y + offsetAboveGround;

                // Si la mano está por debajo del suelo, la levantamos suavemente
                if (transform.position.y < groundY)
                {
                    Vector3 correctedPos = new Vector3(transform.position.x, groundY, transform.position.z);
                    transform.position = Vector3.Lerp(transform.position, correctedPos, Time.deltaTime * smoothSpeed);
                }
            }
            else
            {
                // Si no toca el suelo, volvemos suavemente a su posición original local
                Vector3 desiredWorldPos = transform.parent.TransformPoint(originalLocalPos);
                transform.position = Vector3.Lerp(transform.position, desiredWorldPos, Time.deltaTime * smoothSpeed);
            }

            // Dibujo del raycast (verde si toca suelo, rojo si no)
            Debug.DrawLine(transform.position, transform.position + Vector3.down * rayDistance,
                isHittingGround ? Color.green : Color.red);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * rayDistance);
        }
    }
}



