using System;
using System.Diagnostics;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace BeatsaberVulkan;

internal class PerformanceOverlayController : IInitializable, ITickable, IDisposable
{
    private const float OverlayDistance = 1.35f;
    private const float OverlayLeftOffset = -0.55f;
    private const float OverlayUpOffset = 0.31f;
    private const int UnlimitedTargetFrameRate = -1;

    private readonly PluginConfig pluginConfig;
    private readonly VulkanApiManager vulkanApiManager;
    private readonly Process process = Process.GetCurrentProcess();
    private readonly StringBuilder textBuilder = new(384);

    private GameObject? overlayRoot;
    private TextMeshProUGUI? statsText;
    private Camera? activeCamera;
    private float accumulatedFrameTime;
    private float worstFrameTime;
    private float bestFrameTime = float.MaxValue;
    private float nextTextUpdateTime;
    private int frameCount;
    private double previousCpuSeconds;
    private float previousCpuSampleTime;
    private float cpuPercent;

    public PerformanceOverlayController(PluginConfig pluginConfig, VulkanApiManager vulkanApiManager)
    {
        this.pluginConfig = pluginConfig;
        this.vulkanApiManager = vulkanApiManager;
    }

    public bool IsOverlayEnabled
    {
        get => pluginConfig.ShowPerformanceOverlay;
        set
        {
            pluginConfig.ShowPerformanceOverlay = value;
            pluginConfig.Changed();
            SetOverlayVisible(value);
        }
    }

    public bool IsFpsUnlockerEnabled
    {
        get => pluginConfig.EnableFpsUnlocker;
        set
        {
            pluginConfig.EnableFpsUnlocker = value;
            pluginConfig.Changed();
            ApplyFpsSettings();
        }
    }

    public string OverlayStatusText => IsOverlayEnabled
        ? "Performance overlay: On"
        : "Performance overlay: Off";

    public string FpsUnlockerStatusText => IsFpsUnlockerEnabled
        ? "FPS unlocker: Unlimited"
        : "FPS unlocker: Off";

    public void Initialize()
    {
        ApplyFpsSettings();
        CreateOverlay();
        previousCpuSeconds = process.TotalProcessorTime.TotalSeconds;
        previousCpuSampleTime = Time.realtimeSinceStartup;

        Plugin.Log.Info(IsFpsUnlockerEnabled
            ? "FPS unlocker enabled: v-sync disabled and target frame rate set to unlimited."
            : "FPS unlocker disabled.");
    }

    public void Tick()
    {
        if (IsFpsUnlockerEnabled)
        {
            ApplyFpsSettings();
        }

        if (!IsOverlayEnabled)
        {
            return;
        }

        EnsureOverlay();
        UpdateOverlayTransform();
        CaptureFrameStats();

        if (Time.unscaledTime >= nextTextUpdateTime)
        {
            UpdateText();
            ResetFrameWindow();
            nextTextUpdateTime = Time.unscaledTime + Mathf.Max(0.1f, pluginConfig.OverlayUpdateInterval);
        }
    }

    public void Dispose()
    {
        if (overlayRoot is not null)
        {
            UnityEngine.Object.Destroy(overlayRoot);
        }
    }

    public void ToggleOverlay() => IsOverlayEnabled = !IsOverlayEnabled;

    public void ToggleFpsUnlocker() => IsFpsUnlockerEnabled = !IsFpsUnlockerEnabled;

