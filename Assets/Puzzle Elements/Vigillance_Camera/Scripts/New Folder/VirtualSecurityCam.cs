using Managers.Game;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// GraphicsFormat, FormatUsage

namespace Puzzle_Elements.Vigillance_Camera.Scripts.New_Folder
{
    [DisallowMultipleComponent]
    public class VirtualSecurityCam : MonoBehaviour
    {
        // === Vista / Frustum ===
        [Header("Vista")]
        public Transform viewPivot;
        [Range(10f, 120f)] public float fieldOfView = 60f;
        public LayerMask cullingMask = ~0;
        public float nearClip = 0.03f;
        public float farClip = 500f;

        // === Grabaci�n (arranque) ===
        [Header("Grabaci�n")]
        public bool startRecordingOnAwake = true;

        // === Salida (no se usa para atlas, pero s� para otros usos) ===
        [Header("Salida")]
        public int outputWidth = 512;
        public int outputHeight = 288;
        public bool sRGB = true;

        // === Orientaci�n / �adelante� ===
        public enum ForwardAxis { ZPlus, ZMinus, XPlus, XMinus, YPlus, YMinus }

        [Header("Orientaci�n")]
        [Tooltip("Nivelar horizonte (UP = Vector3.up). Elimina el roll.")]
        public bool lockHorizon = true;

        [Tooltip("Qu� eje de TU malla consider�s como 'adelante'.")]
        public ForwardAxis forwardAxis = ForwardAxis.ZPlus;

        [Tooltip("Offset adicional de rotaci�n (grados) aplicado a tu malla.")]
        public Vector3 rotationOffsetEuler = Vector3.zero;

        // === Detecci�n (para iniciar/parar grabaci�n) ===
        [Header("Detecci�n (player visible / movimiento)")]
        [Tooltip("Asign� aqu� el Transform del Player (sin Find).")]
        public Transform detectionTarget;

        [Tooltip("Capas que bloquean la visi�n (paredes/piso/props).")]
        public LayerMask visibilityBlockers = ~0;

        [Tooltip("Grados/seg necesarios para considerar que la c�mara 'se movi�'.")]
        public float startOnAngularSpeedDegPerSec = 8f;

        [Tooltip("�ngulo m�ximo (�) respecto al forward para considerar visible al target.")]
        public float maxViewAngle = 60f;

        [Tooltip("Segundos sin ver al target para detener la grabaci�n.")]
        public float stopAfterNoTargetSeconds = 1.0f;

        // === Gizmo del frustum (Scene View) ===
        [Header("Gizmo de visi�n (Scene View)")]
        public bool gizmoEnabled = true;
        public Color gizmoColor = new Color(0f, 0.8f, 1f, 0.65f);
        public bool gizmoDrawNearRect = true;
        public bool gizmoDrawFarRect = true;
        public bool gizmoDrawRaysToNear = true;
        public bool gizmoDrawLinksToFar = true;
        public bool gizmoDrawAxes = true;

        // === Internos (RT opcional, no requerido por atlas) ===
        RenderTexture _rt;
        bool _recording;

        public bool IsRecording { get { return _recording; } }
        public RenderTexture Output { get { return _rt; } }
        public Transform Pivot { get { return viewPivot ? viewPivot : transform; } }

        void Awake()
        {
            // RT opcional por compatibilidad con otros flows; el atlas no lo usa.
            _rt = SafeCreateRT(outputWidth, outputHeight, sRGB);
            if (_rt == null)
                Debug.LogError("[VirtualSecurityCam:" + name + "] No se pudo crear RenderTexture compatible.");

            _recording = startRecordingOnAwake;
            CctvPlaneAtlas.RegisterSource(this); // <-- ahora el atlas maneja el registro
        }
        private void Start()
        {
            detectionTarget = GameManager.Instance.player;
        }
        void OnDestroy()
        {
            CctvPlaneAtlas.UnregisterSource(this);
            SafeDestroyRT(ref _rt);
        }

        public void SetRecording(bool value) { _recording = value; }
        public void StartRecording() { _recording = true; }
        public void StopRecording() { _recording = false; }

        public Vector3 GetDesiredForward()
        {
            Transform t = Pivot;
            Vector3 baseFwd;

            switch (forwardAxis)
            {
                case ForwardAxis.ZPlus: baseFwd = t.forward; break;
                case ForwardAxis.ZMinus: baseFwd = -t.forward; break;
                case ForwardAxis.XPlus: baseFwd = t.right; break;
                case ForwardAxis.XMinus: baseFwd = -t.right; break;
                case ForwardAxis.YPlus: baseFwd = t.up; break;
                case ForwardAxis.YMinus: baseFwd = -t.up; break;
                default: baseFwd = t.forward; break;
            }

            Quaternion rotOff = Quaternion.Euler(rotationOffsetEuler);
            baseFwd = rotOff * baseFwd;
            return baseFwd;
        }

        // ===== Helpers RT =====
        static void SafeDestroyRT(ref RenderTexture rt)
        {
            if (rt == null) return;
            try { if (rt.IsCreated()) rt.Release(); } catch { }
            Destroy(rt);
            rt = null;
        }

