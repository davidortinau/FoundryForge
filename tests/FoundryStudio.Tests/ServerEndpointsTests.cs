using FoundryStudio.Core.Server;
using Xunit;

namespace FoundryStudio.Tests;

public class ServerEndpointsTests
{
    [Fact]
    public void Single_url_yields_exact_base_and_copy_payload()
    {
        var urls = new[] { "http://127.0.0.1:5273/" };
        Assert.Equal("http://127.0.0.1:5273", ServerEndpoints.BaseUrl(urls));
        Assert.Equal("http://127.0.0.1:5273", ServerEndpoints.CopyPayload(urls));
    }

    [Fact]
    public void Multiple_urls_all_presented()
    {
        var urls = new[] { "http://127.0.0.1:5273", "http://localhost:5273" };
        var all = ServerEndpoints.AllBaseUrls(urls);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Empty_urls_have_no_live_endpoint()
    {
        var empty = System.Array.Empty<string>();
        Assert.Null(ServerEndpoints.BaseUrl(empty));
        Assert.Null(ServerEndpoints.CopyPayload(empty));
        Assert.Empty(ServerEndpoints.AllBaseUrls(empty));
    }

    [Fact]
    public void Documented_routes_include_chat_and_models()
    {
        var paths = ServerEndpoints.DocumentedRoutes.Select(r => r.Path).ToList();
        Assert.Contains("/v1/chat/completions", paths);
        Assert.Contains("/v1/models", paths);
    }

    [Fact]
    public void Route_url_has_no_double_slash()
    {
        var route = new ServerRoute("/v1/models", "x");
        Assert.Equal("http://127.0.0.1:5273/v1/models", ServerEndpoints.RouteUrl("http://127.0.0.1:5273/", route));
        Assert.Equal("http://127.0.0.1:5273/v1/models", ServerEndpoints.RouteUrl("http://127.0.0.1:5273", route));
    }
}
