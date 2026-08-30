using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

// Text, TextWrapped, TextDisabled, TextColored and SetTooltip are all format functions in Dear
// ImGui, and this binding offers no overload that takes arguments for any of them -- so a per-cent
// sign in the string reaches the formatter as a specifier with nothing behind it. Windows draw
// their prose through here instead, where TextUnformatted formats nothing.
//
// Shared rather than private to one window because ConfigWindow and ChangelogWindow both need it,
// and a second copy would be a second place for the guarantee to lapse.
internal static class UiText
{
    // Pushes the wrap position unconditionally, where ImGui.TextWrapped leaves an outer one alone.
    // Every caller draws in a window or tab body rather than inside a tooltip, so there is no outer
    // wrap to preserve.
    public static void Wrapped(string text)
    {
        ImGui.PushTextWrapPos(0f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    public static void Disabled(string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));

        ImGui.TextUnformatted(text);
    }

    public static void Colored(Vector4 color, string text)
    {
        using var pushed = ImRaii.PushColor(ImGuiCol.Text, color);

        ImGui.TextUnformatted(text);
    }

    private const float TooltipWidthInEm = 35f;

    // Note the argument is built whether or not the tooltip shows, so keep interpolated ones off
    // hot paths.
    public static void Tooltip(string text)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            return;

        using var tooltip = ImRaii.Tooltip();

        ImGui.PushTextWrapPos(ImGui.GetFontSize() * TooltipWidthInEm);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }
}
