using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Debug-only tooling, removed from compilation in every configuration but Debug by the ItemGroup
// in RegionsOfXIV.csproj, so none of this reaches a release build.
//
// Two jobs. It fires banner notifications without the game, because most banners need specific
// content to trigger, several are seasonal, and the interruption path -- one banner arriving while
// another is still on screen -- is close to unreachable on purpose.
//
// And it is how BannerNames.English gets filled in. ScreenImage hands over far more banner ids
// than anything has a name for, so the way to name one is to fire it, read the artwork, and paste
// the line back.
internal sealed class BannerPreviewWindow : Window, IDisposable
{
    private readonly INotificationSink sink;
    private readonly NotificationGate gate;

    private string header = string.Empty;
    private string body = "Quest Accepted";

    // Sequence and list state, all transient. None of it belongs in Configuration: it is how the
    // window is being driven right now, not something a player has chosen.
    private uint[] queue = [];
    private int next;
    private float gapSeconds = 0.4f;
    private float waited;

    private bool unnamedOnly = true;
    private uint[] rows = [];
    private bool rowsAreUnnamedOnly = true;

    public BannerPreviewWindow(INotificationSink sink, NotificationGate gate)
        : base("Banner preview###RegionsOfXIVBannerPreview")
    {
        this.sink = sink;
        this.gate = gate;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() => Stop();

    // A sequence left running against a closed window would keep firing with nothing on screen to
    // explain why.
    public override void OnClose() => Stop();

    private bool Running => this.next < this.queue.Length;

    public override void Draw()
    {
        DrawGateVerdict();

        ImGui.Separator();

        DrawFreeText();

        ImGui.Separator();

        DrawSequenceControls();
        AdvanceSequence();

        ImGui.Separator();

        DrawBannerList();
    }

    // The preview deliberately ignores this verdict -- see Fire. Showing it anyway turns the window
    // into the only live view of the gate there is.
    private void DrawGateVerdict()
    {
        var reason = this.gate.BannerBlockReason();

        if (reason == BannerBlock.None)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.45f, 0.85f, 0.45f, 1f)))
                ImGui.TextUnformatted("Gate: a real banner would be shown.");

            return;
        }

        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.78f, 0.35f, 1f)))
            ImGui.TextWrapped($"Gate: a real banner would be held back -- {Explain(reason)}.");

        ImGui.TextDisabled("Previews below fire anyway.");
    }

    private static string Explain(BannerBlock reason) => reason switch
    {
        BannerBlock.Disabled => "banners are switched off in the settings",
        BannerBlock.LoggedOut => "not logged in",
        BannerBlock.Cutscene => "a cutscene is playing",
        BannerBlock.Pvp => "this is PvP",
        BannerBlock.GPose => "gpose is open",
        BannerBlock.Cooldown => "something else was announced too recently",
        _ => reason.ToString(),
    };

    private void DrawFreeText()
    {
        ImGui.TextDisabled("Anything you like -- long strings, empty headers, non-Latin text.");

        ImGui.InputTextWithHint("##banner-header", "header (optional)", ref this.header, 128);
        ImGui.InputTextWithHint("##banner-body", "body", ref this.body, 256);

        using (ImRaii.Disabled(string.IsNullOrWhiteSpace(this.header) && string.IsNullOrWhiteSpace(this.body)))
        {
            if (ImGui.Button("Fire text"))
            {
                // Straight to the sink rather than through BannerNotification, because the point
                // of this box is to push text the banner path would never produce.
                this.sink.Push(
                    string.IsNullOrWhiteSpace(this.header) ? null : this.header,
                    this.body);
            }
        }
    }

    private void DrawSequenceControls()
    {
        if (Running)
        {
            if (ImGui.Button("Stop"))
                Stop();

            ImGui.SameLine();
            ImGui.TextUnformatted($"{this.next} / {this.queue.Length}");
        }
        else if (ImGui.Button("Play all"))
        {
            // Whatever the filter is currently showing, so "unnamed only" plus play-all walks the
            // transcription backlog in order.
            this.queue = [.. Rows()];
            this.next = 0;
            this.waited = float.MaxValue;
        }

        ImGui.SetNextItemWidth(200f);
        ImGui.SliderFloat("Gap", ref this.gapSeconds, 0f, 3f, "%.2f s");
        Tooltip(
            "Zero fires them back to back, which is the interesting case: it is the only\n" +
            "practical way to reach the crossfade and the interruption path.");
    }

    // Driven from the draw rather than a timer, because sleeping or spawning a thread here would
    // stall the frame the game is drawing.
    private void AdvanceSequence()
    {
        if (!Running)
            return;

        this.waited += ImGui.GetIO().DeltaTime;

        if (this.waited < this.gapSeconds)
            return;

        this.waited = 0f;
        Fire(this.queue[this.next]);
        this.next++;
    }

    // Sorting a thousand-odd ids every frame would be silly, and the set behind them never changes
    // once the sheets have been read.
    private uint[] Rows()
    {
        if (this.rows.Length > 0 && this.rowsAreUnnamedOnly == this.unnamedOnly)
            return this.rows;

        IEnumerable<uint> ids = BannerNameResolver.KnownIds;

        if (this.unnamedOnly)
            ids = ids.Where(id => BannerNameResolver.Resolve(id) is null);

        this.rows = [.. ids.Order()];
        this.rowsAreUnnamedOnly = this.unnamedOnly;

        return this.rows;
    }

    private void DrawBannerList()
    {
        var known = BannerNameResolver.KnownIds.Count;
        var named = BannerNameResolver.All.Count;

        ImGui.TextDisabled($"{known} banner ids, {named} named, {known - named} still to transcribe.");

        if (ImGui.Checkbox("Unnamed only", ref this.unnamedOnly))
            this.rows = [];

        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        Tooltip(
            "Fire an unnamed one, read the wording off the artwork, then click the row to copy\n" +
            "a BannerNames.English line and paste the name in.");

        var listed = Rows();

        using var list = ImRaii.Child("##banner-list", new Vector2(0, 0), border: true);
        if (!list)
            return;

        // A clipper because the unfiltered list runs past a thousand rows, and a debug window that
        // costs frames is a debug window you stop opening.
        var clipper = ImGui.ImGuiListClipper();
        clipper.Begin(listed.Length);

        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                DrawRow(listed[i]);
        }

        clipper.End();
        clipper.Destroy();
    }

    private void DrawRow(uint iconId)
    {
        var name = BannerNameResolver.Resolve(iconId);

        if (ImGui.Button($"Fire##{iconId}"))
            Fire(iconId);

        ImGui.SameLine();

        // The whole label is the copy target, so transcribing is type-the-name rather than
        // type-the-syntax. The indent is included: it pastes straight into the table.
        if (ImGui.Selectable($"{iconId}  {name ?? "--"}##row{iconId}"))
            ImGui.SetClipboardText($"        [{iconId}] = \"{name}\",");

        Tooltip("Click to copy a BannerNames.English line for this id.");
    }

    // Bypasses the gate on purpose. Previewing is most wanted exactly where the gate would refuse
    // -- mid-duty, in combat, in a cutscene -- and a preview that silently does nothing reads as a
    // rendering bug rather than as a suppression rule doing its job.
    //
    // Everything after that is the live path: same shaping, same sink, same renderer.
    private void Fire(uint iconId)
    {
        // An unnamed id still has to draw something, or the one case this window exists for is the
        // one case it does nothing. The id doubles as the label to look for on screen, next to
        // whichever piece of artwork the game has just put up.
        var text = BannerNameResolver.Resolve(iconId) ?? $"Banner {iconId}";

        BannerNotification.PushTo(this.sink, text);
    }

    private void Stop()
    {
        this.queue = [];
        this.next = 0;
        this.waited = 0f;
    }

    private static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }
}
