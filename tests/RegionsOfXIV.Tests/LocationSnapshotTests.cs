using RegionsOfXIV.Models;

namespace RegionsOfXIV.Tests;

public class LocationSnapshotTests
{
    private static LocationSnapshot Snapshot(
        uint territory = 100, uint region = 1, uint zone = 2, uint place = 3,
        uint area = 4, uint subArea = 5) =>
        new(territory, region, zone, place, area, subArea);

    [Fact]
    public void IdenticalSnapshotsDifferInNoTier()
    {
        Assert.Equal(LocationTier.None, Snapshot().DiffTier(Snapshot()));
    }

    [Theory]
    [InlineData(LocationTier.Territory)]
    [InlineData(LocationTier.Region)]
    [InlineData(LocationTier.Zone)]
    [InlineData(LocationTier.Place)]
    [InlineData(LocationTier.Area)]
    [InlineData(LocationTier.SubArea)]
    public void ASingleDifferenceIsReportedAtItsOwnTier(LocationTier tier)
    {
        var moved = tier switch
        {
            LocationTier.Territory => Snapshot(territory: 999),
            LocationTier.Region => Snapshot(region: 999),
            LocationTier.Zone => Snapshot(zone: 999),
            LocationTier.Place => Snapshot(place: 999),
            LocationTier.Area => Snapshot(area: 999),
            _ => Snapshot(subArea: 999),
        };

        Assert.Equal(tier, moved.DiffTier(Snapshot()));
    }

    [Fact]
    public void TheCoarsestDifferenceWins()
    {
        var arrived = Snapshot(zone: 999, place: 999, area: 999, subArea: 999);

        Assert.Equal(LocationTier.Zone, arrived.DiffTier(Snapshot()));
    }

    [Fact]
    public void OnlyTheTerritoryIdDecidesEmptiness()
    {
        Assert.True(LocationSnapshot.Empty.IsEmpty);
        Assert.False(new LocationSnapshot(100, 0, 0, 0, 0, 0).IsEmpty);
    }
}
