using FoundryStudio.Core.Chat;
using FoundryStudio.Core.Models;
using Xunit;

namespace FoundryStudio.Tests;

/// <summary>
/// US7 / Constitution IV (SC-009): consent gate for destructive chat-history ops. Dylib-free, against a
/// temp dir — proves unconfirmed delete/clear remove NOTHING.
/// </summary>
public class ChatHistoryConsentGateTests : IDisposable
{
    private readonly string _dir;
    private readonly FileChatHistoryStore _store;

    public ChatHistoryConsentGateTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fs-chat-tests-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _store = new FileChatHistoryStore(_dir);
    }

    private async Task<ChatSession> SeedAsync()
    {
        var s = ChatSession.New(DateTimeOffset.UnixEpoch) with
        {
            Id = "conv1",
            Messages = new[] { new ChatMessageRecord(ChatTurnRole.User, "hi", DateTimeOffset.UnixEpoch) }
        };
        await _store.SaveAsync(s);
        return s;
    }

    [Fact]
    public async Task Unconfirmed_delete_removes_nothing()
    {
        await SeedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.DeleteAsync("conv1", userConfirmed: false));
        Assert.NotNull(await _store.GetAsync("conv1"));
    }

    [Fact]
    public async Task Unconfirmed_clear_removes_nothing()
    {
        await SeedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.ClearMessagesAsync("conv1", userConfirmed: false));
        var s = await _store.GetAsync("conv1");
        Assert.NotNull(s);
        Assert.Single(s!.Messages);
    }

    [Fact]
    public async Task Confirmed_delete_removes()
    {
        await SeedAsync();
        await _store.DeleteAsync("conv1", userConfirmed: true);
        Assert.Null(await _store.GetAsync("conv1"));
    }

    [Fact]
    public async Task Confirmed_clear_empties_messages_but_keeps_session()
    {
        await SeedAsync();
        await _store.ClearMessagesAsync("conv1", userConfirmed: true);
        var s = await _store.GetAsync("conv1");
        Assert.NotNull(s);
        Assert.Empty(s!.Messages);
    }

    [Fact]
    public async Task Save_survives_reload()
    {
        await SeedAsync();
        var reloaded = new FileChatHistoryStore(_dir);
        var list = await reloaded.ListAsync();
        Assert.Single(list);
        Assert.Equal("conv1", list[0].Id);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }
}
