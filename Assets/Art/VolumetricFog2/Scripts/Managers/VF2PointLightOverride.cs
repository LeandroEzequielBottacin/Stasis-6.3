using UnityEngine;

namespace Art.VolumetricFog2.Scripts.Managers {
    [DisallowMultipleComponent]
    public class VF2PointLightOverride : MonoBehaviour {
        [Header("Overrides (Opcionales)")]
        [Tooltip("Incluye esta luz siempre que esté válida, ignorando orden por distancia (ocupa un slot).")]
        public bool forceInclude;

        [Tooltip("Prioridad de esta luz frente a otras. Mayor = primero. 0 = normal.")]
        public int priority;

        [Tooltip("Multiplicador extra de intensidad para esta luz (además del global).")]
        public float extraIntensityMultiplier = 1f;

        [Tooltip("Multiplicador extra de rango para esta luz (además del global).")]
        public float extraRangeMultiplier = 1f;
    }
}