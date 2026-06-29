using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Pressure_Plate.Scripts
{
    public class PressurePlateGroup : MonoBehaviour
    {
        [SerializeField] private List<PressurePlate> plates;

        [Header("Events")]
        public UnityEvent onAllActivated;
        public UnityEvent onAnyReleased;

        private bool _wasAllActive;

        public void NotifyPlateStateChanged()
        {
            bool allActive = true;
            foreach (var plate in plates)
            {
                if (!plate.pressed)
                {
                    allActive = false;
                    break;
                }
            }

            if (allActive && !_wasAllActive)
            {
                _wasAllActive = true;
                onAllActivated?.Invoke();
            }
            else if (!allActive && _wasAllActive)
            {
                _wasAllActive = false;
                onAnyReleased?.Invoke();
            }
        }
    }
}
