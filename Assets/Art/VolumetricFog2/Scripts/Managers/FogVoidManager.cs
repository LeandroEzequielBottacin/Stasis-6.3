// using UnityEngine;
//
// namespace VolumetricFogAndMist2 {
//
//     [ExecuteInEditMode]
//     public class FogVoidManager : MonoBehaviour, IVolumetricFogManager {
//
//         public string managerName {
//             get {
//                 return "Fog Void Manager";
//             }
//         }
//
//         public const int MAX_FOG_VOID = 8;
//
//         [Header("Void Search Settings")]
//         public Transform trackingCenter;
//         public float newFogVoidCheckInterval = 3f;
//
//         FogVoid[] fogVoids;
//         Vector4[] fogVoidPositionAndSizes;
//         float checkNewFogVoidLastTime;
//         bool requireRefresh;
//
//
//         private void OnEnable() {
//             if (trackingCenter == null) {
//                 Camera cam = null;
//                 Tools.CheckCamera(ref cam);
//                 if (cam != null) {
//                     trackingCenter = cam.transform;
//                 }
//             }
//             if (fogVoidPositionAndSizes == null || fogVoidPositionAndSizes.Length != MAX_FOG_VOID) {
//                 fogVoidPositionAndSizes = new Vector4[MAX_FOG_VOID];
//             }
//         }
//
//         void SubmitFogVoidData() {
//
//             int k = 0;
//             for (int i = 0; k < MAX_FOG_VOID && i < fogVoids.Length; i++) {
//                 FogVoid fogVoid = fogVoids[i];
//                 if (fogVoid == null || !fogVoid.isActiveAndEnabled) continue;
//                 Vector3 pos = fogVoid.transform.position;
//                 fogVoidPositionAndSizes[k].x = pos.x;
//                 fogVoidPositionAndSizes[k].y = 10f * (1f - fogVoid.falloff);
//                 fogVoidPositionAndSizes[k].z = pos.z;
//                 fogVoidPositionAndSizes[k].w = 1f / (0.0001f + fogVoid.radius * fogVoid.radius);
//                 k++;
//             }
//             Shader.SetGlobalVectorArray("_VF2_FogVoidPositionAndSizes", fogVoidPositionAndSizes);
//             Shader.SetGlobalInt("_VF2_FogVoidCount", k);
//         }
//
//         /// <summary>
//         /// Look for nearest point lights
//         /// </summary>
//         public void TrackFogVoids(bool forceImmediateUpdate = false) {
//
//             // Look for new lights?
//             if (forceImmediateUpdate || fogVoids == null || !Application.isPlaying || (newFogVoidCheckInterval > 0 && Time.time - checkNewFogVoidLastTime > newFogVoidCheckInterval)) {
//                 checkNewFogVoidLastTime = Time.time;
//                 fogVoids = Object.FindObjectsOfType<FogVoid>();
//                 System.Array.Sort(fogVoids, fogVoidDistanceComparer);
//             }
//         }
//
//         int fogVoidDistanceComparer(FogVoid v1, FogVoid v2) {
//             float dist1 = (v1.transform.position - trackingCenter.position).sqrMagnitude;
//             float dist2 = (v2.transform.position - trackingCenter.position).sqrMagnitude;
//             if (dist1 < dist2) return -1;
//             if (dist1 > dist2) return 1;
//             return 0;
//         }
//
//         void LateUpdate() {
//             if (requireRefresh) {
//                 requireRefresh = false;
//                 TrackFogVoids(true);
//             } else {
//                 TrackFogVoids();
//             }
//             SubmitFogVoidData();
//         }
//
//         public void Refresh() {
//             requireRefresh = true;
//         }
//
//
//     }
//
// }

using System.Collections.Generic;
using UnityEngine;

