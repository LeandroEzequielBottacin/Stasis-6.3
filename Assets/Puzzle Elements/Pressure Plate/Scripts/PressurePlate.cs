using System.Collections.Generic;
using Puzzle_Elements.AllInterfaces;
using UnityEngine;

namespace Puzzle_Elements.Pressure_Plate.Scripts
{
    public class PressurePlate : MonoBehaviour
    {
        private static readonly int Pressed = Animator.StringToHash("Pressed");

        [Header("Plate Settings")]
        [SerializeField] private PressurePlateGroup[] plateGroup;
        [SerializeField] private Animator animator;
        [SerializeField] private ParticleSystem _particles;

        public bool pressed;
        public bool canBeFreezed;
        public bool IsFreezed { get; private set; }
        
        private List<GameObject> objectsOnPlate = new List<GameObject>();

        private void OnTriggerEnter(Collider other)
        {
            if (IsFreezed) return;
            if (other.gameObject.GetComponent<IPlateActivator>() != null)
            {
                if (!objectsOnPlate.Contains(other.gameObject))
                {
                    objectsOnPlate.Add(other.gameObject);
                }
                
                if (!pressed)
                {
                    pressed = true;
                    UpdateAnimator(pressed);
                    foreach (var group in plateGroup)
                    {
                        group?.NotifyPlateStateChanged();
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsFreezed) return;
            if (other.gameObject.GetComponent<IPlateActivator>() != null)
            {
                objectsOnPlate.Remove(other.gameObject);
                
                if (objectsOnPlate.Count == 0 && pressed)
                {
                    pressed = false;
                    UpdateAnimator(pressed);
                    foreach (var group in plateGroup)
                    {
                        group.NotifyPlateStateChanged();
                    }
                }
            }
        }

        public void StasisEffectActivate()
        {
            if (!canBeFreezed) return;
            IsFreezed = true;
        }

        public void StasisEffectDeactivate()
        {
            if (!canBeFreezed) return;
            IsFreezed = false;
        }

        void UpdateAnimator(bool value)
        {
            animator.SetBool(Pressed, value);

            if (value)
            {
                _particles?.Play();
            }
            else
            {
                _particles?.Stop();
            } 
        }
    }
}