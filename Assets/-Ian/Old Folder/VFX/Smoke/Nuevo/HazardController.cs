using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace _Ian.VFX.Smoke
{
    [DisallowMultipleComponent]
    public class HazardController : MonoBehaviour
    {
        [SerializeReference]
        private List<HazardEntry> hazards = new();

        private void Awake()
        {
            for (int i = 0; i < hazards.Count; i++)
            {
                hazards[i]?.Initialize(this, i);
            }
        }

        private void Update()
        {
            foreach (HazardEntry hazard in hazards)
            {
                hazard?.Tick(this);
            }
        }

        private void LateUpdate()
        {
            foreach (HazardEntry hazard in hazards)
            {
                hazard?.LateTick(this);
            }
        }

        private void OnDisable()
        {
            foreach (HazardEntry hazard in hazards)
            {
                hazard?.Disable(this);
            }
        }

        // =========================================================
        // VFX TRIGGER
        // =========================================================

        public void VFXTriggerEnter(int hazardIndex, Collider other)
        {
            if (!IsValidIndex(hazardIndex))
                return;

            hazards[hazardIndex]?.OnVFXEnter(other);
        }

        public void VFXTriggerExit(int hazardIndex, Collider other)
        {
            if (!IsValidIndex(hazardIndex))
                return;

            hazards[hazardIndex]?.OnVFXExit(other);
        }

        // =========================================================
        // HAZARD TRIGGER
        // =========================================================

        public void HazardTriggerEnter(int hazardIndex, Collider other)
        {
            if (!IsValidIndex(hazardIndex))
                return;

            hazards[hazardIndex]?.OnHazardEnter(this, other);
        }

        public void HazardTriggerStay(int hazardIndex, Collider other)
        {
            if (!IsValidIndex(hazardIndex))
                return;

            hazards[hazardIndex]?.OnHazardStay(this, other);
        }

        public void HazardTriggerExit(int hazardIndex, Collider other)
        {
            if (!IsValidIndex(hazardIndex))
                return;

            hazards[hazardIndex]?.OnHazardExit(this, other);
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < hazards.Count;
        }

        // =========================================================
        // COROUTINES
        // =========================================================

        public Coroutine RunCoroutine(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        public void StopHazardCoroutine(Coroutine routine)
        {
            if (routine != null)
                StopCoroutine(routine);
        }

        // =========================================================
        // HAZARD ENTRY
        // =========================================================

        [Serializable]
        public class HazardEntry
        {
            [SerializeField]
            private string name = "Hazard";

            public string Name => name;

            // =====================================================
            // VFX TRIGGER
            // =====================================================

            [Header("VFX Trigger")]
            [Tooltip("Trigger encargado únicamente de activar y desactivar los VFX.")]
            public Collider vfxTrigger;

            [Header("Filtro")]
            public bool requireTag = true;
            public string targetTag = "Player";

            [Tooltip("Opcional. Nothing significa que no se filtra por Layer.")]
            public LayerMask targetLayers;

            // =====================================================
            // VFX TARGETS
            // =====================================================

            [Header("VFX Targets")]
            public List<VisualEffect> vfxList = new();

            [Header("Parámetro expuesto en VFX Graph")]
            public string parameterName = "Active";
            public float enterValue = 1f;
            public float exitValue = 0f;

            [Header("Eventos opcionales de VFX Graph")]
            public bool sendEnterEvent = false;
            public string enterEventName = "OnEnter";

            public bool sendExitEvent = false;
            public string exitEventName = "OnExit";

            [Header("Comportamiento VFX")]
            public bool setOnEnter = true;
            public bool resetOnExit = true;
            public bool onlyFirstEnter = false;

            // =====================================================
            // HAZARD MODULE
            // =====================================================

            [Header("Hazard Module")]
            [SerializeReference]
            public HazardModule hazardModule;

            // =====================================================
            // POST PROCESS
            // =====================================================

            [Header("Post Process")]
            public PostProcessSettings postProcess = new();

            // =====================================================
            // RUNTIME
            // =====================================================

            [NonSerialized]
            private bool _hasFired;

            [NonSerialized]
            private bool _isInsideHazard;

            // =====================================================
            // INITIALIZE
            // =====================================================

            public void Initialize(HazardController controller, int index)
            {
                if (string.IsNullOrWhiteSpace(parameterName))
                    parameterName = "Active";

                // VFX Trigger
                if (vfxTrigger != null)
                {
                    vfxTrigger.isTrigger = true;

                    HazardTriggerRelay relay =
                        vfxTrigger.GetComponent<HazardTriggerRelay>();

                    if (relay == null)
                        relay = vfxTrigger.gameObject.AddComponent<HazardTriggerRelay>();

                    relay.AddVFXHazard(controller, index);
                }
                else
                {
                    Debug.LogWarning(
                        $"[{controller.name}] Hazard '{name}' no tiene VFX Trigger asignado."
                    );
                }

                // Hazard Module
                hazardModule?.Initialize(controller, this, index);

                // Post Process
                postProcess?.Initialize(controller);
            }

            // =====================================================
            // VFX ENTER
            // =====================================================

            public void OnVFXEnter(Collider other)
            {
                if (!IsValidTarget(other))
                    return;

                if (onlyFirstEnter && _hasFired)
                    return;

                if (setOnEnter)
                    SetParameterOnAll(enterValue);

                if (sendEnterEvent)
                    SendEventAll(enterEventName);

                _hasFired = true;
            }

            // =====================================================
            // VFX EXIT
            // =====================================================

            public void OnVFXExit(Collider other)
            {
                if (!IsValidTarget(other))
                    return;

                if (!resetOnExit)
                    return;

                SetParameterOnAll(exitValue);

                if (sendExitEvent)
                    SendEventAll(exitEventName);
            }

            // =====================================================
            // HAZARD ENTER
            // =====================================================

            public void OnHazardEnter(
                HazardController controller,
                Collider other)
            {
                if (!IsValidTarget(other))
                    return;

                _isInsideHazard = true;

                hazardModule?.OnEnter(controller, this, other);
            }

            // =====================================================
            // HAZARD STAY
            // =====================================================

            public void OnHazardStay(
                HazardController controller,
                Collider other)
            {
                if (!IsValidTarget(other))
                    return;

                _isInsideHazard = true;

                hazardModule?.OnStay(controller, this, other);
            }

            // =====================================================
            // HAZARD EXIT
            // =====================================================

            public void OnHazardExit(
                HazardController controller,
                Collider other)
            {
                if (!IsValidTarget(other))
                    return;

                _isInsideHazard = false;

                hazardModule?.OnExit(controller, this, other);
                postProcess?.ResetEffect();
            }

            // =====================================================
            // UPDATE
            // =====================================================

            public void Tick(HazardController controller)
            {
                hazardModule?.Tick(controller, this);
            }

            public void LateTick(HazardController controller)
            {
                hazardModule?.LateTick(controller, this);

                if (!_isInsideHazard)
                    return;

                if (hazardModule == null || postProcess == null)
                    return;

                float danger = hazardModule.GetDanger();

                postProcess.ApplyDanger(danger);
            }

            // =====================================================
            // DISABLE
            // =====================================================

            public void Disable(HazardController controller)
            {
                _isInsideHazard = false;

                hazardModule?.Disable(controller, this);
                postProcess?.ResetEffect();
            }

            // =====================================================
            // FILTER
            // =====================================================

            private bool IsValidTarget(Collider other)
            {
                if (requireTag && !other.CompareTag(targetTag))
                    return false;

                if (targetLayers.value != 0)
                {
                    int layerMask = 1 << other.gameObject.layer;

                    if ((targetLayers.value & layerMask) == 0)
                        return false;
                }

                return true;
            }

            // =====================================================
            // VFX
            // =====================================================

            private void SetParameterOnAll(float value)
            {
                if (vfxList == null)
                    return;

                foreach (VisualEffect vfx in vfxList)
                {
                    if (vfx == null)
                        continue;

                    vfx.SetFloat(parameterName, value);
                }
            }

            private void SendEventAll(string eventName)
            {
                if (vfxList == null)
                    return;

                foreach (VisualEffect vfx in vfxList)
                {
                    if (vfx == null)
                        continue;

                    vfx.SendEvent(eventName);
                }
            }
        }

        // =========================================================
        // BASE HAZARD MODULE
        // =========================================================

        [Serializable]
        public abstract class HazardModule
        {
            [Header("Hazard Trigger")]
            [Tooltip("Trigger que aplica este Hazard Module mientras el Player permanezca dentro.")]
            public Collider hazardTrigger;

            public virtual void Initialize(
                HazardController controller,
                HazardEntry hazard,
                int hazardIndex)
            {
                if (hazardTrigger == null)
                {
                    Debug.LogWarning(
                        $"[{controller.name}] El Hazard Module no tiene Hazard Trigger asignado."
                    );

                    return;
                }

                hazardTrigger.isTrigger = true;

                HazardTriggerRelay relay =
                    hazardTrigger.GetComponent<HazardTriggerRelay>();

                if (relay == null)
                    relay = hazardTrigger.gameObject.AddComponent<HazardTriggerRelay>();

                relay.AddGameplayHazard(controller, hazardIndex);
            }

            public virtual void OnEnter(
                HazardController controller,
                HazardEntry hazard,
                Collider other)
            {
            }

            public virtual void OnStay(
                HazardController controller,
                HazardEntry hazard,
                Collider other)
            {
            }

            public virtual void OnExit(
                HazardController controller,
                HazardEntry hazard,
                Collider other)
            {
            }

            public virtual void Tick(
                HazardController controller,
                HazardEntry hazard)
            {
            }

            public virtual void LateTick(
                HazardController controller,
                HazardEntry hazard)
            {
            }

            public virtual void Disable(
                HazardController controller,
                HazardEntry hazard)
            {
            }

            public virtual float GetDanger()
            {
                return 0f;
            }
        }
    }
}