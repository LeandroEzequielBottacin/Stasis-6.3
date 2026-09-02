using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Stasis.Rendering
{
    /// <summary>
    /// Screen-space stasis outline.
    ///
    /// StasisEffect sets a rendering layer bit on the renderers it freezes. We draw just
    /// those with an override material into a mask holding object-space position and
    /// coverage, grow that into a distance field, and paint the electric ring in the gap.
    ///
    /// Filtering on the layer means the cost scales with how many objects are actually in
    /// stasis (normally one or two), not with how many could ever be frozen. Marking them
    /// with a material slot instead cost ~12,800 draw calls and ~8,500 SetPass calls per
    /// frame across the 6,820 renderers that carried it.
    ///
    /// Growing a mask cannot tear the way an extruded hull does, so this stays clean on
    /// hard-edged meshes where an inverted hull splits open at every corner.
    /// </summary>
    public class StasisOutlineFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            [Header("Shape")]
            [Tooltip("Outline width in pixels, at the mask's resolution.")]
            [Range(1f, 64f)] public float widthPixels = 6f;

            [Tooltip("Disc samples for the dilation. Lower is cheaper; too low and the outline edge goes ragged.")]
            [Range(4, 96)] public int taps = 48;

            [Tooltip("1 = full resolution. 2 = half, which is much cheaper and slightly softer.")]
            [Range(1, 4)] public int downsample = 1;

            [Tooltip("Higher values pull the glow tighter against the silhouette.")]
            [Range(0.1f, 6f)] public float edgeFalloff = 1f;

            [Tooltip("How much energy bleeds back over the object itself. 0 = outline only.")]
            [Range(0f, 1f)] public float innerGlow = 0f;

            [Header("Colour")]
            [ColorUsage(true, true)] public Color color = new Color(1.64f, 3.44f, 0.76f, 1f);
            [Range(0f, 20f)] public float intensity = 1.4f;
            [Range(0f, 2f)] public float baseFloor = 0.05f;

            [Header("Arcs")]
            [Range(0f, 4f)] public float arcStrength = 2f;
            [Range(0.1f, 200f)] public float arcScale = 55f;
            [Range(0f, 10f)] public float arcSpeed = 2f;
            [Range(1f, 200f)] public float arcThinness = 70f;
            [Range(0.5f, 8f)] public float arcSharpness = 1.5f;
            [Range(0f, 1f)] public float arcBranching = 0.7f;

            [Header("Plasma")]
            [Range(0f, 4f)] public float plasmaStrength = 0.45f;
            [Range(0.1f, 40f)] public float plasmaScale = 8f;
            [Range(0f, 10f)] public float plasmaSpeed = 0.8f;

            [Header("Band Jitter")]
            [Range(0f, 1f)] public float jitterStrength = 0.4f;
            [Range(0.1f, 40f)] public float jitterScale = 9f;
            [Range(0f, 10f)] public float jitterSpeed = 3f;

            [Header("Flicker")]
            [Range(0f, 1f)] public float flickerStrength = 0.35f;
            [Range(0f, 60f)] public float flickerSpeed = 18f;
        }

        [SerializeField] private Settings settings = new Settings();

        private const string ShaderPath = "Hidden/Stasis/OutlineScreen";
        private const string MaskShaderPath = "Hidden/Stasis/OutlineMask";

        private Material _material;
        private Material _maskMaterial;
        private StasisOutlinePass _pass;

        public override void Create()
        {
            var shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"{nameof(StasisOutlineFeature)}: shader '{ShaderPath}' not found. " +
                               "The stasis outline will not render.");
                return;
            }

            var maskShader = Shader.Find(MaskShaderPath);
            if (maskShader == null)
            {
                Debug.LogError($"{nameof(StasisOutlineFeature)}: shader '{MaskShaderPath}' not found. " +
                               "The stasis outline will not render.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            _pass = new StasisOutlinePass(settings, _material, _maskMaterial)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null) return;

            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;

            _pass.renderPassEvent = settings.renderPassEvent;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            CoreUtils.Destroy(_maskMaterial);
            _material = null;
            _maskMaterial = null;
            _pass = null;
        }

        private class StasisOutlinePass : ScriptableRenderPass
        {
            // Any tag the scene's opaque materials actually have. It only decides which
            // renderers are eligible; what gets drawn is the override material.
            private static readonly ShaderTagId ForwardTag = new ShaderTagId("UniversalForward");

            private static readonly int TexelSizeId = Shader.PropertyToID("_StasisTexelSize");
            private static readonly int RadiusId = Shader.PropertyToID("_StasisRadius");
            private static readonly int TapsId = Shader.PropertyToID("_StasisTaps");
            private static readonly int ColorId = Shader.PropertyToID("_StasisColor");
            private static readonly int IntensityId = Shader.PropertyToID("_StasisIntensity");
            private static readonly int BaseFloorId = Shader.PropertyToID("_StasisBaseFloor");
            private static readonly int EdgeFalloffId = Shader.PropertyToID("_StasisEdgeFalloff");
            private static readonly int InnerGlowId = Shader.PropertyToID("_StasisInnerGlow");
            private static readonly int ArcStrengthId = Shader.PropertyToID("_StasisArcStrength");
            private static readonly int ArcScaleId = Shader.PropertyToID("_StasisArcScale");
            private static readonly int ArcSpeedId = Shader.PropertyToID("_StasisArcSpeed");
            private static readonly int ArcThinnessId = Shader.PropertyToID("_StasisArcThinness");
            private static readonly int ArcSharpnessId = Shader.PropertyToID("_StasisArcSharpness");
            private static readonly int ArcBranchingId = Shader.PropertyToID("_StasisArcBranching");
            private static readonly int PlasmaStrengthId = Shader.PropertyToID("_StasisPlasmaStrength");
            private static readonly int PlasmaScaleId = Shader.PropertyToID("_StasisPlasmaScale");
            private static readonly int PlasmaSpeedId = Shader.PropertyToID("_StasisPlasmaSpeed");
            private static readonly int JitterStrengthId = Shader.PropertyToID("_StasisJitterStrength");
            private static readonly int JitterScaleId = Shader.PropertyToID("_StasisJitterScale");
            private static readonly int JitterSpeedId = Shader.PropertyToID("_StasisJitterSpeed");
            private static readonly int FlickerStrengthId = Shader.PropertyToID("_StasisFlickerStrength");
            private static readonly int FlickerSpeedId = Shader.PropertyToID("_StasisFlickerSpeed");
            private static readonly int MaskTexId = Shader.PropertyToID("_StasisMask");
            private static readonly int DilatedTexId = Shader.PropertyToID("_StasisDilated");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

            private readonly Settings _settings;
            private readonly Material _material;
            private readonly Material _maskMaterial;

            public StasisOutlinePass(Settings settings, Material material, Material maskMaterial)
            {
                _settings = settings;
                _material = material;
                _maskMaterial = maskMaterial;
                // The mask pass rejects occluded fragments against the depth texture
                // rather than binding the camera depth as an attachment, which would
                // force the mask to carry the camera's MSAA sample count.
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            private class MaskPassData
            {
                public RendererListHandle RendererList;
            }

            private class BlitPassData
            {
                public Material Material;
                public int Pass;
                public TextureHandle Source;
                public Vector4 TexelSize;
                public float Radius;
                public int Taps;
            }

            private class CompositePassData
            {
                public Material Material;
                public TextureHandle Mask;
                public TextureHandle Dilated;
                public Settings Settings;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();

                int scale = Mathf.Max(1, _settings.downsample);
                var desc = cameraData.cameraTargetDescriptor;
                desc.width = Mathf.Max(1, desc.width / scale);
                desc.height = Mathf.Max(1, desc.height / scale);
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                // Half floats hold an object-space position comfortably, and we need the
                // signed range: a plain UNorm target would clamp everything to 0..1.
                desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;

                var mask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_StasisMask", true);
                var dilated = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_StasisDilated", false);

                var texelSize = new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height);
                float radius = _settings.widthPixels / scale;

                // --- 1. mark the objects in stasis ---------------------------------
                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Stasis Outline Mask", out var data))
                {
                    var sortingSettings = new SortingSettings(cameraData.camera)
                    {
                        criteria = SortingCriteria.CommonOpaque
                    };
                    var drawingSettings = new DrawingSettings(ForwardTag, sortingSettings)
                    {
                        perObjectData = PerObjectData.None,
                        enableInstancing = true,
                        overrideMaterial = _maskMaterial,
                        overrideMaterialPassIndex = 0
                    };
                    // The whole point: only renderers currently flagged as in stasis are
                    // considered, so this costs one or two draws instead of thousands.
                    var filteringSettings = new FilteringSettings(RenderQueueRange.all)
                    {
                        renderingLayerMask = StasisRenderingLayers.StasisMask
                    };

                    data.RendererList = renderGraph.CreateRendererList(
                        new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings));

                    builder.UseRendererList(data.RendererList);
                    builder.SetRenderAttachment(mask, 0);
                    // Occlusion is handled in the mask shader against the depth texture,
                    // so no depth attachment here and the mask stays non-MSAA.
                    builder.UseTexture(resourceData.cameraDepthTexture);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((MaskPassData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(d.RendererList);
                    });
                }

                // --- 2. grow it into a distance field -------------------------------
                AddDilatePass(renderGraph, "Stasis Outline Dilate", mask, dilated, 0, texelSize, radius);

                // --- 3. paint the electricity into the ring -------------------------
                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("Stasis Outline Composite", out var data))
                {
                    data.Material = _material;
                    data.Mask = mask;
                    data.Dilated = dilated;
                    data.Settings = _settings;

                    builder.UseTexture(mask);
                    builder.UseTexture(dilated);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    // The mask textures and _BlitScaleBias are bound as globals below.
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((CompositePassData d, RasterGraphContext ctx) =>
                    {
                        var s = d.Settings;
                        var m = d.Material;

                        m.SetColor(ColorId, s.color);
                        m.SetFloat(IntensityId, s.intensity);
                        m.SetFloat(BaseFloorId, s.baseFloor);
                        m.SetFloat(EdgeFalloffId, s.edgeFalloff);
                        m.SetFloat(InnerGlowId, s.innerGlow);

                        m.SetFloat(ArcStrengthId, s.arcStrength);
                        m.SetFloat(ArcScaleId, s.arcScale);
                        m.SetFloat(ArcSpeedId, s.arcSpeed);
                        m.SetFloat(ArcThinnessId, s.arcThinness);
                        m.SetFloat(ArcSharpnessId, s.arcSharpness);
                        m.SetFloat(ArcBranchingId, s.arcBranching);

                        m.SetFloat(PlasmaStrengthId, s.plasmaStrength);
                        m.SetFloat(PlasmaScaleId, s.plasmaScale);
                        m.SetFloat(PlasmaSpeedId, s.plasmaSpeed);

                        m.SetFloat(JitterStrengthId, s.jitterStrength);
                        m.SetFloat(JitterScaleId, s.jitterScale);
                        m.SetFloat(JitterSpeedId, s.jitterSpeed);

                        m.SetFloat(FlickerStrengthId, s.flickerStrength);
                        m.SetFloat(FlickerSpeedId, s.flickerSpeed);

                        ctx.cmd.SetGlobalTexture(MaskTexId, d.Mask);
                        ctx.cmd.SetGlobalTexture(DilatedTexId, d.Dilated);
                        ctx.cmd.SetGlobalVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));

                        ctx.cmd.DrawProcedural(Matrix4x4.identity, m, 1, MeshTopology.Triangles, 3, 1);
                    });
                }
            }

            private void AddDilatePass(RenderGraph renderGraph, string name, TextureHandle source,
                                       TextureHandle destination, int pass, Vector4 texelSize, float radius)
            {
                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(name, out var data))
                {
                    data.Material = _material;
                    data.Pass = pass;
                    data.Source = source;
                    data.TexelSize = texelSize;
                    data.Radius = radius;
                    data.Taps = _settings.taps;

                    builder.UseTexture(source);
                    builder.SetRenderAttachment(destination, 0);
                    builder.AllowPassCulling(false);
                    // Blitter.BlitTexture binds _BlitTexture and _BlitScaleBias globally.
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((BlitPassData d, RasterGraphContext ctx) =>
                    {
                        d.Material.SetVector(TexelSizeId, d.TexelSize);
                        d.Material.SetFloat(RadiusId, d.Radius);
                        d.Material.SetInteger(TapsId, d.Taps);

                        Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1f, 1f, 0f, 0f), d.Material, d.Pass);
                    });
                }
            }
        }
    }
}
