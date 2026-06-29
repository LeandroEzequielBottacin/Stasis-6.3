using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// URP

namespace Puzzle_Elements.Vigillance_Camera.Scripts.New_Folder
{
    [DisallowMultipleComponent]
    public class CCTVMultiplexer : MonoBehaviour
    {
        // === UI ===
        [Header("UI (asignar si ya existen)")]
        public Canvas useThisCanvas;
        public RectTransform useThisGridParent;
        public bool createEventSystem = true;

        [Header("Layout")]
        public int columns = 3;
        public Vector2 tileSize = new Vector2(320, 180);
        public Vector2 tileSpacing = new Vector2(8, 8);
        public Vector2 padding = new Vector2(12, 12);
        public bool dimWhenIdle = true;

        // === Render ===
        [Header("Render")]
        public Color clearColor = Color.black;
        public bool allowHDR;
        public bool allowMSAA;
        public bool previewEvenIfIdle;
        public bool debugPaintPattern;

        [Header("URP")]
        [Tooltip("�ndice del Renderer limpio (sin features) en el URP Asset")]
        public int urpRendererIndex = -1;

        // === OVERLAY LAYER (se dibuja encima) ===
        [Header("Overlay (Top Layer)")]
        [Tooltip("Cualquier objeto en esta Layer se dibuja por ENCIMA de todo lo dem�s.")]
        public LayerMask overlayTopLayer = 0; // eleg� la layer a superponer

        [Tooltip("Limpiar solo el DEPTH antes de dibujar la capa superior (recomendado).")]
        public bool overlayClearDepth = true;

        // === Internos ===
        static CCTVMultiplexer _instance;
        static readonly List<VirtualSecurityCam> _cams = new List<VirtualSecurityCam>();

        Camera _renderCam;
        GridLayoutGroup _grid;
        RectTransform _gridRt;
        Canvas _canvas;

        class Tile
        {
            public RawImage img;
            public TextMeshProUGUI recText;
            public Outline outline; // borde sutil
        }

        readonly Dictionary<VirtualSecurityCam, Tile> _tiles = new Dictionary<VirtualSecurityCam, Tile>();

        public bool OverlayVisible
        {
            get { return _canvas && _canvas.enabled; }
            set { if (_canvas) _canvas.enabled = value; }
        }
        public Canvas OverlayCanvas { get { return _canvas; } }

        public static IReadOnlyList<VirtualSecurityCam> Cams { get { return _cams; } }
        public static event Action CamsChanged;

        void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            SetupRenderCamera();
            SetupCanvasAndGrid();
            RebuildTiles();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void SetupRenderCamera()
        {
            var go = new GameObject("CCTV_RenderCam", typeof(Camera));
            go.transform.SetParent(transform, false);
            _renderCam = go.GetComponent<Camera>();
            _renderCam.enabled = false; // manual Render()
            _renderCam.clearFlags = CameraClearFlags.SolidColor;
            _renderCam.backgroundColor = clearColor;
            _renderCam.allowHDR = allowHDR;
            _renderCam.allowMSAA = allowMSAA;
            _renderCam.stereoTargetEye = StereoTargetEyeMask.None;
            _renderCam.depth = -100;

            var urpData = go.GetComponent<UniversalAdditionalCameraData>();
            if (!urpData) urpData = go.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderType = CameraRenderType.Base;
            if (urpRendererIndex >= 0) urpData.SetRenderer(urpRendererIndex);
            urpData.antialiasing = AntialiasingMode.None;
            urpData.renderPostProcessing = false;
            urpData.requiresColorTexture = false;
            urpData.requiresDepthTexture = false;
            urpData.stopNaN = false;
            urpData.dithering = false;
        }

