using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lore_Entry_System
{
    /// <summary>
    /// Panel de lectura de entradas de Lore:
    /// - Abre/cierra con tecla (por defecto: I).
    /// - Opcionalmente pausa el juego y muestra cursor.
    /// - Muestra imagen, título y cuerpo (TMP).
    /// - Navegación por “recientes” (Next/Prev).
    /// Requiere que exista un LoreSystem en escena.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoreReaderUI : MonoBehaviour
    {
        [Header("Refs (UI)")]
        [Tooltip("Canvas raíz de este lector (se habilita/deshabilita al abrir/cerrar).")]
        public Canvas rootCanvas;
        [Tooltip("Imagen ilustrativa de la entrada (puede ser null).")]
        public Image image;
        [Tooltip("TMP para el título.")]
        public TMP_Text titleText;
        [Tooltip("TMP para el cuerpo del texto.")]
        public TMP_Text bodyText;

        [Header("Input")]
        [Tooltip("Tecla para abrir/cerrar el lector.")]
        public KeyCode toggleKey = KeyCode.I;
        [Tooltip("Cerrar con Escape además de la tecla principal.")]
        public bool closeWithEscape = true;

        [Header("Comportamiento")]
        [Tooltip("Pausar el juego (Time.timeScale=0) cuando el lector está abierto.")]
        public bool pauseOnOpen = true;
        [Tooltip("Mostrar/ocultar cursor al abrir/cerrar.")]
        public bool manageCursor = true;

        [Header("Estado / Recientes")]
        [Tooltip("Lista de entradas desbloqueadas recientemente (la más nueva en índice 0).")]
        public List<LoreEntry> recentUnlocked = new List<LoreEntry>();

        // Runtime
        private bool _open;
        private int _currentIndex = 0;
        private LoreSystem _system;

        // ===== Ciclo de vida =====

        void Awake()
        {
            if (rootCanvas) rootCanvas.enabled = false; // arrancamos oculto
            _system = FindFirstObjectByType<LoreSystem>();
            if (_system) _system.OnEntryUnlocked += OnUnlocked;
            else Debug.LogWarning("LoreReaderUI: No se encontró LoreSystem en escena.", this);
        }

        void OnDestroy()
        {
            if (_system) _system.OnEntryUnlocked -= OnUnlocked;
        }

        // ===== Eventos desde LoreSystem =====

        private void OnUnlocked(LoreEntry e)
        {
            if (!e) return;
            recentUnlocked.Insert(0, e); // el más nuevo al frente
            // No abrimos automáticamente aquí: el jugador decide con la tecla (o usar LoreAutoOpenOnUnlock).
        }

        // ===== Update / Input =====

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                if (_open) Close();
                else OpenMostRecent();
            }

            if (_open && closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // ===== API pública (para Botones UI) =====

        /// <summary>Abre el lector mostrando la entrada actual (si existe).</summary>
        [ContextMenu("Debug/Open")]
        public void Open()
        {
            if (_open) return;

            // Asegurarnos de tener algo para mostrar
            if (!HasAnyEntry())
            {
                // Intentar conseguir cualquier desbloqueado del sistema (por si entras directo al Journal)
                TrySeedFromSystemAny();
                if (!HasAnyEntry())
                {
                    Debug.Log("LoreReaderUI: No hay entradas desbloqueadas para mostrar.", this);
                    return;
                }
            }

            _open = true;
            ApplyPauseAndCursor(true);
            if (rootCanvas) rootCanvas.enabled = true;

            // Si por alguna razón no hay UI linkeada, no rompemos
            SafePopulateCurrent();
        }

        /// <summary>Cierra el lector y restaura tiempo/cursor si corresponde.</summary>
        [ContextMenu("Debug/Close")]
        public void Close()
        {
            if (!_open) return;
            _open = false;
            if (rootCanvas) rootCanvas.enabled = false;
            ApplyPauseAndCursor(false);
        }

        /// <summary>Muestra la siguiente entrada en la lista de recientes.</summary>
        public void Next()
        {
            if (recentUnlocked.Count == 0) return;
            _currentIndex = Mathf.Clamp(_currentIndex + 1, 0, recentUnlocked.Count - 1);
            SafePopulateCurrent();
        }

        /// <summary>Muestra la entrada anterior en la lista de recientes.</summary>
        public void Prev()
        {
            if (recentUnlocked.Count == 0) return;
            _currentIndex = Mathf.Clamp(_currentIndex - 1, 0, recentUnlocked.Count - 1);
            SafePopulateCurrent();
        }

        // ===== Lógica de apertura =====

        /// <summary>Intenta abrir mostrando el entry más reciente (o cualquiera desbloqueado).</summary>
        public void OpenMostRecent()
        {
            // Si no hay recientes, intentamos sembrar desde el save actual
            if (recentUnlocked.Count == 0) TrySeedFromSystemAny();
            if (recentUnlocked.Count == 0)
            {
                Debug.Log("LoreReaderUI: No hay entradas desbloqueadas aún.", this);
                return;
            }

            _currentIndex = 0;
            SafePopulateCurrent();
            Open();
        }

        private void TrySeedFromSystemAny()
        {
            if (_system == null) return;
            foreach (var id in _system.UnlockedIds)
            {
                if (_system.TryGetEntry(id, out var e) && e != null)
                {
                    // Insertamos sólo si no estaba ya
                    if (!recentUnlocked.Contains(e))
                        recentUnlocked.Insert(0, e);
                }
            }
        }

        private bool HasAnyEntry() => recentUnlocked.Count > 0;

        // ===== Populate / Render =====

        private void SafePopulateCurrent()
        {
            if (recentUnlocked.Count == 0) return;
            _currentIndex = Mathf.Clamp(_currentIndex, 0, recentUnlocked.Count - 1);
            Populate(recentUnlocked[_currentIndex]);
        }

        private void Populate(LoreEntry e)
        {
            if (!e) return;

            if (titleText) titleText.text = e.title;
            if (bodyText) bodyText.text = e.body;

            if (image)
            {
                image.sprite = e.image;
                image.enabled = e.image != null;
                // Si querés resetear tamaño/aspecto, podés tocar aquí (AspectRatioFitter, etc.).
            }

            if (e.openSfx)
                AudioSource.PlayClipAtPoint(e.openSfx, Vector3.zero);
        }

        // ===== Utilidades =====

        private void ApplyPauseAndCursor(bool open)
        {
            // Si tenés un PauseService global, podés reemplazar estas 3 líneas por:
            // PauseService.Instance?.SetPaused(open);

            if (pauseOnOpen)
                Time.timeScale = open ? 0f : 1f;

            if (manageCursor)
            {
                Cursor.visible = open;
                Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            }
        }
    }
}
