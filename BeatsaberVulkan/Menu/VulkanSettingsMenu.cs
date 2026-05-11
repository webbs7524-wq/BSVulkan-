using BeatSaberMarkupLanguage.Attributes;
using TMPro;

namespace BeatsaberVulkan.Menu;

internal class VulkanSettingsMenu
{
    private readonly VulkanApiManager vulkanApiManager;

    [UIComponent("status-text")] private readonly TextMeshProUGUI statusText = null!;
    [UIComponent("auto-relaunch-text")] private readonly TextMeshProUGUI autoRelaunchText = null!;

    public VulkanSettingsMenu(VulkanApiManager vulkanApiManager)
    {
        this.vulkanApiManager = vulkanApiManager;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        RefreshText();
    }

    [UIAction("toggle-auto-relaunch")]
    private void ToggleAutoRelaunch()
    {
        vulkanApiManager.IsAutoRelaunchEnabled = !vulkanApiManager.IsAutoRelaunchEnabled;
        RefreshText();
    }

    [UIAction("relaunch-vulkan")]
    private void RelaunchVulkan()
    {
        vulkanApiManager.RelaunchWithVulkan();
    }

    private void RefreshText()
    {
        statusText.text = vulkanApiManager.StatusText;
        autoRelaunchText.text = vulkanApiManager.IsAutoRelaunchEnabled
            ? "Automatic Vulkan relaunch: On"
            : "Automatic Vulkan relaunch: Off";
    }
}
