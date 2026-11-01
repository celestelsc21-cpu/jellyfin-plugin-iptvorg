using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// Socket connect callback that refuses non-public addresses. Guide URLs come from a
/// third-party list, so a host name that resolves to a LAN address (or a redirect to one)
/// must not reach the server's local network (SSRF / DNS rebinding protection).
/// </summary>
internal static class PublicOnlyConnector
{
    /// <summary>
    /// Connects to the first public address of the requested host.
    /// </summary>
    /// <param name="context">Connection context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connected network stream.</returns>
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var endPoint = context.DnsEndPoint;
        var addresses = IPAddress.TryParse(endPoint.Host, out var literal)
            ? new[] { literal }
            : await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken).ConfigureAwait(false);

        SocketException? lastError = null;
        foreach (var address in addresses)
        {
            if (GuideSourcePlanner.IsPrivateAddress(address))
            {
                continue;
            }

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endPoint.Port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                lastError = ex;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException(
            lastError is null
                ? "The guide host has no public address."
                : "Could not connect to the guide host.",
            lastError);
    }
}
