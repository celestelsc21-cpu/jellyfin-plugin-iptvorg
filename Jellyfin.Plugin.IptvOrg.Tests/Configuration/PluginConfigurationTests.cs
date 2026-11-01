using Jellyfin.Plugin.IptvOrg.Configuration;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Configuration;

public class PluginConfigurationTests
{
    [Fact]
    public void Defaults_AreSafe()
    {
        var config = new PluginConfiguration();

        Assert.False(config.IncludeNsfw);
        Assert.True(config.SkipStreamsWithCustomHeaders);
        Assert.True(config.EnableGuide);
        Assert.Empty(config.Categories);
    }

    [Fact]
    public void Normalize_CleansCodes()
    {
        var config = new PluginConfiguration
        {
            Categories = [" News ", "news", string.Empty, "SPORTS"],
            Languages = ["RUS"],
            Countries = ["ru", "RU"],
            Regions = ["cis"],
            MaxGuideSources = 1000
        };

        config.Normalize();

        Assert.Equal(new[] { "news", "sports" }, config.Categories);
        Assert.Equal(new[] { "rus" }, config.Languages);
        Assert.Equal(new[] { "RU" }, config.Countries);
        Assert.Equal(new[] { "CIS" }, config.Regions);
        Assert.Equal(PluginConfiguration.MaxGuideSourcesLimit, config.MaxGuideSources);
    }

    [Fact]
    public void Normalize_NullArrays_BecomeEmpty()
    {
        var config = new PluginConfiguration { Categories = null!, MaxGuideSources = 0 };

        config.Normalize();

        Assert.Empty(config.Categories);
        Assert.Equal(1, config.MaxGuideSources);
    }
}
