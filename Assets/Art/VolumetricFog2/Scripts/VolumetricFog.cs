// //------------------------------------------------------------------------------------------------------------------
// // Volumetric Fog & Mist 2
// // Created by Kronnect
// //------------------------------------------------------------------------------------------------------------------
//
// using System.Collections.Generic;
// using UnityEngine;
//
// namespace VolumetricFogAndMist2 {
//
//     public enum VolumetricFogShape {
//         Box,
//         Sphere
//     }
//
//     [ExecuteInEditMode]
//     public partial class VolumetricFog : MonoBehaviour {
//
//         public VolumetricFogProfile profile;
//
//         public bool enablePointLights;
//         public bool enableVoids;
//
//         const string SKW_SHAPE_BOX = "V2F_SHAPE_BOX";
//         const string SKW_SHAPE_SPHERE = "V2F_SHAPE_SPHERE";
//         const string SKW_POINT_LIGHTS = "VF2_POINT_LIGHTS";
//         const string SKW_VOIDS = "VF2_VOIDS";
//         const string SKW_FOW = "VF2_FOW";
//         const string SKW_RECEIVE_SHADOWS = "VF2_RECEIVE_SHADOWS";
//         const string SKW_DISTANCE = "VF2_DISTANCE";
//         const string SKW_DETAIL_NOISE = "V2F_DETAIL_NOISE";
//
//         Renderer r;
//         Material fogMat, noiseMat, turbulenceMat;
//         Material fogMat2D, noiseMat2D, turbulenceMat2D;
//         RenderTexture rtNoise, rtTurbulence;
//         float turbAcum;
//         Vector3 windDirectionAcum;
//         Vector3 sunDir;
//         float dayLight;
//         List<string> shaderKeywords;
//         Texture3D detailTex, refDetailTex;
//
//         void OnEnable() {
//             VolumetricFogManager manager = Tools.CheckMainManager();
//             gameObject.layer = manager.fogLayer;
//             FogOfWarInit();
//             UpdateMaterialProperties();
//         }
//
//         private void OnDisable() {
//             if (profile != null) {
//                 profile.onSettingsChanged -= UpdateMaterialProperties;
//             }
//         }
//
//         private void OnValidate() {
//             UpdateMaterialProperties();
//         }
//
//         private void OnDestroy() {
//             if (rtNoise != null) {
//                 rtNoise.Release();
//             }
//             if (rtTurbulence != null) {
//                 rtTurbulence.Release();
//             }
//             if (fogMat != null) {
//                 DestroyImmediate(fogMat);
//                 fogMat = null;
//             }
//             FogOfWarDestroy();
//         }
//
//         void OnDrawGizmosSelected() {
//             Gizmos.color = new Color(1, 1, 0, 0.75F);
//             Gizmos.DrawWireCube(transform.position, transform.lossyScale);
//         }
//
//         void LateUpdate() {
//             if (fogMat == null || r == null || profile == null) return;
//             Bounds bounds = r.bounds;
//             Vector3 center = bounds.center;
//             Vector3 extents = bounds.extents;
//
//             if (profile.shape == VolumetricFogShape.Sphere) {
//                 Vector3 scale = transform.localScale;
//                 if (scale.z != scale.x || scale.y != scale.x) {
//                     scale.z = scale.y = scale.x;
//                     transform.localScale = scale;
//                     extents = r.bounds.extents;
//                 }
//                 extents.x *= extents.x;
//             }
//
//             Vector4 border = new Vector4(extents.x * profile.border + 0.0001f, extents.x * (1f - profile.border), extents.z * profile.border + 0.0001f, extents.z * (1f - profile.border));
//             fogMat.SetVector("_BoundsCenter", center);
//             fogMat.SetVector("_BoundsExtents", extents);
//             fogMat.SetVector("_BoundsBorder", border);
//             fogMat.SetFloat("_BoundsVerticalOffset", profile.verticalOffset);
//
//             VolumetricFogManager globalManager = VolumetricFogManager.instance;
//             Light sun = globalManager.sun;
//             if (sun != null) {
//                 sunDir = -sun.transform.forward;
//                 fogMat.SetVector("_SunDir", sunDir);
//                 dayLight = 1f + sunDir.y * 2f;
//                 if (dayLight < 0) dayLight = 0; else if (dayLight > 1f) dayLight = 1f;
//                 float brightness;
//                 float alpha;
//                 if (profile != null) {
//                     brightness = profile.brightness;
//                     alpha = profile.albedo.a;
//                 } else {
//                     brightness = 1f;
//                     alpha = 1f;
//                 }
//                 Color lightColor = sun.color * (sun.intensity * brightness * dayLight * 2f);
//                 lightColor.a = alpha;
//                 fogMat.SetVector("_LightColor", lightColor);
//             }
//
//             windDirectionAcum += profile.windDirection * Time.deltaTime;
//             fogMat.SetVector("_WindDirection", windDirectionAcum);
//
//             transform.rotation = Quaternion.identity;
//
//             UpdateNoise();
//
//             if (enableFogOfWar) {
//                 UpdateFogOfWar();
//             }
//         }
//
//
//         void UpdateNoise() {
//             if (profile == null) return;
//             Texture noiseTex = profile.noiseTexture as Texture2D;
//             if (noiseTex == null) return;
//
//             if (rtTurbulence == null || rtTurbulence.width != noiseTex.width) {
//                 RenderTextureDescriptor desc = new RenderTextureDescriptor(noiseTex.width, noiseTex.height, RenderTextureFormat.ARGB32, 0);
//                 rtTurbulence = new RenderTexture(desc);
//                 rtTurbulence.wrapMode = TextureWrapMode.Repeat;
//             }
//             turbAcum += Time.deltaTime * profile.turbulence;
//             turbulenceMat.SetFloat("_Amount", turbAcum);
//             turbulenceMat.SetFloat("_NoiseStrength", profile.noiseStrength);
//             turbulenceMat.SetFloat("_NoiseFinalMultiplier", profile.noiseFinalMultiplier);
//             Graphics.Blit(noiseTex, rtTurbulence, turbulenceMat);
//
//             if (rtNoise == null || rtNoise.width != noiseTex.width) {
//                 RenderTextureDescriptor desc = new RenderTextureDescriptor(noiseTex.width, noiseTex.height, RenderTextureFormat.ARGB32, 0);
//                 rtNoise = new RenderTexture(desc);
//                 rtNoise.wrapMode = TextureWrapMode.Repeat;
//             }
//             noiseMat.SetColor("_SpecularColor", profile.specularColor);
//             noiseMat.SetFloat("_SpecularIntensity", profile.specularIntensity);
//
//             float spec = 1.0001f - profile.specularThreshold;
//             float nlighty = sunDir.y > 0 ? (1.0f - sunDir.y) : (1.0f + sunDir.y);
//             float nyspec = nlighty / spec;
//
//             noiseMat.SetFloat("_SpecularThreshold", nyspec);
//             noiseMat.SetVector("_SunDir", sunDir);
//
//             Color ambientColor = RenderSettings.ambientLight;
//             float ambientIntensity = RenderSettings.ambientIntensity;
//             Color ambientMultiplied = ambientColor * ambientIntensity;
//             float fogIntensity = 1.15f;
//             fogIntensity *= dayLight;
//             Color textureBaseColor = Color.Lerp(ambientMultiplied, profile.albedo * fogIntensity, fogIntensity);
//
//             noiseMat.SetColor("_Color", textureBaseColor);
//             Graphics.Blit(rtTurbulence, rtNoise, noiseMat);
//
//             fogMat.SetTexture("_MainTex", rtNoise);
//         }
//
//         public void UpdateMaterialProperties() {
//
//             if (!gameObject.activeInHierarchy) return;
//
//             r = GetComponent<Renderer>();
//
//             if (profile == null) {
//                 if (fogMat == null && r != null) {
//                     fogMat = new Material(Shader.Find("VolumetricFog2/Empty"));
//                     fogMat.hideFlags = HideFlags.DontSave;
//                     r.sharedMaterial = fogMat;
//                 }
//                 return;
//             }
//             profile.onSettingsChanged -= UpdateMaterialProperties;
//             profile.onSettingsChanged += UpdateMaterialProperties;
//
//             if (fogMat2D == null) {
//                 fogMat2D = new Material(Shader.Find("VolumetricFog2/VolumetricFog2DURP"));
//                 fogMat2D.hideFlags = HideFlags.DontSave;
//             }
//             fogMat = fogMat2D;
//             if (turbulenceMat2D == null) {
//                 turbulenceMat2D = new Material(Shader.Find("VolumetricFog2/Turbulence2D"));
//             }
//             turbulenceMat = turbulenceMat2D;
//             if (noiseMat2D == null) {
//                 noiseMat2D = new Material(Shader.Find("VolumetricFog2/Noise2DGen"));
//             }
//             noiseMat = noiseMat2D;
//
//             if (r != null) {
//                 r.sharedMaterial = fogMat;
//             }
//
//             if (fogMat == null || profile == null) return;
//
//             int sortingLayerId = profile.sortingLayerID;
//
//             if (!SortingLayer.IsValid(sortingLayerId)) {
//                 var layers = SortingLayer.layers;
//                 if (layers != null && layers.Length > 0) {
//                     int index = Mathf.Clamp(sortingLayerId, 0, layers.Length - 1);
//                     sortingLayerId = layers[index].id; // ← ID único real
//                 }
//             }
//
//             r.sortingLayerID = sortingLayerId;
//             r.sortingOrder  = profile.sortingOrder;
//             r.sortingOrder = profile.sortingOrder;
//             fogMat.renderQueue = profile.renderQueue;
//             float noiseScale = 0.1f / profile.noiseScale;
//             fogMat.SetFloat("_NoiseScale", noiseScale);
//             fogMat.SetFloat("_DeepObscurance", profile.deepObscurance);
//             fogMat.SetFloat("_LightDiffusionPower", profile.lightDiffusionPower);
//             fogMat.SetFloat("_LightDiffusionIntensity", profile.lightDiffusionIntensity);
//             fogMat.SetFloat("_ShadowIntensity", profile.shadowIntensity);
//             fogMat.SetFloat("_Density", profile.density);
//             fogMat.SetFloat("_FogStepping", profile.raymarchQuality);
//             fogMat.SetFloat("_DitherStrength", profile.dithering * 0.01f);
//             fogMat.SetFloat("_JitterStrength", profile.jittering);
//
//             if (profile.useDetailNoise) {
//                 fogMat.SetFloat("_DetailStrength", profile.detailStrength);
//                 fogMat.SetFloat("_DetailScale", (1f / profile.detailScale) * noiseScale);
//                 if ((detailTex == null || refDetailTex != profile.detailTexture) && profile.detailTexture != null) {
//                     refDetailTex = profile.detailTexture;
//                     Texture3D tex = new Texture3D(profile.detailTexture.width, profile.detailTexture.height, profile.detailTexture.depth, TextureFormat.Alpha8, false);
//                     tex.filterMode = FilterMode.Bilinear;
//                     Color32[] colors = profile.detailTexture.GetPixels32();
//                     for (int k=0;k<colors.Length;k++) { colors[k].a = colors[k].r; }
//                     tex.SetPixels32(colors);
//                     tex.Apply();
//                     detailTex = tex;
//                 }
//                 fogMat.SetTexture("_DetailTex", detailTex);
//             }
//
//             if (shaderKeywords == null) {
//                 shaderKeywords = new List<string>();
//             } else {
//                 shaderKeywords.Clear();
//             }
//
//             if (profile.distance > 0) {
//                 fogMat.SetVector("_DistanceData", new Vector4(0, 10f * (1f - profile.distanceFallOff), 0, 1f / (0.0001f + profile.distance * profile.distance)));
//                 shaderKeywords.Add(SKW_DISTANCE);
//             }
//             if (profile.shape == VolumetricFogShape.Box) shaderKeywords.Add(SKW_SHAPE_BOX); else shaderKeywords.Add(SKW_SHAPE_SPHERE);
//             if (enablePointLights) shaderKeywords.Add(SKW_POINT_LIGHTS);
//             if (enableVoids) shaderKeywords.Add(SKW_VOIDS);
//             if (profile.receiveShadows) shaderKeywords.Add(SKW_RECEIVE_SHADOWS);
//             if (enableFogOfWar) {
//                 fogMat.SetTexture("_FogOfWar", fogOfWarTexture);
//                 fogMat.SetVector("_FogOfWarCenter", fogOfWarCenter);
//                 fogMat.SetVector("_FogOfWarSize", fogOfWarSize);
//                 Vector3 ca = fogOfWarCenter - 0.5f * fogOfWarSize;
//                 fogMat.SetVector("_FogOfWarCenterAdjusted", new Vector3(ca.x / fogOfWarSize.x, 1f, ca.z / (fogOfWarSize.z + 0.0001f)));
//                 shaderKeywords.Add(SKW_FOW);
//             }
//             if (profile.useDetailNoise) shaderKeywords.Add(SKW_DETAIL_NOISE);
//             fogMat.shaderKeywords = shaderKeywords.ToArray();
//         }
//
//
//     }
//
//
// }
//------------------------------------------------------------------------------------------------------------------
// Volumetric Fog & Mist 2  (Enhanced)
// Created by Kronnect
// Revisión: mejoras de robustez, tooltips, null-checks, RT lifecycle, esfera sin forzar escala, gizmos por shape,
//           sorting layer robusto, clamps/specular, cache de Shader.PropertyToID, sin resetear rotación del volumen.
//------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Art.VolumetricFog2.Scripts.Managers;
using UnityEngine;

