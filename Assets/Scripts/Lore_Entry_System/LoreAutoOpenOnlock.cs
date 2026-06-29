using UnityEngine;

namespace Lore_Entry_System
{
    /// Escucha LoreSystem y abre el LoreReaderUI cuando se desbloquea un entry.
    /// Útil si querés auto-pausa+lectura inmediata al recoger.
    [DisallowMultipleComponent]
    public class LoreAutoOpenOnUnlock : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Lector que se abrirá al desbloquear.")]
        public LoreReaderUI reader;

        [Header("Comportamiento")]
        [Tooltip("Mostrar también el toast si existe en escena.")]
        public bool alsoShowToast = true;
        [Tooltip("Demora antes de abrir (para dejar que VFX/SFX terminen).")]
        [Min(0f)] public float openDelay = 0.15f;
        [Tooltip("Sólo auto-abrir la primera vez que se desbloquea algo en esta sesión.")]
        public bool onlyFirstUnlockThisSession = false;

        private bool _alreadyOpened;

        void Awake()
        {
            if (!reader) reader = FindFirstObjectByType<LoreReaderUI>();
            var sys = FindFirstObjectByType<LoreSystem>();
            if (sys) sys.OnEntryUnlocked += OnUnlocked;
        }

        void OnDestroy()
        {
            var sys = FindFirstObjectByType<LoreSystem>();
            if (sys) sys.OnEntryUnlocked -= OnUnlocked;
        }

        private void OnUnlocked(LoreEntry e)
        {
            if (onlyFirstUnlockThisSession && _alreadyOpened) return;
            _alreadyOpened = true;
            if (alsoShowToast)
            {
                var toast = FindFirstObjectByType<LoreToastUI>();
                if (toast) toast.Show();
            }
            if (!reader) return;
            // Abrimos el más reciente (el propio Reader ya se encarga).
            Invoke(nameof(OpenReader), openDelay);
        }

        private void OpenReader()
        {
            if (!reader) return;
            // Forzamos apertura (usa su lógica de pausa/cursor)
            reader.Open();
        }
    }
}