using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace RegionsOfXIV.UI.Components;

// A row of labelled tabs with a gold underline that slides to whichever is chosen. Sits between
// the preview stage and the page, so switching pages never takes the preview out of view.
//
// When the row is too narrow for every label, the tabs that are not chosen keep their icon and
// lose their label, which reads better than eight truncated words.
internal static class TabStrip
{
    public readonly record struct Item(FontAwesomeIcon Icon, string Label, bool Badge = false);

    public const int MaxItems = 10;

    private const float PadX = 12f;
    private const float IconGap = 7f;
    private const float UnderlineHeight = 2.5f;
    private const float UnderlineInset = 10f;
    private const float BadgeRadius = 3f;

    private static readonly float[] Widths = new float[MaxItems];
    private static readonly bool[] Labelled = new bool[MaxItems];

    public static bool Draw(string id, ReadOnlySpan<Item> items, ref int selected, float width, float height)
    {
        var count = Math.Min(items.Length, MaxItems);
        if (count == 0)
        {
            return false;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var current = Math.Clamp(selected, 0, count - 1);

        // Labels in the caption tier: eight of them have to share one row.
        using var font = Fonts.PushCaption();
        var total = ResolveWidths(items, count, current, width);
        var startX = origin.X + MathF.Max(0f, (width - total) * 0.5f);

        ImGui.PushID(id);

        var underlineX = Motion.Approach(Motion.Key("##tab-x"), Offset(current), 18f);
        var underlineWidth = Motion.Approach(Motion.Key("##tab-w"), Widths[current], 18f);
        var inset = UnderlineInset * scale;
        var underlineMin = new Vector2(startX + underlineX + inset, origin.Y + height - (UnderlineHeight * scale));
        var underlineMax = new Vector2(startX + underlineX + underlineWidth - inset, origin.Y + height);
        Paint.Glow(drawList, underlineMin, underlineMax, UnderlineHeight * scale, Styling.AccentGold, 0.6f);
        Paint.Fill(drawList, underlineMin, underlineMax, Styling.AccentGold, UnderlineHeight * scale);

        var changed = false;
        var x = startX;
        for (var index = 0; index < count; index++)
        {
            var tabWidth = Widths[index];
            ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
            ImGui.PushID(index + 1);
            var hit = Hit.Area("##tab", new Vector2(tabWidth, height));
            var hover = Motion.Hover(Motion.Key("##tab"), hit.Hovered);
            ImGui.PopID();

            if (hit.Clicked && selected != index)
            {
                selected = index;
                changed = true;
            }

            var isSelected = index == current;
            if (!isSelected && hover > 0.01f)
            {
                Paint.Fill(drawList, new Vector2(x, origin.Y + (6f * scale)), new Vector2(x + tabWidth, origin.Y + height - (6f * scale)),
                    Styling.WithAlpha(Styling.Surface2, 0.7f * hover), 8f * scale);
            }

            DrawContent(items[index], x, tabWidth, origin.Y, height, isSelected, hover, Labelled[index]);

            if (hit.Hovered && !Labelled[index])
            {
                Tooltip.Show(items[index].Label);
            }

            x += tabWidth;
        }

        ImGui.PopID();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return changed;
    }

    private static float ResolveWidths(ReadOnlySpan<Item> items, int count, int current, float available)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var pad = PadX * 2f * scale;
        var total = 0f;
        for (var index = 0; index < count; index++)
        {
            Labelled[index] = true;
            Widths[index] = ContentWidth(items[index], true) + pad;
            total += Widths[index];
        }

        if (total <= available)
        {
            return total;
        }

        total = 0f;
        for (var index = 0; index < count; index++)
        {
            Labelled[index] = index == current;
            Widths[index] = ContentWidth(items[index], Labelled[index]) + pad;
            total += Widths[index];
        }

        if (total <= available)
        {
            return total;
        }

        var shrink = available / total;
        for (var index = 0; index < count; index++)
        {
            Widths[index] *= shrink;
        }

        return available;
    }

    private static float ContentWidth(in Item item, bool labelled)
    {
        var iconWidth = TextDraw.IconSize(item.Icon).X;
        return labelled ? iconWidth + (IconGap * ImGuiHelpers.GlobalScale) + TextDraw.Measure(item.Label).X : iconWidth;
    }

    private static float Offset(int index)
    {
        var offset = 0f;
        for (var tab = 0; tab < index; tab++)
        {
            offset += Widths[tab];
        }

        return offset;
    }

    private static void DrawContent(in Item item, float x, float width, float top, float height, bool selected, float hover, bool labelled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var iconSize = TextDraw.IconSize(item.Icon);
        var midY = top + (height * 0.5f) - (1f * scale);
        var textColor = selected ? Styling.TextStrong : Vector4.Lerp(Styling.TextDim, Styling.TextSecondary, hover);
        var iconColor = selected ? Styling.AccentGoldSoft : textColor;

        var label = string.Empty;
        var labelSize = Vector2.Zero;
        var gap = 0f;
        if (labelled)
        {
            var available = width - (PadX * 2f * scale) - iconSize.X - (IconGap * scale);
            label = TextDraw.Truncate(item.Label, available);
            labelSize = TextDraw.Measure(label);
            gap = IconGap * scale;
        }

        var contentX = x + ((width - iconSize.X - gap - labelSize.X) * 0.5f);
        TextDraw.Icon(item.Icon, new Vector2(contentX, midY - (iconSize.Y * 0.5f)), iconColor);
        if (labelled)
        {
            TextDraw.At(label, new Vector2(contentX + iconSize.X + gap, midY - (labelSize.Y * 0.5f)), textColor);
        }

        if (!item.Badge)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var badgeCenter = new Vector2(contentX + iconSize.X + (1f * scale), midY - (iconSize.Y * 0.5f));
        drawList.AddCircleFilled(badgeCenter, (BadgeRadius + 1.5f) * scale, Paint.Col(Styling.WindowBg));
        drawList.AddCircleFilled(badgeCenter, BadgeRadius * scale, Paint.Col(Styling.PulseColor(Styling.AccentGold, Styling.AccentGoldSoft, Styling.PulseMedium)));
    }
}
