using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace RegionsOfXIV.UI.Components;

internal static class ToggleSwitch
{
    public const float TrackWidth = 40f;
    public const float TrackHeight = 22f;

    private const float KnobInset = 3f;

    public static bool Draw(string id, ref bool value, bool enabled = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(TrackWidth * scale, TrackHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var hit = Hit.Area(id, size, enabled);

        var changed = false;
        if (hit.Clicked)
        {
            value = !value;
            changed = true;
        }

        var on = Motion.Approach(Motion.Key(id), value ? 1f : 0f, 16f);
        var hover = Motion.Hover(Motion.Key(id, 1), hit.Hovered);
        var drawList = ImGui.GetWindowDrawList();
        var end = origin + size;
        var rounding = size.Y * 0.5f;

        var offColor = Vector4.Lerp(Styling.Surface2, Styling.Surface3, hover);
        var onColor = Vector4.Lerp(Styling.AccentGold, Styling.AccentGoldSoft, hover * 0.4f);
        if (!enabled)
        {
            onColor = Styling.Tint(Styling.Surface3, Styling.AccentGold, 0.35f);
        }

        var track = Vector4.Lerp(offColor, onColor, on);

        if (on > 0.01f && enabled)
        {
            var grow = new Vector2(2f * scale, 2f * scale);
            drawList.AddRectFilled(origin - grow, end + grow, Paint.Col(Styling.WithAlpha(Styling.AccentGold, 0.18f * on)), rounding + grow.X);
        }

        Paint.Fill(drawList, origin, end, track, rounding);
        var border = Vector4.Lerp(Styling.WithAlpha(Styling.BorderDim, 0.8f), Styling.WithAlpha(Styling.AccentGoldSoft, enabled ? 0.6f : 0.25f), on);
        Paint.Stroke(drawList, origin, end, border, rounding);

        var inset = KnobInset * scale;
        var knobRadius = (size.Y - (inset * 2f)) * 0.5f;
        var travel = size.X - ((inset + knobRadius) * 2f);
        var knobCenter = new Vector2(origin.X + inset + knobRadius + (travel * on), origin.Y + (size.Y * 0.5f));
        drawList.AddCircleFilled(knobCenter + new Vector2(0f, 1f * scale), knobRadius, Paint.Col(new Vector4(0f, 0f, 0f, 0.30f)));
        var knob = enabled ? Vector4.Lerp(Styling.TextSecondary, Styling.ForegroundOn(onColor), on) : Styling.TextMuted;
        drawList.AddCircleFilled(knobCenter, knobRadius, Paint.Col(knob));

        return changed;
    }
}
