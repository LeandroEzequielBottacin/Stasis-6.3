using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Hedro_conteiner.Scripts
{
    public class DobleActiveEvent : MonoBehaviour
    {
        [Header("Estado de activadores")]
        [SerializeField] private bool _leftActivate;
        [SerializeField] private bool _rightActivate;

        [Header("Feedback visual")]
        [Tooltip("Renderer del activador izquierdo.")]
        [SerializeField] private Renderer leftIndicator;

        [Tooltip("Renderer del activador derecho.")]
        [SerializeField] private Renderer rightIndicator;

        [Header("Materiales del activador izquierdo")]
        [SerializeField] private Material leftOffMaterial;
        [SerializeField] private Material leftOnMaterial;

        [Header("Materiales del activador derecho")]
        [SerializeField] private Material rightOffMaterial;
        [SerializeField] private Material rightOnMaterial;

        public UnityEvent events;
        public UnityEvent eventsTrainOn;

        private bool _canTrainMove;

        void Start()
        {
            _leftActivate = false;
            _rightActivate = false;
            UpdateIndicators();
        }

        public void ChangeLeftActivator()
        {
            _leftActivate = !_leftActivate;
            UpdateIndicators();
            TryEvent();
        }

        public void ChangeRightActivator()
        {
            _rightActivate = !_rightActivate;
            UpdateIndicators();
            TryEvent();
        }

        public void CanTrainMove()
        {
            _canTrainMove = true;
            TryEventOn();
        }

        public void CantTrainMove()
        {
            _canTrainMove = false;
        }

        public void TryEventOn()
        {
            if (!_canTrainMove) return;
            eventsTrainOn?.Invoke();
        }

        public void TryEvent()
        {
            if (!_rightActivate || !_leftActivate) return;
            events?.Invoke();
        }

        private void UpdateIndicators()
        {
            // Indicador izquierdo
            if (leftIndicator != null)
            {
                leftIndicator.material = _leftActivate ? leftOnMaterial : leftOffMaterial;
            }

            // Indicador derecho
            if (rightIndicator != null)
            {
                rightIndicator.material = _rightActivate ? rightOnMaterial : rightOffMaterial;
            }
        }
    }
}
