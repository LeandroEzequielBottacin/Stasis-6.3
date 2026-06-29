using UnityEngine;

namespace Player.Scripts
{
    public class AttachPartToBone : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform bone; // El hueso donde se va a pegar

        [Header("Options")]
        [SerializeField] private bool keepWorldPosition; 
        // true = mantiene la posición global actual
        // false = adopta la posición local del hueso

        private void Start()
        {
            if (bone == null)
            {
                Debug.LogWarning($"[AttachPartToBone] No se asignó un hueso en {gameObject.name}");
                return;
            }

            transform.SetParent(bone, keepWorldPosition);
        }
    }
}


