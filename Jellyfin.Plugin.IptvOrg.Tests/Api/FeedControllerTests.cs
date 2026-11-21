using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvOrg.Api;
using Jellyfin.Plugin.IptvOrg.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    public async Task Playlist_ReturnsFileOnlyWhenPresent(bool exists)
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
            var controller = new IptvOrgFeedController(service.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            controller.Request.Scheme = "http";
            controller.Request.Host = new HostString("10.244.100.13", 8096);
            var result = await controller.GetPlaylist(CancellationToken.None);

            if (exists)
            {
                var file = Assert.IsType<ContentResult>(result);
                Assert.Equal("application/x-mpegURL; charset=utf-8", file.ContentType);
                Assert.Contains("url-tvg=\"http://10.244.100.13:8096/IptvOrg/Epg\"", file.Content, StringComparison.Ordinal);
                Assert.Contains("x-tvg-url=\"http://10.244.100.13:8096/IptvOrg/Epg\"", file.Content, StringComparison.Ordinal);
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