        static RenderTexture SafeCreateRT(int w, int h, bool wantSRGB)
        {
            GraphicsFormat[] candidates = {
                wantSRGB ? GraphicsFormat.R8G8B8A8_SRGB  : GraphicsFormat.R8G8B8A8_UNorm,
                wantSRGB ? GraphicsFormat.B8G8R8A8_SRGB  : GraphicsFormat.B8G8R8A8_UNorm,
                GraphicsFormat.R16G16B16A16_SFloat
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                var fmt = candidates[i];
                if (fmt != GraphicsFormat.None && SystemInfo.IsFormatSupported(fmt, FormatUsage.Render))
                {
                    var desc = new RenderTextureDescriptor(w, h);
                    desc.graphicsFormat = fmt;
                    desc.depthBufferBits = 0;
                    desc.msaaSamples = 1;
                    desc.mipCount = 1;
                    desc.volumeDepth = 1;
                    desc.sRGB = wantSRGB;
                    desc.useMipMap = false;
                    desc.autoGenerateMips = false;

                    var rt = new RenderTexture(desc);
                    rt.name = "VRT_" + w + "x" + h + "_" + fmt;
                    rt.filterMode = FilterMode.Bilinear;
                    rt.wrapMode = TextureWrapMode.Clamp;

                    if (rt.Create()) return rt;
                    Destroy(rt);
                }
            }

            var rtLegacy = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            rtLegacy.name = "VRT_LEGACY_" + w + "x" + h + "_ARGB32";
            rtLegacy.filterMode = FilterMode.Bilinear;
            rtLegacy.wrapMode = TextureWrapMode.Clamp;
            rtLegacy.useMipMap = false;
            rtLegacy.autoGenerateMips = false;
            rtLegacy.antiAliasing = 1;

            if (rtLegacy.Create()) return rtLegacy;
            Destroy(rtLegacy);
            return null;
        }

        // ===== GIZMO (Scene View) =====
        void OnDrawGizmos()
        {
            if (!gizmoEnabled) return;

            float fov = Mathf.Max(1f, fieldOfView);
            float near = Mathf.Max(0.001f, nearClip);
            float far = Mathf.Max(near + 0.01f, farClip);
            float aspect = outputHeight > 0 ? (outputWidth / (float)outputHeight) : (16f / 9f);

            Transform p = Pivot;
            Vector3 fwd = GetDesiredForward();
            if (fwd.sqrMagnitude < 1e-6f) fwd = p.forward;
            Quaternion rot = lockHorizon ? Quaternion.LookRotation(fwd, Vector3.up)
                : (p.rotation * Quaternion.Euler(rotationOffsetEuler));
            Vector3 pos = p.position;

            float halfFovRad = fov * 0.5f * Mathf.Deg2Rad;
            float nearH = Mathf.Tan(halfFovRad) * near;
            float nearW = nearH * aspect;
            float farH = Mathf.Tan(halfFovRad) * far;
            float farW = farH * aspect;

            Vector3 z = rot * Vector3.forward;
            Vector3 x = rot * Vector3.right;
            Vector3 y = rot * Vector3.up;

            Vector3 nc = pos + z * near;
            Vector3 fc = pos + z * far;

            Vector3 ntl = nc + y * nearH - x * nearW;
            Vector3 ntr = nc + y * nearH + x * nearW;
            Vector3 nbl = nc - y * nearH - x * nearW;
            Vector3 nbr = nc - y * nearH + x * nearW;

            Vector3 ftl = fc + y * farH - x * farW;
            Vector3 ftr = fc + y * farH + x * farW;
            Vector3 fbl = fc - y * farH - x * farW;
            Vector3 fbr = fc - y * farH + x * farW;

            Gizmos.color = gizmoColor;

            if (gizmoDrawRaysToNear)
            { Gizmos.DrawLine(pos, ntl); Gizmos.DrawLine(pos, ntr); Gizmos.DrawLine(pos, nbl); Gizmos.DrawLine(pos, nbr); }

            if (gizmoDrawNearRect)
            { Gizmos.DrawLine(ntl, ntr); Gizmos.DrawLine(ntr, nbr); Gizmos.DrawLine(nbr, nbl); Gizmos.DrawLine(nbl, ntl); }

            if (gizmoDrawFarRect)
            { Gizmos.DrawLine(ftl, ftr); Gizmos.DrawLine(ftr, fbr); Gizmos.DrawLine(fbr, fbl); Gizmos.DrawLine(fbl, ftl); }

            if (gizmoDrawLinksToFar)
            { Gizmos.DrawLine(ntl, ftl); Gizmos.DrawLine(ntr, ftr); Gizmos.DrawLine(nbl, fbl); Gizmos.DrawLine(nbr, fbr); }

            if (gizmoDrawAxes)
            {
                Gizmos.color = new Color(1, 0, 0, 0.9f); Gizmos.DrawLine(pos, pos + (rot * Vector3.right) * near * 0.6f);
                Gizmos.color = new Color(0, 1, 0, 0.9f); Gizmos.DrawLine(pos, pos + (rot * Vector3.up) * near * 0.6f);
                Gizmos.color = new Color(0, 0, 1, 0.9f); Gizmos.DrawLine(pos, pos + (rot * Vector3.forward) * near * 1.0f);
            }
        }
    }
}
