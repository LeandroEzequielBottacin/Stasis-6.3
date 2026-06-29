using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace UIScripts.UI_Pause_Menu
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Pause Menu Controller")]
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private MenuManager menuManager;
        [SerializeField] private string pausePanelId = "Pause";
        [SerializeField] private ConfirmDialogController confirmDialog; // Yes/No

        [Header("Eventos para enganchar tu lógica")]
        [Tooltip("Enganchá tu sistema de checkpoints/guardado aquí")]
        public UnityEvent OnRestartFromLastSaveRequested;

        [Tooltip("Reload de escena completa (si querés inyectar fade, etc.)")]
        public UnityEvent OnRestartRequested;

        [Tooltip("Salir del juego (si querés hacer limpieza)")]
        public UnityEvent OnQuitRequested;

        private void Reset()
        {
            if (menuManager == null) menuManager = FindObjectOfType<MenuManager>();
            if (confirmDialog == null) confirmDialog = FindObjectOfType<ConfirmDialogController>(true);
        }

        // --- Botones ---
        public void Btn_Continue()
        {
            if (menuManager != null) menuManager.Resume();
        }

        public void Btn_RestartFromLastSave()
        {
            // Por defecto disparamos UnityEvent para que vos conectes tu sistema.
            if (OnRestartFromLastSaveRequested != null && OnRestartFromLastSaveRequested.GetPersistentEventCount() > 0)
            {
                OnRestartFromLastSaveRequested.Invoke();
                if (menuManager != null) menuManager.Resume();
            }
            else
            {
                // Fallback: recargar escena (placeholder si no conectaste nada aún)
                Debug.LogWarning("[PauseMenu] OnRestartFromLastSaveRequested no está conectado. Se recarga la escena como fallback.");
                ReloadActiveScene();
            }
        }

        public void Btn_Restart()
        {
            // Reinicio completo de la escena
            if (OnRestartRequested != null && OnRestartRequested.GetPersistentEventCount() > 0)
            {
                OnRestartRequested.Invoke();
            }
            else
            {
                ReloadActiveScene();
            }
        }

        public void Btn_QuitGame()
        {
            if (confirmDialog == null)
            {
                Debug.LogWarning("[PauseMenu] No hay ConfirmDialog asignado. Saliendo sin confirmar.");
                DoQuit();
                return;
            }

            confirmDialog.Show(
                title: "Quit Game",
                message: "Are you sure you want to quit?",
                onYes: DoQuit,
                onNo: null);
        }

        // --- Acciones concretas ---
        private void ReloadActiveScene()
        {
            if (menuManager != null && menuManager.IsPaused()) menuManager.Resume();
            var scene = SceneManager.GetActiveScene().name;
            Time.timeScale = 1f; // por seguridad
            AudioListener.pause = false;
            SceneManager.LoadScene(scene);
        }

        private void DoQuit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }
    }
}
