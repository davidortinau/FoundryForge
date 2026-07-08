using FoundryForge.Core.Abstractions;
using FoundryForge.Core.PostV1;
using Xunit;

namespace FoundryForge.Tests;

/// <summary>
/// US4 (FR-013, SC-010): the post-v1 service stubs are HONEST — <c>IsSupported == false</c> and every
/// operation throws <see cref="NotSupportedException"/> rather than returning fake/empty data. FL-free,
/// so they run in the dylib-free test project (SC-008).
/// </summary>
public class PostV1StubTests
{
    [Fact]
    public async Task Embedding_stub_is_unsupported_and_throws()
    {
        IEmbeddingService svc = new StubEmbeddingService();
        Assert.False(svc.IsSupported);
        await Assert.ThrowsAsync<NotSupportedException>(() => svc.EmbedAsync(new[] { "hi" }));
    }

    [Fact]
    public async Task Transcription_stub_is_unsupported_and_throws()
    {
        ITranscriptionService svc = new StubTranscriptionService();
        Assert.False(svc.IsSupported);
        await Assert.ThrowsAsync<NotSupportedException>(() => svc.TranscribeAsync(Stream.Null));
    }

    [Fact]
    public async Task LocalServer_stub_is_unsupported_throws_and_reports_no_urls()
    {
        ILocalServerService svc = new StubLocalServerService();
        Assert.False(svc.IsSupported);
        Assert.Empty(svc.Urls);
        await Assert.ThrowsAsync<NotSupportedException>(() => svc.StartAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => svc.StopAsync());
    }
}
