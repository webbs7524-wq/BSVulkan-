using BeatSaberMarkupLanguage.Attributes;
using TMPro;

namespace BeatsaberVulkan.Menu;

internal class VulkanSettingsMenu
{
    private readonly VulkanApiManager vulkanApiManager;
    private readonly PerformanceOverlayController performanceOverlayController;

    [UIComponent("status-text")] private readonly TextMeshProUGUI statusText = null!;
    [UIComponent("dxvk-status-text")] private readonly TextMeshProUGUI dxvkStatusText = null!;
    [UIComponent("overlay-status-text")] private readonly TextMeshProUGUI overlayStatusText = null!;
    [UIComponent("fps-unlocker-status-text")] private readonly TextMeshProUGUI fpsUnlockerStatusText = null!;

    public VulkanSettingsMenu(
        VulkanApiManager vulkanApiManager,
        PerformanceOverlayController performanceOverlayController)
    {
        this.vulkanApiManager = vulkanApiManager;
        this.performanceOverlayController = performanceOverlayController;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        RefreshText();
    }

    [UIAction("toggle-overlay")]
    private void ToggleOverlay()
    {
        performanceOverlayController.ToggleOverlay();
        RefreshText();
    }

    [UIAction("toggle-fps-unlocker")]
    private void ToggleFpsUnlocker()
    {
        performanceOverlayController.ToggleFpsUnlocker();
        RefreshText();
    }

    private void RefreshText()
    {
        statusText.text = vulkanApiManager.StatusText;
        dxvkStatusText.text = vulkanApiManager.DxvkStatusText;
        overlayStatusText.text = performanceOverlayController.OverlayStatusText;
        fpsUnlockerStatusText.text = performanceOverlayController.FpsUnlockerStatusText;
    }
}
