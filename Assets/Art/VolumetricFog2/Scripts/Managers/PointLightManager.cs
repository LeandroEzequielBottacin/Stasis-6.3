// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// namespace VolumetricFogAndMist2 {
//
//     [ExecuteInEditMode]
//     public class PointLightManager : MonoBehaviour, IVolumetricFogManager {
//
//         public string managerName {
//             get {
//                 return "Point Light Manager";
//             }
//         }
//
//         public const int MAX_POINT_LIGHTS = 16;
//
//         [Header("Point Light Search Settings")]
//         [Tooltip("Point lights are sorted by distance to tracking center object")]
//         public Transform trackingCenter;
//         public float newLightsCheckInterval = 3f;
//
//         [Header("Common Settings")]
//         [Tooltip("Global inscattering multiplier for point lights")]
//         public float inscattering = 1f;
//         [Tooltip("Global intensity multiplier for point lights")]
//         public float intensity = 1f;
//         [Tooltip("Reduces light intensity near point lights")]
//         public float insideAtten;
//
//         Light[] pointLights;
//         Vector4[] pointLightColorBuffer;
//         Vector4[] pointLightPositionBuffer;
//         float checkNewLightsLastTime;
//
//         private void OnEnable() {
//             if (trackingCenter == null) {
//                 Camera cam = null;
//                 Tools.CheckCamera(ref cam);
//                 if (cam != null) {
//                     trackingCenter = cam.transform;
//                 }
//             }
//             if (pointLightColorBuffer == null || pointLightColorBuffer.Length != MAX_POINT_LIGHTS) {
//                 pointLightColorBuffer = new Vector4[MAX_POINT_LIGHTS];
//             }
//             if (pointLightPositionBuffer == null || pointLightPositionBuffer.Length != MAX_POINT_LIGHTS) {
//                 pointLightPositionBuffer = new Vector4[MAX_POINT_LIGHTS];
//             }
//         }
//
//         private void LateUpdate() {
//             TrackPointLights();
//             SubmitPointLightData();
//         }
//
//         void SubmitPointLightData() {
//
//             int k = 0;
//             for (int i = 0; k < MAX_POINT_LIGHTS && i < pointLights.Length; i++) {
//                 Light light = pointLights[i];
//                 if (light == null || !light.isActiveAndEnabled || light.type != LightType.Point) continue;
//                 Vector3 pos = light.transform.position;
//                 float range = light.range * inscattering / 25f; // note: 25 comes from Unity point light attenuation equation
//                 float multiplier = light.intensity * intensity;
//
//                 if (range > 0 && multiplier > 0) {
//                     pointLightPositionBuffer[k].x = pos.x;
//                     pointLightPositionBuffer[k].y = pos.y;
//                     pointLightPositionBuffer[k].z = pos.z;
//                     pointLightPositionBuffer[k].w = 0;
//                     Color color = light.color;
//                     pointLightColorBuffer[k].x = color.r * multiplier;
//                     pointLightColorBuffer[k].y = color.g * multiplier;
//                     pointLightColorBuffer[k].z = color.b * multiplier;
//                     pointLightColorBuffer[k].w = range;
//                     k++;
//                 }
//             }
//
//             Shader.SetGlobalVectorArray("_VF2_PointLightColor", pointLightColorBuffer);
//             Shader.SetGlobalVectorArray("_VF2_FogPointLightPosition", pointLightPositionBuffer);
//             Shader.SetGlobalFloat("_VF2_PointLightInsideAtten", insideAtten);
//             Shader.SetGlobalInt("_VF2_PointLightCount", k);
//         }
//
//         /// <summary>
//         /// Look for nearest point lights
//         /// </summary>
//         public void TrackPointLights(bool forceImmediateUpdate = false) {
//
//             // Look for new lights?
//             if (forceImmediateUpdate || pointLights == null || !Application.isPlaying || (newLightsCheckInterval > 0 && Time.time - checkNewLightsLastTime > newLightsCheckInterval)) {
//                 checkNewLightsLastTime = Time.time;
//                 pointLights = FindObjectsOfType<Light>();
//                 System.Array.Sort(pointLights, pointLightsDistanceComparer);
//             }
//         }
//
//
//         int pointLightsDistanceComparer(Light l1, Light l2) {
//             float dist1 = (l1.transform.position - trackingCenter.position).sqrMagnitude;
//             float dist2 = (l2.transform.position - trackingCenter.position).sqrMagnitude;
//             if (dist1 < dist2) return -1;
//             if (dist1 > dist2) return 1;
//             return 0;
//         }
//
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
    public class PointLightManager : MonoBehaviour, IVolumetricFogManager {

        // ---- Interfaz requerida por el sistema ----
        public string managerName => "Point Light Manager";

        // ---- Prop IDs (cache) ----
        static readonly int PID_PointLightColor       = Shader.PropertyToID("_VF2_PointLightColor");
        static readonly int PID_PointLightPosition    = Shader.PropertyToID("_VF2_FogPointLightPosition");
        static readonly int PID_PointLightInsideAtten = Shader.PropertyToID("_VF2_PointLightInsideAtten");
        static readonly int PID_PointLightCount       = Shader.PropertyToID("_VF2_PointLightCount");

        // ---- Límite del shader ----
        public const int MAX_POINT_LIGHTS = 16;

        // ==============================
        //       Ajustes de Búsqueda
        // ==============================
        [Header("Point Light Search Settings")]
        [Tooltip("Centro de seguimiento para ordenar por distancia. Si está vacío, se toma la cámara activa.")]
        public Transform trackingCenter;

        [Tooltip("Intervalo (segundos) entre barridos para detectar/ordenar luces. 0 = cada frame (evitar en producción).")]
        [Min(0f)] public float newLightsCheckInterval = 3f;

        [Tooltip("Filtra por capas (solo luces en estas capas serán consideradas).")]
        public LayerMask lightLayerMask = ~0; // Everything

        [Tooltip("Si > 0, solo se consideran luces dentro de este radio del centro de tracking.")]
        [Min(0f)] public float trackingRadius;

        [Tooltip("Descarta luces con intensidad menor a este valor (antes de multiplicadores).")]
        [Min(0f)] public float minLightIntensity = 0.01f;

        [Tooltip("Descarta luces con rango menor a este valor (antes de multiplicadores).")]
        [Min(0f)] public float minLightRange = 0.1f;

        // ==============================
        //       Ajustes Globales
        // ==============================
        [Header("Common Settings")]
        [Tooltip("Multiplicador global de in-scattering para los point lights (afecta el alcance efectivo).")]
        [Min(0f)] public float inscattering = 1f;

        [Tooltip("Multiplicador global de intensidad de los point lights (afecta el color emitido).")]
        [Min(0f)] public float intensity = 1f;

        [Tooltip("Atenúa la intensidad cuando la cámara/fragmento está muy cerca de la luz. 0 = sin atenuación, 1 = máxima.")]
        [Range(0f, 1f)] public float insideAtten;

        [Tooltip("Si está activo, solo se envían luces habilitadas (isActiveAndEnabled).")]
        public bool onlyEnabledLights = true;

        // ==============================
        //     Política de Selección
        // ==============================
        [Header("Selection Policy")]
        [Tooltip("Favorece mantener luces ya seleccionadas para evitar cambios bruscos (popping).")]
        public bool preferPreviousSelection = true;

        [Tooltip("Ventaja (metros) aplicada a luces ya seleccionadas. A mayor valor, más estables permanecen.")]
        [Min(0f)] public float previousStayBiasMeters = 3f;

        [Tooltip("Cuántas luces crudas máximo se consideran por barrido antes de ordenar/filtrar (0 = sin límite).")]
        [Min(0f)] public int maxCandidatesPerScan;

        // ==============================
        //     Buffers y Estado Interno
        // ==============================
        Light[] _allSceneLights;                         // cache crudo del último barrido
        readonly List<Light> _candidates = new(128);     // post-filtro (capas/radio/tipo)
        readonly List<Light> _forced = new(32);          // con forceInclude
        readonly List<Light> _sorted = new(128);         // ordenados por prioridad/distancia
        readonly HashSet<Light> _lastSelected = new();   // para histeresis
        Vector4[] _pointLightColorBuffer;
        Vector4[] _pointLightPositionBuffer;

        float _checkNewLightsLastTime;
        Camera _cachedCam; // fallback tracking

        // cache overrides
        readonly Dictionary<Light, VF2PointLightOverride> _overrides = new(256);

        // ==============================
        //            Unity
        // ==============================
        void OnEnable() {
            EnsureTrackingCenter();
            EnsureBuffers();

            TrackPointLights(true);
            SubmitPointLightData();
        }

        void OnDisable() {
            Shader.SetGlobalInt(PID_PointLightCount, 0);
            _lastSelected.Clear();
            _overrides.Clear();
        }

        void LateUpdate() {
            TrackPointLights();
            SubmitPointLightData();
        }

        void OnValidate() {
            newLightsCheckInterval = Mathf.Max(0f, newLightsCheckInterval);
            inscattering = Mathf.Max(0f, inscattering);
            intensity = Mathf.Max(0f, intensity);
            insideAtten = Mathf.Clamp01(insideAtten);
            trackingRadius = Mathf.Max(0f, trackingRadius);
            minLightIntensity = Mathf.Max(0f, minLightIntensity);
            minLightRange = Mathf.Max(0f, minLightRange);
            previousStayBiasMeters = Mathf.Max(0f, previousStayBiasMeters);
            maxCandidatesPerScan = Mathf.Max(0, maxCandidatesPerScan);

            EnsureBuffers();
            TrackPointLights(true);
            SubmitPointLightData();
        }

        void OnDrawGizmosSelected() {
            if (trackingCenter == null) EnsureTrackingCenter();
            if (trackingCenter == null) return;

            if (trackingRadius > 0f) {
                Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.25f);
                Gizmos.DrawWireSphere(trackingCenter.position, trackingRadius);
            }
        }

        // ==============================
        //            API
        // ==============================
        [ContextMenu("Force Refresh Now")]
        public void ForceRefreshNow() {
            TrackPointLights(true);
            SubmitPointLightData();
        }

        /// <summary>
        /// Busca/ordena luces puntuales cercanas.
        /// </summary>
        public void TrackPointLights(bool forceImmediateUpdate = false) {
            bool needScan =
                forceImmediateUpdate ||
                _allSceneLights == null ||
                !Application.isPlaying ||
                (newLightsCheckInterval == 0f) ||
                (Time.time - _checkNewLightsLastTime > newLightsCheckInterval);

            if (!needScan) return;

            _checkNewLightsLastTime = Time.time;

            // Barrido crudo
#if UNITY_2023_1_OR_NEWER
            _allSceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            _allSceneLights = FindObjectsOfType<Light>();
#endif
            // Filtrado + cache de overrides
            RebuildCandidateLists();
            // Orden/Selección final
            BuildSortedSelection();
        }

        // ==============================
        //        Implementación
        // ==============================
        void EnsureTrackingCenter() {
            if (trackingCenter != null) return;

            if (_cachedCam == null) {
                Tools.Tools.CheckCamera(ref _cachedCam);
                if (_cachedCam == null) _cachedCam = Camera.main;
            }
            trackingCenter = _cachedCam ? _cachedCam.transform : transform;
        }

        void EnsureBuffers() {
            if (_pointLightColorBuffer == null || _pointLightColorBuffer.Length != MAX_POINT_LIGHTS) {
                _pointLightColorBuffer = new Vector4[MAX_POINT_LIGHTS];
            }
            if (_pointLightPositionBuffer == null || _pointLightPositionBuffer.Length != MAX_POINT_LIGHTS) {
                _pointLightPositionBuffer = new Vector4[MAX_POINT_LIGHTS];
            }
        }

        void RebuildCandidateLists() {
            _candidates.Clear();
            _forced.Clear();
            _overrides.Clear();

            if (_allSceneLights == null || _allSceneLights.Length == 0) return;

            Vector3 center = trackingCenter ? trackingCenter.position : Vector3.zero;
            bool useRadius = trackingRadius > 0f;
            float r2 = trackingRadius * trackingRadius;

            int added = 0;
            for (int i = 0; i < _allSceneLights.Length; i++) {
                var l = _allSceneLights[i];
                if (l == null) continue;
                if (l.type != LightType.Point) continue;
                if (onlyEnabledLights && !l.isActiveAndEnabled) continue;
                if (((1 << l.gameObject.layer) & lightLayerMask.value) == 0) continue;
                if (l.intensity < minLightIntensity) continue;
                if (l.range < minLightRange) continue;

                if (useRadius) {
                    float d2 = (l.transform.position - center).sqrMagnitude;
                    if (d2 > r2) continue;
                }

                // Cache override (si existe)
                var ov = l.GetComponent<VF2PointLightOverride>();
                if (ov != null) _overrides[l] = ov;

                bool force = ov != null && ov.forceInclude;
                if (force) _forced.Add(l);
                else _candidates.Add(l);

                // Limitar candidatos para performance si se solicitó
                if (maxCandidatesPerScan > 0 && ++added >= maxCandidatesPerScan) break;
            }
        }

        void BuildSortedSelection() {
            _sorted.Clear();
            _sorted.AddRange(_candidates);

            Vector3 center = trackingCenter ? trackingCenter.position : Vector3.zero;
            float stayBias2 = previousStayBiasMeters * previousStayBiasMeters;

            // Orden: prioridad (desc), luego distancia (asc, con sesgo a las ya seleccionadas)
            _sorted.Sort((a, b) => {
                int pa = 0, pb = 0;
                if (_overrides.TryGetValue(a, out var ova)) pa = ova.priority;
                if (_overrides.TryGetValue(b, out var ovb)) pb = ovb.priority;

                if (pa != pb) return pb.CompareTo(pa); // mayor prioridad primero

                float d2a = (a.transform.position - center).sqrMagnitude;
                float d2b = (b.transform.position - center).sqrMagnitude;

                if (preferPreviousSelection) {
                    if (_lastSelected.Contains(a)) d2a = Mathf.Max(0f, d2a - stayBias2);
                    if (_lastSelected.Contains(b)) d2b = Mathf.Max(0f, d2b - stayBias2);
                }

                return d2a.CompareTo(d2b); // más cerca primero
            });

            // Inserta forzadas al frente (respetando prioridad relativa entre ellas)
            if (_forced.Count > 0) {
                _forced.Sort((a, b) => {
                    int pa = 0, pb = 0;
                    if (_overrides.TryGetValue(a, out var ova)) pa = ova.priority;
                    if (_overrides.TryGetValue(b, out var ovb)) pb = ovb.priority;
                    return pb.CompareTo(pa);
                });
                _sorted.InsertRange(0, _forced);
            }

            // Recorta a top N para guardar como “última selección”
            _lastSelected.Clear();
            int cap = Mathf.Min(MAX_POINT_LIGHTS, _sorted.Count);
            for (int i = 0; i < cap; i++) _lastSelected.Add(_sorted[i]);
        }

        void SubmitPointLightData() {
            int count = 0;

            // Si no hubo scan (ej. primer frame) construimos selección rápida
            if (_sorted.Count == 0 && (_candidates.Count > 0 || _forced.Count > 0)) {
                BuildSortedSelection();
            }

            // Produce hasta MAX_POINT_LIGHTS con multiplicadores globales + overrides
            for (int i = 0; count < MAX_POINT_LIGHTS && i < _sorted.Count; i++) {
                Light light = _sorted[i];
                if (light == null) continue;
                if (onlyEnabledLights && !light.isActiveAndEnabled) continue;
                if (light.type != LightType.Point) continue;

                // Multiplicadores
                float localIntensityMul = 1f;
                float localRangeMul = 1f;
                if (_overrides.TryGetValue(light, out var ov)) {
                    localIntensityMul = Mathf.Max(0f, ov.extraIntensityMultiplier);
                    localRangeMul = Mathf.Max(0f, ov.extraRangeMultiplier);
                }

                // Posición
                Vector3 pos = light.transform.position;

                // Nota 25f: emula ecuación de atenuación de Unity
                float range = light.range * inscattering * localRangeMul / 25f;
                float mult  = light.intensity * intensity * localIntensityMul;

                if (range <= 0f || mult <= 0f) continue;

                // Posición (w sin uso)
                _pointLightPositionBuffer[count].x = pos.x;
                _pointLightPositionBuffer[count].y = pos.y;
                _pointLightPositionBuffer[count].z = pos.z;
                _pointLightPositionBuffer[count].w = 0f;

                // Color multiplicado
                Color c = light.color;
                _pointLightColorBuffer[count].x = c.r * mult;
                _pointLightColorBuffer[count].y = c.g * mult;
                _pointLightColorBuffer[count].z = c.b * mult;
                _pointLightColorBuffer[count].w = range;

                count++;
            }

            // Subida al shader
#if UNITY_2021_2_OR_NEWER
            Shader.SetGlobalVectorArray(PID_PointLightColor,    _pointLightColorBuffer);
            Shader.SetGlobalVectorArray(PID_PointLightPosition, _pointLightPositionBuffer);
            Shader.SetGlobalFloat(PID_PointLightInsideAtten,    insideAtten);
            Shader.SetGlobalInt(PID_PointLightCount,            count);
#else
            Shader.SetGlobalVectorArray("_VF2_PointLightColor",       _pointLightColorBuffer);
            Shader.SetGlobalVectorArray("_VF2_FogPointLightPosition", _pointLightPositionBuffer);
            Shader.SetGlobalFloat("_VF2_PointLightInsideAtten",       insideAtten);
            Shader.SetGlobalInt("_VF2_PointLightCount",               count);
#endif
        }
    }
}
