using FoundryForge.Core.Chat;
using FoundryForge.Core.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace FoundryForge.Tests;

public class TranscriptAssemblerTests
{
    private static ChatMessageRecord User(string c) => new(ChatTurnRole.User, c, DateTimeOffset.UnixEpoch);
    private static ChatMessageRecord Assistant(string c) => new(ChatTurnRole.Assistant, c, DateTimeOffset.UnixEpoch);

    [Fact]
    public void System_prompt_prepends_then_turns_in_order()
    {
        var session = ChatSession.New(DateTimeOffset.UnixEpoch) with
        {
            SystemPrompt = "You are terse.",
            Messages = new[] { User("hi"), Assistant("hello"), User("bye"), Assistant("goodbye") }
        };

        var msgs = TranscriptAssembler.Assemble(session);

        Assert.Equal(5, msgs.Count);
        Assert.Equal(ChatRole.System, msgs[0].Role);
        Assert.Equal("You are terse.", msgs[0].Text);
        Assert.Equal(ChatRole.User, msgs[1].Role);
        Assert.Equal("hi", msgs[1].Text);
        Assert.Equal(ChatRole.Assistant, msgs[2].Role);
        Assert.Equal("goodbye", msgs[4].Text);
    }

    [Fact]
    public void No_system_prompt_means_no_system_message()
    {
        var session = ChatSession.New(DateTimeOffset.UnixEpoch) with
        {
            SystemPrompt = null,
            Messages = new[] { User("hi") }
        };

        var msgs = TranscriptAssembler.Assemble(session);

        Assert.Single(msgs);
        Assert.Equal(ChatRole.User, msgs[0].Role);
    }

    [Fact]
    public void Tool_turn_preserved()
    {
        var session = ChatSession.New(DateTimeOffset.UnixEpoch) with
        {
            Messages = new[]
            {
                User("what time is it"),
                new ChatMessageRecord(ChatTurnRole.Tool, "12:00", DateTimeOffset.UnixEpoch),
                Assistant("noon")
            }
        };

        var msgs = TranscriptAssembler.Assemble(session);

        Assert.Equal(3, msgs.Count);
        Assert.Equal(ChatRole.Tool, msgs[1].Role);
    }
}