namespace Art.VolumetricFog2.Scripts {

    public enum VolumetricFogShape {
        Box,
        Sphere
    }

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public partial class VolumetricFog : MonoBehaviour {

        // -------------------------
        // Inspector (equipo)
        // -------------------------
        [Header("Profile / Comportamiento")]
        [Tooltip("Asset de configuración con densidad, ruido, color, sombras, etc.")]
        public VolumetricFogProfile profile;

        [Header("Features (Shader Toggles)")]
        [Tooltip("Activa soporte de luces puntuales en el shader (costo extra).")]
        public bool enablePointLights;

        [Tooltip("Activa 'voids' (huecos) en el volumen (costo extra).")]
        public bool enableVoids;

        // -------------------------
        // Shader Keywords
        // -------------------------
        const string SKW_SHAPE_BOX         = "V2F_SHAPE_BOX";
        const string SKW_SHAPE_SPHERE      = "V2F_SHAPE_SPHERE";
        const string SKW_POINT_LIGHTS      = "VF2_POINT_LIGHTS";
        const string SKW_VOIDS             = "VF2_VOIDS";
        const string SKW_FOW               = "VF2_FOW";
        const string SKW_RECEIVE_SHADOWS   = "VF2_RECEIVE_SHADOWS";
        const string SKW_DISTANCE          = "VF2_DISTANCE";
        const string SKW_DETAIL_NOISE      = "V2F_DETAIL_NOISE";

        // -------------------------
        // Shader Property IDs (cache)
        // -------------------------
        static readonly int PID_BoundsCenter         = Shader.PropertyToID("_BoundsCenter");
        static readonly int PID_BoundsExtents        = Shader.PropertyToID("_BoundsExtents");
        static readonly int PID_BoundsBorder         = Shader.PropertyToID("_BoundsBorder");
        static readonly int PID_BoundsVerticalOffset = Shader.PropertyToID("_BoundsVerticalOffset");

        static readonly int PID_SunDir               = Shader.PropertyToID("_SunDir");
        static readonly int PID_LightColor           = Shader.PropertyToID("_LightColor");
        static readonly int PID_WindDirection        = Shader.PropertyToID("_WindDirection");

        static readonly int PID_MainTex              = Shader.PropertyToID("_MainTex");
        static readonly int PID_NoiseScale           = Shader.PropertyToID("_NoiseScale");
        static readonly int PID_DeepObscurance       = Shader.PropertyToID("_DeepObscurance");
        static readonly int PID_LightDiffPower       = Shader.PropertyToID("_LightDiffusionPower");
        static readonly int PID_LightDiffIntensity   = Shader.PropertyToID("_LightDiffusionIntensity");
        static readonly int PID_ShadowIntensity      = Shader.PropertyToID("_ShadowIntensity");
        static readonly int PID_Density              = Shader.PropertyToID("_Density");
        static readonly int PID_FogStepping          = Shader.PropertyToID("_FogStepping");
        static readonly int PID_DitherStrength       = Shader.PropertyToID("_DitherStrength");
        static readonly int PID_JitterStrength       = Shader.PropertyToID("_JitterStrength");

        static readonly int PID_DetailStrength       = Shader.PropertyToID("_DetailStrength");
        static readonly int PID_DetailScale          = Shader.PropertyToID("_DetailScale");
        static readonly int PID_DetailTex            = Shader.PropertyToID("_DetailTex");

        static readonly int PID_DistanceData         = Shader.PropertyToID("_DistanceData");

        static readonly int PID_TurbAmount           = Shader.PropertyToID("_Amount");
        static readonly int PID_TurbNoiseStrength    = Shader.PropertyToID("_NoiseStrength");
        static readonly int PID_TurbNoiseFinalMult   = Shader.PropertyToID("_NoiseFinalMultiplier");

        static readonly int PID_NoiseSpecColor       = Shader.PropertyToID("_SpecularColor");
        static readonly int PID_NoiseSpecIntensity   = Shader.PropertyToID("_SpecularIntensity");
        static readonly int PID_NoiseSpecThreshold   = Shader.PropertyToID("_SpecularThreshold");
        static readonly int PID_NoiseColor           = Shader.PropertyToID("_Color");

        static readonly int PID_FOW_Tex              = Shader.PropertyToID("_FogOfWar");
        static readonly int PID_FOW_Center           = Shader.PropertyToID("_FogOfWarCenter");
        static readonly int PID_FOW_Size             = Shader.PropertyToID("_FogOfWarSize");
        static readonly int PID_FOW_CenterAdjusted   = Shader.PropertyToID("_FogOfWarCenterAdjusted");

        // -------------------------
        // Runtime
        // -------------------------
        Renderer r;
        Material fogMat, noiseMat, turbulenceMat;
        Material fogMat2D, noiseMat2D, turbulenceMat2D;
        RenderTexture rtNoise, rtTurbulence;
        float turbAcum;
        Vector3 windDirectionAcum;
        Vector3 sunDir;
        float dayLight;
        List<string> shaderKeywords;
        Texture3D detailTex, refDetailTex;

        // FOW (definido en otra parte de la clase parcial)
        // bool enableFogOfWar; Texture2D fogOfWarTexture; Vector3 fogOfWarCenter, fogOfWarSize;
        // void FogOfWarInit() { ... } void FogOfWarDestroy() { ... } void UpdateFogOfWar() { ... }

        void OnEnable() {
            // Asegura manager y layer
            VolumetricFogManager manager = Tools.Tools.CheckMainManager();
            if (manager != null) {
                gameObject.layer = manager.fogLayer;
            }

            FogOfWarInit(); // (si la otra parte no existe, comentá esta línea)

            UpdateMaterialProperties();
        }

        private void OnDisable() {
            if (profile != null) {
                profile.onSettingsChanged -= UpdateMaterialProperties;
            }
            // Liberar RTs al desactivar también, evita picos de memoria al togglear objetos
            if (rtNoise != null)  { rtNoise.Release();      rtNoise = null; }
            if (rtTurbulence != null) { rtTurbulence.Release();  rtTurbulence = null; }
        }

        private void OnValidate() {
            UpdateMaterialProperties();
        }

        private void OnDestroy() {
            if (rtNoise != null) { rtNoise.Release(); rtNoise = null; }
            if (rtTurbulence != null) { rtTurbulence.Release(); rtTurbulence = null; }
            if (fogMat != null) {
                DestroyImmediate(fogMat);
                fogMat = null;
            }
            FogOfWarDestroy(); // (si usás FOW)
        }

        void OnDrawGizmosSelected() {
            var rend = GetComponent<Renderer>();
            if (rend == null) return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.75f);

            if (profile != null && profile.shape == VolumetricFogShape.Sphere) {
                // Radio efectivo: usa el mayor eje para no deformar la esfera
                Vector3 lossy = transform.lossyScale;
                float radius = 0.5f * Mathf.Max(lossy.x, Mathf.Max(lossy.y, lossy.z));
                Gizmos.DrawWireSphere(rend.bounds.center, radius);
            } else {
                Gizmos.DrawWireCube(rend.bounds.center, rend.bounds.size);
            }
        }