    private void CreateOverlay()
    {
        if (overlayRoot is not null)
        {
            return;
        }

        overlayRoot = new GameObject("BeatsaberVulkan Performance Overlay");
        UnityEngine.Object.DontDestroyOnLoad(overlayRoot);

        var canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = short.MaxValue;

        var scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;
        scaler.referencePixelsPerUnit = 100f;

        var rootTransform = overlayRoot.GetComponent<RectTransform>();
        rootTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 520f);
        rootTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 220f);
        rootTransform.localScale = Vector3.one * Mathf.Max(0.0006f, pluginConfig.OverlayScale);

        var panelObject = new GameObject("Stats Panel");
        panelObject.transform.SetParent(overlayRoot.transform, false);

        var panelTransform = panelObject.AddComponent<RectTransform>();
        panelTransform.anchorMin = Vector2.zero;
        panelTransform.anchorMax = Vector2.one;
        panelTransform.offsetMin = Vector2.zero;
        panelTransform.offsetMax = Vector2.zero;

        var panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.02f, 0.025f, 0.03f, 0.72f);
        panel.raycastTarget = false;

        var textObject = new GameObject("Stats Text");
        textObject.transform.SetParent(panelObject.transform, false);

        var textTransform = textObject.AddComponent<RectTransform>();
        textTransform.anchorMin = new Vector2(0f, 0f);
        textTransform.anchorMax = new Vector2(1f, 1f);
        textTransform.offsetMin = new Vector2(18f, 14f);
        textTransform.offsetMax = new Vector2(-18f, -14f);

        statsText = textObject.AddComponent<TextMeshProUGUI>();
        statsText.alignment = TextAlignmentOptions.TopLeft;
        statsText.color = new Color(0.92f, 0.98f, 1f, 1f);
        statsText.fontSize = 24f;
        statsText.enableWordWrapping = false;
        statsText.raycastTarget = false;
        statsText.text = "BSVulkan stats loading...";

        SetOverlayVisible(IsOverlayEnabled);
        UpdateOverlayTransform();
        UpdateText();
    }

    private void EnsureOverlay()
    {
        if (overlayRoot is null || statsText is null)
        {
            CreateOverlay();
        }
    }

    private void SetOverlayVisible(bool isVisible)
    {
        if (overlayRoot is not null)
        {
            overlayRoot.SetActive(isVisible);
        }
    }

    private void UpdateOverlayTransform()
    {
        if (overlayRoot is null)
        {
            return;
        }

        var camera = GetActiveCamera();
        if (camera is null)
        {
            return;
        }

        var cameraTransform = camera.transform;
        overlayRoot.transform.SetPositionAndRotation(
            cameraTransform.position +
            cameraTransform.forward * OverlayDistance +
            cameraTransform.right * OverlayLeftOffset +
            cameraTransform.up * OverlayUpOffset,
            cameraTransform.rotation);
    }

    private Camera? GetActiveCamera()
    {
        if (activeCamera is not null && activeCamera.isActiveAndEnabled)
        {
            return activeCamera;
        }

        activeCamera = Camera.main;
        if (activeCamera is not null)
        {
            return activeCamera;
        }

        var cameras = Camera.allCameras;
        activeCamera = cameras.Length > 0 ? cameras[0] : null;
        return activeCamera;
    }

    private void CaptureFrameStats()
    {
        var frameTime = Mathf.Max(Time.unscaledDeltaTime, 0.000001f);
        accumulatedFrameTime += frameTime;
        worstFrameTime = Mathf.Max(worstFrameTime, frameTime);
        bestFrameTime = Mathf.Min(bestFrameTime, frameTime);
        frameCount++;
    }

    private void UpdateText()
    {
        if (statsText is null)
        {
            return;
        }

        UpdateCpuPercent();

        var averageFrameTime = frameCount > 0 ? accumulatedFrameTime / frameCount : Time.unscaledDeltaTime;
        var averageFps = averageFrameTime > 0f ? 1f / averageFrameTime : 0f;
        var lowFps = worstFrameTime > 0f ? 1f / worstFrameTime : 0f;
        var highFps = bestFrameTime < float.MaxValue && bestFrameTime > 0f ? 1f / bestFrameTime : 0f;
        var monoMemoryMb = GC.GetTotalMemory(false) / 1048576f;
        var workingSetMb = process.WorkingSet64 / 1048576f;

        textBuilder.Clear();
        textBuilder.AppendLine("BSVulkan Performance");
        textBuilder.Append("FPS ").Append(averageFps.ToString("F0"))
            .Append("  Low ").Append(lowFps.ToString("F0"))
            .Append("  High ").Append(highFps.ToString("F0")).AppendLine();
        textBuilder.Append("Frame ").Append((averageFrameTime * 1000f).ToString("F2"))
            .Append(" ms  Worst ").Append((worstFrameTime * 1000f).ToString("F2")).Append(" ms").AppendLine();
        textBuilder.Append("CPU ").Append(cpuPercent.ToString("F0")).Append("%  RAM ")
            .Append(workingSetMb.ToString("F0")).Append(" MB  GC ")
            .Append(monoMemoryMb.ToString("F0")).Append(" MB").AppendLine();
        textBuilder.Append("API ").Append(vulkanApiManager.CurrentGraphicsApi);

        if (vulkanApiManager.IsDxvkInstalled)
        {
            textBuilder.Append(" + DXVK");
        }

        textBuilder.Append("  VSync ").Append(QualitySettings.vSyncCount)
            .Append("  Cap ").Append(Application.targetFrameRate < 0 ? "Unlimited" : Application.targetFrameRate.ToString());

        statsText.text = textBuilder.ToString();
    }

    private void UpdateCpuPercent()
    {
        var now = Time.realtimeSinceStartup;
        var elapsed = now - previousCpuSampleTime;
        if (elapsed <= 0f)
        {
            return;
        }

        var cpuSeconds = process.TotalProcessorTime.TotalSeconds;
        var cpuDelta = cpuSeconds - previousCpuSeconds;
        cpuPercent = Mathf.Clamp((float)(cpuDelta / elapsed / Environment.ProcessorCount * 100.0), 0f, 100f);
        previousCpuSeconds = cpuSeconds;
        previousCpuSampleTime = now;
    }

    private void ResetFrameWindow()
    {
        accumulatedFrameTime = 0f;
        worstFrameTime = 0f;
        bestFrameTime = float.MaxValue;
        frameCount = 0;
    }

    private void ApplyFpsSettings()
    {
        if (!pluginConfig.EnableFpsUnlocker)
        {
            return;
        }

        if (QualitySettings.vSyncCount != 0)
        {
            QualitySettings.vSyncCount = 0;
        }

        if (Application.targetFrameRate != UnlimitedTargetFrameRate)
        {
            Application.targetFrameRate = UnlimitedTargetFrameRate;
        }
    }
}
