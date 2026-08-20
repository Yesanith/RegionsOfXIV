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

internal sealed class AreaNotification
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(200);

    private readonly Easing fadeIn;
    private readonly Easing? motion;
    private readonly Easing? reveal;
    private readonly Easing fadeOut;

    private readonly TimeSpan showDuration;

    private DateTime phaseStartedAt;

    private float fadeOutFrom = 1f;

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

    public float StackOffset { get; set; }

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

    public void ApplyCasing(bool uppercase)
    {
        if (this.casedAsUpper == uppercase)
            return;

        this.casedAsUpper = uppercase;

        CasedText = uppercase ? Text.ToUpperInvariant() : Text;
        CasedHeader = Header == null
            ? string.Empty
            : uppercase ? Header.ToUpperInvariant() : Header;

        Cipher = null;
    }

    public void Update()
    {
        var elapsed = DateTime.UtcNow - this.phaseStartedAt;

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
