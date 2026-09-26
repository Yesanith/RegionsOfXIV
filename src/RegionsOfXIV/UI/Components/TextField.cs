using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI.Components;

// A text input in the shell's frame. Committed is set on the frame the field loses focus after
// an edit, which is when a typed path is worth acting on.
internal static class TextField
{
    public readonly record struct Result(bool Changed, bool Committed, bool Active);

    private const float PadX = 12f;

    public static Result Draw(string id, string hint, ref string text, float width, int maxLength = 512, float height = Layout.FieldHeight, bool enabled = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var scaledHeight = height * scale;
        var end = origin + new Vector2(width, scaledHeight);
        var padX = PadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Styling.FrameRounding * scale;
        var focus = Motion.Approach(Motion.Key(id, 1), ImGui.IsMouseHoveringRect(origin, end) ? 0.5f : 0f, 16f);

        Paint.Fill(drawList, origin, end, Styling.WithAlpha(Styling.Surface0, 0.9f), rounding);

        ImGui.SetCursorScreenPos(new Vector2(origin.X + padX, origin.Y + ((scaledHeight - ImGui.GetFrameHeight()) * 0.5f)));
        ImGui.SetNextItemWidth(MathF.Max(1f, width - (padX * 2f)));

        bool changed;
        using (ImRaii.Disabled(!enabled))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Vector4.Zero)
            .Push(ImGuiCol.FrameBgHovered, Vector4.Zero)
            .Push(ImGuiCol.FrameBgActive, Vector4.Zero)
            .Push(ImGuiCol.Text, Styling.TextStrong)
            .Push(ImGuiCol.TextDisabled, Styling.TextMuted))
        {
            changed = ImGui.InputTextWithHint(id, hint, ref text, maxLength);
        }

        var active = ImGui.IsItemActive();
        var committed = ImGui.IsItemDeactivatedAfterEdit();
        var lit = MathF.Max(focus, active ? 1f : 0f);
        Paint.Stroke(drawList, origin, end,
            Vector4.Lerp(Styling.WithAlpha(Styling.BorderDim, 0.75f), Styling.WithAlpha(Styling.AccentGoldSoft, 0.85f), lit), rounding);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, scaledHeight));
        return new Result(changed, committed, active);
    }
}
