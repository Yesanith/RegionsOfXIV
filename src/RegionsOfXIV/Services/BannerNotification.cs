namespace RegionsOfXIV.Services;

// What a banner looks like once it reaches the screen, in one place.
//
// The live path and the debug preview both come through here. They differ only in whether the
// gate is consulted first; everything after that has to be identical or the preview is measuring
// something the player never sees.
internal static class BannerNotification
{
    // Invariant casing, not the user's culture: banner names come out of game data, and a
    // Turkish locale would upcase "i" to a dotted capital for those players alone.
    public static void PushTo(INotificationSink sink, string text) =>
        sink.Push(null, text.ToUpperInvariant());
}
