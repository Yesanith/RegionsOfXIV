using System;
using Dalamud.Interface.Animation;
using Dalamud.Interface.Animation.EasingFunctions;

namespace RegionsOfXIV.UI;

internal enum NotificationPhase
{
    FadeIn,
    Hold,
    Reveal,
    Show,
    FadeOut,
    Done,
}

// One notification's life, from fade-in to gone. The phases run in order and each one owns the
// property the renderer reads from it -- Opacity, MotionProgress, RevealProgress -- so the
// renderer never has to work out what stage anything is at.
//
// Pinned notifications sit in Show indefinitely; that is how the config-window preview stays put.
internal sealed class AreaNotification
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(200);

    // Short enough that an interrupted line is faint almost at once, long enough that it still
    // reads as leaving rather than being switched off.
    private static readonly TimeSpan MinInterruptedFadeOut = TimeSpan.FromMilliseconds(120);

    private static readonly TimeSpan StackSlideDuration = TimeSpan.FromMilliseconds(180);

    private readonly Easing fadeIn;
    private readonly Easing? motion;
    private readonly Easing? reveal;
    private readonly Easing fadeOut;
    private readonly Easing interruptedFadeOut;
    private readonly Easing stackSlide = new OutCubic(StackSlideDuration);

    private Easing activeFadeOut;

    private readonly TimeSpan showDuration;

    private DateTime phaseStartedAt;

    private float fadeOutFrom = 1f;

    private float stackFrom;
    private float stackTarget;

    private bool? casedAsUpper;

    public AreaNotification(
        string? header,
        string text,
        TimeSpan fadeInDuration,
        TimeSpan motionDuration,
        TimeSpan revealDuration,
        TimeSpan showDuration,
        TimeSpan fadeOutDuration)
    {
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(header))
        {
            text = header!;
            header = null;
        }

        Header = header;
        Text = text;

        this.showDuration = showDuration;

        this.fadeIn = new OutCubic(fadeInDuration);
        this.fadeOut = new InCubic(fadeOutDuration);

        // OutCubic rather than the InCubic used for a natural fade, and that is the point: InCubic
        // holds near full opacity before dropping, which is exactly when the incoming line is being
        // drawn over the top of this one. OutCubic sheds most of the opacity in the first few
        // frames, so the two are only legible together very briefly.
        this.interruptedFadeOut = new OutCubic(InterruptedFadeOutFor(fadeOutDuration));
        this.activeFadeOut = this.fadeOut;

        if (motionDuration > TimeSpan.Zero)
            this.motion = new OutCubic(motionDuration);
        else
            MotionProgress = 1f;

        if (revealDuration > TimeSpan.Zero)
            this.reveal = new InOutCubic(revealDuration);
        else
            RevealProgress = 1f;

        this.phaseStartedAt = DateTime.UtcNow;
        this.fadeIn.Start();
        this.motion?.Start();
    }

    public string? Header { get; }

    public string Text { get; }

    public uint IconId { get; init; }

    private NotificationPhase Phase { get; set; } = NotificationPhase.FadeIn;

    public float Opacity { get; private set; }

    public float MotionProgress { get; private set; }

    public float RevealProgress { get; private set; }

    // Half the configured fade, floored so it does not simply snap, and never longer than the
    // fade it stands in for -- FadeOutDuration goes down to 0.05s, well under the floor.
    //
    // Derived rather than configured, so there is no new setting to migrate or carry in a preset.
    // If people want control over it, this is the one place it would become a slider.
    internal static TimeSpan InterruptedFadeOutFor(TimeSpan natural)
    {
        var shortened = natural / 2;

        if (shortened < MinInterruptedFadeOut)
            shortened = MinInterruptedFadeOut;

        return shortened > natural ? natural : shortened;
    }

    // Read every frame by the renderer, and eased rather than jumped: the push happens the instant
    // the next arrival lands, and at the distances this now moves a teleport is very visible.
    public float StackOffset =>
        this.stackFrom + ((this.stackTarget - this.stackFrom) * (float)this.stackSlide.ValueClamped);

    public void PushDown(float distance)
    {
        this.stackFrom = StackOffset;
        this.stackTarget += distance;

        this.stackSlide.Restart();
    }

    public bool IsPinned { get; set; }

    public bool IsDone => Phase == NotificationPhase.Done;

    public bool IsFadingOut => Phase == NotificationPhase.FadeOut;

    public ParticleField Particles { get; } = new();

    public string? Cipher { get; set; }

    public LineLayout DisplayLayout { get; } = new();

    public LineLayout CipherLayout { get; } = new();

    public LineLayout HeaderLayout { get; } = new();

    public string CasedText { get; private set; } = string.Empty;

    public string CasedHeader { get; private set; } = string.Empty;

    // Memoised, and the identity of the returned strings matters: LineLayout keys its caches on
    // them by reference, so handing back a fresh string each frame would defeat both caches.
    public void ApplyCasing(bool uppercase)
    {
        if (this.casedAsUpper == uppercase)
            return;

        this.casedAsUpper = uppercase;

        // Invariant, not the user's culture: these are place names out of game data rather than
        // anything the player typed, and a Turkish locale would upcase "i" to a dotted capital for
        // those players alone.
        CasedText = uppercase ? Text.ToUpperInvariant() : Text;
        CasedHeader = Header == null
            ? string.Empty
            : uppercase ? Header.ToUpperInvariant() : Header;

        Cipher = null;
    }

    public void Update()
    {
        var elapsed = DateTime.UtcNow - this.phaseStartedAt;

        this.stackSlide.Update();

        switch (Phase)
        {
            case NotificationPhase.FadeIn:
                this.fadeIn.Update();
                Opacity = (float)this.fadeIn.ValueClamped;

                if (this.motion is { } arriving)
                {
                    arriving.Update();
                    MotionProgress = (float)arriving.ValueClamped;
                }

                if (this.fadeIn.IsDone && this.motion is not { IsDone: false })
                {
                    Opacity = 1f;
                    MotionProgress = 1f;
                    Advance(NotificationPhase.Hold);
                }

                break;

            case NotificationPhase.Hold:
                Opacity = 1f;
                if (elapsed >= HoldDuration)
                {
                    if (this.reveal is { } decoding)
                    {
                        decoding.Start();
                        Advance(NotificationPhase.Reveal);
                    }
                    else
                    {
                        Advance(NotificationPhase.Show);
                    }
                }

                break;

            case NotificationPhase.Reveal:
                this.reveal!.Update();
                RevealProgress = (float)this.reveal.ValueClamped;
                if (this.reveal.IsDone)
                {
                    RevealProgress = 1f;
                    Advance(NotificationPhase.Show);
                }

                break;

            case NotificationPhase.Show:
                if (!IsPinned && elapsed >= this.showDuration)
                    StartFadeOut(this.fadeOut);

                break;

            case NotificationPhase.FadeOut:
                this.activeFadeOut.Update();
                Opacity = this.fadeOutFrom * (1f - (float)this.activeFadeOut.ValueClamped);
                if (this.activeFadeOut.IsDone)
                {
                    Opacity = 0f;
                    Advance(NotificationPhase.Done);
                }

                break;
        }
    }

    // Cut short by the next arrival rather than reaching the end of its life, and deliberately on
    // a shorter fade than a natural one. The configured FadeOutDuration is a reading time -- it is
    // how long a finished notification lingers -- but when it is spent with the next line drawn
    // over the top, both stay legible at once and the result is unreadable. This is the fix for
    // that, so do not collapse it back onto this.fadeOut.
    //
    // Fades from wherever the opacity currently is rather than from full, so a line interrupted
    // halfway through arriving does not flash brighter on its way out.
    public void Dismiss()
    {
        if (Phase is NotificationPhase.FadeOut or NotificationPhase.Done)
            return;

        StartFadeOut(this.interruptedFadeOut);
    }

    private void StartFadeOut(Easing easing)
    {
        this.fadeOutFrom = Opacity;
        this.activeFadeOut = easing;

        easing.Start();
        Advance(NotificationPhase.FadeOut);
    }

    private void Advance(NotificationPhase next)
    {
        Phase = next;
        this.phaseStartedAt = DateTime.UtcNow;
    }
}
