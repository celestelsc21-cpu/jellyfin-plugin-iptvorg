using System;
using System.IO;
using System.Reflection;
using Jellyfin.Plugin.IptvOrg.Api;
using Jellyfin.Plugin.IptvOrg.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Api;

public class FeedControllerTests
{
    [Fact]
    public void Feeds_AllowAnonymousClients()
    {
        Assert.NotNull(typeof(IptvOrgFeedController).GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Playlist_ReturnsFileOnlyWhenPresent(bool exists)
    {
        var path = Path.Combine(Path.GetTempPath(), $"iptvorg-{Guid.NewGuid():N}.m3u");
        if (exists)
        {
            System.IO.File.WriteAllText(path, "#EXTM3U\n");
        }

        try
        {
            var service = new Mock<IIptvOrgService>();
            service.SetupGet(x => x.PlaylistPath).Returns(path);
            var result = new IptvOrgFeedController(service.Object).GetPlaylist();

            if (exists)
            {
                var file = Assert.IsType<PhysicalFileResult>(result);
                Assert.Equal(path, file.FileName);
                Assert.Equal("application/x-mpegURL", file.ContentType);
            }
            else
            {
                Assert.IsType<NotFoundResult>(result);
            }
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Epg_ReturnsFileOnlyWhenPresent(bool exists)
    {
        var path = Path.Combine(Path.GetTempPath(), $"iptvorg-{Guid.NewGuid():N}.xml");
        if (exists)
        {
            System.IO.File.WriteAllText(path, "<tv />");
        }

        try
        {
            var service = new Mock<IIptvOrgService>();
            service.SetupGet(x => x.GuidePath).Returns(path);
            var result = new IptvOrgFeedController(service.Object).GetEpg();

            if (exists)
            {
                var file = Assert.IsType<PhysicalFileResult>(result);
                Assert.Equal(path, file.FileName);
                Assert.Equal("application/xml", file.ContentType);
            }
            else
            {
                Assert.IsType<NotFoundResult>(result);
            }
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
