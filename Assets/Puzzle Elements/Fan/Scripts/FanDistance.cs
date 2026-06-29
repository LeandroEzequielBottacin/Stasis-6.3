using Managers.Game;
using UnityEngine;

namespace Puzzle_Elements.Fan.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class FanDistance : MonoBehaviour
    {
        private Collider _col;
        private Transform _player;

        [Header("Referencia al Fan")]
        [Tooltip("Referencia al componente Fan que se verá afectado por la distancia al jugador.")]
        public Fan fan;

        [Header("Detección (Trigger)")]
        [Tooltip("Distancia máxima a la que el fan afecta al jugador (activa el trigger).")]
        public float dist = 20f;

        [Header("Velocidad de rotación según distancia")]
        [Tooltip("Distancia hasta la que se escala la velocidad. Más lejos que esto = velocidad mínima.")]
        public float maxSpeedDistance = 8f;

        [Tooltip("Velocidad cuando el jugador está más lejos que maxSpeedDistance.")]
        public float minRotateSpeed = 60f;

        [Tooltip("Velocidad cuando el jugador está muy cerca (distancia 0).")]
        public float maxRotateSpeed = 720f;

        [Header("Partículas (emisión según distancia)")]
        [Tooltip("Sistema de partículas del viento. Si está vacío, intenta usar el del componente Fan.")]
        public ParticleSystem windParticles;

        [Tooltip("Rate over Time cuando el jugador está lejos.")]
        public float minEmissionRate = 5f;

        [Tooltip("Rate over Time cuando el jugador está muy cerca.")]
        public float maxEmissionRate = 40f;

        private void Start()
        {
            _col = GetComponent<Collider>();
            _player = GameManager.Instance.player.transform;

            if (fan == null)
                fan = GetComponent<Fan>();

            if (windParticles == null && fan != null)
                windParticles = fan.windParticles;
        }

        private void Update()
        {
            if (_player == null || fan == null)
                return;

            float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

            bool insideRange = distanceToPlayer < dist;
            _col.isTrigger = insideRange;

            float d = distanceToPlayer;

            float proximity;
            if (d >= maxSpeedDistance)
            {
                proximity = 0f;
            }
            else
            {
                float t = Mathf.Clamp01(d / maxSpeedDistance); 
                proximity = 1f - t;                           
            }

            float currentSpeed = Mathf.Lerp(minRotateSpeed, maxRotateSpeed, proximity);
            fan.velocidadRotacion = currentSpeed;

            if (windParticles != null)
            {
                var emission = windParticles.emission;
                var main = windParticles.main;
                main.startSpeed = 0.2f;
                if (d >= maxSpeedDistance)
                {
                    emission.rateOverTime = minEmissionRate;
                    main.startSpeed = 4f;
                }
                else
                {
                    emission.rateOverTime = maxEmissionRate;
                    main.startSpeed = 10f;
                }
            }
        }
    }
}