        void LateUpdate() {
            if (fogMat == null || r == null || profile == null) return;

            // --- Bounds base (no tocamos rotación del GO)
            Bounds bounds = r.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            // --- Esfera: NO forzar escala del transform; calcular radio efectivo
            if (profile.shape == VolumetricFogShape.Sphere) {
                Vector3 lossy = transform.lossyScale;
                float maxAxis = Mathf.Max(lossy.x, Mathf.Max(lossy.y, lossy.z));
                float radius = 0.5f * maxAxis;
                float r2 = radius * radius;
                // El shader original parece usar extents.x^2 como radio^2; conservamos esta semántica:
                extents.x = r2;
            }

            // --- Borde y uniforms de volumen
            Vector4 border = new Vector4(
                extents.x * profile.border + 0.0001f,
                extents.x * (1f - profile.border),
                extents.z * profile.border + 0.0001f,
                extents.z * (1f - profile.border)
            );

            fogMat.SetVector(PID_BoundsCenter, center);
            fogMat.SetVector(PID_BoundsExtents, extents);
            fogMat.SetVector(PID_BoundsBorder, border);
            fogMat.SetFloat(PID_BoundsVerticalOffset, profile.verticalOffset);

            // --- Luz direccional (sun)
            VolumetricFogManager globalManager = VolumetricFogManager.instance;
            Light sun = globalManager != null ? globalManager.sun : null;
            if (sun != null) {
                sunDir = -sun.transform.forward;
                fogMat.SetVector(PID_SunDir, sunDir);

                dayLight = 1f + sunDir.y * 2f;
                dayLight = Mathf.Clamp01(dayLight);

                float brightness = profile.brightness;
                float alpha = profile.albedo.a;

                Color lightColor = sun.color * (sun.intensity * brightness * dayLight * 2f);
                lightColor.a = alpha;
                fogMat.SetVector(PID_LightColor, lightColor);
            }

            // --- Viento
            windDirectionAcum += profile.windDirection * Time.deltaTime;
            fogMat.SetVector(PID_WindDirection, windDirectionAcum);

            // --- Ruido / Turbulencia
            UpdateNoise();

            // --- Fog of War
            if (enableFogOfWar) {
                UpdateFogOfWar();
            }
        }

