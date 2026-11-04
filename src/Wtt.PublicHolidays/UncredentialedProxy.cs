using System.Net;

namespace Wtt.PublicHolidays;

/// <summary>
/// The system proxy — from <c>HTTPS_PROXY</c> and its siblings, or Windows' proxy settings — with every credential taken
/// away. The runtime's default proxy keeps the user and password of <c>http://user:pass@host:port</c> as its own
/// <see cref="IWebProxy.Credentials"/>, and the handler answers a 407 with them even when its own proxy credentials are
/// null; so this proxy never has credentials, and the proxy address it gives carries no user information either.
/// Which proxy is used, and which addresses bypass it, stays the system's choice.
/// </summary>
internal sealed class UncredentialedProxy(IWebProxy system) : IWebProxy
{
    public ICredentials? Credentials
    {
        get => null;
        set { }
    }

    public Uri? GetProxy(Uri destination) => WithoutUserInfo(system.GetProxy(destination));

    public bool IsBypassed(Uri host) => system.IsBypassed(host);

    private static Uri? WithoutUserInfo(Uri? proxy) =>
        proxy is { IsAbsoluteUri: true, UserInfo.Length: > 0 }
            ? new UriBuilder(proxy) { UserName = string.Empty, Password = string.Empty }.Uri
            : proxy;
}
