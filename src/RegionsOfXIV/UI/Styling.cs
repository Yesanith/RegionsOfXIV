using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

// The palette and the ImGui style the plugin's own windows are drawn with. Gold on warm black,
// after the icon; everything the shell paints by hand reads its colours from here.
internal static class Styling
{
    public static readonly Vector4 AccentGold = new(0.875f, 0.761f, 0.584f, 1.00f);
    public static readonly Vector4 AccentGoldSoft = new(0.965f, 0.890f, 0.760f, 1.00f);
    public static readonly Vector4 AccentEmber = new(0.870f, 0.470f, 0.240f, 1.00f);
    public static readonly Vector4 InkOnGold = new(0.145f, 0.110f, 0.070f, 1.00f);
    public static readonly Vector4 AccentMint = new(0.460f, 0.860f, 0.660f, 1.00f);
    public static readonly Vector4 AccentMintSoft = new(0.660f, 0.960f, 0.800f, 1.00f);
    public static readonly Vector4 AccentAmber = new(0.940f, 0.740f, 0.340f, 1.00f);
    public static readonly Vector4 AccentAmberSoft = new(1.000f, 0.860f, 0.520f, 1.00f);
    public static readonly Vector4 AccentRose = new(0.930f, 0.420f, 0.480f, 1.00f);
    public static readonly Vector4 AccentRoseSoft = new(1.000f, 0.620f, 0.680f, 1.00f);
    public static readonly Vector4 AccentBlue = new(0.420f, 0.660f, 0.960f, 1.00f);
    public static readonly Vector4 AccentBlueSoft = new(0.640f, 0.810f, 1.000f, 1.00f);
    public static readonly Vector4 AccentDiscord = new(0.345f, 0.396f, 0.949f, 1.00f);

    // Buy Me a Coffee's own yellow, the way AccentDiscord is Discord's own blurple. A tile people
    // are meant to recognise at a glance is worth the brand colour rather than a palette one, and
    // ForegroundOn puts dark ink on it, since it is far too bright to carry light text.
    public static readonly Vector4 AccentCoffee = new(1.000f, 0.867f, 0.000f, 1.00f);

    public static readonly Vector4 WindowBg = new(0.051f, 0.043f, 0.035f, 0.985f);
    public static readonly Vector4 Surface0 = new(0.082f, 0.071f, 0.059f, 1.00f);
    public static readonly Vector4 Surface1 = new(0.110f, 0.096f, 0.080f, 1.00f);
    public static readonly Vector4 Surface2 = new(0.145f, 0.127f, 0.106f, 1.00f);
    public static readonly Vector4 Surface3 = new(0.188f, 0.165f, 0.137f, 1.00f);
    public static readonly Vector4 SliderBg = new(0.170f, 0.150f, 0.125f, 1.00f);
    public static readonly Vector4 BorderDim = new(0.300f, 0.262f, 0.212f, 1.00f);

    public static readonly Vector4 TextStrong = new(0.965f, 0.945f, 0.910f, 1.00f);
    public static readonly Vector4 TextSecondary = new(0.820f, 0.790f, 0.740f, 1.00f);
    public static readonly Vector4 TextDim = new(0.620f, 0.585f, 0.525f, 1.00f);
    public static readonly Vector4 TextMuted = new(0.450f, 0.420f, 0.370f, 1.00f);

    public static readonly Vector4 Hairline = new(1f, 1f, 1f, 0.055f);

    public const float WindowRounding = 14f;
    public const float CardRounding = 10f;
    public const float FrameRounding = 7f;

    public const double PulseMedium = 800.0;
    public const double PulseBreath = 2600.0;
    public const double PulseOrbit = 3400.0;

    // Gold is light enough that white on it reads poorly, so anything brighter than mid-grey
    // takes dark ink instead.
    private const float InkLuminanceThreshold = 0.45f;

    public static float Pulse(double periodMs = PulseMedium)
    {
        var t = (Environment.TickCount % periodMs) / periodMs;
        return (float)((Math.Sin(t * Math.PI * 2.0) + 1.0) * 0.5);
    }

    public static Vector4 PulseColor(Vector4 from, Vector4 to, double periodMs = PulseMedium)
        => Vector4.Lerp(from, to, Pulse(periodMs));

    public static float Phase(double periodMs)
        => (float)((Environment.TickCount % periodMs) / periodMs);

