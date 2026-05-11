using BeatSaberMarkupLanguage.Attributes;
using TMPro;

namespace BeatsaberVulkan.Menu;

internal class VulkanSettingsMenu
{
    private readonly VulkanApiManager vulkanApiManager;

    [UIComponent("status-text")] private readonly TextMeshProUGUI statusText = null!;
    [UIComponent("dxvk-status-text")] private readonly TextMeshProUGUI dxvkStatusText = null!;

    public VulkanSettingsMenu(VulkanApiManager vulkanApiManager)
    {
        this.vulkanApiManager = vulkanApiManager;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        RefreshText();
    }

    private void RefreshText()
    {
        statusText.text = vulkanApiManager.StatusText;
        dxvkStatusText.text = vulkanApiManager.DxvkStatusText;
    }
}
