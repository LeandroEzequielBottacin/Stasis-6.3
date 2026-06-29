using DG.Tweening;
using UIScripts;
using UnityEngine;

namespace Player.Camera
{
    public class PlayerCam : MonoBehaviour
    {
        [Header("Sensibilidad")]
        public float sens = 700f;

        [Header("Referencias")]
        public Transform orientation;  // Para girar el cuerpo/player
        public Transform camHolder;    // Padre de la cámara

        float xRotation;
        float yRotation;
        public bool canRotateCamera;
        private void Start()
        {
            LockCursor();
        }
        private void Update()
        {
            if (!canRotateCamera) return;
            // ESC siempre libera el cursor (para abrir menú, etc.)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UnlockCursor();
            }

            // Clic izquierdo para volver a bloquear (solo si NO estamos en pausa)
            if (!PauseMenuManager.IsGamePaused &&
                Input.GetMouseButtonDown(0) &&
                Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }
            
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sens;
                float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sens;

                yRotation += mouseX;
                xRotation -= mouseY;
                xRotation = Mathf.Clamp(xRotation, -90f, 90f);

                camHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0);
                orientation.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }
        public void CanRotateCamera()
        {
            canRotateCamera = true;
        }
        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        public void DoFov(float endValue)
        {
            GetComponent<UnityEngine.Camera>().DOFieldOfView(endValue, 0.25f);
        }
        
        public void DoTilt(float zTilt)
        {
            transform.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
        }
    }
}
