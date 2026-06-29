using UnityEngine;

namespace Puzzle_Elements.PlataformasCorregidas
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerPlatformHandler : MonoBehaviour
    {
        private Rigidbody rb;
        [SerializeField]private Rigidbody plataformaActual;
        [SerializeField]private bool plataformaEsInercial;






        [Header("Raycast")]
        [Tooltip("Punto desde donde se dispara el rayo hacia abajo (ej: 'Feet' transform).")]
        [SerializeField] private Transform rayOrigin;

        [Tooltip("Distancia m�xima del rayo hacia abajo.")]
        [Min(0f)]
        [SerializeField] private float maxDistance = 3f;

        [Tooltip("Capas a considerar en el raycast.")]
        [SerializeField] private LayerMask layerMask = ~0;

        [Tooltip("Ignorar o no colliders tipo Trigger.")]
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Visualizaci�n")]
        [SerializeField] private bool drawDebugRay = true;

        public RaycastHit LastHit { get; private set; }
        public Rigidbody LastHitRigidbody { get; private set; }

        public Rigidbody rbRayDetected;
        void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            // Si el jugador est� en el suelo (useGravity = false)
            // y est� sobre una plataforma, lo movemos con ella
            if (plataformaActual != null)
            {
                rb.position += plataformaActual.linearVelocity * Time.fixedDeltaTime;

            }







            if (rayOrigin == null)
            {
                Debug.LogWarning($"{name}: No se asign� el origen del rayo. Asigna un Transform en 'rayOrigin'.");
                return;
            }

            Vector3 origin = rayOrigin.position;
            Vector3 dir = Vector3.down;

            // Debug.Log( "El rayo da " + Physics.Raycast(origin, dir, out RaycastHit hitt, maxDistance, layerMask, triggerInteraction));


            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, layerMask, triggerInteraction))
            {
          
                Rigidbody rb = hit.rigidbody ?? hit.collider.attachedRigidbody;
                if (rb == null) return;
                InercialPlatform inercialPlatform = rb.gameObject.GetComponent<InercialPlatform>();
                if(inercialPlatform != null)
                {
                    plataformaActual = rb;
                    rbRayDetected = rb;
                    //rb.velocity += plataformaActual.velocity;
                    if (rb != null)
                    {
                        LastHit = hit;
                        LastHitRigidbody = rb;
                    }
                    else
                    {
                        LastHitRigidbody = null;
                    }
                }
           
           

            }
            else
            {
                LastHitRigidbody = null;
                if(rbRayDetected != null)
                {
                    rbRayDetected = null;
                    plataformaEsInercial = false;
                    plataformaActual = null;
                }
            }
        }
        //private void OnCollisionStay(Collision collision)
        //{
        //    if(collision.gameObject.GetComponent<InercialPlatform>() != null)
        //    {
        //        plataformaEsInercial = true;
        //        plataformaActual = collision.rigidbody;
        //    }
        //}
        //private void OnCollisionExit(Collision collision)
        //{
        //    if(collision.gameObject.GetComponent<InercialPlatform>() != null)
        //    {
        //        if(plataformaEsInercial && plataformaActual != null)
        //        {
        //            rb.velocity += plataformaActual.velocity;
        //        }
        //    }
        //}


        private void OnTriggerStay(Collider other)
        {
            if (other.gameObject.CompareTag("Plataforma"))
            {
                plataformaActual = other.attachedRigidbody;
                plataformaEsInercial = false;
            }
            //else if(other.gameObject.GetComponent<InercialPlatform>() != null)
            //{
            //    plataformaActual = other.attachedRigidbody;
            //    plataformaEsInercial = true;
            //}
        }
        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.CompareTag("Plataforma") ||
                other.gameObject.GetComponent<InercialPlatform>() != null)
            {
                // Si estaba sobre una plataforma inercial, transferir velocidad al saltar
                if (plataformaEsInercial && plataformaActual != null)
                {
                    rb.linearVelocity += plataformaActual.linearVelocity;
                }

                plataformaActual = null;
                plataformaEsInercial = false;
            }
        }
   
    }
}

