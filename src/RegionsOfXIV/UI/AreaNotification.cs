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

// One on-screen notification and its animation state:
//   fade in -> hold -> reveal -> show -> fade out -> done
// The pause before the reveal is what makes it read as deliberate rather than
// twitchy; keep it even if the visuals change.
//
// The motion runs inside the fade-in stage, and the decode is the reveal that
// follows it. They are deliberately consecutive rather than simultaneous: the
// line arrives in Eorzean script, lands, pauses, and only then resolves into
// something readable. Run at once they compete, and each is over before it has
// been seen.
//
// Either can be absent. A notification built with no motion is the plugin as it
// shipped — fade in, hold, decode — and one built with no decode simply ends its
// reveal before it starts. Both are expressed as a zero duration by the caller,
// which knows what the config says and whether the Eorzean font actually loaded.
internal sealed class AreaNotification
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(200);

    private readonly Easing fadeIn;
    private readonly Easing? motion;
    private readonly Easing? reveal;
    private readonly Easing fadeOut;

    private readonly TimeSpan showDuration;

    private DateTime phaseStartedAt;

    // What the fade-out counts down from. One by default, because the ordinary
    // route into FadeOut is the end of Show, where the notification is fully
    // opaque. Dismiss() is the exception — see there.
    private float fadeOutFrom = 1f;

    public AreaNotification(
        string? header,
        string text,
        TimeSpan fadeInDuration,
        TimeSpan motionDuration,
        TimeSpan revealDuration,
        TimeSpan showDuration,
        TimeSpan fadeOutDuration)
    {
        // Promote the header when there is no main text, so we never render an
        // empty notification.
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

        // A stage with no duration is a stage that does not happen, and its
        // progress starts where it would have ended: nothing downstream then has
        // to ask whether it ran.
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

    // Private: the phase is this class's own bookkeeping, and IsDone is the only
    // question anyone outside has ever needed to ask about it.
    private NotificationPhase Phase { get; set; } = NotificationPhase.FadeIn;

    public float Opacity { get; private set; }

    // 0..1, drives the motion: where each glyph is on its way in. Runs first, and
    // is already 1 when there is no motion to run.
    public float MotionProgress { get; private set; }

    // 0..1, drives the decode. Starts only once the motion has finished, so the
    // two never overlap.
    public float RevealProgress { get; private set; }

    // Vertical offset in px, applied when a newer notification pushes this down.
    public float StackOffset { get; set; }

    // While set, the Show phase does not time out. The config window's live
    // preview holds a notification open this way for as long as settings are
    // changing, then unpins it — see NotificationOverlay.TouchPreview. Every other
    // phase runs normally, so a pinned notification still fades in, reveals, and
    // can still be dismissed by a real announcement arriving on top of it.
    public bool IsPinned { get; set; }

    public bool IsDone => Phase == NotificationPhase.Done;

    // Whether the notification is on its way out. The particle field asks, so it
    // can stop spawning while letting what is already in the air finish drifting:
    // fresh hearts appearing under text that is fading reads as a glitch.
    public bool IsFadingOut => Phase == NotificationPhase.FadeOut;

    // The ambient particles playing around this notification. Owned here so they
    // live and die with it — two notifications overlapping during a handover keep
    // their own, and nothing needs cleaning up when one ends.
    public ParticleField Particles { get; } = new();

    public void Update()
    {
        var elapsed = DateTime.UtcNow - this.phaseStartedAt;

        switch (Phase)
        {
            // The fade and the motion run together and the stage ends when both
            // are done, so a motion longer than the fade is not cut short by it.
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
                    // Nothing to decode: the line has already arrived readable,
                    // so it goes straight to being held on screen.
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
                {
                    this.fadeOut.Start();
                    Advance(NotificationPhase.FadeOut);
                }

                break;

            case NotificationPhase.FadeOut:
                this.fadeOut.Update();
                Opacity = this.fadeOutFrom * (1f - (float)this.fadeOut.ValueClamped);
                if (this.fadeOut.IsDone)
                {
                    Opacity = 0f;
                    Advance(NotificationPhase.Done);
                }

                break;
        }
    }

    // Cut short when a newer notification supersedes this one.
    //
    // This can land mid fade-in, at any opacity. Fading out from wherever we got to
    // rather than from full is what keeps that from reading as a flash: two quick
    // announcements — or two clicks of the config window's Preview button — would
    // otherwise brighten the outgoing one on its way off screen.
    public void Dismiss()
    {
        if (Phase is NotificationPhase.FadeOut or NotificationPhase.Done)
            return;

        this.fadeOutFrom = Opacity;
        this.fadeOut.Start();
        Advance(NotificationPhase.FadeOut);
    }

    private void Advance(NotificationPhase next)
    {
        Phase = next;
        this.phaseStartedAt = DateTime.UtcNow;
    }
}
