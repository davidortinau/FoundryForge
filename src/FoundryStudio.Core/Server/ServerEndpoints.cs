namespace FoundryStudio.Core.Server;

/// <summary>One documented OpenAI-compatible route the exposed server serves.</summary>
public sealed record ServerRoute(string Path, string Description);

/// <summary>
/// Derives copy-friendly endpoints and the documented route list from the server's REAL bound addresses
/// (US1, US2). Never fabricates an address — when <c>urls</c> is empty there is no live endpoint, so
/// <see cref="BaseUrl"/>/<see cref="CopyPayload"/> return null (FR-008/009). Pure, dylib-free.
/// </summary>
public static class ServerEndpoints
{
    /// <summary>The documented OpenAI-compatible surface (labeled "documented", not runtime-verified — FR-011, R3).</summary>
    public static IReadOnlyList<ServerRoute> DocumentedRoutes { get; } = new[]
    {
        new ServerRoute("/v1/chat/completions", "Chat completions (streaming and non-streaming)"),
        new ServerRoute("/v1/models", "List available models"),
        new ServerRoute("/v1/embeddings", "Text embeddings")
    };

    /// <summary>The primary real base URL (trailing slash trimmed), or null when there is no live endpoint.</summary>
    public static string? BaseUrl(IReadOnlyList<string> urls)
    {
        if (urls is null || urls.Count == 0)
        {
            return null;
        }

        var first = urls.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        return first is null ? null : Normalize(first);
    }

    /// <summary>Every real base URL (e.g. when FL binds multiple), normalized.</summary>
    public static IReadOnlyList<string> AllBaseUrls(IReadOnlyList<string> urls)
    {
        if (urls is null || urls.Count == 0)
        {
            return Array.Empty<string>();
        }

        return urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(Normalize)
            .ToList();
    }

    /// <summary>The exact base URL to copy, or null when there is no live endpoint.</summary>
    public static string? CopyPayload(IReadOnlyList<string> urls) => BaseUrl(urls);

    /// <summary>Concatenate a base URL and a route path without a double slash.</summary>
    public static string RouteUrl(string baseUrl, ServerRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var b = Normalize(baseUrl);
        var path = route.Path.StartsWith('/') ? route.Path : "/" + route.Path;
        return b + path;
    }

    private static string Normalize(string url) => url.TrimEnd('/');
}
