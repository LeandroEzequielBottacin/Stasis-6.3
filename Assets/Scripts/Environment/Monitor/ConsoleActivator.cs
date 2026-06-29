using Player.Scripts.Interactor;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;
using UnityEngine.Events;

namespace Environment.Monitor
{
    [RequireComponent(typeof(Collider))]
    public class ConsoleActivator : MonoBehaviour, IInteractable
    {


        [Header("Animator (opcional)")]
        public Animator animator;
        public string animatorParam = "Active";

        [Header("Eventos")]
        public UnityEvent onActivated;



        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Implementación de IInteractable
        /// </summary>
        public void Interact()
        {
            Debug.Log("Interact");
            Fire();
        }
        private void OnTriggerStay(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if (player)
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    Interact();
                }

            }
        }
        private void Fire()
        {

            // Animator
            animator.SetBool("Active", true);
            onActivated?.Invoke();

        }

    

        // Método público por si querés llamarlo desde otros scripts
        public void ActivateManually()
        {
            Fire();
        }
    }
}