namespace Art.VolumetricFog2.Scripts.Managers {

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class FogVoidManager : MonoBehaviour, IVolumetricFogManager {

        // ---- Interfaz requerida por el sistema ----
        public string managerName => "Fog Void Manager";

        // ---- Límite soportado por el shader ----
        public const int MAX_FOG_VOID = 8;

        // ---- Prop IDs (cache) ----
        static readonly int PID_FogVoidPosSize = Shader.PropertyToID("_VF2_FogVoidPositionAndSizes");
        static readonly int PID_FogVoidCount   = Shader.PropertyToID("_VF2_FogVoidCount");

        // ==============================
        //     Ajustes de Búsqueda
        // ==============================
        [Header("Void Search Settings")]
        [Tooltip("Centro de seguimiento para ordenar por distancia. Si está vacío, se toma la cámara activa.")]
        public Transform trackingCenter;

        [Tooltip("Intervalo (segundos) entre barridos para detectar y ordenar voids. 0 = cada frame (evitar en producción).")]
        [Min(0f)] public float newFogVoidCheckInterval = 3f;

        [Tooltip("Filtra por capas (solo FogVoid en estas capas serán considerados).")]
        public LayerMask fogVoidLayerMask = ~0; // Everything

        [Tooltip("Si > 0, solo se consideran FogVoids dentro de este radio del centro de tracking.")]
        [Min(0f)] public float trackingRadius;

        [Tooltip("Cantidad máxima de candidatos a considerar por barrido (0 = sin límite). Ayuda a performance en escenas grandes).")]
        [Min(0)] public int maxCandidatesPerScan;

        // ==============================
        //      Buffers y Estado
        // ==============================
        FogVoid[] _allVoidsRaw;
        readonly List<FogVoid> _candidates = new(64);
        readonly List<FogVoid> _sorted = new(64);

        Vector4[] _fogVoidPositionAndSizes; // x = pos.x, y = 10*(1-falloff), z = pos.z, w = 1/(radius^2 + eps)

        float _checkNewFogVoidLastTime;
        bool _requireRefresh;
        Camera _cachedCam;

        // ==============================
        //            Unity
        // ==============================
        void OnEnable() {
            EnsureTrackingCenter();
            EnsureBuffers();
            TrackFogVoids(true);
            SubmitFogVoidData();
        }

        void OnDisable() {
            // Limpia estado global para no arrastrar conteo previo
            Shader.SetGlobalInt(PID_FogVoidCount, 0);
        }

        void OnValidate() {
            newFogVoidCheckInterval = Mathf.Max(0f, newFogVoidCheckInterval);
            trackingRadius = Mathf.Max(0f, trackingRadius);
            maxCandidatesPerScan = Mathf.Max(0, maxCandidatesPerScan);
            EnsureBuffers();
            // Re-scan ligero en editor
            TrackFogVoids(true);
            SubmitFogVoidData();
        }

        void LateUpdate() {
            if (_requireRefresh) {
                _requireRefresh = false;
                TrackFogVoids(true);
            } else {
                TrackFogVoids();
            }
            SubmitFogVoidData();
        }

        void OnDrawGizmosSelected() {
            if (trackingCenter == null) EnsureTrackingCenter();
            if (trackingCenter == null) return;

            if (trackingRadius > 0f) {
                Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.25f);
                Gizmos.DrawWireSphere(trackingCenter.position, trackingRadius);
            }
        }

        // ==============================
        //             API
        // ==============================
        [ContextMenu("Force Refresh Now")]
        public void ForceRefreshNow() {
            TrackFogVoids(true);
            SubmitFogVoidData();
        }

        /// <summary>
        /// Marca que en el próximo LateUpdate se fuerce el reescaneo.
        /// </summary>
        public void Refresh() {
            _requireRefresh = true;
        }

        /// <summary>
        /// Busca y ordena los FogVoid cercanos.
        /// </summary>
        public void TrackFogVoids(bool forceImmediateUpdate = false) {

            bool needScan =
                forceImmediateUpdate ||
                _allVoidsRaw == null ||
                !Application.isPlaying ||
                (newFogVoidCheckInterval == 0f) ||
                (Time.time - _checkNewFogVoidLastTime > newFogVoidCheckInterval);

            if (!needScan) return;

            _checkNewFogVoidLastTime = Time.time;

            // Barrido crudo
#if UNITY_2023_1_OR_NEWER
            _allVoidsRaw = FindObjectsByType<FogVoid>(FindObjectsSortMode.None);
#else
            _allVoidsRaw = FindObjectsOfType<FogVoid>();
#endif
            // Filtrado y orden
            RebuildCandidateList();
            BuildSortedSelection();
        }

