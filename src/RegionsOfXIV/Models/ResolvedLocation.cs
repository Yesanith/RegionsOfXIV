namespace RegionsOfXIV.Models;

// A LocationSnapshot's row ids turned into display strings. Kept separate from the
// snapshot so a language change needs no re-read of game memory: the ids are
// stable, only their rendering is not.
//
// Any tier may be null. The game does not name every tier in every zone, and a
// null here means "not named" rather than "lookup failed".
public readonly record struct ResolvedLocation(
    string? Region,
    string? Zone,
    string? Place,
    string? Area,
    string? SubArea);
