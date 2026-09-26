using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI.Components;

// Popups inherit the shell's zero window padding just like tooltips do, so menus restore their
// own spacing here.
internal static class ContextMenu
{
    private const float PaddingX = 8f;
    private const float PaddingY = 8f;
    private const float ItemPadX = 10f;
    private const float ItemPadY = 6f;

    public sealed class Scope : IDisposable
    {
        private readonly ImRaii.StyleDisposable style;
        private readonly ImRaii.ColorDisposable color;

        public bool Open { get; }

        public Scope(string id)
        {
            var scale = ImGuiHelpers.GlobalScale;
            this.style = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(PaddingX, PaddingY) * scale)
                .Push(ImGuiStyleVar.PopupRounding, Styling.CardRounding * scale)
                .Push(ImGuiStyleVar.PopupBorderSize, 1f)
                .Push(ImGuiStyleVar.ItemSpacing, new Vector2(ItemPadX, ItemPadY) * scale)
                .Push(ImGuiStyleVar.FramePadding, new Vector2(ItemPadX, ItemPadY) * scale);
            this.color = ImRaii.PushColor(ImGuiCol.PopupBg, Styling.WithAlpha(Styling.Surface2, 0.98f))
                .Push(ImGuiCol.Border, Styling.WithAlpha(Styling.BorderDim, 0.85f))
                .Push(ImGuiCol.Text, Styling.TextSecondary)
                .Push(ImGuiCol.TextDisabled, Styling.TextMuted)
                .Push(ImGuiCol.HeaderHovered, Styling.WithAlpha(Styling.AccentGold, 0.22f))
                .Push(ImGuiCol.Separator, Styling.WithAlpha(Styling.BorderDim, 0.6f));
            this.Open = ImGui.BeginPopup(id);
        }

        public void Dispose()
        {
            if (this.Open)
            {
                ImGui.EndPopup();
            }

            this.color.Dispose();
            this.style.Dispose();
        }
    }

    public static Scope Begin(string id) => new(id);

    public static bool Item(string label)
        => ImGui.Selectable(label);
}
