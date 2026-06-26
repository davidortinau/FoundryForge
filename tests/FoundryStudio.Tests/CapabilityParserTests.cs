using FoundryStudio.Core.Catalog;
using Xunit;

namespace FoundryStudio.Tests;

public class CapabilityParserTests
{
    [Fact]
    public void Vision_from_input_modalities_tool_from_flag_reasoning_from_caps()
    {
        var caps = CapabilityParser.Parse(capabilities: "reasoning", inputModalities: "text,image", supportsToolCalling: true);
        Assert.True(caps.Vision);
        Assert.True(caps.ToolCalling);
        Assert.True(caps.ToolCallingKnown);
        Assert.True(caps.Reasoning);
    }

    [Fact]
    public void All_null_yields_no_capabilities_and_tool_calling_unknown()
    {
        var caps = CapabilityParser.Parse(null, null, null);
        Assert.False(caps.Vision);
        Assert.False(caps.ToolCalling);
        Assert.False(caps.Reasoning);
        Assert.False(caps.ToolCallingKnown); // distinguishes "no" from "unknown"
    }

    [Fact]
    public void Tool_calling_false_is_known_false_not_unknown()
    {
        var caps = CapabilityParser.Parse("", "text", supportsToolCalling: false);
        Assert.False(caps.ToolCalling);
        Assert.True(caps.ToolCallingKnown);
        Assert.False(caps.Vision);
    }

    [Fact]
    public void Reasoning_is_never_inferred_from_text_without_a_declared_token()
    {
        var caps = CapabilityParser.Parse(capabilities: "chat-completion", inputModalities: "text", supportsToolCalling: true);
        Assert.False(caps.Reasoning);
    }
}