    public static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };

    public static Vector4 Lighten(Vector4 color, float amount)
        => Vector4.Lerp(color, Vector4.One, amount) with { W = color.W };

    public static Vector4 Darken(Vector4 color, float amount)
        => Vector4.Lerp(color, Vector4.Zero, amount) with { W = color.W };

    public static Vector4 Tint(Vector4 baseColor, Vector4 accent, float amount)
        => Vector4.Lerp(baseColor, accent, amount) with { W = baseColor.W };

    public static Vector4 ForegroundOn(Vector4 fill)
        => Luminance(fill) > InkLuminanceThreshold ? InkOnGold : TextStrong;

    private static float Luminance(Vector4 color)
        => (0.2126f * Linear(color.X)) + (0.7152f * Linear(color.Y)) + (0.0722f * Linear(color.Z));

    private static float Linear(float channel)
        => channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);

    public static void VSpace(float pixels)
        => ImGui.Dummy(new Vector2(0f, pixels * ImGuiHelpers.GlobalScale));

    public static void SectionLabel(string label)
    {
        using (Fonts.PushHeadline())
        using (ImRaii.PushColor(ImGuiCol.Text, TextStrong))
        {
            ImGui.TextUnformatted(label);
        }
    }

    public static IDisposable PushChrome(Vector2 windowPadding)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var style = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, WindowRounding * scale)
            .Push(ImGuiStyleVar.WindowBorderSize, 1f)
            .Push(ImGuiStyleVar.WindowPadding, windowPadding * scale)
            .Push(ImGuiStyleVar.ChildRounding, CardRounding * scale)
            .Push(ImGuiStyleVar.ChildBorderSize, 0f)
            .Push(ImGuiStyleVar.PopupRounding, CardRounding * scale)
            .Push(ImGuiStyleVar.PopupBorderSize, 1f)
            .Push(ImGuiStyleVar.FrameRounding, FrameRounding * scale)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(10f, 6f) * scale)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(10f, 8f) * scale)
            .Push(ImGuiStyleVar.ItemInnerSpacing, new Vector2(6f, 4f) * scale)
            .Push(ImGuiStyleVar.ScrollbarSize, 9f * scale)
            .Push(ImGuiStyleVar.ScrollbarRounding, 9f * scale)
            .Push(ImGuiStyleVar.GrabRounding, 6f * scale)
            .Push(ImGuiStyleVar.GrabMinSize, 12f * scale);

        var color = ImRaii.PushColor(ImGuiCol.WindowBg, WindowBg)
            .Push(ImGuiCol.ChildBg, Vector4.Zero)
            .Push(ImGuiCol.PopupBg, Surface1 with { W = 0.985f })
            .Push(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 0.09f))
            .Push(ImGuiCol.BorderShadow, Vector4.Zero)
            .Push(ImGuiCol.FrameBg, SliderBg)
            .Push(ImGuiCol.FrameBgHovered, Surface2)
            .Push(ImGuiCol.FrameBgActive, Surface3)
            .Push(ImGuiCol.ScrollbarBg, Vector4.Zero)
            .Push(ImGuiCol.ScrollbarGrab, new Vector4(1f, 1f, 1f, 0.12f))
            .Push(ImGuiCol.ScrollbarGrabHovered, new Vector4(1f, 1f, 1f, 0.20f))
            .Push(ImGuiCol.ScrollbarGrabActive, new Vector4(1f, 1f, 1f, 0.28f))
            .Push(ImGuiCol.Button, Surface1)
            .Push(ImGuiCol.ButtonHovered, Surface2)
            .Push(ImGuiCol.ButtonActive, Tint(Surface2, AccentGold, 0.35f))
            .Push(ImGuiCol.Header, Tint(Surface1, AccentGold, 0.30f))
            .Push(ImGuiCol.HeaderHovered, Surface2)
            .Push(ImGuiCol.HeaderActive, Tint(Surface2, AccentGold, 0.40f))
            .Push(ImGuiCol.CheckMark, AccentGoldSoft)
            .Push(ImGuiCol.SliderGrab, AccentGold)
            .Push(ImGuiCol.SliderGrabActive, AccentGoldSoft)
            .Push(ImGuiCol.Text, TextStrong)
            .Push(ImGuiCol.TextDisabled, TextMuted)
            .Push(ImGuiCol.Separator, Hairline)
            .Push(ImGuiCol.ResizeGrip, Vector4.Zero)
            .Push(ImGuiCol.ResizeGripHovered, Vector4.Zero)
            .Push(ImGuiCol.ResizeGripActive, Vector4.Zero)
            .Push(ImGuiCol.TextSelectedBg, WithAlpha(AccentGold, 0.40f));

        return new ChromeScope(style, color);
    }

    private sealed class ChromeScope(IDisposable style, IDisposable color) : IDisposable
    {
        public void Dispose()
        {
            color.Dispose();
            style.Dispose();
        }
    }
}
