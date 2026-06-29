using Puzzle_Elements.Hedro_conteiner.Scripts;
using UnityEngine;

namespace Environment.Platforms
{
    /// <summary>
    /// Vincula un HedronContainerIn con un PlatformSignSystem.
    /// - Suscribe a onHedronPlaced / onHedronRemoved.
    /// - Opcionalmente activa/desactiva el GameObject del sistema de carteles.
    /// - Llama a RequestClose() cuando se coloca el hedro y a RequestOpen() cuando se retira.
    /// </summary>
    [DisallowMultipleComponent]
    public class HedronPlatformSignLink : MonoBehaviour
    {
        [Header("=== Source ===")]
        [Tooltip("Referencia al contenedor que emite los eventos onHedronPlaced / onHedronRemoved.")]
        public HedronContainerIn hedronContainer;

        [Header("=== Target ===")]
        [Tooltip("GameObject que contiene el PlatformSignSystem (puede ser el mismo cartel o la plataforma).")]
        public GameObject signSystemObject;

        [Tooltip("Se buscará un PlatformSignSystem en este objeto. Si está vacío, se intenta en el signSystemObject.")]
        public PlatformSignSystem platformSignSystem;

        [Header("=== Behavior ===")]
        [Tooltip("Al colocar el hedro (onHedronPlaced) se solicitará Cerrar (Close).")]
        public bool closeOnPlaced = true;

        [Tooltip("Al retirar el hedro (onHedronRemoved) se solicitará Abrir (Open).")]
        public bool openOnRemoved = true;

        [Tooltip("Activar automáticamente el GameObject del sistema de carteles al producirse el evento.")]
        public bool autoActivateTargetGO = true;

        [Tooltip("Desactivar automáticamente el GameObject del sistema al quedar en estado Close (opcional).")]
        public bool autoDeactivateOnCloseEnd;

        // Interno
        bool _subscribed;

        private void Reset()
        {
            // Intento auto-detectar en el mismo GO
            if (!hedronContainer) hedronContainer = GetComponent<HedronContainerIn>();
            if (!platformSignSystem) platformSignSystem = GetComponentInChildren<PlatformSignSystem>(true);

            if (!signSystemObject && platformSignSystem)
                signSystemObject = platformSignSystem.gameObject;
        }

        private void Awake()
        {
            // Fallbacks razonables
            if (!hedronContainer) hedronContainer = GetComponent<HedronContainerIn>();

            if (!platformSignSystem && signSystemObject)
                platformSignSystem = signSystemObject.GetComponentInChildren<PlatformSignSystem>(true);

            if (!signSystemObject && platformSignSystem)
                signSystemObject = platformSignSystem.gameObject;
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            TryUnsubscribe();
        }

        void TrySubscribe()
        {
            if (_subscribed) return;
            if (!hedronContainer)
            {
                Debug.LogWarning($"[{name}] HedronPlatformSignLink: falta HedronContainerIn.");
                return;
            }
            if (!platformSignSystem)
            {
                Debug.LogWarning($"[{name}] HedronPlatformSignLink: falta PlatformSignSystem.");
                return;
            }

            hedronContainer.onHedronPlaced.AddListener(OnHedronPlaced);
            hedronContainer.onHedronRemoved.AddListener(OnHedronRemoved);

            // Escuchar el final de animación si queremos auto-desactivar
            if (autoDeactivateOnCloseEnd)
                platformSignSystem.onReachCloseEnd.AddListener(HandleCloseEnd);

            _subscribed = true;
        }

        void TryUnsubscribe()
        {
            if (!_subscribed) return;

            if (hedronContainer != null)
            {
                hedronContainer.onHedronPlaced.RemoveListener(OnHedronPlaced);
                hedronContainer.onHedronRemoved.RemoveListener(OnHedronRemoved);
            }

            if (platformSignSystem != null && autoDeactivateOnCloseEnd)
            {
                platformSignSystem.onReachCloseEnd.RemoveListener(HandleCloseEnd);
            }

            _subscribed = false;
        }

        void OnHedronPlaced()
        {
            if (!platformSignSystem) return;

            if (autoActivateTargetGO && signSystemObject && !signSystemObject.activeSelf)
                signSystemObject.SetActive(true);

            if (closeOnPlaced)
                platformSignSystem.RequestClose();
        }

        void OnHedronRemoved()
        {
            if (!platformSignSystem) return;

            if (autoActivateTargetGO && signSystemObject && !signSystemObject.activeSelf)
                signSystemObject.SetActive(true);

            if (openOnRemoved)
                platformSignSystem.RequestOpen();
        }

        void HandleCloseEnd()
        {
            if (!autoDeactivateOnCloseEnd) return;
            if (signSystemObject)
                signSystemObject.SetActive(false);
        }
    }
}
