using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIScripts.UI_Scan
{
    [AddComponentMenu("Scan/Scanner Manager")]
    public class ScannerManager : MonoBehaviour
    {
        public static readonly HashSet<Scannable> Registry = new();
        public static void Register(Scannable s) { if (s != null) Registry.Add(s); }
        public static void Unregister(Scannable s) { if (s != null) Registry.Remove(s); }

        [Header("Input")]
        public KeyCode key = KeyCode.LeftControl;
        [Tooltip("Mostrar mientras se mantiene presionado (ON) o con pulso (OFF).")]
        public bool holdToShow = true;
        public float pulseDuration = 1.5f;

        [Header("Rango y Visión")]
        public Transform player;                // si se deja vacío, usa Camera.main.transform
        public Camera cam;                      // cámara usada para raycast y centro de mira
        public float maxDistance = 20f;
        [Tooltip("Radio en pantalla (0–1) donde se considera que apuntás al objeto.")]
        public float screenRadius = 0.15f;

        [Header("UI")]
        public Transform uiRoot;                // Canvas Overlay
        public ScanLabelUI labelPrefab;

        private bool _showing;
        private Coroutine _pulseCo;
        private Scannable _current;             // objeto actualmente resaltado

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (player == null && cam != null) player = cam.transform;
        }

        void Update()
        {
            bool down = Input.GetKeyDown(key);
            bool up = Input.GetKeyUp(key);
            bool held = Input.GetKey(key);

            if (holdToShow)
            {
                if (down) StartShow();
                if (!held && _showing) StopShow();
            }
            else
            {
                if (down)
                {
                    if (_showing) StopShow();
                    else StartPulse(pulseDuration);
                }
            }

            if (_showing)
            {
                UpdateTarget();
            }
        }

        private void StartShow()
        {
            _showing = true;
        }

        private void StartPulse(float dur)
        {
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            StartShow();
            _pulseCo = StartCoroutine(Co_Pulse(dur));
        }

        private IEnumerator Co_Pulse(float dur)
        {
            yield return new WaitForSecondsRealtime(dur);
            StopShow();
            _pulseCo = null;
        }

        private void StopShow()
        {
            _showing = false;
            HideCurrent();
        }

        // ------------------------------------------------------------
        // NUEVO: selecciona el objeto más relevante al centro
        // ------------------------------------------------------------
        private void UpdateTarget()
        {
            if (cam == null || Registry.Count == 0) return;

            Scannable best = null;
            float bestScore = float.MaxValue;

            Vector2 screenCenter = new(Screen.width * 0.5f, Screen.height * 0.5f);

            foreach (var s in Registry)
            {
                if (!s || s.data == null) continue;

                // Distancia en mundo
                float dist = Vector3.Distance(cam.transform.position, s.WorldPoint);
                float limit = s.data.maxShowDistance > 0 ? Mathf.Min(maxDistance, s.data.maxShowDistance) : maxDistance;
                if (dist > limit) continue;

                // Si el objeto está detrás de la cámara, descartamos
                var wp = cam.WorldToScreenPoint(s.WorldPoint);
                if (wp.z < 0f) continue;

                // 1) Chequeo “círculo central” (fallback rápido)
                float screenDistNorm = Vector2.Distance(screenCenter, new Vector2(wp.x, wp.y)) / (Screen.height * 0.5f);
                bool passesCircle = screenDistNorm <= screenRadius;

                // 2) Chequeo por AABB proyectado (más permisivo y cómodo)
                bool passesRect = false;
                float rectPenalty = 1f;
                if (TryGetScreenRect(cam, s, out var rect))
                {
                    var pad = s.screenPadding;
                    rect.xMin -= pad; rect.xMax += pad;
                    rect.yMin -= pad; rect.yMax += pad;

                    passesRect = rect.Contains(screenCenter);
                    rectPenalty = DistancePointToRect(screenCenter, rect); // px a borde (0 si adentro)
                }

                if (!passesCircle && !passesRect) continue;

                // Score: combinamos distancia, desviación del centro y penalización por rect
                float score = dist * 0.5f + screenDistNorm * 12f + rectPenalty * 0.02f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }

            if (best != _current)
            {
                HideCurrent();
                _current = best;
                if (_current != null) ShowLabel(_current);
            }
        }

        // ---------- Helpers ----------
        private static bool TryGetScreenRect(Camera cam, Scannable s, out Rect rect)
        {
            rect = new Rect();
            var rends = s.targetRenderers;
            if (rends == null || rends.Length == 0)
            {
                rends = s.GetComponentsInChildren<Renderer>();
                if (rends == null || rends.Length == 0) return false;
            }

            bool valid = false;
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);

            foreach (var r in rends)
            {
                if (!r || !r.enabled) continue;
                var b = r.bounds;
                // 8 esquinas del AABB
                for (int xi = 0; xi < 2; xi++)
                for (int yi = 0; yi < 2; yi++)
                for (int zi = 0; zi < 2; zi++)
                {
                    Vector3 p = new Vector3(
                        xi == 0 ? b.min.x : b.max.x,
                        yi == 0 ? b.min.y : b.max.y,
                        zi == 0 ? b.min.z : b.max.z
                    );
                    Vector3 sp = cam.WorldToScreenPoint(p);
                    if (sp.z <= 0f) continue; // detrás, ignoramos ese punto
                    valid = true;
                    min = Vector2.Min(min, new Vector2(sp.x, sp.y));
                    max = Vector2.Max(max, new Vector2(sp.x, sp.y));
                }
            }

            if (!valid) return false;
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return rect.width > 1f && rect.height > 1f;
        }

        private static float DistancePointToRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy); // 0 si está dentro
        }

        private void ShowLabel(Scannable s)
        {
            if (s == null) return;
            s.EnsureLabel(uiRoot, labelPrefab);
            s.Show();
        }

        private void HideCurrent()
        {
            if (_current != null)
            {
                _current.Hide();
                _current = null;
            }
        }
    }
}
