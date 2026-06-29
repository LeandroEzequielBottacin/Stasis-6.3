using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.ProceduralIK
{
    public class ProceduralClimbIK : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private Model model;

        [SerializeField] private Transform rHandTarget; // Target de la mano Izquierda
        [SerializeField] private Transform lHandTarget; // Target de la mano Derecha

        [Header("Cycle Settings")]
        [Tooltip("Velocidad del ciclo de escalada (más rápido = manos más rápidas)")]
        [SerializeField]
        private float cycleSpeed = 2.5f;

        [Tooltip("Altura vertical de cada 'paso' de la mano")] [SerializeField]
        private float stepHeight = 0.7f;

        [Tooltip("El valor X fijo para separar los brazos (anchura de hombros)")] [SerializeField]
        private float shoulderWidth = 0.55f;

        [Tooltip("Separación horizontal extra para el movimiento cíclico de la mano (0 para no moverse)")]
        [SerializeField]
        private float handCycleWidth = 0.15f;

        [Tooltip("Offset Y de las manos relativo al centro del personaje (altura de hombros)")] [SerializeField]
        private float shoulderHeightOffset = 1.8f;

        [Header("Surface Adaptation")]
        [Tooltip("Distancia para buscar la pared (desde el 'ideal' de la mano)")]
        [SerializeField]
        private float raycastSearchDistance = 0.3f;

        [Tooltip("Offset desde la superficie de la pared (para que la mano no la atraviese)")] [SerializeField]
        private float handOffsetFromWall = 0.08f;

        [Tooltip("Velocidad de interpolación de la posición de la mano")] [SerializeField]
        private float positionLerpSpeed = 12f;

        [Tooltip("Velocidad de adaptación de la rotación de la mano a la superficie")] [SerializeField]
        private float rotationLerpSpeed = 10f;

        [Tooltip("Offset vertical para 'asentar' la mano sobre el borde en el mantle (negativo = más abajo)")]
        [SerializeField]
        private float mantleHandYOffset = -0.05f;

        private float _climbCycle;

        private void LateUpdate()
        {
            if (!model || !rHandTarget || !lHandTarget || !model.rb)
            {
                return;
            }

            if (model.isMantlingState)
            {
                HandleMantleIK();
            }
            else if (model.isAtLedge)
            {
                HandleMantleIK();
            }
            else if (model.isClimbingState)
            {
                HandleClimbIK();
            }
        }

        private void HandleClimbIK()
        {
            float vSpeed = model.rb.linearVelocity.y;
            Vector3 wallNormal = model.climbWallNormal;
            Vector3 wallPoint = model.climbWallPoint;
            LayerMask wallMask = model.wallMask | model.groundMask;

            if (wallNormal.sqrMagnitude < 0.1f) return;

            _climbCycle += vSpeed * cycleSpeed * Time.deltaTime;

            Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;

            Debug.DrawRay(model.rb.position + Vector3.up * 1.5f, wallRight * 2f, Color.red);

            Vector3 playerOnWall = wallPoint + Vector3.ProjectOnPlane(model.rb.position - wallPoint, wallNormal);

            UpdateHandClimb(rHandTarget, -1, _climbCycle, playerOnWall, wallRight, wallNormal, wallMask);
            UpdateHandClimb(lHandTarget, 1, _climbCycle + Mathf.PI, playerOnWall, wallRight, wallNormal, wallMask);
        }

        private void HandleMantleIK()
        {
            Vector3 wallNormal = model.climbWallNormal;
            Vector3 ledgePoint = model.mantleLedgePoint;

            if (wallNormal.sqrMagnitude < 0.1f) return;

            Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;

            Vector3 verticalOffset = Vector3.up * mantleHandYOffset;

            Vector3 leftPos = ledgePoint + wallRight * (-shoulderWidth / 2f) + verticalOffset;

            Vector3 rightPos = ledgePoint + wallRight * (shoulderWidth / 2f) + verticalOffset;

            Quaternion targetRot = Quaternion.LookRotation(Vector3.down, wallNormal);

            rHandTarget.position =
                Vector3.Lerp(rHandTarget.position, leftPos, Time.deltaTime * positionLerpSpeed);
            rHandTarget.rotation =
                Quaternion.Slerp(rHandTarget.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);

            lHandTarget.position =
                Vector3.Lerp(lHandTarget.position, rightPos, Time.deltaTime * positionLerpSpeed);
            lHandTarget.rotation =
                Quaternion.Slerp(lHandTarget.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);
        }

        private void UpdateHandClimb(Transform target, float side, float cycle, Vector3 playerOnWall, Vector3 wallRight,
            Vector3 wallNormal, LayerMask wallMask)
        {
            float yOffset = Mathf.Sin(cycle) * (stepHeight / 2f);
            float xCycleOffset = Mathf.Cos(cycle) * (handCycleWidth / 2f * side);

            float targetY = (model.rb.position.y + shoulderHeightOffset) + yOffset;
            float xShoulderOffset = shoulderWidth / 2f * side;

            // Posición teórica de la mano en el plano (puede estar flotando tras una esquina)
            Vector3 basePos = playerOnWall + wallRight * (xShoulderOffset + xCycleOffset);
            basePos.y = targetY;

            Vector3 targetPosition = basePos; // Fallback por defecto
            Quaternion targetRotation = Quaternion.LookRotation(-wallNormal, Vector3.up);

            // --- CORRECCIÓN DE ESQUINAS ---

            // 1. Calcular dirección hacia la "columna" del personaje (ignorando altura)
            Vector3 spinePoint = playerOnWall;
            spinePoint.y = targetY;
            Vector3 dirToSpine = (spinePoint - basePos).normalized;

            // 2. Mezclar la normal de la pared con la dirección hacia el cuerpo.
            // Esto hace que el rayo apunte en diagonal hacia la esquina, asegurando el impacto.
            Vector3 castDirection = Vector3.Slerp(-wallNormal, dirToSpine, 0.5f).normalized;

            // 3. Ajustar el origen: Nos alejamos un poco más en la dirección inversa al cast 
            // para asegurar que no empezamos dentro de la pared si la esquina es muy aguda.
            Vector3 rayOrigin = basePos - (castDirection * raycastSearchDistance);

            // Usamos SphereCast para mayor fiabilidad en bordes finos (radio 0.05f aprox)
            if (Physics.SphereCast(rayOrigin, 0.05f, castDirection, out RaycastHit hit, raycastSearchDistance * 2.5f,
                    wallMask, QueryTriggerInteraction.Ignore))
            {
                targetPosition = hit.point + hit.normal * handOffsetFromWall;

                // Lerp de rotación de la mano para que se adapte suavemente a la nueva normal
                targetRotation = Quaternion.LookRotation(-hit.normal, Vector3.up);

                // Debug visual para entender qué está pasando
                Debug.DrawLine(rayOrigin, hit.point, Color.green);
            }
            else
            {
                // Si falla, intentamos pegarnos a la posición base pero usando la normal original
                // Esto evita que la mano desaparezca, manteniéndola en el plano "frontal"
                targetPosition = basePos + wallNormal * handOffsetFromWall;
                Debug.DrawRay(rayOrigin, castDirection * raycastSearchDistance * 2.5f, Color.red);
            }

            target.position = Vector3.Lerp(target.position, targetPosition, Time.deltaTime * positionLerpSpeed);
            target.rotation = Quaternion.Slerp(target.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
        }
    }
}