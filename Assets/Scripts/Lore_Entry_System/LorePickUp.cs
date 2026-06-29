using UnityEngine;
using UnityEngine.Events;

namespace Lore_Entry_System
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class LorePickup : MonoBehaviour
    {
        [Header("Asignación")]
        [Tooltip("ID del LoreEntry a desbloquear. Debe existir en la LoreDatabase del LoreSystem.")]
        public string entryId;

        [Header("Condiciones")]
        [Tooltip("Tag que debe tener el jugador para recoger.")]
        public string playerTag = "Player";

        [Header("Desaparición")]
        [Tooltip("Destruir este GameObject al recoger.")]
        public bool destroyOnPickup = true;
        [Tooltip("Ocultar renderers al recoger (si no destruimos).")]
        public bool hideOnPickup = false;
        [Tooltip("Siempre destruir aunque ya estuviera desbloqueado (por ejemplo, si reaparece por respawn).")]
        public bool alwaysDestroyOnTrigger = false;
        [Tooltip("Demora antes de destruir (deja terminar SFX).")]
        [Min(0f)] public float destroyDelay = 0f;

        [Header("Feedback (opc)")]
        [Tooltip("SFX al recoger (Play() en este AudioSource).")]
        public AudioSource sfxOnPickup;
        [Tooltip("VFX al recoger (se instancia en la posición).")]
        public GameObject vfxPrefab;

        [Header("Eventos")]
        [Tooltip("Se dispara cuando el pickup se toma (útil para UI: LoreToastUI.Show()).")]
        public UnityEvent onPicked;

        [Header("UI hook (opcional)")]
        [Tooltip("Arrastrá aquí el GO que tiene LoreToastUI (Panel_Toast). Si está vacío, se buscará en escena.")]
        public LoreToastUI toast;

        private bool _taken;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col) col.isTrigger = true;
        }

        void Awake()
        {
            var col = GetComponent<Collider>();
            if (!col)
            {
                Debug.LogError("LorePickup: Falta Collider en el MISMO GameObject del script.", this);
            }
            else if (!col.isTrigger)
            {
                // Evita que falle el trigger si te olvidaste de marcarlo
                col.isTrigger = true;
            }
        }

        [ContextMenu("Debug/Pick (simulate)")]
        private void DebugPick() => DoPick(null);

        private void OnTriggerEnter(Collider other)
        {
            if (_taken) return;
            if (!other.CompareTag(playerTag)) return;
            DoPick(other.transform);
        }

        private void DoPick(Transform picker)
        {
            var system = FindFirstObjectByType<LoreSystem>();
            if (!system)
            {
                Debug.LogError("LorePickup: No existe un LoreSystem en escena.", this);
                return;
            }

            // 1) Desbloquear
            bool unlockedNow = system.Unlock(entryId); // false si ID inválido o ya desbloqueado

            // 2) Mostrar toast (opcional, si asignaste el campo o hay uno en escena)
            TryShowToast(system);

            // 3) Logs si no se desbloqueó (para diagnóstico)
            if (!unlockedNow)
            {
                if (string.IsNullOrWhiteSpace(entryId))
                    Debug.LogWarning("LorePickup: entryId vacío.", this);
                else if (!system.IsUnlocked(entryId))
                    Debug.LogWarning($"LorePickup: entryId '{entryId}' NO existe en la Database.", this);
                else
                    Debug.Log($"LorePickup: entryId '{entryId}' ya estaba desbloqueado.", this);

                if (!alwaysDestroyOnTrigger && !hideOnPickup)
                    return; // no limpiamos si no queremos
            }

            // 4) Feedback y limpieza
            _taken = true;

            if (sfxOnPickup) sfxOnPickup.Play();
            if (vfxPrefab) Instantiate(vfxPrefab, transform.position, Quaternion.identity);

            onPicked?.Invoke(); // aquí podés enganchar LoreToastUI.Show() desde el Inspector

            if (hideOnPickup) SetRenderersEnabled(false);

            if (destroyOnPickup || alwaysDestroyOnTrigger)
            {
                if (destroyDelay > 0f) Destroy(gameObject, destroyDelay);
                else Destroy(gameObject);
            }
            else
            {
                var col = GetComponent<Collider>();
                if (col) col.enabled = false;
            }
        }

        private void TryShowToast(LoreSystem system)
        {
            // Si asignaste el Panel_Toast en el campo, se usa; si no, buscamos uno en escena.
            if (!toast) toast = FindFirstObjectByType<LoreToastUI>();
            if (!toast) return;

            string txt = "Press I to read new entry";
            if (system.TryGetEntry(entryId, out var entry) && entry && !string.IsNullOrEmpty(entry.title))
                txt = $"Press I to read: {entry.title}";

            float seconds = (toast.autoHideSeconds > 0f) ? toast.autoHideSeconds : -1f;
            toast.Show(txt, seconds); // requiere que LoreToastUI tenga Show() y/o overload sin params
        }

        private void SetRenderersEnabled(bool enabled)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = enabled;
        }
    }
}
