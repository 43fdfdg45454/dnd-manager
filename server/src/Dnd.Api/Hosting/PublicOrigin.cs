namespace Dnd.Api.Hosting;

public static class PublicOrigin
{
    /// <summary>
    /// <c>scheme://host[:port]</c> of the request (after the forwarded headers were applied), normalised
    /// (lower case, default port dropped), or null when it is not a clean http(s) origin.
    /// </summary>
    public static string? FromRequest(HttpRequest request)
    {
        if (!request.Host.HasValue)
        {
            return null;
        }

        return Normalize($"{request.Scheme}://{request.Host.Value}");
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length > 0
            || string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        // Only the origin: no path, query or fragment smuggled through the host value.
        if (uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