        // ==============================
        //       Implementación
        // ==============================
        void EnsureTrackingCenter() {
            if (trackingCenter != null) return;
            if (_cachedCam == null) {
                Tools.Tools.CheckCamera(ref _cachedCam); // helper del framework
                if (_cachedCam == null) _cachedCam = Camera.main;
            }
            trackingCenter = _cachedCam ? _cachedCam.transform : transform;
        }

        void EnsureBuffers() {
            if (_fogVoidPositionAndSizes == null || _fogVoidPositionAndSizes.Length != MAX_FOG_VOID) {
                _fogVoidPositionAndSizes = new Vector4[MAX_FOG_VOID];
            }
        }

        void RebuildCandidateList() {
            _candidates.Clear();
            if (_allVoidsRaw == null || _allVoidsRaw.Length == 0) return;

            Vector3 center = trackingCenter ? trackingCenter.position : Vector3.zero;
            bool useRadius = trackingRadius > 0f;
            float r2 = trackingRadius * trackingRadius;

            int added = 0;
            for (int i = 0; i < _allVoidsRaw.Length; i++) {
                var v = _allVoidsRaw[i];
                if (v == null) continue;
                if (!v.isActiveAndEnabled) continue;
                if (((1 << v.gameObject.layer) & fogVoidLayerMask.value) == 0) continue;

                if (useRadius) {
                    float d2 = (v.transform.position - center).sqrMagnitude;
                    if (d2 > r2) continue;
                }

                _candidates.Add(v);
                if (maxCandidatesPerScan > 0 && ++added >= maxCandidatesPerScan) break;
            }
        }

        void BuildSortedSelection() {
            _sorted.Clear();
            if (_candidates.Count == 0) return;

            Vector3 center = trackingCenter ? trackingCenter.position : Vector3.zero;

            // Orden por distancia ascendente
            _sorted.AddRange(_candidates);
            if (_sorted.Count > 1) {
                _sorted.Sort((a, b) => {
                    float d1 = (a.transform.position - center).sqrMagnitude;
                    float d2 = (b.transform.position - center).sqrMagnitude;
                    return d1.CompareTo(d2);
                });
            }
        }

        void SubmitFogVoidData() {
            int k = 0;

            if (_sorted.Count == 0 && _candidates.Count > 0) {
                // Si por timing no se ordenó aún, al menos tomamos candidatos crudos
                BuildSortedSelection();
            }

            int count = Mathf.Min(MAX_FOG_VOID, _sorted.Count);
            for (int i = 0; i < count; i++) {
                var fogVoid = _sorted[i];
                if (fogVoid == null || !fogVoid.isActiveAndEnabled) continue;

                // Clamps de seguridad (evitan NaNs en shader)
                float falloff = Mathf.Clamp01(fogVoid.falloff);
                float radius  = Mathf.Max(0.0001f, fogVoid.radius);

                Vector3 pos = fogVoid.transform.position;

                _fogVoidPositionAndSizes[k].x = pos.x;
                _fogVoidPositionAndSizes[k].y = 10f * (1f - falloff);
                _fogVoidPositionAndSizes[k].z = pos.z;
                _fogVoidPositionAndSizes[k].w = 1f / (radius * radius + 0.0001f);
                k++;
            }

            // Sube arrays y conteo (el shader ignorará slots sobrantes mediante count)
#if UNITY_2021_2_OR_NEWER
            Shader.SetGlobalVectorArray(PID_FogVoidPosSize, _fogVoidPositionAndSizes);
            Shader.SetGlobalInt(PID_FogVoidCount, k);
#else
            Shader.SetGlobalVectorArray("_VF2_FogVoidPositionAndSizes", _fogVoidPositionAndSizes);
            Shader.SetGlobalInt("_VF2_FogVoidCount", k);
#endif
        }
    }
}
