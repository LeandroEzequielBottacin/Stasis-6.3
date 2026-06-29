using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Scenes.Level.Lau.Tutorial_Assets
{
    public class TriggerEventActivator : MonoBehaviour
    {
        [Header("Evento cuando el Player entra al trigger")]
        public UnityEvent onEnterEvent;

        [Header("Evento cuando termina el tiempo activo")]
        public UnityEvent onFinishEvent;

        [Header("Duración del efecto")]
        public float activeTime = 2f;

        [Header("Tag del Player")]
        public string playerTag = "Player";

        private bool isRunning;

        private void OnTriggerEnter(Collider other)
        {
            if (!isRunning && other.CompareTag(playerTag))
            {
                StartCoroutine(ActivationRoutine());
            }
        }

        private IEnumerator ActivationRoutine()
        {
            isRunning = true;

            // Evento al entrar
            onEnterEvent?.Invoke();

            // Tiempo activo
            yield return new WaitForSeconds(activeTime);

            // Evento al terminar
            onFinishEvent?.Invoke();

            isRunning = false;
        }
    }
}


