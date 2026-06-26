using Microsoft.Extensions.AI;
using Xunit;

namespace FoundryStudio.Tests;

/// <summary>
/// SC-010 / FR-029: tool invocation is genuine MEAI middleware (UseFunctionInvocation), not faked UI. A
/// fake inner IChatClient scripts a tool call; the middleware must invoke a REAL AIFunction and feed its
/// result back. Dylib-free.
/// </summary>
public class ToolInvocationWiringTests
{
    [Fact]
    public async Task UseFunctionInvocation_invokes_a_real_tool_and_feeds_result_back()
    {
        var toolInvoked = false;
        var tool = AIFunctionFactory.Create(() =>
        {
            toolInvoked = true;
            return 42;
        }, "get_test_value");

        var inner = new ScriptedChatClient();
        IChatClient client = inner.AsBuilder().UseFunctionInvocation().Build();

        var options = new ChatOptions { Tools = new List<AITool> { tool } };
        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "what is the value") }, options);

        Assert.True(toolInvoked); // the real .NET function ran via middleware
        Assert.True(inner.CallCount >= 2); // first emitted the call, second produced the final answer
        Assert.Contains("42", response.Text);
    }

    /// <summary>First call emits a function call; subsequent calls (after the tool result) emit the answer.</summary>
    private sealed class ScriptedChatClient : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount == 1)
            {
                var call = new FunctionCallContent("call-1", "get_test_value", new Dictionary<string, object?>());
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, new List<AIContent> { call })));
            }

            // After the middleware appended the FunctionResultContent, answer using it.
            var result = messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionResultContent>()
                .FirstOrDefault();
            var value = result?.Result?.ToString() ?? "unknown";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"the value is {value}")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
