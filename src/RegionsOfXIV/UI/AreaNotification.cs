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
internal sealed class AreaNotification
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(200);

    private readonly Easing fadeIn;
    private readonly Easing reveal;
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
        this.reveal = new InOutCubic(revealDuration);
        this.fadeOut = new InCubic(fadeOutDuration);

        this.phaseStartedAt = DateTime.UtcNow;
        this.fadeIn.Start();
    }

    public string? Header { get; }

    public string Text { get; }

    public NotificationPhase Phase { get; private set; } = NotificationPhase.FadeIn;

    public float Opacity { get; private set; }

    // 0..1, drives the decode/wipe effect.
    public float RevealProgress { get; private set; }

    // Vertical offset in px, applied when a newer notification pushes this down.
    public float StackOffset { get; set; }

    public bool IsDone => Phase == NotificationPhase.Done;

    public void Update()
    {
        var elapsed = DateTime.UtcNow - this.phaseStartedAt;

        switch (Phase)
        {
            case NotificationPhase.FadeIn:
                this.fadeIn.Update();
                Opacity = (float)this.fadeIn.ValueClamped;
                if (this.fadeIn.IsDone)
                {
                    Opacity = 1f;
                    Advance(NotificationPhase.Hold);
                }

                break;

            case NotificationPhase.Hold:
                Opacity = 1f;
                if (elapsed >= HoldDuration)
                {
                    this.reveal.Start();
                    Advance(NotificationPhase.Reveal);
                }

                break;

            case NotificationPhase.Reveal:
                this.reveal.Update();
                RevealProgress = (float)this.reveal.ValueClamped;
                if (this.reveal.IsDone)
                {
                    RevealProgress = 1f;
                    Advance(NotificationPhase.Show);
                }

                break;

            case NotificationPhase.Show:
                if (elapsed >= this.showDuration)
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
