using Unity.Cinemachine;
using UnityEngine;

namespace Player.Scripts.Opening_Cinematic
{
    public class CameraShakeOnRelease : MonoBehaviour
    {
        [Header("Referencias")]
        public RagdollHanger ragdollHanger;                // Referencia al script del hanger
        public CinemachineImpulseSource impulseSource;     // Componente de Cinemachine que genera el impulso

        [Header("Configuración")]
        public float delayBeforeShake;                // Retraso opcional antes del shake

        private bool hasShaken;

        void Update()
        {
            if (ragdollHanger == null || impulseSource == null)
                return;

            // Cuando el hanger hace release (usa el bool hasReleased)
            if (ragdollHanger.hasReleased && !hasShaken)
            {
                hasShaken = true;
                Invoke(nameof(TriggerShake), delayBeforeShake);
            }
        }

        void TriggerShake()
        {
            impulseSource.GenerateImpulse();
        }
    }
}


