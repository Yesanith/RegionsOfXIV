using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI.Components;

// A colour shown as a swatch that opens ImGui's full picker in a popup. The swatch is painted by
// hand so it matches the rest of the row; only the picker itself is stock.
internal static class ColorSwatch
{
    private const float PickerWidth = 230f;
    private const float PopupPad = 10f;

    private const ImGuiColorEditFlags PickerFlags = ImGuiColorEditFlags.AlphaBar
        | ImGuiColorEditFlags.AlphaPreviewHalf
        | ImGuiColorEditFlags.NoSidePreview
        | ImGuiColorEditFlags.NoSmallPreview
        | ImGuiColorEditFlags.DisplayHex;

    public static bool Draw(string id, ref Vector4 value, float width, out bool hovered, bool enabled = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width * scale, ImGui.GetFrameHeight());
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var hit = Hit.Area(id, size, enabled);
        var hover = Motion.Hover(Motion.Key(id), hit.Hovered);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Styling.FrameRounding * scale;
        var popupId = string.Concat(id, "_picker");
        var open = ImGui.IsPopupOpen(popupId);
        hovered = hit.Hovered;

        if (hover > 0.01f || open)
        {
            Paint.Glow(drawList, origin, end, rounding, Styling.AccentGold, open ? 1f : hover * 0.6f);
        }

        Paint.Swatch(drawList, origin, end, enabled ? value : Styling.WithAlpha(value, value.W * 0.4f), rounding);
        Paint.TopLight(drawList, origin, end, rounding, 0.18f);
        Paint.Stroke(drawList, origin, end,
            Vector4.Lerp(Styling.WithAlpha(Styling.BorderDim, 0.9f), Styling.WithAlpha(Styling.AccentGoldSoft, 0.9f), open ? 1f : hover), rounding);

        if (hit.Clicked)
        {
            ImGui.OpenPopup(popupId);
        }

        return DrawPicker(popupId, ref value, scale);
    }

    private static bool DrawPicker(string popupId, ref Vector4 value, float scale)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(PopupPad, PopupPad) * scale)
            .Push(ImGuiStyleVar.PopupRounding, Styling.CardRounding * scale)
            .Push(ImGuiStyleVar.PopupBorderSize, 1f)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 8f) * scale);
        using var colors = ImRaii.PushColor(ImGuiCol.PopupBg, Styling.WithAlpha(Styling.Surface1, 0.985f))
            .Push(ImGuiCol.Border, Styling.WithAlpha(Styling.AccentGold, 0.42f))
            .Push(ImGuiCol.FrameBg, Styling.SliderBg)
            .Push(ImGuiCol.Text, Styling.TextSecondary);
        using var popup = ImRaii.Popup(popupId);
        if (!popup)
        {
            return false;
        }

        ImGui.SetNextItemWidth(PickerWidth * scale);
        return ImGui.ColorPicker4("##picker", ref value, PickerFlags);
    }
}