        void SetupCanvasAndGrid()
        {
            _canvas = useThisCanvas;
            if (!_canvas)
            {
                var cgo = new GameObject("CCTV_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                cgo.transform.SetParent(transform, false);
                _canvas = cgo.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = cgo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 1f;

                if (createEventSystem)
                {
                    var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                    es.transform.SetParent(transform, false);
                }
            }

            if (useThisGridParent)
            {
                _gridRt = useThisGridParent;
                _grid = _gridRt.GetComponent<GridLayoutGroup>();
                if (_grid == null) _grid = _gridRt.gameObject.AddComponent<GridLayoutGroup>();
            }
            else
            {
                var gridGo = new GameObject("CCTV_Grid", typeof(RectTransform), typeof(GridLayoutGroup));
                gridGo.transform.SetParent(_canvas.transform, false);
                _gridRt = gridGo.GetComponent<RectTransform>();
                _grid = gridGo.GetComponent<GridLayoutGroup>();
            }

            _grid.cellSize = tileSize;
            _grid.spacing = tileSpacing;
            _grid.padding = new RectOffset((int)padding.x, (int)padding.x, (int)padding.y, (int)padding.y);
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = Mathf.Max(1, columns);

            _gridRt.anchorMin = Vector2.zero;
            _gridRt.anchorMax = Vector2.one;
            _gridRt.sizeDelta = Vector2.zero;
            _gridRt.anchoredPosition = Vector2.zero;
        }

        Tile CreateTile(VirtualSecurityCam v)
        {
            var tileGo = new GameObject("Tile_" + v.name, typeof(RectTransform), typeof(RawImage), typeof(Outline));
            tileGo.transform.SetParent(_gridRt, false);

            var img = tileGo.GetComponent<RawImage>();
            img.texture = v.Output;
            img.raycastTarget = false;

            var outline = tileGo.GetComponent<Outline>();
            outline.effectDistance = new Vector2(2f, -2f);
            outline.effectColor = new Color(1f, 1f, 1f, 0.2f);

            var recGo = new GameObject("REC", typeof(RectTransform), typeof(TextMeshProUGUI));
            recGo.transform.SetParent(tileGo.transform, false);
            var recRt = recGo.GetComponent<RectTransform>();
            recRt.anchorMin = new Vector2(0, 1);
            recRt.anchorMax = new Vector2(0, 1);
            recRt.pivot = new Vector2(0, 1);
            recRt.anchoredPosition = new Vector2(10, -10);
            recRt.sizeDelta = new Vector2(80, 26);

            var recText = recGo.GetComponent<TextMeshProUGUI>();
            recText.text = "REC";
            recText.alignment = TextAlignmentOptions.MidlineLeft;
            recText.fontSize = 18;

            Tile t = new Tile();
            t.img = img;
            t.recText = recText;
            t.outline = outline;

            UpdateTileAppearance(v, t);
            return t;
        }

        void UpdateTileAppearance(VirtualSecurityCam v, Tile t)
        {
            bool rtValid = (v.Output != null && v.Output.IsCreated());
            bool on = rtValid && (v.IsRecording || previewEvenIfIdle);

            t.img.color = on ? Color.white : (dimWhenIdle ? new Color(1f, 1f, 1f, 0.15f) : Color.white);
            if (t.recText)
                t.recText.color = v.IsRecording ? new Color(0.95f, 0.15f, 0.15f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
        }

        void RebuildTiles()
        {
            for (int i = _gridRt.childCount - 1; i >= 0; i--) Destroy(_gridRt.GetChild(i).gameObject);
            _tiles.Clear();
            for (int i = 0; i < _cams.Count; i++)
            {
                var v = _cams[i];
                _tiles[v] = CreateTile(v);
            }
        }

        void LateUpdate()
        {
            if (_renderCam == null) return;

            for (int i = 0; i < _cams.Count; i++)
            {
                var v = _cams[i];

                if (v.Output == null || !v.Output.IsCreated())
                {
                    Tile tile;
                    if (_tiles.TryGetValue(v, out tile))
                    {
                        tile.img.texture = null;
                        UpdateTileAppearance(v, tile);
                    }
                    continue;
                }

                // Si el RT se recre�, reasignar
                Tile t;
                if (_tiles.TryGetValue(v, out t) && t.img.texture != v.Output)
                    t.img.texture = v.Output;

                if (debugPaintPattern)
                {
                    var prev = RenderTexture.active;
                    RenderTexture.active = v.Output;
                    GL.Clear(true, true, new Color(1f, 0f, 1f, 1f));
                    RenderTexture.active = prev;
                }

                bool shouldRender = (v.IsRecording || previewEvenIfIdle);
                if (!shouldRender)
                {
                    Tile tile0;
                    if (_tiles.TryGetValue(v, out tile0)) UpdateTileAppearance(v, tile0);
                    continue;
                }

                // Pose/rotaci�n (horizonte nivelado si as� est� configurado)
                Transform p = v.Pivot;
                Vector3 desiredFwd = v.GetDesiredForward();
                if (desiredFwd.sqrMagnitude < 1e-6f) desiredFwd = p.forward;

                Quaternion rot = v.lockHorizon
                    ? Quaternion.LookRotation(desiredFwd, Vector3.up)
                    : (p.rotation * Quaternion.Euler(v.rotationOffsetEuler));

                _renderCam.transform.SetPositionAndRotation(p.position, rot);

                // Frustum & culling base
                _renderCam.fieldOfView = v.fieldOfView;

                // 1) PASADA BASE (todo menos la capa superior)
                int layerMaskBase = v.cullingMask.value;
                if (overlayTopLayer != 0)
                {
                    // quitar del base la overlayTopLayer para no dibujarla dos veces
                    layerMaskBase = layerMaskBase & ~overlayTopLayer.value;
                }
                if (layerMaskBase == 0) layerMaskBase = ~0; // fallback si qued� vac�o

                _renderCam.cullingMask = layerMaskBase;
                _renderCam.nearClipPlane = Mathf.Max(0.001f, v.nearClip);
                _renderCam.farClipPlane = v.farClip;

                var oldFlags = _renderCam.clearFlags;
                _renderCam.clearFlags = CameraClearFlags.SolidColor; // limpia color+depth en la primera pasada
                _renderCam.targetTexture = v.Output;
                _renderCam.Render();

                if (overlayTopLayer != 0)
                {
                    _renderCam.cullingMask = v.cullingMask.value & overlayTopLayer.value;
                    if (_renderCam.cullingMask != 0)
                    {
                        // LIMPIAR SOLO DEPTH para que nada la tape
                        _renderCam.clearFlags = overlayClearDepth ? CameraClearFlags.Depth : CameraClearFlags.Nothing;
                        _renderCam.targetTexture = v.Output;
                        _renderCam.Render();
                    }
                }

                // Restaurar
                _renderCam.clearFlags = oldFlags;
                _renderCam.targetTexture = null;

                Tile tile2;
                if (_tiles.TryGetValue(v, out tile2))
                    UpdateTileAppearance(v, tile2);
            }
        }

        // Registro est�tico
        public static void Register(VirtualSecurityCam v)
        {
            if (!_cams.Contains(v)) _cams.Add(v);
            if (_instance != null) _instance.RebuildTiles();
            if (CamsChanged != null) CamsChanged();
        }
        public static void Unregister(VirtualSecurityCam v)
        {
            if (_cams.Remove(v) && _instance != null) _instance.RebuildTiles();
            if (CamsChanged != null) CamsChanged();
        }
    }
}
