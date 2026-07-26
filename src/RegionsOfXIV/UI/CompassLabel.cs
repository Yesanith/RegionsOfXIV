using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Persistent label pinned above the minimap, showing the current area.
//
// STATUS: stub. Do not build this out before the ROADMAP Phase 0 check — if
// _NaviMap already displays the current area this is restyling rather than new
// information, and IDtrBar is the cheaper, more configurable surface.
internal sealed class CompassLabel : Window, IDisposable
{
    private readonly Configuration config;
    private readonly NaviMapAnchor anchor;

    private string? text;

    public CompassLabel(Configuration config, NaviMapAnchor anchor)
        : base("##RegionsOfXIVCompassLabel")
    {
        this.config = config;
        this.anchor = anchor;

        Flags = ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoSavedSettings;

        RespectCloseHotkey = false;
        AllowPinning = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;

        IsOpen = true;
    }

    public void Dispose() { }

    public void Set(string? value) => this.text = value;

    public override bool DrawConditions() =>
        this.config.ShowCompassLabel
        && this.anchor.IsAvailable
        && !string.IsNullOrWhiteSpace(this.text);

    public override void PreDraw()
    {
        var rect = this.anchor.Rect;
        Position = new Vector2(rect.X, rect.Y);
        Size = new Vector2(rect.Z, 24f);
        PositionCondition = ImGuiCond.Always;
        SizeCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        // TODO(Phase 7): optional gradient backing (AddRectFilledMultiColor),
        // fade out on minimap mouse-over, hide while the full map is open.
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetWindowPos();
        var width = ImGui.GetWindowSize().X;

        TextPainter.DrawStrokedCentered(
            drawList,
            pos.X + (width / 2f),
            pos.Y + 4f,
            this.text!,
            0xFFFFFFFF,
            0xCC000000);
    }
}
