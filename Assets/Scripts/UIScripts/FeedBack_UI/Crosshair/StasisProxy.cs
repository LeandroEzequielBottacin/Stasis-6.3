using Player.Stasis;
using UnityEngine;

namespace UIScripts.FeedBack_UI.Crosshair
{
    /// <summary>
    /// Colocá este componente en un hijo vacío (proxy) con un Collider en modo Trigger.
    /// - Layer recomendada: "StasisProxy"
    /// - (Opcional) Tag: "StasisProxy"
    /// Se registra automáticamente en el StasisRegistry.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Assets/Scripts/UIScripts/FeedBack_UI/Crosshair")]
    public class StasisProxy : MonoBehaviour
    {
        [Tooltip("Componente que implementa Player.Stasis.IStasis. Si está vacío, se busca en padres.")]
        public MonoBehaviour owner;

        [Tooltip("Collider del proxy (debe ser Trigger). Si está vacío, toma el del mismo GO.")]
        public Collider proxyCollider;

        [Header("Auto-setup (opcional)")]
        [Tooltip("Si no hay collider, crea automáticamente un SphereCollider trigger.")]
        public bool autoCreateSphere = true;

        [Tooltip("Radio del SphereCollider auto-creado.")]
        public float defaultRadius = 0.15f;

        private IStasis _stasis;

        private void Awake()
        {
            // Resolver owner IStasis
            if (owner == null)
                owner = GetComponentInParent<MonoBehaviour>();

            _stasis = owner as IStasis;
            if (_stasis == null)
            {
                var monos = GetComponentsInParent<MonoBehaviour>(true);
                for (int i = 0; i < monos.Length; i++)
                {
                    if (monos[i] is IStasis s)
                    {
                        _stasis = s;
                        break;
                    }
                }
            }

            if (_stasis == null)
            {
                Debug.LogWarning($"[StasisProxy] No se encontró IStasis en padres de '{name}'.", this);
            }

            // Resolver/crear collider
            if (proxyCollider == null)
                proxyCollider = GetComponent<Collider>();

            if (proxyCollider == null && autoCreateSphere)
            {
                var sc = gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = Mathf.Max(0.001f, defaultRadius);
                proxyCollider = sc;
            }

            if (proxyCollider != null)
                proxyCollider.isTrigger = true;
        }

        private void OnEnable()
        {
            if (proxyCollider != null && _stasis != null)
                StasisRegistry.Register(proxyCollider, _stasis);
        }

        private void OnDisable()
        {
            if (proxyCollider != null && _stasis != null)
                StasisRegistry.Unregister(proxyCollider, _stasis);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (proxyCollider is SphereCollider sc)
            {
                Gizmos.color = Color.green;
                Vector3 pos = transform.TransformPoint(sc.center);
                float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                Gizmos.DrawWireSphere(pos, sc.radius * scale);
            }
            else if (proxyCollider is BoxCollider bc)
            {
                Gizmos.color = Color.green;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(bc.center, bc.size);
            }
        }
#endif
    }
}
