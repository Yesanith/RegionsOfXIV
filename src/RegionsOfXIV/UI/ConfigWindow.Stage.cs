using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

// The preview stage under the header: a dark sky with the notification painted on it at actual
// size, and three buttons for what the stage cannot show by itself.
internal sealed partial class ConfigWindow
{
    private static readonly Vector4 SkyTop = new(0.080f, 0.095f, 0.145f, 1f);
    private static readonly Vector4 SkyBottom = new(0.028f, 0.028f, 0.040f, 1f);

    private const float ToolbarGap = 8f;
    private const float CaptionInset = 16f;

    private PreviewSample stageSample;
    private string? stageSampleLanguage;
    private bool stageSampleBuilt;

    private void DrawStage(Vector2 origin, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width, height);
        var end = origin + size;

        ImGui.SetCursorScreenPos(origin);
        using var child = ImRaii.Child("##rox-stage", size, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!child)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        Paint.Gradient(drawList, origin, end, SkyTop, SkyBottom, 0f);
        DrawSkyGlow(drawList, origin, size);

        var anchorY = origin.Y + (Layout.StagePad * scale) + (this.stage.AboveAnchor() * scale);
        this.stage.Draw(origin, size, anchorY, StageSample());

        Paint.Hairline(drawList, new Vector2(origin.X, end.Y - 0.5f), new Vector2(end.X, end.Y - 0.5f));
        DrawStageCaption(origin, end);
        DrawStageToolbar(end);
    }

    // A faint warm pool behind the name, so the stage reads as a scene rather than a black box.
    private static void DrawSkyGlow(ImDrawListPtr drawList, Vector2 origin, Vector2 size)
    {
        const int layers = 14;
        var center = origin + new Vector2(size.X * 0.5f, size.Y * 0.55f);
        var radius = size.X * 0.42f;
        var peak = 0.035f;
        var layerAlpha = peak * 2f / layers;
        drawList.PushClipRect(origin, origin + size, true);
        for (var layer = layers; layer >= 1; layer--)
        {
            var fraction = layer / (float)layers;
            var alpha = (layerAlpha * (1f - Motion.Smoothstep(fraction))) + (layerAlpha * 0.1f);
            drawList.AddCircleFilled(center, radius * fraction, Paint.Col(Styling.WithAlpha(Styling.AccentGold, alpha)), 48);
        }

        drawList.PopClipRect();
    }

    private static void DrawStageCaption(Vector2 origin, Vector2 end)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var inset = CaptionInset * scale;
        var title = Loc.Get("preview.title", "Live preview");
        var caption = Loc.Get("preview.caption", "Actual size. The vertical position applies on the game screen.");

        using (Fonts.PushCaption())
        {
            var captionSize = TextDraw.Measure(caption);
            var titleSize = TextDraw.SmallCapsSize(title);
            var y = end.Y - inset - captionSize.Y;
            TextDraw.At(caption, new Vector2(origin.X + inset, y), Styling.WithAlpha(Styling.TextDim, 0.85f));
            TextDraw.SmallCaps(title, new Vector2(origin.X + inset, y - titleSize.Y - (2f * scale)), Styling.WithAlpha(Styling.AccentGoldSoft, 0.85f));
        }
    }

    private void DrawStageToolbar(Vector2 end)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var inset = CaptionInset * scale;
        var gap = ToolbarGap * scale;
        var height = Layout.StageToolbarHeight;
        var top = end.Y - inset - (height * scale);

        var show = Loc.Get("preview.onscreen", "Show on screen");
        var pin = Loc.Get("preview.pin", "Pin on screen");
        var replay = Loc.Get("preview.replay", "Replay");

        var x = end.X - inset - PillButton.Width(show, FontAwesomeIcon.Desktop);
        ImGui.SetCursorScreenPos(new Vector2(x, top));
        if (PillButton.Draw("##rox-stage-onscreen", show, Styling.AccentGold, PillButton.Emphasis.Tinted, FontAwesomeIcon.Desktop, height: height,
                tooltip: Loc.Get("preview.onscreen.tooltip", "Fires a sample notification on the game screen, where the position sliders place it.")))
        {
            this.actions.Preview(Sample);
        }

        x -= gap + PillButton.Width(pin, FontAwesomeIcon.Thumbtack);
        ImGui.SetCursorScreenPos(new Vector2(x, top));
        if (PillButton.Draw("##rox-stage-pin", pin, Styling.AccentAmber, this.pinned ? PillButton.Emphasis.Filled : PillButton.Emphasis.Tinted,
                FontAwesomeIcon.Thumbtack, height: height,
                tooltip: Loc.Get(
                    "preview.pin.tooltip",
                    "Keeps one sample notification on the game screen while you work, so you can\n" +
                    "check the position and size where it will really appear.\n\n" +
                    "Zone announcements are held back while this is on. It switches itself\n" +
                    "off when you close this window.")))
        {
            SetPinned(!this.pinned);
        }

        x -= gap + PillButton.Width(replay, FontAwesomeIcon.Redo);
        ImGui.SetCursorScreenPos(new Vector2(x, top));
        if (PillButton.Draw("##rox-stage-replay", replay, Styling.AccentBlue, PillButton.Emphasis.Tinted, FontAwesomeIcon.Redo, height: height,
                tooltip: Loc.Get("preview.replay.tooltip", "Plays the preview from the top, so the motion and the decode run again.")))
        {
            this.stage.Replay();
        }
    }

    // The stage asks for the sample every frame, so it is rebuilt only when the banner language
    // it depends on changes.
    private PreviewSample StageSample()
    {
        if (this.stageSampleBuilt && this.stageSampleLanguage == this.config.BannerNameLanguage)
        {
            return this.stageSample;
        }

        this.stageSample = BuildSample();
        this.stageSampleLanguage = this.config.BannerNameLanguage;
        this.stageSampleBuilt = true;
        return this.stageSample;
    }
}
