using FoundryStudio.Core.Chat;
using FoundryStudio.Core.Models;
using Xunit;

namespace FoundryStudio.Tests;

public class ChatHistoryDocumentTests
{
    [Fact]
    public void Round_trips_field_for_field()
    {
        var session = new ChatSession(
            Id: "abc123",
            Title: "Why is the sky blue",
            SystemPrompt: "You are concise.",
            Parameters: new InferenceParameters(0.5, 256, 0.9, 0.1),
            ModelAlias: "qwen2.5-0.5b",
            Messages: new[]
            {
                new ChatMessageRecord(ChatTurnRole.User, "why is the sky blue", DateTimeOffset.UnixEpoch),
                new ChatMessageRecord(ChatTurnRole.Assistant, "Rayleigh scattering.", DateTimeOffset.UnixEpoch.AddSeconds(2),
                    new GenerationMetrics(TimeSpan.FromMilliseconds(120), 80.5, 42, StopReason.Natural), StopReason.Natural)
            },
            CreatedAt: DateTimeOffset.UnixEpoch,
            UpdatedAt: DateTimeOffset.UnixEpoch.AddSeconds(2),
            SchemaVersion: 1);

        var json = ChatHistoryDocument.Serialize(session);
        var back = ChatHistoryDocument.Deserialize(json);

        Assert.NotNull(back);
        Assert.Equal(session.Id, back!.Id);
        Assert.Equal(session.Title, back.Title);
        Assert.Equal(session.SystemPrompt, back.SystemPrompt);
        Assert.Equal(session.Parameters, back.Parameters);
        Assert.Equal(session.ModelAlias, back.ModelAlias);
        Assert.Equal(2, back.Messages.Count);
        Assert.Equal("Rayleigh scattering.", back.Messages[1].Content);
        Assert.Equal(StopReason.Natural, back.Messages[1].StopReason);
        Assert.Equal(42, back.Messages[1].Metrics!.TotalTokens);
        Assert.Equal(session.SchemaVersion, back.SchemaVersion);
    }

    [Fact]
    public void Garbage_json_is_recoverable_no_throw()
    {
        Assert.Null(ChatHistoryDocument.Deserialize("{ this is not valid json"));
        Assert.Null(ChatHistoryDocument.Deserialize(""));
        Assert.Null(ChatHistoryDocument.Deserialize("{}")); // no id => treat as corrupt
    }
}
