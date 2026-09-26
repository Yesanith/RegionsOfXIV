using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

// The vocabulary every page draws with, rather than anything about the window itself.
//
// They all take the current value and hand back the new one, setting a shared "changed" flag, so
// a page can act once at the end instead of after each control. Every one of them is a settings
// row: the label and help on the left, the control on the right.
internal sealed partial class ConfigWindow
{
    // Trump Gothic, Jupiter and Axis are the names of typefaces and are not translated in any
    // language -- they are what the files are called. Only the two entries carrying words go
    // through Loc.
    private static string Label(FontChoice choice) => choice switch
    {
        FontChoice.NotoSansCjk => Loc.Format(
            "fonts.choice.noto", "{0} (recommended)", "Noto Sans CJK"),
        FontChoice.TrumpGothic => "Trump Gothic",
        FontChoice.Jupiter => "Jupiter",
        FontChoice.Axis => "Axis",
        FontChoice.Custom => Loc.Get("fonts.choice.custom", "Custom file"),
        _ => choice.ToString(),
    };

    // The two "None"s keep separate keys because a language that inflects for gender will not
    // want one word for both.
    private static string Label(MotionEffect effect) => effect switch
    {
        MotionEffect.None => Loc.Get("motion.choice.none", "None"),
        MotionEffect.Typewriter => Loc.Get("motion.choice.typewriter", "Typewriter"),
        MotionEffect.Rise => Loc.Get("motion.choice.rise", "Rise"),
        MotionEffect.Wave => Loc.Get("motion.choice.wave", "Wave"),
        MotionEffect.Burn => Loc.Get("motion.choice.burn", "Burn"),
        _ => effect.ToString(),
    };

    private static string Label(ParticleEffect effect) => effect switch
    {
        ParticleEffect.None => Loc.Get("motion.particles.none", "None"),
        ParticleEffect.Hearts => Loc.Get("motion.particles.hearts", "Hearts"),
        ParticleEffect.Embers => Loc.Get("motion.particles.embers", "Embers"),
        ParticleEffect.Sparkles => Loc.Get("motion.particles.sparkles", "Sparkles"),
        ParticleEffect.Petals => Loc.Get("motion.particles.petals", "Petals"),
        _ => effect.ToString(),
    };

    private static readonly ChoiceLabels<FontChoice> FontChoiceLabels = new(Label);
    private static readonly ChoiceLabels<MotionEffect> MotionLabels = new(Label);
    private static readonly ChoiceLabels<ParticleEffect> ParticleLabels = new(Label);

    private readonly ColumnLayout columns = new();

    private static bool Toggle(string id, string label, string? help, bool value, ref bool changed, bool enabled = true)
    {
        var row = SettingsRow.Begin(label, help, Layout.ToggleWidth, SettingsRow.ToggleHeight, enabled);
        if (ToggleSwitch.Draw(id, ref value, enabled))
        {
            changed = true;
        }

        row.End();
        return value;
    }

    // format reaches native sprintf. Handing the whole string to a translator would put the
    // specifier in their keeping, and one that no longer matches the float being passed is
    // undefined behaviour rather than a wrong-looking number -- so callers concatenate a
    // translated unit onto a literal specifier instead of translating the format.
    private static float Slider(
        string id, string label, string? help, float value, float min, float max, string format, ref bool changed,
        ImGuiSliderFlags flags = ImGuiSliderFlags.None, bool enabled = true)
    {
        var row = SettingsRow.Begin(label, help, Layout.RowSliderWidth, 0f, enabled);
        if (SliderControl(id, ref value, min, max, format, flags, enabled))
        {
            changed = true;
        }

        row.End();
        return value;
    }

