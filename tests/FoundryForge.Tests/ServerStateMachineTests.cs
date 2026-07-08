using FoundryForge.Core.Server;
using Xunit;

namespace FoundryForge.Tests;

public class ServerStateMachineTests
{
    [Theory]
    [InlineData(ServerState.Stopped, ServerState.Starting)]
    [InlineData(ServerState.Starting, ServerState.Running)]
    [InlineData(ServerState.Running, ServerState.Stopping)]
    [InlineData(ServerState.Stopping, ServerState.Stopped)]
    [InlineData(ServerState.Error, ServerState.Starting)]
    [InlineData(ServerState.Error, ServerState.Stopped)]
    [InlineData(ServerState.Running, ServerState.Error)]
    [InlineData(ServerState.Starting, ServerState.Error)]
    public void Legal_edges_allowed(ServerState from, ServerState to)
        => Assert.True(ServerStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(ServerState.Stopped, ServerState.Running)]
    [InlineData(ServerState.Running, ServerState.Starting)]
    [InlineData(ServerState.Stopped, ServerState.Stopping)]
    [InlineData(ServerState.Running, ServerState.Stopped)]
    public void Illegal_edges_rejected(ServerState from, ServerState to)
        => Assert.False(ServerStateMachine.CanTransition(from, to));

    [Fact]
    public void Pilot_light_lit_only_when_running()
    {
        Assert.True(ServerStateMachine.PilotLightLit(ServerState.Running));
        Assert.False(ServerStateMachine.PilotLightLit(ServerState.Stopped));
        Assert.False(ServerStateMachine.PilotLightLit(ServerState.Starting));
        Assert.False(ServerStateMachine.PilotLightLit(ServerState.Stopping));
        Assert.False(ServerStateMachine.PilotLightLit(ServerState.Error));
    }
}
