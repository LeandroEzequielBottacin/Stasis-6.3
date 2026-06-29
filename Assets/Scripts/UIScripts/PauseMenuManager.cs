using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIScripts
{
    public class PauseMenuManager : MonoBehaviour
    {
        public static bool IsGamePaused;
        public GameObject pauseMenuUI;
        public GameObject confirmQuitUI;

        private void Awake()
        {
            confirmQuitUI.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsGamePaused) Resume();
                else Pause();
            }
        }

        public void Resume()
        {
            pauseMenuUI.SetActive(false);
            confirmQuitUI.SetActive(false);
            Time.timeScale = 1f;
            IsGamePaused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Pause()
        {
            pauseMenuUI.SetActive(true);
            confirmQuitUI.SetActive(false);
            Time.timeScale = 0f;
            IsGamePaused = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnQuitPressed()
        {
            confirmQuitUI.SetActive(true);
            pauseMenuUI.SetActive(false);
        }

        public void OnQuitYes()
        {
            #if UNITY_STANDALONE
                Application.Quit();
            #endif
            #if UNITY_EDITOR
                EditorApplication.isPlaying = false;
            #endif
        }

        public void OnQuitNo()
        {
            confirmQuitUI.SetActive(false);
            pauseMenuUI.SetActive(true);
        }

        // ----- NUEVO: Reiniciar la escena actual -----
        public void RestartLevel()
        {
            // Aseguramos que el tiempo vuelva a la velocidad normal
            Time.timeScale = 1f;
            IsGamePaused = false;
            // Recargamos la escena actual
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnApplicationQuit()
        {
            Debug.Log("Aplicación cerrándose...");
            // Aquí podés guardar datos o limpiar recursos
        }
    }
}
