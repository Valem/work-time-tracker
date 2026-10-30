using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using Wtt.Application.Ports;
using Wtt.Domain.Planning;
using static Wtt.PublicHolidays.OpenHolidays;

namespace Wtt.PublicHolidays;

/// <summary>
/// Asks OpenHolidays for a canton's public holidays of a year: one HTTPS GET with <c>Accept: application/json</c> and no
/// other header, cookie or credential, direct or through the system proxy. The certificate is checked by the runtime's
/// default — chain, name and expiry — without a revocation fetch. No redirect is followed, nothing is decompressed, and
/// at most <see cref="OpenHolidays.MaxAnswerBytes"/> are taken within <see cref="OpenHolidays.AnswerWithin"/>.
/// </summary>
public sealed class OpenHolidaysSource(TimeProvider clock) : IPublicHolidaySource
{
    private const string Json = "application/json";

    public HolidaySource Source => OpenHolidays.Source;

    /// <summary>The request exactly as it is sent, one line for the request and one per header.</summary>
    public static string Describe(Canton canton, int year)
    {
        using var request = Request(canton, year);
        return string.Join(
            Environment.NewLine,
            [$"{request.Method} {request.RequestUri!.AbsoluteUri}", .. request.Headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}")]);
    }

    public async Task<PublicHolidayYear> ReadAsync(Canton canton, int year, CancellationToken cancel)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(AnswerWithin);
        using var client = new HttpClient(Handler(), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = Request(canton, year);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            RequireTakeable(response);
            var answer = await ReadAtMostAsync(response.Content, deadline.Token);
            return OpenHolidaysAnswer.Translate(answer, canton, year, clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new PublicHolidayReadException(LoadFailure.Unreachable, $"No complete answer within {AnswerWithin.TotalSeconds:0} s.");
        }
        catch (HttpRequestException failure) when (AsksForProxySignIn(failure))
        {
            throw new PublicHolidayReadException(LoadFailure.ProxySignIn, "The proxy asks for a sign-in; no credentials are sent.", failure);
        }
        catch (HttpRequestException failure)
        {
            throw new PublicHolidayReadException(LoadFailure.Unreachable, $"The source could not be reached: {failure.Message}", failure);
        }
        catch (IOException failure)
        {
            throw new PublicHolidayReadException(LoadFailure.Unreachable, "The connection broke off during the answer.", failure);
        }
    }

    private static HttpRequestMessage Request(Canton canton, int year)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, RequestUri(canton, year))
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(Json));
        return request;
    }

    /// <summary>No credentials for the server or a proxy, no cookies, no tracing headers, no redirects, no decompression.</summary>
    private static SocketsHttpHandler Handler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false,
        UseProxy = true,
        Credentials = null,
        DefaultProxyCredentials = null,
        PreAuthenticate = false,
        ActivityHeadersPropagator = DistributedContextPropagator.CreateNoOutputPropagator(),
        ConnectTimeout = AnswerWithin,
        SslOptions = { CertificateRevocationCheckMode = X509RevocationMode.NoCheck },
    };

    private static void RequireTakeable(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw status switch
            {
                (int)HttpStatusCode.ProxyAuthenticationRequired =>
                    new PublicHolidayReadException(LoadFailure.ProxySignIn, "The proxy asks for a sign-in; no credentials are sent."),
                >= 300 and < 400 => Refused(Invariant($"The source answered with a redirect (status {status}), which is not followed.")),
                408 or 429 or >= 500 => new PublicHolidayReadException(LoadFailure.Unreachable, Invariant($"The source answered status {status}.")),
                _ => Refused(Invariant($"The source answered status {status}.")),
            };
        }

        var content = response.Content.Headers;
        if (content.ContentEncoding.Count > 0)
        {
            throw Refused("The answer is compressed; it is not decompressed.");
        }

        if (!string.Equals(content.ContentType?.MediaType, Json, StringComparison.OrdinalIgnoreCase))
        {
            throw Refused("The answer is not declared as JSON.");
        }

        if (content.ContentLength > MaxAnswerBytes)
        {
            throw Refused($"The answer is larger than {MaxAnswerBytes / 1024} KiB.");
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadAtMostAsync(HttpContent content, CancellationToken cancel)
    {
        await using var stream = await content.ReadAsStreamAsync(cancel);
        var buffer = new byte[MaxAnswerBytes + 1];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), cancel)) > 0)
        {
            length += read;
        }

        return length > MaxAnswerBytes
            ? throw Refused($"The answer is larger than {MaxAnswerBytes / 1024} KiB.")
            : buffer.AsMemory(0, length);
    }

    /// <summary>
    /// A proxy refusing the tunnel with 407 surfaces as a tunnel error whose status the runtime names only in its message.
    /// </summary>
    private static bool AsksForProxySignIn(HttpRequestException failure) =>
        failure.StatusCode == HttpStatusCode.ProxyAuthenticationRequired
        || (failure.HttpRequestError == HttpRequestError.ProxyTunnelError && failure.Message.Contains("407", StringComparison.Ordinal));

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
