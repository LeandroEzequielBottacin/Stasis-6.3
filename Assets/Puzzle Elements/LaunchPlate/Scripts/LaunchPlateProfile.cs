using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [CreateAssetMenu(menuName = "Puzzle/Launch Plate Profile", fileName = "LaunchPlateProfile")]
    public class LaunchPlateProfile : ScriptableObject
    {
        public enum PowerMode { Constant, ByDistance, ByHoldTime }

        [Header("Base")]
        [Tooltip("Velocidad inicial base (m/s) antes de aplicar curvas o multiplicadores.")]
        [Min(0)] public float baseInitialSpeed = 18f;

        [Tooltip("Límite superior del ángulo de lanzamiento (en grados). Evita arcos demasiado altos.")]
        [Range(5f, 45f)] public float maxElevationDeg = 25f;

        [Tooltip("Si existen dos soluciones de tiempo (rápida/lenta) similares, preferir la más lenta para trayectorias más estables.")]
        public bool preferSlowerBranch = true;

        [Header("Curvas (0..1)")]
        [Tooltip("Multiplicador de velocidad en función de la distancia normalizada (0..1). 1 = sin cambio. Útil para hacer saltos largos más veloces.")]
        public AnimationCurve speedByDistance = AnimationCurve.Linear(0, 0.9f, 1, 1.25f);

        [Tooltip("Ángulo de elevación (grados) en función de la distancia normalizada (0..1). Define si el arco es más chato o empinado.")]
        public AnimationCurve elevationByDistance = AnimationCurve.Linear(0, 10f, 1, 30f);

        [Tooltip("Multiplicador de potencia en función del tiempo de 'hold' normalizado (0..1). Usado cuando PowerMode = ByHoldTime.")]
        public AnimationCurve powerByHold = AnimationCurve.EaseInOut(0, 0f, 1, 1f);

        [Header("Búsqueda de T (tiempo de vuelo)")]
        [Tooltip("Tiempo mínimo de vuelo permitido para el solver (segundos).")]
        [Min(0.05f)] public float tMin = 0.15f;

        [Tooltip("Tiempo máximo de vuelo permitido para el solver (segundos).")]
        [Min(0.1f)] public float tMax = 2.0f;

        [Tooltip("Iteraciones de la búsqueda 1D del tiempo óptimo (más = más preciso, más costo).")]
        [Range(8, 64)] public int tSearchIterations = 32;

        [Header("Feel")]
        [Tooltip("Porción de momentum horizontal entrante a preservar (0 = reemplazar por completo, 1 = mantener totalmente).")]
        [Range(0,1)] public float preserveIncomingHoriz = 0.35f;

        [Tooltip("Retardo previo al lanzamiento (seg). Útil para anticipación/VFX/sonido.")]
        [Range(0,0.25f)] public float windupSeconds = 0.12f;

        [Tooltip("Margen de tolerancia tras entrar al trigger (seg) para permitir lanzar aunque se pierda el contacto instantáneo.")]
        [Range(0,0.2f)] public float coyoteTime = 0.08f;

        [Tooltip("Escala de tiempo aplicada durante el windup (1 = sin cambio). Menor a 1 genera leve slow-motion.")]
        [Range(0.5f,1f)] public float timeDilationDuringWindup = 1f; // 1 = sin cambio

        [Header("Modo de potencia")]
        [Tooltip("Cómo se determina la potencia: Constant (solo baseInitialSpeed), ByDistance (usa curvas por distancia), ByHoldTime (usa powerByHold).")]
        public PowerMode powerMode = PowerMode.Constant;

        [Tooltip("Rango de distancia en metros que se mapea a 0..1 para las curvas (x=min, y=max). Distancias fuera del rango se clampéan.")]
        public Vector2 distanceRemapMeters = new Vector2(2f, 25f);

        [Header("Nudges (micro-correcciones en vuelo)")]
        [Tooltip("Si > 0, remuestrea la ruta a esta cantidad de checkpoints uniformes (suaviza y hace los nudges más predecibles). 0 = usar hijos tal cual.")]
        [Range(0, 32)] public int overrideCheckpointCount;

        [Tooltip("Ventana temporal (seg) alrededor del instante objetivo de cada nudge. Más chico = corrección más puntual.")]
        [Range(0.02f, 0.6f)] public float nudgeTimeWindow = 0.12f;

        [Tooltip("Radio de influencia espacial (metros) para activar el nudge alrededor del checkpoint.")]
        [Min(0.05f)] public float nudgeProximity = 1.25f;

        [Tooltip("Ganancia proporcional (posición) del controlador de nudge. Sube exactitud pero puede verse 'forzado' si es muy alto.")]
        public float nudgeKp = 1.6f;

        [Tooltip("Ganancia derivativa (velocidad) del controlador de nudge. Ayuda a amortiguar y reducir oscilaciones.")]
        public float nudgeKd = 0.42f;

        [Tooltip("Fuerza máxima aplicada por nudge (Newtons). Limita cuánto puede corregir en cada frame.")]
        [Min(1f)] public float maxNudgeForce = 100f;

        [Tooltip("Si está activo, los nudges solo actúan en el plano lateral (no empujan en la dirección de la velocidad actual).")]
        public bool lateralOnly = true;
    }
}
