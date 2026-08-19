namespace RegionsOfXIV.Models;

public readonly record struct ResolvedLocation(
    string? Region,
    string? Zone,
    string? Place,
    string? Area,
    string? SubArea);
