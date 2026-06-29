using Managers.Game;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace _Ian.VFX.Smoke
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class SmokeHazardTrigger : MonoBehaviour
    {
        [Header("Filtro de objetivo")]
        [Tooltip("Si está activo, solo afecta a colliders con este tag (ej: Player).")]
        public bool requireTag = true;
        public string targetTag = "Player";

        [Tooltip("Opcional: filtra por capas (dejar en Nothing para ignorar).")]
        public LayerMask playerLayers;

        [Header("Referencia al Player")]
        [Tooltip("Model del jugador que expone hazardSpeedMultiplier. Asignar por inspector.")]
        public Model playerModel;

        [Header("Ralentización (ahogo)")]
        [Tooltip("Tiempo para pasar de 1 a minMultiplier dentro del humo.")]
        [Min(0.1f)] public float slowdownDuration = 1.2f;

        [Tooltip("Valor mínimo al que llegamos (nunca bajamos de esto).")]
        [Range(0.01f, 0.3f)] public float minMultiplier = 0.05f;

        [Tooltip("Por debajo de este valor empezamos a contar tiempo de muerte.")]
        [Range(0.05f, 0.5f)] public float deathMultiplierThreshold = 0.2f;

        [Header("Muerte diferida")]
        [Tooltip("Tiempo que debe permanecer por debajo del umbral antes de morir.")]
        [Min(0f)] public float deathDelay = 0.8f;

        [Tooltip("Cooldown entre muertes para evitar múltiples disparos.")]
        [Min(0f)] public float repeatCooldown = 0.25f;

        [Header("Comportamiento al salir del humo")]
        [Tooltip("Si el jugador sale del humo antes de morir, restaurar el multiplicador a 1.")]
        public bool resetMultiplierOnExit = true;

        [Header("Freno al entrar")]
        [Tooltip("Aplicar un freno inmediato a la velocidad horizontal al entrar en el humo.")]
        public bool applyEntryBrake = true;

        [Tooltip("Factor de velocidad horizontal al entrar (1 = sin cambio, 0.5 = mitad).")]
        [Range(0f, 1f)] public float entryHorizontalVelocityScale = 0.75f;

        private bool _playerInside;
        private Coroutine _slowRoutine;
        private float _nextAllowedDeathTime;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning($"[{name}] SmokeHazardTrigger requiere que el Collider sea Trigger. Lo ajusto en runtime.");
                col.isTrigger = true;
            }

            if (!playerModel)
            {
                Debug.LogWarning($"[{name}] No hay Model asignado en 'playerModel'. Asignalo por inspector.");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsValidTarget(other)) return;
            if (!playerModel) return;

            _playerInside = true;

            if (applyEntryBrake)
                ApplyEntryVelocityBrake();

            StartSlowdownRoutine();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsValidTarget(other)) return;
            _playerInside = true;
            // La corutina hace el resto.
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsValidTarget(other)) return;

            _playerInside = false;
            StopSlowdownAndReset();
        }
        private void Start()
        {
            
        }
        private bool IsValidTarget(Collider other)
        {
            if (requireTag && !other.CompareTag(targetTag))
                return false;

            if (playerLayers.value != 0)
            {
                if ((playerLayers.value & (1 << other.gameObject.layer)) == 0)
                    return false;
            }

            return true;
        }

        private void ApplyEntryVelocityBrake()
        {
            if (playerModel == null || playerModel.rb == null) return;

            Vector3 v = playerModel.rb.linearVelocity;
            Vector3 horizontal = new Vector3(v.x, 0f, v.z);

            horizontal *= entryHorizontalVelocityScale;

            playerModel.rb.linearVelocity = horizontal + Vector3.up * v.y;
        }

        private void StartSlowdownRoutine()
        {
            if (_slowRoutine != null) return;
            if (!playerModel) return;

            _slowRoutine = StartCoroutine(SlowdownRoutine());
        }

        private void StopSlowdownAndReset()
        {
            if (_slowRoutine != null)
            {
                StopCoroutine(_slowRoutine);
                _slowRoutine = null;
            }

            if (resetMultiplierOnExit && playerModel != null)
            {
                playerModel.hazardSpeedMultiplier = 1f;
            }
        }

        private System.Collections.IEnumerator SlowdownRoutine()
        {
            if (playerModel == null)
            {
                _slowRoutine = null;
                yield break;
            }

            float t = 0f;
            float deathTimer = 0f;

            // Empezamos desde el valor actual (por si otro hazard ya había tocado el multiplicador)
            float start = Mathf.Clamp01(playerModel.hazardSpeedMultiplier);
            float targetMin = Mathf.Clamp01(minMultiplier);

            while (_playerInside && playerModel != null)
            {
                t += Time.deltaTime;

                float normalized = slowdownDuration > 0f
                    ? Mathf.Clamp01(t / slowdownDuration)
                    : 1f;

                // Fade lineal desde start hasta targetMin
                float m = Mathf.Lerp(start, targetMin, normalized);
                m = Mathf.Clamp01(m);

                playerModel.hazardSpeedMultiplier = m;

                // Empezamos a contar muerte cuando pasamos el umbral
                if (m <= deathMultiplierThreshold)
                {
                    deathTimer += Time.deltaTime;

                    if (deathTimer >= deathDelay)
                    {
                        if (GameManager.Instance != null && Time.time >= _nextAllowedDeathTime)
                        {
                            GameManager.Instance.PlayerDeath();
                            _nextAllowedDeathTime = Time.time + repeatCooldown;
                        }
                        break;
                    }
                }
                else
                {
                    // Si sube por encima del umbral (por salir/entrar), reseteamos el contador
                    deathTimer = 0f;
                }

                yield return null;
            }

            _slowRoutine = null;
        }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider b)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(b.center, b.size);
            }
            else if (col is SphereCollider s)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawSphere(s.center, s.radius);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 1f);
            }
        }
#endif
    }
}
