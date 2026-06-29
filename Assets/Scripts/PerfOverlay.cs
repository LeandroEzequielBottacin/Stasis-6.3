using System;
using System.Diagnostics;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_2020_2_OR_NEWER
using Unity.Profiling;
#endif

[DefaultExecutionOrder(20000)]
public class PerfOverlay : MonoBehaviour
{
    [Header("UI")]
    public KeyCode toggleKey = KeyCode.F1;
    public bool startVisible = true;
    public bool dontDestroyOnLoad = true;
    [Range(8, 24)] public int fontSize = 14;
    public Vector2 panelSize = new Vector2(340, 104);
    public Vector2 margin = new Vector2(10, 10);
    public Color bg = new Color(0, 0, 0, 0.55f);
    public Color fg = Color.white;

    [Header("Refresco")]
    public float refreshRate = 0.5f;

    // UI
    Canvas _canvas;
    RectTransform _panel;
    TextMeshProUGUI _tmp;

    // Texto
    readonly StringBuilder _sb = new StringBuilder(256);
    float _nextUpdate;
    int _frames;
    float _fpsAccum;

    // CPU proceso (%)
    Process _proc;
    TimeSpan _lastCpuTotal;
    double _lastWall;
    float _cpuUsagePercent;

#if UNITY_2020_2_OR_NEWER
    ProfilerRecorder _cpuFrame; // "CPU Total Frame Time"
    ProfilerRecorder _gpuFrame; // "GPU Frame Time"
#endif

    void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        BuildUI();

        _proc = Process.GetCurrentProcess();
        _lastCpuTotal = _proc.TotalProcessorTime;
        _lastWall = Time.realtimeSinceStartupAsDouble;

#if UNITY_2020_2_OR_NEWER
        TryStartRecorder(r: ref _cpuFrame, "CPU Total Frame Time", 64);
        TryStartRecorder(r: ref _gpuFrame, "GPU Frame Time", 64);
#endif
        SetVisible(startVisible);
    }

    void OnDestroy()
    {
#if UNITY_2020_2_OR_NEWER
        if (_cpuFrame.Valid) _cpuFrame.Dispose();
        if (_gpuFrame.Valid) _gpuFrame.Dispose();
#endif
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) SetVisible(!_canvas.enabled);

        // FPS acumulado en la ventana
        _fpsAccum += 1f / Mathf.Max(1e-6f, Time.unscaledDeltaTime);
        _frames++;

        // CPU % proceso (cada ~250 ms)
        /*
        var now = Time.realtimeSinceStartupAsDouble;
        var dt = now - _lastWall;
        if (dt >= 0.25)
        {
            _proc.Refresh();
            var cur = _proc.TotalProcessorTime;
            var cpuDelta = (cur - _lastCpuTotal).TotalSeconds;
            var logical = Math.Max(1, Environment.ProcessorCount);
            _cpuUsagePercent = Mathf.Clamp((float)(cpuDelta / (dt * logical) * 100.0), 0f, 100f);
            _lastCpuTotal = cur;
            _lastWall = now;
        }*/

        if (Time.unscaledTime >= _nextUpdate)
        {
            var avgFps = _fpsAccum / Mathf.Max(1, _frames);
            var frameMs = 1000.0f / Mathf.Max(1f, avgFps);

#if UNITY_2020_2_OR_NEWER
            var cpuMs = RecorderMs(_cpuFrame);
            var gpuMs = RecorderMs(_gpuFrame);
#endif
            _sb.Clear();
            _sb.AppendFormat("FPS: {0,5:0.0}    FT: {1,5:0.0} ms\n", avgFps, frameMs);
            _sb.AppendFormat("CPU: {0,5:0.0} ms   ({1,5:0.0}% proc)\n", cpuMs, _cpuUsagePercent);
            if (gpuMs <= 0)
                _sb.Append("GPU:  0.0 ms\n");
            else
                _sb.AppendFormat("GPU: {0,5:0.0} ms\n", gpuMs);

            _tmp.text = _sb.ToString();

            _fpsAccum = 0; _frames = 0;
            _nextUpdate = Time.unscaledTime + refreshRate;
        }
    }

    void BuildUI()
    {
        // Canvas overlay
        var goCanvas = new GameObject("PerfOverlay_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        goCanvas.layer = gameObject.layer;
        _canvas = goCanvas.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = short.MaxValue;
        var scaler = goCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        if (dontDestroyOnLoad) DontDestroyOnLoad(goCanvas);

        // Panel anclado arriba a la derecha
        var goPanel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        goPanel.transform.SetParent(goCanvas.transform, false);
        _panel = goPanel.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(1, 1);
        _panel.anchorMax = new Vector2(1, 1);
        _panel.pivot = new Vector2(1, 1);
        _panel.sizeDelta = panelSize;
        _panel.anchoredPosition = new Vector2(-margin.x, -margin.y);
        var img = goPanel.GetComponent<Image>();
        img.color = bg;

        // Texto centrado
        var goText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        goText.transform.SetParent(goPanel.transform, false);
        var rt = goText.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8, 8); rt.offsetMax = new Vector2(-8, -8);
        _tmp = goText.GetComponent<TextMeshProUGUI>();
        _tmp.fontSize = fontSize;
        _tmp.color = fg;
        _tmp.enableWordWrapping = false;
        _tmp.richText = false;
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.text = "PerfOverlay…";
    }

    void SetVisible(bool v) { if (_canvas) _canvas.enabled = v; }

#if UNITY_2020_2_OR_NEWER
    static void TryStartRecorder(ref ProfilerRecorder r, string statName, int capacity)
    {
        r = ProfilerRecorder.StartNew(ProfilerCategory.Render, statName, capacity);
        if (!r.Valid) r = ProfilerRecorder.StartNew(ProfilerCategory.Internal, statName, capacity);
        if (!r.Valid)
            r = new ProfilerRecorder(statName, capacity,
                ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
    }

    static double RecorderMs(ProfilerRecorder r)
    {
        if (!r.Valid || r.Count == 0) return 0;
        long raw = r.LastValue;
        try
        {
            if (r.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds) return raw * 1e-6; // ns → ms
        }
        catch
        {
            // ignored
        }

        return raw; // algunos contadores ya vienen en ms
    }
#endif
}