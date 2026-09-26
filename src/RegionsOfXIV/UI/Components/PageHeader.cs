using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace RegionsOfXIV.UI.Components;

internal static class PageHeader
{
    private const float SubtitleGap = 5f;

    public static void Draw(string title, string subtitle, Vector4? subtitleColor = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;

        Vector2 titleSize;
        using (Fonts.PushTitle())
        {
            titleSize = TextDraw.Measure(title);
            TextDraw.At(title, origin, Styling.TextStrong);
        }

        var subtitleY = origin.Y + titleSize.Y + (SubtitleGap * scale);
        var subtitleSize = TextDraw.MeasureWrapped(subtitle, width);
        TextDraw.Wrapped(subtitle, new Vector2(origin.X, subtitleY), width, subtitleColor ?? Styling.TextDim);

        ImGui.Dummy(new Vector2(width, titleSize.Y + (SubtitleGap * scale) + subtitleSize.Y));
        Paint.Divider(12f);
    }
}
