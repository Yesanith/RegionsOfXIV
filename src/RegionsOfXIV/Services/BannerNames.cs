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
        [120084] = "FATE Joined",
        [120085] = "FATE Complete",
        [120086] = "FATE Failed",

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
}
