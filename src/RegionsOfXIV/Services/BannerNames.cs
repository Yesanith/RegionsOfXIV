using System;
using System.Collections.Generic;

namespace RegionsOfXIV.Services;

// The wording on a banner is painted into its artwork and exists nowhere in the game's data as
// text, so these were read off the screen by hand. English only, because that is the client they
// were read on.
//
// Which ids exist is not the hard part -- ScreenImage lists them, and BannerNameResolver takes
// them from there. Naming them is, and the way to do it is "/regions preview" in a Debug build:
// fire an unnamed id, read the artwork, click the row to copy the line, paste it in below.
//
// A banner missing from here keeps the game's own version rather than being replaced, so an
// incomplete list degrades quietly.
internal static class BannerNames
{
    public static readonly IReadOnlyDictionary<uint, string> English = new Dictionary<uint, string>
    {
        [120001] = "Quest Accepted",
        [120002] = "Quest Complete",
        [120011] = "Quest Accepted",
        [120012] = "Quest Complete",
        [120021] = "Duty Commenced",
        [120022] = "Duty Complete",
        [120023] = "Duty Failed",
        [120024] = "Forward!",
        [120025] = "Act Complete",
        [120026] = "Next Act!",
        [120031] = "Levequest Accepted",
        [120032] = "Levequest Complete",
        [120055] = "Delivery Complete",

        [120061] = "Free Company Formed",
        [120064] = "Company Rank Up!",
        [120068] = "Allegiance Changed",
        [120069] = "Land Acquired!",
        [120070] = "Estate Hall Complete!",
        [120071] = "Private Chambers Acquired!",
        [120072] = "Apartment Acquired!",
        [120073] = "Relocation Complete!",
        [120130] = "Company Workshop Acquired",
        [120131] = "Materials Contributed",
        [120132] = "Progress Made",
        [120133] = "Excellent Progress Made",

        [120081] = "FATE Joined",
        [120082] = "FATE Complete",
        [120083] = "FATE Failed",
        [120084] = "FATE Joined (Bonus)",
        [120085] = "FATE Complete (Bonus)",
        [120086] = "FATE Failed (Bonus)",

        [120091] = "Difficulty Rank Unlocked",
        [120092] = "Reputation Up!",
        [120093] = "Treasure Obtained!",
        [120094] = "Treasure Found!",
        [120095] = "Venture Commenced!",
        [120096] = "Venture Accomplished!",
        [120097] = "Trials of the Braves Complete",
        [120098] = "All Vistas Recorded",
        [120105] = "PvP Rank Up!",
        [120108] = "Rank Up!",
        [120109] = "Level Up!",
        [120111] = "Level Up!",
        [120114] = "Light Party",
        [120115] = "Full Party",
        [120116] = "Rank Up!",
        [120117] = "Rank Up!",
        [120118] = "Level Up!",
        [120119] = "Level Down...",

        [120101] = "Fight!",
        [120102] = "Claws Win!",
        [120103] = "Fangs Win!",
        [120104] = "Draw!",
        [120106] = "Victory!",
        [120107] = "Defeat!",
        [120121] = "Engage!",
        [120122] = "The Maelstrom Wins!",
        [120123] = "The Order of the Twin Adder Wins!",
        [120124] = "The Immortal Flames Win!",
        [120125] = "Draw!",
        [120126] = "Sudden Death",
        [120127] = "Culling Time",
    };

    // The same banners in Turkish. Unlike English this was not read off a screen: there is no
    // Turkish FFXIV client, so nothing here transcribes anything and every string is a choice.
    // A player picking this is asking for the game's own artwork to be replaced by wording the
    // plugin invented, which is what the Announcements tab warns about.
    //
    // Two decisions worth knowing about. Quest and Duty both want "görev", and two banners
    // reading alike would be worse than an imperfect word, so Duty is "etkinlik" here and in
    // the tr locale. Grand Company and questline names are left in English because they are
    // proper nouns the game never translates for anyone.
    public static readonly IReadOnlyDictionary<uint, string> Turkish = new Dictionary<uint, string>
    {
        [120001] = "Görev Kabul Edildi",
        [120002] = "Görev Tamamlandı",
        [120011] = "Görev Kabul Edildi",
        [120012] = "Görev Tamamlandı",
        [120021] = "Etkinlik Başladı",
        [120022] = "Etkinlik Tamamlandı",
        [120023] = "Etkinlik Başarısız",
        [120024] = "İleri!",
        [120025] = "Bölüm Tamamlandı",
        [120026] = "Sıradaki Bölüm!",
        [120031] = "Levequest Kabul Edildi",
        [120032] = "Levequest Tamamlandı",
        [120055] = "Teslimat Tamamlandı",

        [120061] = "Free Company Kuruldu",
        [120064] = "Şirket Rütbesi Yükseldi!",
        [120068] = "Bağlılık Değişti",
        [120069] = "Arsa Alındı!",
        [120070] = "Konak Tamamlandı!",
        [120071] = "Özel Oda Alındı!",
        [120072] = "Daire Alındı!",
        [120073] = "Taşınma Tamamlandı!",
        [120130] = "Şirket Atölyesi Alındı",
        [120131] = "Malzemeler Bağışlandı",
        [120132] = "İlerleme Kaydedildi",
        [120133] = "Mükemmel İlerleme Kaydedildi",

        [120081] = "FATE'e Katılındı",
        [120082] = "FATE Tamamlandı",
        [120083] = "FATE Başarısız",
        [120084] = "FATE'e Katılındı (Bonus)",
        [120085] = "FATE Tamamlandı (Bonus)",
        [120086] = "FATE Başarısız (Bonus)",

        [120091] = "Zorluk Derecesi Açıldı",
        [120092] = "İtibar Yükseldi!",
        [120093] = "Hazine Elde Edildi!",
        [120094] = "Hazine Bulundu!",
        [120095] = "Sefer Başladı!",
        [120096] = "Sefer Tamamlandı!",
        [120097] = "Trials of the Braves Tamamlandı",
        [120098] = "Tüm Manzaralar Kaydedildi",
        [120105] = "PvP Rütbesi Yükseldi!",
        [120108] = "Rütbe Yükseldi!",
        [120109] = "Seviye Atladı!",
        [120111] = "Seviye Atladı!",
        [120114] = "Küçük Takım",
        [120115] = "Tam Takım",
        [120116] = "Rütbe Yükseldi!",
        [120117] = "Rütbe Yükseldi!",
        [120118] = "Seviye Atladı!",
        [120119] = "Seviye Düştü...",

        [120101] = "Dövüş!",
        [120102] = "Pençeler Kazandı!",
        [120103] = "Dişler Kazandı!",
        [120104] = "Berabere!",
        [120106] = "Zafer!",
        [120107] = "Yenilgi!",
        [120121] = "Saldır!",
        [120122] = "The Maelstrom Kazandı!",
        [120123] = "The Order of the Twin Adder Kazandı!",
        [120124] = "The Immortal Flames Kazandı!",
        [120125] = "Berabere!",
        [120126] = "Ani Ölüm",
        [120127] = "Eleme Zamanı",
    };

    // Every language the plugin has banner wording for, by the two-letter code the client reports.
    // A second language is a dictionary above and one line here, and nothing else: keeping the
    // tables separate rather than folding them into one id to (en, de, fr, ja) map means adding
    // German does not touch the ninety entries English already has.
    //
    // The Announcements tab offers exactly these, so a language cannot be picked that has no words
    // behind it.
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> ByLanguage =
        new Dictionary<string, IReadOnlyDictionary<uint, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = English,
            ["tr"] = Turkish,
        };
}