        void UpdateNoise() {
            if (profile == null) return;
            Texture noiseTex = profile.noiseTexture;
            if (noiseTex == null) return;

            // --- Turbulence RT
            if (rtTurbulence == null || rtTurbulence.width != noiseTex.width) {
                var desc = new RenderTextureDescriptor(noiseTex.width, noiseTex.height, RenderTextureFormat.ARGB32, 0);
                rtTurbulence = new RenderTexture(desc) { wrapMode = TextureWrapMode.Repeat };
            }
            turbAcum += Time.deltaTime * profile.turbulence;

            if (turbulenceMat == null) return;
            turbulenceMat.SetFloat(PID_TurbAmount, turbAcum);
            turbulenceMat.SetFloat(PID_TurbNoiseStrength, profile.noiseStrength);
            turbulenceMat.SetFloat(PID_TurbNoiseFinalMult, profile.noiseFinalMultiplier);
            Graphics.Blit(noiseTex, rtTurbulence, turbulenceMat);

            // --- Noise RT
            if (rtNoise == null || rtNoise.width != noiseTex.width) {
                var desc = new RenderTextureDescriptor(noiseTex.width, noiseTex.height, RenderTextureFormat.ARGB32, 0);
                rtNoise = new RenderTexture(desc) { wrapMode = TextureWrapMode.Repeat };
            }

            if (noiseMat == null) return;

            noiseMat.SetColor(PID_NoiseSpecColor, profile.specularColor);
            noiseMat.SetFloat(PID_NoiseSpecIntensity, profile.specularIntensity);

            // Evitar división por 0 en spec
            float spec = Mathf.Max(1e-3f, 1.0001f - profile.specularThreshold);
            float nlighty = sunDir.y > 0 ? (1.0f - sunDir.y) : (1.0f + sunDir.y);
            float nyspec = Mathf.Clamp(nlighty / spec, 0f, 8f);

            noiseMat.SetFloat(PID_NoiseSpecThreshold, nyspec);
            noiseMat.SetVector(PID_SunDir, sunDir);

            // Color base del fog (mezcla con ambiente)
            Color ambientColor = RenderSettings.ambientLight;
            float ambientIntensity = RenderSettings.ambientIntensity;
            Color ambientMultiplied = ambientColor * ambientIntensity;

            float fogIntensity = 1.15f * dayLight;
            Color textureBaseColor = Color.Lerp(ambientMultiplied, profile.albedo * fogIntensity, fogIntensity);
            noiseMat.SetColor(PID_NoiseColor, textureBaseColor);

            Graphics.Blit(rtTurbulence, rtNoise, noiseMat);

            fogMat.SetTexture(PID_MainTex, rtNoise);
        }

