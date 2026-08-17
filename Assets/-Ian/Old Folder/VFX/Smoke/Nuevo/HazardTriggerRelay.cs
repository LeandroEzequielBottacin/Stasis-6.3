using _Ian.VFX.Smoke;
using System.Collections.Generic;
using UnityEngine;

public class HazardTriggerRelay : MonoBehaviour
{
    private enum TriggerType
    {
        VFX,
        Gameplay
    }

    private class Binding
    {
        public HazardController controller;
        public int hazardIndex;
        public TriggerType triggerType;
    }

    private readonly List<Binding> _bindings = new();

    public void AddVFXHazard(HazardController controller, int hazardIndex)
    {
        AddBinding(controller, hazardIndex, TriggerType.VFX);
    }

    public void AddGameplayHazard(HazardController controller, int hazardIndex)
    {
        AddBinding(controller, hazardIndex, TriggerType.Gameplay);
    }

    private void AddBinding(
        HazardController controller,
        int hazardIndex,
        TriggerType triggerType)
    {
        foreach (Binding binding in _bindings)
        {
            if (binding.controller == controller &&
                binding.hazardIndex == hazardIndex &&
                binding.triggerType == triggerType)
            {
                return;
            }
        }

        _bindings.Add(new Binding
        {
            controller = controller,
            hazardIndex = hazardIndex,
            triggerType = triggerType
        });
    }

    private void OnTriggerEnter(Collider other)
    {
        foreach (Binding binding in _bindings)
        {
            if (binding.controller == null)
                continue;

            if (binding.triggerType == TriggerType.VFX)
            {
                binding.controller.VFXTriggerEnter(
                    binding.hazardIndex,
                    other
                );
            }
            else
            {
                binding.controller.HazardTriggerEnter(
                    binding.hazardIndex,
                    other
                );
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        foreach (Binding binding in _bindings)
        {
            if (binding.controller == null)
                continue;

            if (binding.triggerType == TriggerType.Gameplay)
            {
                binding.controller.HazardTriggerStay(
                    binding.hazardIndex,
                    other
                );
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        foreach (Binding binding in _bindings)
        {
            if (binding.controller == null)
                continue;

            if (binding.triggerType == TriggerType.VFX)
            {
                binding.controller.VFXTriggerExit(
                    binding.hazardIndex,
                    other
                );
            }
            else
            {
                binding.controller.HazardTriggerExit(
                    binding.hazardIndex,
                    other
                );
            }
        }
    }
}