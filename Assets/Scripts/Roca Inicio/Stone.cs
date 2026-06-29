using UnityEngine;

namespace Roca_Inicio
{
    public class Stone : MonoBehaviour
    {
        private Animator _animator;

        public GameObject light1;
        public GameObject light2;
        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }
    
        // Llamar para activar animaci�n de impacto
        public void PlayImpactAnimation()
        {
            if (_animator != null)
            {
                _animator.SetTrigger("stoneFall");
            }
        }

        // Ejemplo de c�mo detener una animaci�n si us�s bools
        public void StopAllAnimations()
        {
            if (_animator != null)
            {
                _animator.Play("Idle", -1, 0f); // Vuelve a animaci�n Idle o cualquier otra
            }
        }
        public void ActivateLights()
        {
            light1.gameObject.SetActive(true);
            light2.gameObject.SetActive(true);
        }
    }
}