using System.Collections;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    public class ConstantPropulsor : MonoBehaviour
    {
        [Header("Punto de salida del impulso")]
        public Transform launchPoint;

        [Header("Fuerza / velocidad final del impulso")]
        public float force = 20f;

        [Header("Dirección del impulso (si no usás LaunchPoint forward)")]
        public Vector3 direction = Vector3.forward;

        [Header("Curva que define la velocidad (0 a 1 del trayecto)")]
        public AnimationCurve forceCurve = AnimationCurve.Linear(0, 1, 1, 1);
        public float duration = 0.5f;

        private bool useCurve;

        // Ahora se obtiene automáticamente
        private Rigidbody playerRb;

        void Start()
        {
            if (forceCurve != null && forceCurve.keys.Length > 1)
                useCurve = true;
        }

        void OnTriggerEnter(Collider other)
        {
            // Comprueba el tag Player
            if (!other.CompareTag("Player")) return;

            // Busca el Rigidbody del Player si no lo teníamos guardado
            if (!playerRb)
                playerRb = other.attachedRigidbody;

            if (!playerRb) return;

            Launch();
        }

        void Launch()
        {
            if (launchPoint)
                playerRb.position = launchPoint.position;

            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;

            if (useCurve)
            {
                StopAllCoroutines();
                StartCoroutine(LaunchCurve());
            }
            else
            {
                Vector3 dir = launchPoint ? launchPoint.forward : direction.normalized;
                playerRb.linearVelocity = dir * force;
            }
        }

        IEnumerator LaunchCurve()
        {
            float t = 0f;
            Vector3 dir = launchPoint ? launchPoint.forward : direction.normalized;

            while (t < duration)
            {
                float mult = forceCurve.Evaluate(t / duration);

                // Reemplaza la velocidad según la curva
                playerRb.linearVelocity = dir * (force * mult);

                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            playerRb.linearVelocity = dir * (force * forceCurve.Evaluate(1f));
        }
    }
}


