using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace RegionsOfXIV.UI.Components;

// A miniature of the screen with the notification's anchor drawn on it. Dragging the anchor sets
// both position sliders at once, which is a good deal quicker than two sliders for a point.
internal static class PositionPad
{
    private const float Aspect = 9f / 16f;
    private const float DotRadius = 5f;
    private const float Inset = 1f;

    public static bool Draw(string id, ref float horizontal, ref float vertical, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width, width * Aspect);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var hit = Hit.Area(id, size);
        var hover = Motion.Hover(Motion.Key(id), hit.Hovered);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Styling.FrameRounding * scale;

        var changed = false;
        if (hit.Held)
        {
            var mouse = ImGui.GetMousePos();
            var nextHorizontal = MathF.Round(Math.Clamp((mouse.X - origin.X) / size.X, 0f, 1f) * 100f);
            var nextVertical = MathF.Round(Math.Clamp((mouse.Y - origin.Y) / size.Y, 0f, 1f) * 100f);
            if (nextHorizontal != horizontal || nextVertical != vertical)
            {
                horizontal = nextHorizontal;
                vertical = nextVertical;
                changed = true;
            }
        }

        Paint.Gradient(drawList, origin, end, new Vector4(0.10f, 0.12f, 0.17f, 1f), new Vector4(0.04f, 0.045f, 0.06f, 1f), rounding);
        Paint.Stroke(drawList, origin, end, Vector4.Lerp(Styling.WithAlpha(Styling.BorderDim, 0.8f), Styling.WithAlpha(Styling.AccentGoldSoft, 0.8f), MathF.Max(hover, hit.Held ? 1f : 0f)), rounding);

        var thirds = Paint.Col(new Vector4(1f, 1f, 1f, 0.05f));
        for (var line = 1; line < 3; line++)
        {
            drawList.AddLine(new Vector2(origin.X + (size.X * line / 3f), origin.Y + Inset), new Vector2(origin.X + (size.X * line / 3f), end.Y - Inset), thirds);
            drawList.AddLine(new Vector2(origin.X + Inset, origin.Y + (size.Y * line / 3f)), new Vector2(end.X - Inset, origin.Y + (size.Y * line / 3f)), thirds);
        }

        var dot = new Vector2(origin.X + (size.X * horizontal / 100f), origin.Y + (size.Y * vertical / 100f));
        var guide = Paint.Col(Styling.WithAlpha(Styling.AccentGold, 0.25f + (0.25f * hover)));
        drawList.AddLine(new Vector2(origin.X + Inset, dot.Y), new Vector2(end.X - Inset, dot.Y), guide);
        drawList.AddLine(new Vector2(dot.X, origin.Y + Inset), new Vector2(dot.X, end.Y - Inset), guide);

        var radius = DotRadius * scale * (1f + (0.2f * hover));
        drawList.AddCircleFilled(dot, radius * 2.2f, Paint.Col(Styling.WithAlpha(Styling.AccentGold, 0.18f)));
        drawList.AddCircleFilled(dot, radius, Paint.Col(Styling.AccentGoldSoft));
        drawList.AddCircle(dot, radius, Paint.Col(Styling.InkOnGold), 0, 1.2f * scale);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        return changed;
    }
}
