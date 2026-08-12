using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace _Ian.VFX.Smoke
{
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public class VFXTriggerActivator : MonoBehaviour
    {
        [Header("Trigger")]
        [Tooltip("Si está activo, solo reaccionará a colliders con este tag.")]
        public bool requireTag = false;
        public string targetTag = "Player";

        [Header("VFX Targets (VisualEffect)")]
        [Tooltip("Arrastra aquí los VisualEffect (VFX Graph) que quieras controlar.")]
        public List<VisualEffect> vfxList = new List<VisualEffect>();

        [Header("Parámetro expuesto en VFX Graph")]
        [Tooltip("Nombre del parámetro expuesto. Recomendado: 'Active' (float).")]
        public string parameterName = "Active";
        [Tooltip("Valor que se aplicará al entrar en el trigger.")]
        public float enterValue = 1.5f;
        [Tooltip("Valor que se aplicará al salir del trigger.")]
        public float exitValue = 0f;

        [Header("Eventos opcionales de VFX Graph")]
        [Tooltip("Enviar un evento al entrar (útil para Start System Event o ramas personalizadas).")]
        public bool sendEnterEvent = false;
        public string enterEventName = "OnEnter";
        [Tooltip("Enviar un evento al salir.")]
        public bool sendExitEvent = false;
        public string exitEventName = "OnExit";

        [Header("Comportamiento")]
        [Tooltip("Aplica enterValue al entrar.")]
        public bool setOnEnter = true;
        [Tooltip("Aplica exitValue al salir.")]
        public bool resetOnExit = true;
        [Tooltip("Solo dispara una vez; ignora entradas posteriores.")]
        public bool onlyFirstEnter = false;

        private bool _hasFired = false;

        private void Reset()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true; // Asegura que sea trigger
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                parameterName = "Active";
        }

        private void OnTriggerEnter(Collider other)
        {
            if (requireTag && !other.CompareTag(targetTag)) return;
            if (onlyFirstEnter && _hasFired) return;

            if (setOnEnter) SetParameterOnAll(enterValue);
            if (sendEnterEvent) SendEventAll(enterEventName);

            _hasFired = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (requireTag && !other.CompareTag(targetTag)) return;
            if (!resetOnExit) return;

            SetParameterOnAll(exitValue);
            if (sendExitEvent) SendEventAll(exitEventName);
        }

        private void SetParameterOnAll(float value)
        {
            for (int i = 0; i < vfxList.Count; i++)
            {
                var v = vfxList[i];
                if (!v) continue;

                // Preferimos float, pero hacemos fallback a int si el graph expuso un int
                if (v.HasFloat(parameterName))
                {
                    v.SetFloat(parameterName, value);
                }
                else if (v.HasInt(parameterName))
                {
                    v.SetInt(parameterName, Mathf.RoundToInt(value));
                }
                else
                {
                    Debug.LogWarning($"[{name}] El parámetro '{parameterName}' no existe en '{v.name}'. Verifica que esté EXPUESTO en el Graph.");
                }
            }
        }

        private void SendEventAll(string eventName)
        {
            for (int i = 0; i < vfxList.Count; i++)
            {
                var v = vfxList[i];
                if (!v) continue;
                v.SendEvent(eventName);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test ► Enter Value")]
        private void EditorSetEnter() => SetParameterOnAll(enterValue);

        [ContextMenu("Test ► Exit Value")]
        private void EditorSetExit() => SetParameterOnAll(exitValue);
#endif
    }
}