        public void UpdateMaterialProperties() {
            if (!gameObject.activeInHierarchy) return;

            r = GetComponent<Renderer>();
            if (r == null) {
                Debug.LogError("VolumetricFog requiere un Renderer en el GameObject.", this);
                return;
            }

            if (profile == null) {
                // Material vacío por fallback
                if (fogMat == null) {
                    Shader empty = Shader.Find("VolumetricFog2/Empty");
                    if (empty == null) {
                        Debug.LogError("Shader 'VolumetricFog2/Empty' no encontrado.", this);
                        return;
                    }
                    fogMat = new Material(empty) { hideFlags = HideFlags.DontSave };
                    r.sharedMaterial = fogMat;
                }
                return;
            }

            // Re-suscribir a cambios del profile
            profile.onSettingsChanged -= UpdateMaterialProperties;
            profile.onSettingsChanged += UpdateMaterialProperties;

            // Asegurar materiales/shaders 2D
            if (fogMat2D == null) {
                Shader sh = Shader.Find("VolumetricFog2/VolumetricFog2DURP");
                if (sh == null) {
                    Debug.LogError("Shader 'VolumetricFog2/VolumetricFog2DURP' no encontrado.", this);
                    return;
                }
                fogMat2D = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            fogMat = fogMat2D;

            if (turbulenceMat2D == null) {
                Shader sh = Shader.Find("VolumetricFog2/Turbulence2D");
                if (sh == null) {
                    Debug.LogError("Shader 'VolumetricFog2/Turbulence2D' no encontrado.", this);
                    return;
                }
                turbulenceMat2D = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            turbulenceMat = turbulenceMat2D;

            if (noiseMat2D == null) {
                Shader sh = Shader.Find("VolumetricFog2/Noise2DGen");
                if (sh == null) {
                    Debug.LogError("Shader 'VolumetricFog2/Noise2DGen' no encontrado.", this);
                    return;
                }
                noiseMat2D = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            noiseMat = noiseMat2D;

            if (r != null) {
                r.sharedMaterial = fogMat;
            }

            if (fogMat == null) return;

            // Sorting Layer (robusto): si el ID no es válido, usar Default (0)
            int sortingLayerId = profile.sortingLayerID;
            if (!SortingLayer.IsValid(sortingLayerId)) {
                sortingLayerId = 0; // Default
            }
            r.sortingLayerID = sortingLayerId;
            r.sortingOrder   = profile.sortingOrder;

            // Render Queue
            fogMat.renderQueue = profile.renderQueue;

            // Escala de ruido (mantiene coherencia con shader)
            float noiseScale = 0.1f / Mathf.Max(0.1f, profile.noiseScale);
            fogMat.SetFloat(PID_NoiseScale, noiseScale);

            // Propiedades de iluminación/densidad
            fogMat.SetFloat(PID_DeepObscurance,       Mathf.Clamp(profile.deepObscurance, 0f, 2f));
            fogMat.SetFloat(PID_LightDiffPower,       Mathf.Clamp(profile.lightDiffusionPower, 0f, 64f));
            fogMat.SetFloat(PID_LightDiffIntensity,   Mathf.Clamp01(profile.lightDiffusionIntensity));
            fogMat.SetFloat(PID_ShadowIntensity,      Mathf.Clamp01(profile.shadowIntensity));
            fogMat.SetFloat(PID_Density,              Mathf.Max(0f, profile.density));
            fogMat.SetFloat(PID_FogStepping,          Mathf.Clamp(profile.raymarchQuality, 1f, 16f));
            fogMat.SetFloat(PID_DitherStrength,       Mathf.Clamp(profile.dithering * 0.01f, 0f, 0.02f)); // 1 => 1%
            fogMat.SetFloat(PID_JitterStrength,       Mathf.Clamp(profile.jittering, 0f, 2f));

            // Detail noise
            if (profile.useDetailNoise) {
                fogMat.SetFloat(PID_DetailStrength, Mathf.Clamp01(profile.detailStrength));
                fogMat.SetFloat(PID_DetailScale,    (1f / Mathf.Max(0.01f, profile.detailScale)) * noiseScale);

                if ((detailTex == null || refDetailTex != profile.detailTexture) && profile.detailTexture != null) {
                    refDetailTex = profile.detailTexture;

                    // Convertir a Alpha8 con canal R en A (optimiza memoria/sampling)
                    Texture3D tex = new Texture3D(profile.detailTexture.width, profile.detailTexture.height, profile.detailTexture.depth, TextureFormat.Alpha8, false) {
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Repeat
                    };
                    Color32[] colors = profile.detailTexture.GetPixels32();
                    for (int k = 0; k < colors.Length; k++) { colors[k].a = colors[k].r; }
                    tex.SetPixels32(colors);
                    tex.Apply();
                    detailTex = tex;
                }
                fogMat.SetTexture(PID_DetailTex, detailTex);
            } else {
                fogMat.SetTexture(PID_DetailTex, null);
            }

            // Keywords
            if (shaderKeywords == null) shaderKeywords = new List<string>(); else shaderKeywords.Clear();

            // Distance fade
            if (profile.distance > 0f) {
                float fall = 10f * (1f - Mathf.Clamp01(profile.distanceFallOff));
                float invDist2 = 1f / (0.0001f + profile.distance * profile.distance);
                fogMat.SetVector(PID_DistanceData, new Vector4(0, fall, 0, invDist2));
                shaderKeywords.Add(SKW_DISTANCE);
            }

            // Shape
            shaderKeywords.Add(profile.shape == VolumetricFogShape.Box ? SKW_SHAPE_BOX : SKW_SHAPE_SPHERE);

            // Toggles
            if (enablePointLights) shaderKeywords.Add(SKW_POINT_LIGHTS);
            if (enableVoids)       shaderKeywords.Add(SKW_VOIDS);
            if (profile.receiveShadows) shaderKeywords.Add(SKW_RECEIVE_SHADOWS);

            // Fog Of War (si está activo)
            if (enableFogOfWar) {
                fogMat.SetTexture(PID_FOW_Tex, fogOfWarTexture);
                fogMat.SetVector(PID_FOW_Center, fogOfWarCenter);
                fogMat.SetVector(PID_FOW_Size,   fogOfWarSize);

                Vector3 ca = fogOfWarCenter - 0.5f * fogOfWarSize;
                fogMat.SetVector(PID_FOW_CenterAdjusted, new Vector3(
                    ca.x / Mathf.Max(0.0001f, fogOfWarSize.x),
                    1f,
                    ca.z / Mathf.Max(0.0001f, fogOfWarSize.z)
                ));

                shaderKeywords.Add(SKW_FOW);
            }

            if (profile.useDetailNoise) shaderKeywords.Add(SKW_DETAIL_NOISE);

            fogMat.shaderKeywords = shaderKeywords.ToArray();
        }
    }
}
