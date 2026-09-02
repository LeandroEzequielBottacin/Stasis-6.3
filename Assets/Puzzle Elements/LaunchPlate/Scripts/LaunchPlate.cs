using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class LaunchPlate : MonoBehaviour
    {
        [Header("Configuración de Salto")]
        [Tooltip("La cantidad de fuerza que se suma al jugador u objeto al tocar el Pad.")]
        [SerializeField] private float launchForce = 20f;
        
        [Tooltip("Si está activo, el impulso se hace hacia el 'Arriba' del objeto Pad (por si está rotado). Si está apagado, el salto es siempre vertical (hacia el techo del mundo).")]
        [SerializeField] private bool useLocalUp = false;

        [Tooltip("Si el jugador viene cayendo muy rápido, ¿queremos frenar su caída antes de impulsarlo? Si está apagado, un jugador que cae rapidísimo rebotará menos.")]
        [SerializeField] private bool resetFallingVelocity = true;

        [Header("Cooldown")]
        [Tooltip("Tiempo en segundos antes de que el Pad pueda volver a activarse.")]
        [SerializeField] private float cooldown = 0.25f;

        public event System.Action OnLaunch;

        private bool _busy;
        private float _lastTriggerTime;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_busy) return;

            // Buscar el Rigidbody del objeto que entró
            var rb = other.attachedRigidbody ?? other.GetComponentInParent<Rigidbody>();
            if (!rb) return;

            // Evitar triggers múltiples accidentales en el mismo frame
            if (Time.time - _lastTriggerTime < 0.05f) return;
            _lastTriggerTime = Time.time;

            // Dirección del impulso
            Vector3 jumpDirection = useLocalUp ? transform.up : Vector3.up;

            // Si queremos frenar la caída vertical para garantizar un rebote consistente
            if (resetFallingVelocity)
            {
                // Usamos la API nueva de Unity 6 (linearVelocity)
                Vector3 currentVel = rb.linearVelocity;
                
                // Si el salto es vertical (mundo) y el objeto está cayendo
                if (!useLocalUp && currentVel.y < 0)
                {
                    currentVel.y = 0;
                    rb.linearVelocity = currentVel;
                }
                // Si el salto es relativo al Pad y la velocidad va en contra del salto
                else if (useLocalUp)
                {
                    float velocityAlongJump = Vector3.Dot(currentVel, jumpDirection);
                    if (velocityAlongJump < 0)
                    {
                        currentVel -= jumpDirection * velocityAlongJump;
                        rb.linearVelocity = currentVel;
                    }
                }
            }

            // Sumar la fuerza de salto, conservando intacto el momentum horizontal ("que se mueva como él quiera")
            rb.AddForce(jumpDirection * launchForce, ForceMode.VelocityChange);

            OnLaunch?.Invoke();

            _busy = true;
            Invoke(nameof(ClearBusy), cooldown);
        }

        private void ClearBusy()
        {
            _busy = false;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
            Vector3 dir = useLocalUp ? transform.up : Vector3.up;
            
            // Dibujar una flecha simple para ver hacia dónde impulsa
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + dir * 2f;
            Gizmos.DrawLine(startPos, endPos);
            Gizmos.DrawSphere(endPos, 0.2f);
        }
#endif
    }
}
