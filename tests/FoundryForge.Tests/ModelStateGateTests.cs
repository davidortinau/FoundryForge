using FoundryForge.Core.Abstractions;
using FoundryForge.Core.Concurrency;
using Xunit;

namespace FoundryForge.Tests;

/// <summary>
/// US2 (FR-008/009/010, SC-004): the load/unload concurrency gate. Pure logic — runs with NO native
/// Foundry Local dylib (SC-008). Verifies drain, reject, mutation serialization, and per-model isolation.
/// </summary>
public class ModelStateGateTests
{
    private const string ModelA = "qwen2.5-0.5b";
    private const string ModelB = "phi-3.5-mini";

    [Fact]
    public async Task Reject_throws_ModelBusyException_while_a_generation_is_active()
    {
        var gate = new ModelStateGate();
        await using var lease = await gate.BeginGenerationAsync(ModelA);

        await Assert.ThrowsAsync<ModelBusyException>(() =>
            gate.MutateAsync(ModelA, MutationPolicy.Reject, () => Task.CompletedTask));
    }

    [Fact]
    public async Task Reject_succeeds_when_no_generation_is_active()
    {
        var gate = new ModelStateGate();
        var mutated = false;

        await gate.MutateAsync(ModelA, MutationPolicy.Reject, () =>
        {
            mutated = true;
            return Task.CompletedTask;
        });

        Assert.True(mutated);
    }

    [Fact]
    public async Task Drain_waits_for_the_in_flight_generation_to_finish_before_mutating()
    {
        var gate = new ModelStateGate();
        var lease = await gate.BeginGenerationAsync(ModelA);

        var mutationRan = false;
        var mutation = gate.MutateAsync(ModelA, MutationPolicy.Drain, () =>
        {
            mutationRan = true;
            return Task.CompletedTask;
        });

        // The mutation must NOT proceed while the generation lease is held.
        var finishedEarly = await Task.WhenAny(mutation, Task.Delay(150)) == mutation;
        Assert.False(finishedEarly, "Drain mutation ran before the in-flight generation was released.");
        Assert.False(mutationRan);

        // Release the generation → drain completes → mutation proceeds.
        await lease.DisposeAsync();
        await mutation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(mutationRan);
    }

    [Fact]
    public async Task Concurrent_mutations_on_one_model_never_overlap()
    {
        var gate = new ModelStateGate();
        var concurrent = 0;
        var maxObserved = 0;
        var sync = new object();

        async Task Mutation()
        {
            lock (sync)
            {
                concurrent++;
                maxObserved = Math.Max(maxObserved, concurrent);
            }

            await Task.Delay(25);

            lock (sync)
            {
                concurrent--;
            }
        }

        var mutations = Enumerable.Range(0, 8)
            .Select(_ => gate.MutateAsync(ModelA, MutationPolicy.Drain, Mutation))
            .ToArray();

        await Task.WhenAll(mutations);

        Assert.Equal(0, concurrent);
        Assert.Equal(1, maxObserved); // serialized: never two mutations at once
    }

    [Fact]
    public async Task Mutating_model_B_is_not_blocked_by_an_active_generation_on_model_A()
    {
        var gate = new ModelStateGate();
        await using var leaseOnA = await gate.BeginGenerationAsync(ModelA);

        var mutatedB = false;
        await gate.MutateAsync(ModelB, MutationPolicy.Reject, () =>
        {
            mutatedB = true;
            return Task.CompletedTask;
        }).WaitAsync(TimeSpan.FromSeconds(5)); // must not hang on A's lease

        Assert.True(mutatedB);
    }

    [Fact]
    public async Task Drain_waits_for_all_concurrent_generations()
    {
        var gate = new ModelStateGate();
        var l1 = await gate.BeginGenerationAsync(ModelA);
        var l2 = await gate.BeginGenerationAsync(ModelA);

        var mutationRan = false;
        var mutation = gate.MutateAsync(ModelA, MutationPolicy.Drain, () =>
        {
            mutationRan = true;
            return Task.CompletedTask;
        });

        await l1.DisposeAsync();
        Assert.False(mutationRan); // one generation still active

        await l2.DisposeAsync();
        await mutation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(mutationRan);
    }
}
