using RegionsOfXIV;

namespace RegionsOfXIV.Tests;

public class HeaderRuleTests
{
    [Fact]
    public void TurningTheHeaderOffDropsItEvenWhenThereIsOneToShow()
    {
        var config = new Configuration { IncludeParentTierAsHeader = false };

        Assert.Null(config.HeaderFor("Middle La Noscea", "Summerford Farms"));
    }

    [Fact]
    public void LeavingTheHeaderOnKeepsIt()
    {
        var config = new Configuration { IncludeParentTierAsHeader = true };

        Assert.Equal("Middle La Noscea", config.HeaderFor("Middle La Noscea", "Summerford Farms"));
    }

    [Fact]
    public void AHeaderThatRepeatsTheNameIsDropped()
    {
        var config = new Configuration();

        Assert.Null(config.HeaderFor("Summerford Farms", "Summerford Farms"));
        Assert.Null(config.HeaderFor("SUMMERFORD FARMS", "summerford farms"));
    }

    [Fact]
    public void NoParentMeansNoHeader()
    {
        Assert.Null(new Configuration().HeaderFor(null, "Summerford Farms"));
    }

    [Fact]
    public void ThePreviewAndTheLiveAnnouncementAskTheSameQuestion()
    {
        var off = new Configuration { IncludeParentTierAsHeader = false };
        var on = new Configuration { IncludeParentTierAsHeader = true };

        foreach (var (parent, text) in new (string?, string)[]
                 {
                     ("Middle La Noscea", "Summerford Farms"),
                     ("Summerford Farms", "Summerford Farms"),
                     (null, "Summerford Farms"),
                 })
        {
            Assert.Null(off.HeaderFor(parent, text));
            Assert.Equal(on.HeaderFor(parent, text), on.HeaderFor(parent, text));
        }
    }
}
