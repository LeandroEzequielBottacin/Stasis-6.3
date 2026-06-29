using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Hedro_conteiner.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class HedronContainerOut : MonoBehaviour
    {
        [Header("Eventos")]
        public UnityEvent onOccupied;    // pasa a true (algo adentro)
        public UnityEvent onUnoccupied;  // pasa a false (ya no hay nada)

        [Header("Estado")]
        public bool isOccupied;          // true si se detect� algo este frame

        [Header("Filtro (opcional)")]
        public bool requirePhysicsBox = true; // si true, solo cuenta objetos con componente "PhysicsBox"

        bool _stayDetectedThisFrame;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void Update()
        {
            // Cambio a ocupado
            if (_stayDetectedThisFrame && !isOccupied)
            {
                isOccupied = true;
                onOccupied?.Invoke();
            }
            // Cambio a desocupado
            else if (!_stayDetectedThisFrame && isOccupied)
            {
                isOccupied = false;
                onUnoccupied?.Invoke();
            }

            // reset flag para el pr�ximo frame
            _stayDetectedThisFrame = false;
        }

        void OnTriggerStay(Collider other)
        {
            if (requirePhysicsBox && !HasPhysicsBox(other.gameObject)) return;
            _stayDetectedThisFrame = true;
        }

        bool HasPhysicsBox(GameObject go)
        {
            return go.GetComponent("PhysicsBox") != null;
        }
    }
}
