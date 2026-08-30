namespace RegionsOfXIV.Services;

// What a banner looks like once it reaches the screen, in one place.
//
// The live path and the debug preview both come through here. They differ only in whether the
// gate is consulted first; everything after that has to be identical or the preview is measuring
// something the player never sees.
internal static class BannerNotification
{
    // Cased in the language the wording is written in, which BannerNameResolver knows because it
    // chose the table the wording came from. Not the player's locale: that is a different thing
    // and casing by it would upcase an English banner with Turkish rules for a Turkish player.
    //
    // Invariant is not right either, which is what this used to do. It leaves Turkish "ı" in
    // lower case, because invariant has no upper case for a dotless i at all, and it drops the
    // dot from every "i".
    //
    // The dozen or so names that come from GroupPoseStamp are in the client's language rather
    // than the chosen one, so for those this cases by the wrong rules. That is the same small
    // mixture the stamps already introduce, and it reaches one letter of one banner in practice.
    public static void PushTo(INotificationSink sink, string text) =>
        sink.PushBanner(Format(text));

    // Exposed so the config window's preview can show the same wording the live path produces.
    // Building the sample by hand there would drift from this the moment either changed.
    public static string Format(string text) => text.ToUpper(BannerNameResolver.Casing);
}
