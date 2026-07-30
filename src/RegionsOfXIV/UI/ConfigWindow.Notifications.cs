using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

// What gets announced, and what the game is stopped from announcing itself.
internal sealed partial class ConfigWindow
{
    private void DrawNotificationsTab()
    {
        using var tab = ImRaii.TabItem("Notifications");
        if (!tab) return;

        var changed = false;

        ImGui.TextWrapped(
            "This plugin replaces the game's own location text rather than drawing alongside " +
            "it. If you turn the suppression below off, the game's version comes back.");
        ImGui.Separator();

        var zone = this.config.ZoneNotificationEnabled;
        if (ImGui.Checkbox("Zone changes", ref zone))
        {
            this.config.ZoneNotificationEnabled = zone;
            changed = true;
        }

        var area = this.config.AreaNotificationEnabled;
        if (ImGui.Checkbox("Area changes", ref area))
        {
            this.config.AreaNotificationEnabled = area;
            changed = true;
        }

        var subArea = this.config.SubAreaNotificationEnabled;
        if (ImGui.Checkbox("Sub-area changes", ref subArea))
        {
            this.config.SubAreaNotificationEnabled = subArea;
            changed = true;
        }

        ImGui.Separator();

        var includeParent = this.config.IncludeParentTierAsHeader;
        if (ImGui.Checkbox("Show the parent tier as a header", ref includeParent))
        {
            this.config.IncludeParentTierAsHeader = includeParent;
            changed = true;
        }

        var hideNative = this.config.HideNativeAreaText;
        if (ImGui.Checkbox("Hide the game's own area text", ref hideNative))
        {
            this.config.HideNativeAreaText = hideNative;
            changed = true;

            if (!hideNative)
                this.actions.RestoreNativeAreaText();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Suppresses the native \"_AreaText\" flash, which draws underneath this plugin.");

        var hideLoadingTitle = this.config.HideNativeLoadingTitle;
        if (ImGui.Checkbox("Hide the loading-screen zone title", ref hideLoadingTitle))
        {
            this.config.HideNativeLoadingTitle = hideLoadingTitle;
            changed = true;

            if (!hideLoadingTitle)
                this.actions.RestoreNativeLoadingTitle();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Suppresses \"_LocationTitle\" and \"_LocationTitleShort\", the gold title\n" +
                "drawn over the loading screen, and shows the same names in this\n" +
                "plugin's style instead.");
        }

        if (changed)
            this.config.Save();
    }
}
