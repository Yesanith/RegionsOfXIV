using FFXIVClientStructs.FFXIV.Client.UI;

namespace RegionsOfXIV.Services;

// Dalamud has no audio service. Rather than bundling audio and taking on NAudio
// plus our own volume/device handling, we borrow the game's own UI sounds.
// Off by default — unexpected audio is the fastest route to a one-star review.
internal sealed class SoundService
{
    private readonly Configuration config;

    public SoundService(Configuration config) => this.config = config;

    public void PlayReveal()
    {
        if (!this.config.RevealSoundEnabled)
            return;
        Play(this.config.RevealSoundEffectId);
    }

    public void PlayVanish()
    {
        if (!this.config.VanishSoundEnabled)
            return;
        Play(this.config.VanishSoundEffectId);
    }

    // Framework thread only: calls into game code.
    private static unsafe void Play(uint effectId)
    {
        if (effectId == 0)
            return;

        UIGlobals.PlaySoundEffect(effectId);
    }
}
