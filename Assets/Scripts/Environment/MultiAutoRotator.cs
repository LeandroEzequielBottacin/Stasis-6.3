using System;
using System.Collections.Generic;
using UnityEngine;

namespace Environment
{
    [AddComponentMenu("Gameplay/Multirotador (varios objetos)")]
    public class MultiAutoRotator : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // MULTI AUTO ROTATOR
        // -------------------------------------------------------------------------
        // Propósito:
        //  - Rotar N objetos con configuración independiente: eje, velocidad y espacio.
        //  - Pensado para cero dropcalls: sin asignaciones por frame ni LINQ.
        //  - Todo configurable desde Inspector con tooltips y sliders.
        //
        // Cómo usar:
        //  1) Agregá este componente a un GameObject "sistema" (p.ej. _Rotators).
        //  2) En la lista "items", agregá entradas para cada objeto a rotar.
        //  3) Elegí eje, velocidad, espacio (Local/World) y modo de tiempo.
        //
        // Notas de performance:
        //  - Usa un bucle for indexado (sin GC).
        //  - Salta items con velocidad ~0 para evitar trabajo innecesario.
        //  - Cachea el vector de eje en OnValidate/Awake para no recalcular cada frame.
        // -------------------------------------------------------------------------

        [Serializable]
        public enum Axis
        {
            X, Y, Z, Custom
        }

        [Serializable]
        public enum SpaceMode
        {
            Local,    // rota en espacio local (habitual para props)
            World     // rota respecto al mundo (útil para antenas, señales, etc.)
        }

        [Serializable]
        public enum TimeMode
        {
            Scaled,    // usa Time.deltaTime (afecta pausa, timescale, etc.)
            Unscaled   // usa Time.unscaledDeltaTime (UI o props que deben seguir)
        }

        [Serializable]
        public class RotatorItem
        {
            [Header("Objeto a rotar")]
            [Tooltip("Transform del objeto que va a rotar.")]
            public Transform target;

            [Header("Eje de rotación")]
            [Tooltip("Eje base de rotación. Usá 'Custom' para un vector propio.")]
            public Axis axis = Axis.Y;

            [Tooltip("Vector de eje cuando el modo es 'Custom'.")]
            public Vector3 customAxis = Vector3.up;

            [Header("Velocidad")]
            [Tooltip("Grados por segundo. Negativo invierte el sentido.")]
            [Range(-720f, 720f)]
            public float speed = 90f;

            [Header("Espacio & Tiempo")]
            [Tooltip("Local: relativo al objeto. World: relativo al mundo.")]
            public SpaceMode space = SpaceMode.Local;

            [Tooltip("Scaled: deltaTime | Unscaled: unscaledDeltaTime")]
            public TimeMode timeMode = TimeMode.Scaled;

            [Header("Extras")]
            [Tooltip("Si está activo, aplica un ángulo inicial aleatorio en el eje elegido.")]
            public bool randomizeStartAngle = false;

            // ----------------- Caché interno (evita cálculos por frame) -----------------
            [NonSerialized] internal Vector3 cachedAxis = Vector3.up;
            [NonSerialized] internal bool axisValid = true;
            [NonSerialized] internal bool initialized = false;
        }

        [Header("Lista de objetos a rotar")]
        [Tooltip("Agregá una entrada por objeto. Cada uno tiene su propia config.")]
        public List<RotatorItem> items = new List<RotatorItem>();

        [Header("Opciones globales")]
        [Tooltip("Permite ejecutar en modo edición (Scene) para previsualizar rotaciones.")]
        public bool runInEditMode = false;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Recalcular ejes cacheados cuando cambian valores en el inspector.
            RebuildAxisCache();
        }
#endif

        private void Awake()
        {
            // Cache inicial en runtime.
            RebuildAxisCache();
            InitializeRandomStarts();
        }

        private void Update()
        {
#if UNITY_EDITOR
            // En modo Editor, permitir vista previa en Scene si se marca la opción.
            if (!Application.isPlaying && !runInEditMode) return;
#endif
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < count; i++)
            {
                RotatorItem it = items[i];
                if (it == null || it.target == null) continue;

                // Si velocidad casi cero, evitamos trabajo.
                if (it.speed > -0.0001f && it.speed < 0.0001f) continue;
                if (!it.axisValid) continue;

                float dt = (it.timeMode == TimeMode.Scaled) ? Time.deltaTime : Time.unscaledDeltaTime;
                float angle = it.speed * dt;
                if (Mathf.Approximately(angle, 0f)) continue;

                // Rotación según espacio elegido
                if (it.space == SpaceMode.Local)
                    it.target.Rotate(it.cachedAxis, angle, Space.Self);
                else
                    it.target.Rotate(it.cachedAxis, angle, Space.World);
            }
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private void RebuildAxisCache()
        {
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var it = items[i];
                if (it == null) continue;

                // Determinar vector de eje base
                Vector3 axisVec;
                switch (it.axis)
                {
                    case Axis.X: axisVec = Vector3.right; break;
                    case Axis.Y: axisVec = Vector3.up;    break;
                    case Axis.Z: axisVec = Vector3.forward; break;
                    case Axis.Custom: axisVec = it.customAxis; break;
                    default: axisVec = Vector3.up; break;
                }

                // Normalizar si es posible; validar
                float mag = axisVec.magnitude;
                if (mag > 1e-5f)
                {
                    it.cachedAxis = axisVec / mag;
                    it.axisValid = true;
                }
                else
                {
                    // Eje inválido (custom = 0,0,0)
                    it.cachedAxis = Vector3.up;
                    it.axisValid = false;
                }
            }
        }

        private void InitializeRandomStarts()
        {
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var it = items[i];
                if (it == null || it.target == null) continue;
                if (it.initialized) continue;

                if (it.randomizeStartAngle && it.axisValid)
                {
                    float startAngle = UnityEngine.Random.Range(0f, 360f);
                    if (it.space == SpaceMode.Local)
                        it.target.Rotate(it.cachedAxis, startAngle, Space.Self);
                    else
                        it.target.Rotate(it.cachedAxis, startAngle, Space.World);
                }

                it.initialized = true;
            }
        }
    }
}