    private static TimeSpan Seconds(
        string id, string label, string? help, TimeSpan value, float min, float max, ref bool changed, ref bool settled, bool enabled = true)
    {
        var seconds = (float)value.TotalSeconds;
        var row = SettingsRow.Begin(label, help, Layout.RowSliderWidth, 0f, enabled);
        var edited = SliderControl(id, ref seconds, min, max, "%.2f " + Loc.Unit("units.seconds", "s"), ImGuiSliderFlags.None, enabled);
        settled |= ImGui.IsItemDeactivatedAfterEdit();
        row.End();

        if (!edited)
        {
            return value;
        }

        changed = true;
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool SliderControl(string id, ref float value, float min, float max, string format, ImGuiSliderFlags flags, bool enabled)
    {
        ImGui.SetNextItemWidth(Layout.RowSliderWidth * ImGuiHelpers.GlobalScale);
        using var disabled = ImRaii.Disabled(!enabled);
        using var colors = ImRaii.PushColor(ImGuiCol.SliderGrab, Styling.AccentGold)
            .Push(ImGuiCol.SliderGrabActive, Styling.AccentGoldSoft)
            .Push(ImGuiCol.FrameBg, Styling.SliderBg)
            .Push(ImGuiCol.FrameBgHovered, Styling.Surface3)
            .Push(ImGuiCol.FrameBgActive, Styling.Surface3)
            .Push(ImGuiCol.Text, Styling.TextStrong);

        return ImGui.SliderFloat(id, ref value, min, max, format, flags);
    }

    // The alpha bar is floored at Configuration.MinAlpha rather than running to zero. Unbounded,
    // it was easy to drag to nothing by accident and impossible to undo, because picking a new
    // colour leaves the alpha where it was -- so the line simply vanished and read as a broken
    // plugin. Dragging below the floor now stops there instead.
    private static Vector4 Colour(string id, string label, string? help, Vector4 value, ref bool changed, bool enabled = true)
    {
        var row = SettingsRow.Begin(label, help, Layout.RowSwatchWidth, 0f, enabled);
        if (ColorSwatch.Draw(id, ref value, Layout.RowSwatchWidth, out var hovered, enabled))
        {
            changed = true;
            value = value with { W = Math.Max(value.W, Configuration.MinAlpha) };
        }

        if (hovered)
        {
            Tooltip.Show(Loc.Format(
                "common.colour.tooltip",
                "Currently {0:F0}% solid.\n\nClick for the full picker. The narrow chequered strip "
                + "right of the rainbow is alpha (how solid this colour is) and it stops at "
                + "{1:F0}%, far enough back to sit behind the other lines but not so far that the "
                + "line disappears and looks like a fault.",
                value.W * 100f,
                Configuration.MinAlpha * 100f));
        }

        row.End();
        return value;
    }

    private static T Choice<T>(string id, string label, string? help, T value, ChoiceLabels<T> labels, ref bool changed, bool enabled = true)
        where T : struct, Enum
    {
        var row = SettingsRow.Begin(label, help, Layout.RowDropdownWidth, 0f, enabled);
        var selected = labels.IndexOf(value);
        if (Dropdown.Draw(id, labels.Resolve(), ref selected, Layout.RowDropdownWidth, enabled))
        {
            value = labels.Values[selected];
            changed = true;
        }

        row.End();
        return value;
    }

    // Rows that only mean something while a switch is on are shown only then. A page reads as
    // the choices that apply, and a switch that unfolds its own settings explains itself.
    private static ImRaii.StyleDisposable? Reveal(string id, bool shown) => Motion.PushSection(id, shown);

    // The wording of an enum's options, rebuilt only when the language table is swapped, so a
    // dropdown drawn every frame does not translate its list every frame.
    private sealed class ChoiceLabels<T>(Func<T, string> name)
        where T : struct, Enum
    {
        public readonly T[] Values = Enum.GetValues<T>();

        private readonly string[] labels = new string[Enum.GetValues<T>().Length];

        private int generation = -1;

        public string[] Resolve()
        {
            if (this.generation == Loc.Generation)
            {
                return this.labels;
            }

            for (var index = 0; index < this.Values.Length; index++)
            {
                this.labels[index] = name(this.Values[index]);
            }

            this.generation = Loc.Generation;
            return this.labels;
        }

        public int IndexOf(T value)
        {
            for (var index = 0; index < this.Values.Length; index++)
            {
                if (this.Values[index].Equals(value))
                {
                    return index;
                }
            }

            return 0;
        }
    }

    // Two columns of groups when the page is wide enough for them, one when it is not. Next moves
    // to the top of the next column; End drops the cursor below the taller one.
    private sealed class ColumnLayout
    {
        private Vector2 origin;
        private float width;
        private float gap;
        private float bottom;
        private int count;
        private int current;

        public void Begin(int wanted)
        {
            var scale = ImGuiHelpers.GlobalScale;
            this.origin = ImGui.GetCursorScreenPos();
            this.gap = Layout.ColumnGap * scale;
            var available = ImGui.GetContentRegionAvail().X;
            var fit = (int)MathF.Floor((available + this.gap) / ((Layout.ColumnMinWidth * scale) + this.gap));
            this.count = Math.Clamp(Math.Min(wanted, fit), 1, wanted);
            this.width = (available - (this.gap * (this.count - 1))) / this.count;
            this.bottom = this.origin.Y;
            this.current = -1;
            SettingsGroup.ColumnWidth = this.width;
        }

        public void Next()
        {
            Track();
            this.current++;
            if (this.count == 1 || this.current >= this.count)
            {
                return;
            }

            ImGui.SetCursorScreenPos(new Vector2(this.origin.X + (this.current * (this.width + this.gap)), this.origin.Y));
        }

        public void End()
        {
            Track();
            SettingsGroup.ColumnWidth = 0f;
            ImGui.SetCursorScreenPos(new Vector2(this.origin.X, this.bottom));
            ImGui.Dummy(new Vector2((this.width * this.count) + (this.gap * (this.count - 1)), 0f));
        }

        private void Track() => this.bottom = MathF.Max(this.bottom, ImGui.GetCursorScreenPos().Y);
    }

    private static void Warn(string text) => NoticeCard.Draw(Styling.AccentAmber, FontAwesomeIcon.ExclamationTriangle, text);

    private static void Fault(string text) => NoticeCard.Draw(Styling.AccentRose, FontAwesomeIcon.ExclamationCircle, text);

    private static void Good(string text) => NoticeCard.Draw(Styling.AccentMint, FontAwesomeIcon.CheckCircle, text);
}
