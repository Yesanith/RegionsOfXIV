using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// The vocabulary every tab draws with, rather than anything about the window itself.
//
// They all take the current value and hand back the new one, setting a shared "changed" flag, so
// a tab can act once at the end instead of after each control. That shape is why they are worth
// having: a tab reads as a list of settings rather than as a list of if-edited blocks.
//
// The enum Label overloads live here too. They are what Choice is given to turn a value into
// wording, and no tab owns them: Motion and Particles are drawn on Motion, fonts on Fonts.
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

    // Loc.Get rather than Loc.Label: Choice supplies the identity from the enum value itself, so
    // these are wording only. The two "None"s keep separate keys because a language that inflects
    // for gender will not want one word for both.
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

    // format reaches native sprintf. Handing the whole string to a translator would put the
    // specifier in their keeping, and one that no longer matches the float being passed is
    // undefined behaviour rather than a wrong-looking number -- so callers concatenate a
    // translated unit onto a literal specifier instead of translating the format.
    //
    // The unit half goes through Loc.Unit, which doubles any per-cent sign in it. Concatenating
    // on its own only moves the problem one string along: the unit reaches sprintf as well.
    private static float Slider(
        string label, float value, float min, float max, string format, ref bool changed,
        ImGuiSliderFlags flags = ImGuiSliderFlags.None)
    {
        if (ImGui.SliderFloat(label, ref value, min, max, format, flags))
            changed = true;

        return value;
    }

    private static TimeSpan DrawSeconds(
        string label, TimeSpan value, float min, float max, ref bool changed, ref bool settled)
    {
        var seconds = (float)value.TotalSeconds;
        var edited = ImGui.SliderFloat(
            label, ref seconds, min, max, "%.2f " + Loc.Unit("units.seconds", "s"));

        settled |= ImGui.IsItemDeactivatedAfterEdit();

        if (!edited)
            return value;

        changed = true;
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool Checkbox(string label, bool value, ref bool changed)
    {
        if (ImGui.Checkbox(label, ref value))
            changed = true;

        return value;
    }

    // The alpha bar is back, but floored at Configuration.MinAlpha rather than running to zero.
    // Unbounded, it was easy to drag to nothing by accident and impossible to undo, because
    // picking a new colour leaves the alpha where it was -- so the line simply vanished and read
    // as a broken plugin. Dragging below the floor now stops there instead.
    //
    // NoTooltip replaces ImGui's own colour tooltip with the one below, which is the only place
    // the floor is explained.
    private static Vector4 ColorPicker(string label, Vector4 value, ref bool changed)
    {
        var edited = ImGui.ColorEdit4(
            label,
            ref value,
            ImGuiColorEditFlags.NoInputs
            | ImGuiColorEditFlags.AlphaBar
            | ImGuiColorEditFlags.AlphaPreviewHalf
            | ImGuiColorEditFlags.NoTooltip);

        if (edited)
        {
            changed = true;
            value = value with { W = Math.Max(value.W, Configuration.MinAlpha) };
        }

        UiText.Tooltip(Loc.Format(
            "common.colour.tooltip",
            "Currently {0:F0}% solid.\n\nClick for the full picker. The narrow chequered strip "
            + "right of the rainbow is alpha (how solid this colour is) and it stops at "
            + "{1:F0}%, far enough back to sit behind the other lines but not so far that the "
            + "line disappears and looks like a fault.",
            value.W * 100f,
            Configuration.MinAlpha * 100f));

        return value;
    }

    private static T Choice<T>(string label, T value, Func<T, string> name, ref bool changed)
        where T : struct, Enum
    {
        using var combo = ImRaii.Combo(label, name(value));
        if (!combo)
            return value;

        foreach (var option in Enum.GetValues<T>())
        {
            // The enum value is the identity, not the wording. Two options that translate to the
            // same word would otherwise be one entry, and the one you could not pick would look
            // like a dropdown that ignores you.
            if (!ImGui.Selectable($"{name(option)}###{option}", option.Equals(value)))
                continue;

            value = option;
            changed = true;
        }

        return value;
    }

    // What a row of buttons will occupy, including the spacing before each one. Used to reserve
    // room beside a field rather than guessing at it -- button words are translated, and several
    // languages set them a good deal wider than English does.
    //
    // Measured with the identity hidden, because these labels carry "###key" and CalcTextSize
    // counts that as visible text unless told not to.
    private static float ButtonRowWidth(params string[] labels)
    {
        var style = ImGui.GetStyle();
        var width = 0f;

        foreach (var label in labels)
        {
            width += ImGui.CalcTextSize(label, hideTextAfterDoubleHash: true).X
                     + (style.FramePadding.X * 2f)
                     + style.ItemSpacing.X;
        }

        return width;
    }

    private static readonly Vector4 GoodColor = new(0.45f, 0.85f, 0.45f, 1f);

    private static readonly Vector4 CautionColor = new(1f, 0.78f, 0.35f, 1f);

    private static readonly Vector4 FaultColor = new(1f, 0.35f, 0.35f, 1f);

    // This window's own two compositions -- a colour plus a wrap -- over the shared drawing in
    // UiText. Everything else in here calls UiText directly.
    private static void Warn(Vector4 color, string text)
    {
        using var pushed = ImRaii.PushColor(ImGuiCol.Text, color);

        UiText.Wrapped(text);
    }

    private static void DisabledWrapped(string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));

        UiText.Wrapped(text);
    }
}
