using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RegionsOfXIV.UI.Components;

internal static class Hit
{
    public readonly record struct Result(bool Clicked, bool Hovered, bool Held);

    // allowOverlap lets an item drawn later inside this area take the hover and the click, which
    // is how a small button sits on a larger card.
    public static Result Area(string id, Vector2 size, bool enabled = true, bool handCursor = true, bool allowOverlap = false)
    {
        size = new Vector2(MathF.Max(1f, size.X), MathF.Max(1f, size.Y));
        if (!enabled)
        {
            ImGui.Dummy(size);
            return default;
        }

        var clicked = ImGui.InvisibleButton(id, size);
        if (allowOverlap)
        {
            ImGui.SetItemAllowOverlap();
        }

        var hovered = ImGui.IsItemHovered();
        if (hovered && handCursor)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return new Result(clicked, hovered, ImGui.IsItemActive());
    }

    public static bool HoveringRect(Vector2 min, Vector2 max)
        => ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(min, max);
}
