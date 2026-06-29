using System.Collections.Generic;
using UnityEngine;

namespace UIScripts.UI_Scan
{
    [DisallowMultipleComponent]
    public class Scannable : MonoBehaviour
    {
        public ScanDescriptor data;

        [Header("Pivots (uno o varios)")]
        [Tooltip("Si hay varios, el sistema elegirá el pivot más relevante respecto a la cámara.")]
        public List<Transform> pivots = new List<Transform>();

        [Tooltip("Compatibilidad: si no usás 'pivots', podés seguir usando este pivot único.")]
        public Transform pivot;

        [Tooltip("Offset aplicado al pivot elegido (útil para subir el rótulo).")]
        public Vector3 worldOffset = new Vector3(0, 1.6f, 0);

        [Header("Área de puntería")]
        [Tooltip("Si se deja vacío, se intentan buscar Renderers en hijos.")]
        public Renderer[] targetRenderers;

        [Tooltip("Padding en pantalla (px) alrededor del rect proyectado.")]
        public float screenPadding = 24f;

        [HideInInspector] public ScanLabelUI spawned;

        void OnEnable()  => ScannerManager.Register(this);
        void OnDisable() => ScannerManager.Unregister(this);

        void Reset()
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
            // Conveniencia: si no hay pivots, sugerimos uno por defecto
            if (pivots == null || pivots.Count == 0)
            {
                // no agregamos nada automáticamente para no ensuciar la jerarquía;
                // deja el campo listo para que arrastres tus empties
            }
        }

        /// <summary>
        /// Punto por defecto (compatibilidad). Usa Camera.main si existe.
        /// </summary>
        public Vector3 WorldPoint
        {
            get
            {
                var cam = Camera.main;
                return GetWorldPoint(cam);
            }
        }

        /// <summary>
        /// Devuelve el mejor punto de anclaje para este scannable en función de la cámara.
        /// - Si hay 'pivots' → elige el que esté más "centrado" en pantalla (y delante de la cámara).
        /// - Si no hay 'pivots' pero hay 'pivot' único → usa ese.
        /// - Si no hay nada → usa transform.position.
        /// </summary>
        public Vector3 GetWorldPoint(Camera cam)
        {
            // Candidatos: todos los pivots si existen, sino 'pivot' único, sino el propio transform
            var candidates = GetCandidateWorldPositions();

            if (cam == null)
            {
                // Sin cámara (caso raro), devolvemos el primero + offset
                return candidates[0] + worldOffset;
            }

            // Elegimos el candidato cuya proyección 2D esté más cerca del centro de pantalla y visible (z>0)
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float bestScore = float.MaxValue;
            Vector3 best = candidates[0];

            foreach (var c in candidates)
            {
                Vector3 wp = c + worldOffset;
                Vector3 sp = cam.WorldToScreenPoint(wp);
                if (sp.z <= 0f) continue; // detrás de la cámara → descartamos

                float screenDist = Vector2.Distance(center, new Vector2(sp.x, sp.y)) / (Screen.height * 0.5f); // normalizado
                float depth      = sp.z; // preferir puntos más cerca de cámara (ligero)
                float score      = screenDist * 10f + depth * 0.01f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            return best + worldOffset;
        }

        /// <summary>
        /// Devuelve todos los puntos candidatos (sin offset).
        /// </summary>
        public List<Vector3> GetCandidateWorldPositions()
        {
            var list = new List<Vector3>();

            if (pivots != null && pivots.Count > 0)
            {
                foreach (var p in pivots)
                    if (p) list.Add(p.position);
            }
            else if (pivot != null)
            {
                list.Add(pivot.position);
            }
            else
            {
                list.Add(transform.position);
            }

            return list;
        }

        public void EnsureLabel(Transform uiRoot, ScanLabelUI prefab)
        {
            if (spawned) return;
            spawned = Instantiate(prefab, uiRoot);
            spawned.Bind(this);
        }

        public void Show(bool instant = false) { if (spawned) spawned.Show(instant); }
        public void Hide(bool instant = false) { if (spawned) spawned.Hide(instant); }
    }
}
