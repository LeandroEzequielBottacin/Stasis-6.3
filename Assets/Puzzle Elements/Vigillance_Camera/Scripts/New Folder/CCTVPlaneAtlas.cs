// CCTVPlaneAtlas.cs - MASTER/FOLLOWER with burst recording + LOOP playback
// - Solo graba mientras ve al Player, hasta recordMaxSeconds.
// - Luego deja de renderear y hace loop del clip: play -> blackout -> play (repite).
// - Master graba y compone; Followers copian tiles del Master (su propia selecci�n).
// - Layout manual centrado por fila. Sin shaders custom obligatorios.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Puzzle_Elements.Vigillance_Camera.Scripts.New_Folder
{
    [DisallowMultipleComponent]
    public class CctvPlaneAtlas : MonoBehaviour
    {
        public enum InstanceMode
        {
            Master,
            Follower
        }

        [Header("Instance Mode")] [Tooltip("Master hace render/record/playback. Follower copia tiles desde el Master.")]
        public InstanceMode mode = InstanceMode.Master;

        [Tooltip("Follower: referencia al Master para leer atlas y mapeo de tiles.")]
        public CctvPlaneAtlas masterForFollower;

        // ===== Registro est�tico =====
        static readonly List<VirtualSecurityCam> SSources = new();

        public static void RegisterSource(VirtualSecurityCam v)
        {
            if (v && !SSources.Contains(v)) SSources.Add(v);
        }

        public static void UnregisterSource(VirtualSecurityCam v)
        {
            if (v) SSources.Remove(v);
        }

        // ===== Fuentes =====
        [Header("Sources (grid order)")]
        [Tooltip("Master: vac�o => auto-fill desde registro. Follower: usa EXACTAMENTE estos cams.")]
        public VirtualSecurityCam[] cameras;

        // ===== Target =====
        [Header("Target (Plane)")] public Renderer targetRenderer;

        [Tooltip("Nombre de property de textura. Vac�o = auto-resolver.")]
        public string texturePropertyName = "";

        // ===== Layout manual =====
        [Header("Atlas Grid - Manual")] public int columns = 3;
        public int tileWidth = 384;
        public int tileHeight = 216;
        public int paddingX = 8;
        public int paddingY = 8;
        public Color clearColor = Color.black;

        // ===== Render (Master) =====
        [Header("Render - Master")] public bool allowHDR;
        public bool allowMSAA;

        [Header("URP - Master")] public int urpRendererIndex = -1;

        [Header("Overlay Top Layer - Master")] public LayerMask overlayTopLayer = 0;
        public bool overlayClearDepth = true;

        // ===== Recording / Playback =====
        [Header("Recording - Master")] [Tooltip("FPS mientras se escribe al ring buffer.")]
        public int recordFPS = 8;

        [Tooltip("M�ximo de segundos por burst (duraci�n del clip).")]
        public float recordMaxSeconds = 4f;

        public RenderTextureFormat recordFormat = RenderTextureFormat.ARGB32;

        [Header("Playback - Master")] public Transform playbackTriggerTarget;
        public float playbackTriggerDistance = 6f;

        [Tooltip("Si true, solo compone atlas cuando el viewer est� cerca del plane.")]
        public bool playbackRequiresView = true;

        public float playbackBlackoutSeconds = 1.0f;

        [Tooltip("Loop del clip de forma indefinida (play -> blackout -> play ...).")]
        public bool loopPlayback = true;

        // ===== Performance =====
        [Header("Performance - Master")] [Tooltip("Frecuencia (Hz) para componer atlas (playback/blackout).")]
        public float atlasUpdateFPS = 12f;

        [Tooltip("M�ximo de c�maras rendereadas por frame (solo Recording).")]
        public int maxCamRendersPerFrame = 2;

        [Tooltip("M�ximo de blits tile->atlas por frame.")]
        public int maxBlitsPerFrame = 3;

        public bool lazyClearAtlas = true;

        [Header("Performance - Follower")] public int followerMaxCopiesPerFrame = 8;

        public enum ForceProperty
        {
            Auto,
            BaseMap,
            MainTex,
            Custom
        }

        [Header("Art - Safe Controls (sin shaders custom)")]
        public bool forceUrpUnlit = true;

        public bool forceUnlitSetup = true;
        public ForceProperty forceTextureProperty = ForceProperty.Auto;
        public string customTextureProperty = "_BaseMap";
        public Color planeTint = Color.white;
        [Min(0f)] public float planeEmissionBoost;

        [Header("Art - Atlas Output (optional)")]
        public Material blitMaterial;

        public float atlasBrightness = 1f;
        public Color atlasColorMultiply = Color.white;

        [Header("Art - Per Tile")] public Color tileBlackoutColor = Color.black;

        [Header("Art - RenderTextures")] public RenderTextureFormat atlasFormat = RenderTextureFormat.ARGB32;
        public FilterMode atlasFilterMode = FilterMode.Bilinear;
        [Range(0, 16)] public int atlasAniso;

        private static Camera _sSharedCam;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
        private static readonly int AlbedoMap = Shader.PropertyToID("_AlbedoMap");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color1 = Shader.PropertyToID("_Color");
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");
        private static readonly int Brightness = Shader.PropertyToID("_Brightness");
        Camera _renderCam;
        RenderTexture _atlas;
        int _rows;
        string _resolvedProp;
        MaterialPropertyBlock _mpb;
        float _atlasUpdateTimer;
        bool _atlasEverCleared;
        private readonly List<VirtualSecurityCam> _activeMasterCams = new();

        class CamRuntime
        {
            public enum State
            {
                Idle,
                Recording,
                Playing,
                Blackout,
                RearmWait
            }

            // FSM
            public State state;
            public bool armed;

            // Render
            public RenderTexture tileRT;
            public float noTargetTimer;

            // Buffer
            public RenderTexture[] frames;
            public int frameWrite;
            public int frameCount;
            public int maxFrames;
            public float frameInterval;
            public float frameTimer;

            // Playback
            public bool playing;
            public bool inBlackout;
            public float blackoutTimer;
            public int playIndex; // �ndice absoluto dentro del ring
            public int playShownCount; // cu�ntos frames del clip se mostraron en este ciclo
            public float playTimer;
            public float playFrameInterval;
            public bool tileDirty;
            public int lastShownIndex;

            // Snapshot del clip (para loop)
            public int clipStartIndex; // inicio del clip en el ring
            public int clipFrameCount; // longitud del clip (frames)
        }

        private readonly Dictionary<VirtualSecurityCam, CamRuntime> _rt = new();

        private static void SafeDestroyRT(ref RenderTexture rt)
        {
            if (!rt) return;
            try
            {
                if (rt.IsCreated()) rt.Release();
            }
            catch
            {
                // ignored
            }

            Destroy(rt);
            rt = null;
        }

        private static void SafeDestroyRT(RenderTexture[] rts)
        {
            if (rts == null) return;
            for (int i = 0; i < rts.Length; i++) SafeDestroyRT(ref rts[i]);
        }

        private RenderTexture GetCurrentAtlas() => _atlas;
        public IReadOnlyList<VirtualSecurityCam> GetActiveMasterCameras() => _activeMasterCams;

        private bool GetMasterTilePixelRect(VirtualSecurityCam v, out RectInt rect)
        {
            rect = default;
            if (mode != InstanceMode.Master)
                return (masterForFollower) && masterForFollower.GetMasterTilePixelRect(v, out rect);

            int count = _activeMasterCams?.Count ?? 0;
            if (_activeMasterCams != null)
            {
                int idx = (count > 0) ? _activeMasterCams.IndexOf(v) : -1;
                if (idx < 0 || !_atlas) return false;

                int cols = Mathf.Max(1, columns);
                int row = idx / cols;
                int col = idx % cols;

                int tilesInRow = (row == _rows - 1) ? Mathf.Max(1, count - row * cols) : cols;
                tilesInRow = Mathf.Clamp(tilesInRow, 1, cols);

                int rowPixelWidth = tilesInRow * tileWidth + (tilesInRow + 1) * paddingX;
                int x0 = Mathf.Max(0, (_atlas.width - rowPixelWidth) / 2);

                int px = x0 + paddingX + col * (tileWidth + paddingX);
                int py = paddingY + row * (tileHeight + paddingY);
                rect = new RectInt(px, py, tileWidth, tileHeight);
            }

            return true;
        }

        // ===== Lifecycle =====
        void Awake()
        {
            _mpb ??= new MaterialPropertyBlock();

            if (mode == InstanceMode.Master)
            {
                SetupSharedRenderCamera();
                if (cameras == null || cameras.Length == 0) cameras = SSources.ToArray();
                RecreateAtlas();
                SafeSetupPlaneMaterial();
                BindAtlasToPlane(_atlas);
                InitPerCamState_Master();
            }
            else
            {
                RecreateAtlas(minimal: true, countOverride: cameras?.Length ?? 0);
                SafeSetupPlaneMaterial();
                BindAtlasToPlane(_atlas);
            }

            ApplyPlaneStyling();
        }

        void OnDestroy()
        {
            SafeDestroyRT(ref _atlas);
            foreach (var kv in _rt)
            {
                var cr = kv.Value;
                SafeDestroyRT(ref cr.tileRT);
                SafeDestroyRT(cr.frames);
            }

            _rt.Clear();
            _activeMasterCams.Clear();
        }

        // ===== Shared Render Camera =====
        void SetupSharedRenderCamera()
        {
            if (_sSharedCam)
            {
                _renderCam = _sSharedCam;
                ConfigureRenderCamera(_renderCam);
                return;
            }

            var go = new GameObject("CCTV_RenderCam_SHARED", typeof(Camera));
            _sSharedCam = go.GetComponent<Camera>();
            _renderCam = _sSharedCam;
            DontDestroyOnLoad(go);
            ConfigureRenderCamera(_renderCam);
        }

        void ConfigureRenderCamera(Camera cam)
        {
            if (!cam) return;
            cam.enabled = false;
            cam.backgroundColor = clearColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = allowHDR;
            cam.allowMSAA = allowMSAA;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.depth = -100;

            var urp = cam.GetComponent<UniversalAdditionalCameraData>();
            if (!urp) urp = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urp.renderType = CameraRenderType.Base;
            if (urpRendererIndex >= 0) urp.SetRenderer(urpRendererIndex);
            urp.antialiasing = AntialiasingMode.None;
            urp.renderPostProcessing = false;
            urp.requiresColorTexture = false;
            urp.requiresDepthTexture = false;
            urp.stopNaN = false;
            urp.dithering = false;
        }

        // ===== Plane material =====
        void SafeSetupPlaneMaterial()
        {
            if (!targetRenderer) return;

            if (forceUrpUnlit)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh)
                {
                    var mat = targetRenderer.sharedMaterial;
                    if (!mat || mat.shader != sh) targetRenderer.sharedMaterial = new Material(sh);

                    if (forceUnlitSetup)
                    {
                        targetRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        targetRenderer.receiveShadows = false;
                        var m = targetRenderer.material;
                        if (m)
                        {
                            if (m.HasProperty(BaseColor)) m.SetColor(BaseColor, Color.white);
                            if (m.HasProperty(Color1)) m.SetColor(Color1, Color.white);
                        }
                    }
                }
            }
        }

        void BindAtlasToPlane(Texture src)
        {
            if (!targetRenderer || !src) return;
            var mat = targetRenderer.material;
            if (!mat) return;

            string prop;
            switch (forceTextureProperty)
            {
                case ForceProperty.BaseMap: prop = "_BaseMap"; break;
                case ForceProperty.MainTex: prop = "_MainTex"; break;
                case ForceProperty.Custom: prop = customTextureProperty; break;
                default: prop = ResolveTextureProperty(mat, texturePropertyName); break;
            }

            if (!string.IsNullOrEmpty(prop) && mat.HasProperty(prop))
            {
                _resolvedProp = prop;
                if (mat.GetTexture(_resolvedProp) != src) mat.SetTexture(_resolvedProp, src);
            }
            else
            {
                _resolvedProp = ResolveTextureProperty(mat, texturePropertyName);
                if (!string.IsNullOrEmpty(_resolvedProp) && mat.GetTexture(_resolvedProp) != src)
                    mat.SetTexture(_resolvedProp, src);
            }

            if (mat.HasProperty(BaseColor)) mat.SetColor(BaseColor, Color.white);
            if (mat.HasProperty(Color1)) mat.SetColor(Color1, Color.white);
        }

        void ApplyPlaneStyling()
        {
            if (!targetRenderer) return;
            _mpb ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_mpb);

            if (planeTint != Color.white)
            {
                var sm = targetRenderer.sharedMaterial;
                if (sm && sm.HasProperty(BaseColor)) _mpb.SetColor(BaseColor, planeTint);
                if (sm && sm.HasProperty(Color1)) _mpb.SetColor(Color1, planeTint);
            }

            if (planeEmissionBoost > 0f)
            {
                Color e = planeTint * Mathf.LinearToGammaSpace(planeEmissionBoost);
                _mpb.SetColor(EmissionColor, e);
            }

            targetRenderer.SetPropertyBlock(_mpb);
        }

        string ResolveTextureProperty(Material m, string preferred)
        {
            if (!string.IsNullOrEmpty(preferred) && m.HasProperty(preferred)) return preferred;
            if (m.HasProperty(BaseMap)) return "_BaseMap";
            if (m.HasProperty(MainTex)) return "_MainTex";
            if (m.HasProperty(BaseColorMap)) return "_BaseColorMap";
            if (m.HasProperty(AlbedoMap)) return "_AlbedoMap";
            return "";
        }

        // ===== Atlas =====
        void RecreateAtlas(bool minimal = false, int countOverride = -1)
        {
            var count = (countOverride >= 0) ? countOverride : cameras?.Length ?? 0;
            columns = Mathf.Max(1, columns);
            _rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));

            int w, h;
            if (minimal || count <= 0)
            {
                w = Mathf.Max(16, paddingX * 2 + 16);
                h = Mathf.Max(16, paddingY * 2 + 16);
            }
            else
            {
                w = columns * tileWidth + (columns + 1) * paddingX;
                h = _rows * tileHeight + (_rows + 1) * paddingY;
            }

            if (_atlas && _atlas.width == w && _atlas.height == h && _atlas.format == atlasFormat) return;

            SafeDestroyRT(ref _atlas);
            _atlas = new RenderTexture(w, h, 0, atlasFormat, RenderTextureReadWrite.Default)
            {
                name = (mode == InstanceMode.Master ? "CCTV_ATLAS_MASTER_" : "CCTV_ATLAS_FOLLOWER_") + w + "x" + h,
                filterMode = atlasFilterMode,
                anisoLevel = atlasAniso,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            _atlas.Create();
            _atlasEverCleared = false;
        }

        Rect TileViewport01_Centered(int idx, int count)
        {
            int cols = Mathf.Max(1, columns);
            int col = idx % cols;
            int row = idx / cols;

            int tilesInRow = (row == _rows - 1) ? Mathf.Max(1, count - row * cols) : cols;
            tilesInRow = Mathf.Clamp(tilesInRow, 1, cols);

            int rowPixelWidth = tilesInRow * tileWidth + (tilesInRow + 1) * paddingX;
            int x0 = Mathf.Max(0, (_atlas.width - rowPixelWidth) / 2);

            int px = x0 + paddingX + col * (tileWidth + paddingX);
            int py = paddingY + row * (tileHeight + paddingY);

            float nx = px / (float)_atlas.width;
            float ny = py / (float)_atlas.height;
            float nw = tileWidth / (float)_atlas.width;
            float nh = tileHeight / (float)_atlas.height;
            return new Rect(nx, ny, nw, nh);
        }

        void ClearAtlasAll()
        {
            if (!_atlas) return;
            var prev = RenderTexture.active;
            RenderTexture.active = _atlas;
            GL.Viewport(new Rect(0, 0, _atlas.width, _atlas.height));
            GL.Clear(true, true, clearColor);
            RenderTexture.active = prev;
            _atlasEverCleared = true;
        }

        void ClearTileColor(int tileIndex, Color col, int count)
        {
            if (!_atlas) return;
            Rect vp = TileViewport01_Centered(tileIndex, count);

            int px = Mathf.RoundToInt(vp.x * _atlas.width);
            int py = Mathf.RoundToInt(vp.y * _atlas.height);
            int pw = Mathf.RoundToInt(vp.width * _atlas.width);
            int ph = Mathf.RoundToInt(vp.height * _atlas.height);

            var prev = RenderTexture.active;
            RenderTexture.active = _atlas;
            GL.Viewport(new Rect(px, py, pw, ph));
            GL.Clear(true, true, col);
            RenderTexture.active = prev;
        }

        void BlitTileToAtlas(RenderTexture src, int tileIndex, int count)
        {
            if (!_atlas || !src || !src.IsCreated()) return;
            Rect vp = TileViewport01_Centered(tileIndex, count);

            int px = Mathf.RoundToInt(vp.x * _atlas.width);
            int py = Mathf.RoundToInt(vp.y * _atlas.height);
            int pw = Mathf.RoundToInt(vp.width * _atlas.width);
            int ph = Mathf.RoundToInt(vp.height * _atlas.height);

            bool usePost = blitMaterial || atlasColorMultiply != Color.white ||
                           Mathf.Abs(atlasBrightness - 1f) > 0.0001f;

            var prev = RenderTexture.active;
            RenderTexture.active = _atlas;
            GL.Viewport(new Rect(px, py, pw, ph));

            if (usePost && blitMaterial)
            {
                if (blitMaterial.HasProperty(Color1)) blitMaterial.SetColor(Color1, atlasColorMultiply);
                if (blitMaterial.HasProperty(Brightness)) blitMaterial.SetFloat(Brightness, atlasBrightness);
                Graphics.Blit(src, _atlas, blitMaterial);
            }
            else if (src.width == pw && src.height == ph)
            {
                Graphics.CopyTexture(src, 0, 0, 0, 0, src.width, src.height, _atlas, 0, 0, px, py);
            }
            else
            {
                Graphics.Blit(src, (RenderTexture)null);
            }

            RenderTexture.active = prev;
        }

        // ===== Utils =====
        static bool IsTargetVisible(VirtualSecurityCam v, Vector3 camPos, Vector3 camFwd)
        {
            if (!v || !v.detectionTarget) return false;

            Vector3 toT = v.detectionTarget.position - camPos;
            float dist = toT.magnitude;
            if (dist < v.nearClip || dist > v.farClip) return false;

            float ang = Vector3.Angle(camFwd, toT);
            if (ang > Mathf.Max(1f, v.maxViewAngle)) return false;

            if (Physics.Raycast(camPos, toT.normalized, out var hit, dist, v.visibilityBlockers,
                    QueryTriggerInteraction.Ignore))
            {
                if (hit.collider && hit.collider.transform != v.detectionTarget) return false;
            }

            return true;
        }

        void EnsureCamerasArray_Master()
        {
            if (cameras == null || cameras.Length == 0) cameras = SSources.ToArray();
        }

        // ===== FSM =====
        void BeginRecording(CamRuntime cr)
        {
            cr.frameWrite = 0;
            cr.frameCount = 0;
            cr.frameTimer = 0f;
            cr.state = CamRuntime.State.Recording;

            cr.playing = false;
            cr.inBlackout = false;
            cr.playShownCount = 0;
            cr.lastShownIndex = -1;
            cr.tileDirty = true;
        }

        void FinishRecordingAndPlay(CamRuntime cr)
        {
            // Snapshot del clip (lo que se alcanz� a grabar)
            cr.clipStartIndex = Mod(cr.frameWrite - cr.frameCount, cr.maxFrames);
            cr.clipFrameCount = cr.frameCount;

            cr.playing = true;
            cr.state = CamRuntime.State.Playing;
            cr.playShownCount = 0;
            cr.playTimer = 0f;
            cr.playIndex = Mod(cr.clipStartIndex - 1, cr.maxFrames); // para que el primer advance muestre el inicio
            cr.lastShownIndex = -1;
            cr.tileDirty = true;
        }

        void EnterBlackout(CamRuntime cr)
        {
            cr.playing = false;
            cr.inBlackout = true;
            cr.state = CamRuntime.State.Blackout;
            cr.blackoutTimer = 0f;
        }

        // ===== MAIN =====
        void LateUpdate()
        {
            if (mode == InstanceMode.Follower)
            {
                LateUpdate_Follower();
                return;
            }

            // MASTER
            EnsureCamerasArray_Master();
            if (!_renderCam || cameras == null) return;

            // Layout esperado (NO uses _rows previo para la comparaci�n)
            int expectedRows = Mathf.Max(1, Mathf.CeilToInt((cameras.Length) / (float)Mathf.Max(1, columns)));
            int expectedW = columns * tileWidth + (columns + 1) * paddingX;
            int expectedH = expectedRows * tileHeight + (expectedRows + 1) * paddingY;

            bool needRecreate = (!_atlas) ||
                                (_atlas.width != expectedW) ||
                                (_atlas.height != expectedH) ||
                                (_atlas.format != atlasFormat) ||
                                (_rows != expectedRows);

            if (needRecreate)
            {
                RecreateAtlas(minimal: cameras.Length == 0, countOverride: cameras.Length);
                InitPerCamState_Master(); // solo cuando cambia layout/cantidad
            }

            float updateInterval = 1f / Mathf.Max(1f, atlasUpdateFPS);
            _atlasUpdateTimer += Time.deltaTime;
            bool doAtlasWork = _atlasUpdateTimer >= updateInterval;
            if (doAtlasWork) _atlasUpdateTimer -= updateInterval;

            if (!lazyClearAtlas || !_atlasEverCleared) ClearAtlasAll();

            float dt = Time.deltaTime;

            bool viewingPlane = !playbackRequiresView;
            if (playbackRequiresView && playbackTriggerTarget && targetRenderer)
            {
                Vector3 planePos = targetRenderer.bounds.center;
                float d = Vector3.Distance(planePos, playbackTriggerTarget.position);
                viewingPlane = (d <= playbackTriggerDistance);
            }

            int rendersLeft = Mathf.Max(0, maxCamRendersPerFrame);
            int blitsLeft = Mathf.Max(0, maxBlitsPerFrame);

            int masterCount = cameras.Length;
            if (masterCount != _rt.Count) InitPerCamState_Master();

            // ==== RENDER + GRAB (solo RECORDING y solo si ve al Player) ====
            for (int i = 0; i < masterCount && rendersLeft > 0; i++)
            {
                var v = cameras[i];
                if (!v) continue;
                if (!_rt.TryGetValue(v, out var cr)) continue;

                var p = v.Pivot;
                Vector3 desiredFwd = v.GetDesiredForward();
                if (desiredFwd.sqrMagnitude < 1e-6f) desiredFwd = p.forward;
                Quaternion rot = v.lockHorizon
                    ? Quaternion.LookRotation(desiredFwd, Vector3.up)
                    : (p.rotation * Quaternion.Euler(v.rotationOffsetEuler));
                Vector3 camPos = p.position;
                Vector3 camFwd = (rot * Vector3.forward);

                bool targetVisible = IsTargetVisible(v, camPos, camFwd);

                // Arranque de grabaci�n: solo si ve al Player
                if (cr.state == CamRuntime.State.Idle && targetVisible)
                    BeginRecording(cr);

                // Si ya tenemos clip (Playing/Blackout) y loopPlayback= true, NO volvemos a grabar.
                bool clipYaArmado = (cr.clipFrameCount > 0);
                if (clipYaArmado && (cr.state == CamRuntime.State.Playing || cr.state == CamRuntime.State.Blackout))
                {
                    // no hacemos nada caro aqu�
                }
                else if (cr.state == CamRuntime.State.Recording)
                {
                    // Si no ve, esperamos hasta stopAfterNoTargetSeconds para cerrar clip con lo que hay
                    if (!targetVisible)
                    {
                        cr.noTargetTimer += dt;
                        if (cr.noTargetTimer >= Mathf.Max(0.05f, v.stopAfterNoTargetSeconds))
                        {
                            // cerrar clip con lo acumulado
                            FinishRecordingAndPlay(cr);
                        }

                        // NO render ni grab si no ve (ahorra costo)
                        continue;
                    }

                    cr.noTargetTimer = 0f;

                    // Render solo si ve al Player
                    int maskBase = v.cullingMask.value;
                    if (overlayTopLayer != 0) maskBase = maskBase & ~overlayTopLayer.value;
                    if (maskBase == 0) maskBase = ~0;

                    _renderCam.transform.SetPositionAndRotation(camPos, rot);
                    _renderCam.fieldOfView = v.fieldOfView;
                    _renderCam.nearClipPlane = Mathf.Max(0.001f, v.nearClip);
                    _renderCam.farClipPlane = v.farClip;
                    _renderCam.cullingMask = maskBase;
                    _renderCam.targetTexture = cr.tileRT;
                    _renderCam.clearFlags = CameraClearFlags.SolidColor;
                    _renderCam.backgroundColor = clearColor;
                    _renderCam.Render();

                    if (overlayTopLayer != 0)
                    {
                        int overlayMask = v.cullingMask.value & overlayTopLayer.value;
                        if (overlayMask != 0)
                        {
                            _renderCam.cullingMask = overlayMask;
                            _renderCam.clearFlags =
                                overlayClearDepth ? CameraClearFlags.Depth : CameraClearFlags.Nothing;
                            _renderCam.Render();
                        }
                    }

                    _renderCam.targetTexture = null;
                    rendersLeft--;

                    // Escribir al ring a recordFPS
                    cr.frameTimer += dt;
                    if (cr.frameTimer >= cr.frameInterval)
                    {
                        cr.frameTimer -= cr.frameInterval;

                        var dst = cr.frames[cr.frameWrite];
                        if (dst) Graphics.Blit(cr.tileRT, dst);

                        cr.frameWrite = (cr.frameWrite + 1) % cr.maxFrames;
                        cr.frameCount = Mathf.Min(cr.frameCount + 1, cr.maxFrames);
                        cr.tileDirty = true;

                        // Si alcanzamos el m�ximo, cerramos clip y pasamos a loop
                        if (cr.frameCount >= cr.maxFrames)
                            FinishRecordingAndPlay(cr);
                    }
                }
            }

            // ==== PLAYBACK -> ATLAS (loop + blackout) ====
            if (viewingPlane && doAtlasWork)
            {
                for (int i = 0; i < masterCount && blitsLeft > 0; i++)
                {
                    var v = cameras[i];
                    if (!v) continue;
                    if (!_rt.TryGetValue(v, out var cr)) continue;

                    // Si a�n no hay nada grabado, limpiar tile
                    if (cr.clipFrameCount == 0 && cr.frameCount == 0)
                    {
                        ClearTileColor(i, tileBlackoutColor, masterCount);
                        continue;
                    }

                    // Estado Blackout: solo contar y limpiar
                    if (cr.state == CamRuntime.State.Blackout)
                    {
                        cr.blackoutTimer += (1f / Mathf.Max(1f, atlasUpdateFPS));
                        ClearTileColor(i, tileBlackoutColor, masterCount);

                        if (cr.blackoutTimer >= Mathf.Max(0.01f, playbackBlackoutSeconds))
                        {
                            if (loopPlayback && cr.clipFrameCount > 0)
                            {
                                // reiniciar PLAYING del clip
                                cr.inBlackout = false;
                                cr.playing = true;
                                cr.state = CamRuntime.State.Playing;
                                cr.playShownCount = 0;
                                cr.playTimer = 0f;
                                cr.playIndex = Mod(cr.clipStartIndex - 1, cr.maxFrames);
                                cr.lastShownIndex = -1;
                                cr.tileDirty = true;
                            }
                            else
                            {
                                // si no quer�s loop, podr�as pasar a RearmWait. Ac� mantenemos loop por defecto.
                                cr.blackoutTimer = 0f;
                            }
                        }

                        continue;
                    }

                    // Si estamos reproduciendo un clip
                    if (cr.state == CamRuntime.State.Playing && cr.clipFrameCount > 0)
                    {
                        cr.playTimer += (1f / Mathf.Max(1f, atlasUpdateFPS));
                        if (cr.playTimer >= cr.playFrameInterval)
                        {
                            cr.playTimer -= cr.playFrameInterval;

                            int nextRel = (cr.playShownCount + 1) % cr.clipFrameCount;
                            int next = Mod(cr.clipStartIndex + nextRel, cr.maxFrames);

                            cr.playIndex = next;
                            cr.playShownCount = Mathf.Min(cr.playShownCount + 1, cr.clipFrameCount);
                            cr.tileDirty = true;

                            if (cr.playShownCount >= cr.clipFrameCount)
                            {
                                EnterBlackout(cr);
                            }
                        }

                        if (cr.tileDirty && cr.playIndex != cr.lastShownIndex)
                        {
                            BlitTileToAtlas(cr.frames[cr.playIndex], i, masterCount);
                            cr.lastShownIndex = cr.playIndex;
                            cr.tileDirty = false;
                            blitsLeft--;
                        }
                    }
                    else
                    {
                        // Idle/Recording pero con alg�n dato: mostramos �ltimo frame v�lido
                        int baseIdx = (cr.clipFrameCount > 0)
                            ? Mod(cr.clipStartIndex + cr.clipFrameCount - 1, cr.maxFrames)
                            : Mod(cr.frameWrite - 1, cr.maxFrames);

                        if (baseIdx >= 0 && baseIdx < cr.frames.Length && cr.frames[baseIdx])
                        {
                            BlitTileToAtlas(cr.frames[baseIdx], i, masterCount);
                            blitsLeft--;
                        }
                        else
                        {
                            ClearTileColor(i, tileBlackoutColor, masterCount);
                        }
                    }
                }
            }

            BindAtlasToPlane(_atlas);
            ApplyPlaneStyling();

            if (!_atlasEverCleared && lazyClearAtlas) ClearAtlasAll();
        }

        // ===== Follower =====
        void LateUpdate_Follower()
        {
            int followerCount = cameras?.Length ?? 0;

            if (!masterForFollower || !masterForFollower.GetCurrentAtlas())
            {
                RecreateAtlas(minimal: true, countOverride: followerCount);
                BindAtlasToPlane(_atlas);
                ApplyPlaneStyling();
                return;
            }

            RecreateAtlas(minimal: followerCount == 0, countOverride: followerCount);
            if (!_atlasEverCleared) ClearAtlasAll();

            var masterAtlas = masterForFollower.GetCurrentAtlas();
            int copiesLeft = Mathf.Max(0, followerMaxCopiesPerFrame);

            if (followerCount == 0)
            {
                ClearAtlasAll();
            }
            else
            {
                for (int i = 0; i < followerCount && copiesLeft > 0; i++)
                {
                    var v = cameras?[i];
                    if (!v)
                    {
                        ClearTileColor(i, tileBlackoutColor, followerCount);
                        continue;
                    }

                    if (!masterForFollower.GetMasterTilePixelRect(v, out var srcRect))
                    {
                        ClearTileColor(i, tileBlackoutColor, followerCount);
                        continue;
                    }


                    Rect vpDst = TileViewport01_Centered(i, followerCount);
                    int dx = Mathf.RoundToInt(vpDst.x * _atlas.width);
                    int dy = Mathf.RoundToInt(vpDst.y * _atlas.height);
                    int dw = Mathf.RoundToInt(vpDst.width * _atlas.width);
                    int dh = Mathf.RoundToInt(vpDst.height * _atlas.height);

                    bool copied;
                    try
                    {
                        Graphics.CopyTexture(masterAtlas, 0, 0, srcRect.x, srcRect.y, srcRect.width, srcRect.height,
                            _atlas, 0, 0, dx, dy);
                        copied = true;
                    }
                    catch
                    {
                        copied = false;
                    }

                    if (!copied)
                    {
                        var prev = RenderTexture.active;
                        RenderTexture.active = _atlas;
                        GL.Viewport(new Rect(dx, dy, dw, dh));
                        if (blitMaterial)
                        {
                            if (blitMaterial.HasProperty(MainTexSt))
                            {
                                Vector2 scale = new Vector2((float)srcRect.width / masterAtlas.width,
                                    (float)srcRect.height / masterAtlas.height);
                                Vector2 offset = new Vector2((float)srcRect.x / masterAtlas.width,
                                    (float)srcRect.y / masterAtlas.height);
                                blitMaterial.SetVector(MainTexSt, new Vector4(scale.x, scale.y, offset.x, offset.y));
                            }

                            Graphics.Blit(masterAtlas, _atlas, blitMaterial);
                            if (blitMaterial.HasProperty(MainTexSt))
                                blitMaterial.SetVector(MainTexSt, new Vector4(1, 1, 0, 0));
                        }
                        else
                        {
                            Graphics.Blit(masterAtlas, (RenderTexture)null);
                        }

                        RenderTexture.active = prev;
                    }

                    copiesLeft--;
                }
            }

            BindAtlasToPlane(_atlas);
            ApplyPlaneStyling();
        }

        // ===== Init por-c�mara =====
        void InitPerCamState_Master()
        {
            _rt.Clear();
            _activeMasterCams.Clear();

            int n = cameras?.Length ?? 0;
            if (n <= 0) return;

            int pooled = Mathf.Max(1, Mathf.FloorToInt(recordMaxSeconds * Mathf.Max(1, recordFPS)));

            for (int i = 0; i < n; i++)
            {
                if (cameras != null)
                {
                    var v = cameras[i];
                    if (!v) continue;

                    var cr = new CamRuntime
                    {
                        tileRT = CreateRT(tileWidth, tileHeight, 16, recordFormat),
                        frameInterval = 1f / Mathf.Max(1, recordFPS)
                    };
                    cr.playFrameInterval = cr.frameInterval;
                    cr.maxFrames = pooled;

                    cr.frames = new RenderTexture[pooled];
                    for (int k = 0; k < pooled; k++) cr.frames[k] = CreateRT(tileWidth, tileHeight, 0, recordFormat);

                    cr.frameWrite = 0;
                    cr.frameCount = 0;

                    cr.state = CamRuntime.State.Idle;
                    cr.armed = true;

                    cr.playing = false;
                    cr.inBlackout = false;
                    cr.playIndex = 0;
                    cr.playShownCount = 0;
                    cr.lastShownIndex = -1;
                    cr.tileDirty = true;

                    cr.clipStartIndex = 0;
                    cr.clipFrameCount = 0;

                    _rt[v] = cr;
                    _activeMasterCams.Add(v);
                }
            }
        }

        RenderTexture CreateRT(int w, int h, int depth, RenderTextureFormat fmt)
        {
            var rt = new RenderTexture(w, h, depth, fmt, RenderTextureReadWrite.Default)
            {
                name = "CCTV_RT_" + w + "x" + h + "_" + fmt,
                filterMode = atlasFilterMode,
                anisoLevel = atlasAniso,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            rt.Create();
            return rt;
        }

        static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }
    }
}