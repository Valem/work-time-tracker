using System.Net;

namespace Wtt.PublicHolidays;

/// <summary>
/// A system proxy that carries credentials — the user and password of <c>HTTPS_PROXY=http://user:pass@host:port</c>,
/// kept as its own <see cref="IWebProxy.Credentials"/> — with them taken away: the handler answers a 407 with them even
/// when its own proxy credentials are null. This proxy never has credentials, and the proxy address it gives carries no
/// user information either. Which proxy is used, and which addresses bypass it, stays the system's choice.
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
