using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace RegionsOfXIV.UI.Components;

// A tinted card with an icon and a paragraph: warnings, faults, confirmations and notices all
// take this shape, told apart by their accent.
internal static class NoticeCard
{
    private const float PadX = 14f;
    private const float PadY = 11f;
    private const float IconGap = 11f;
    private const float BottomGap = 10f;

    public static void Draw(Vector4 accent, FontAwesomeIcon icon, string text, float width = 0f, Action? trailing = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var cardWidth = width > 0f ? width : ImGui.GetContentRegionAvail().X;
        var padX = PadX * scale;
        var padY = PadY * scale;
        var iconSize = TextDraw.IconSize(icon);
        var textX = origin.X + padX + iconSize.X + (IconGap * scale);
        var textWidth = MathF.Max(1f, origin.X + cardWidth - padX - textX);
        var textHeight = TextDraw.MeasureWrapped(text, textWidth).Y;
        var height = (padY * 2f) + MathF.Max(textHeight, iconSize.Y);
        var max = origin + new Vector2(cardWidth, height);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Styling.CardRounding * scale;

        Paint.Fill(drawList, origin, max, Styling.WithAlpha(accent, 0.09f), rounding);
        Paint.Fill(drawList, origin, new Vector2(origin.X + (3f * scale), max.Y), Styling.WithAlpha(accent, 0.85f), rounding, ImDrawFlags.RoundCornersLeft);
        Paint.Stroke(drawList, origin, max, Styling.WithAlpha(accent, 0.35f), rounding);

        TextDraw.Icon(icon, new Vector2(origin.X + padX, origin.Y + padY), Styling.Lighten(accent, 0.2f));
        TextDraw.Wrapped(text, new Vector2(textX, origin.Y + padY), textWidth, Styling.TextSecondary);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(cardWidth, height));

        if (trailing is not null)
        {
            trailing();
        }

        Styling.VSpace(BottomGap);
    }
}
