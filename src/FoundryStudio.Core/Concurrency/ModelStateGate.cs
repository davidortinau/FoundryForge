using System.Collections.Concurrent;
using FoundryStudio.Core.Abstractions;

namespace FoundryStudio.Core.Concurrency;

/// <summary>
/// The single load/unload concurrency gate (Constitution V, FR-008/009/010). One gate backs the one
/// <c>FoundryLocalManager</c> and the future exposed server. Pure logic — no Foundry Local, no UI, no dylib.
///
/// Contract:
/// - A model mutation (load/unload) never proceeds while a generation streams on that model: it either
///   <see cref="MutationPolicy.Drain"/>s (awaits in-flight generations to finish) or
///   <see cref="MutationPolicy.Reject"/>s with a typed <see cref="ModelBusyException"/>.
/// - Concurrent mutations on the same model serialize.
/// - Per-model isolation: mutating model B never blocks generations on model A.
/// </summary>
public sealed class ModelStateGate : IModelStateGate
{
    private readonly ConcurrentDictionary<string, ModelGate> _gates = new(StringComparer.Ordinal);

    public async Task<IAsyncDisposable> BeginGenerationAsync(string modelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelId);
        var gate = _gates.GetOrAdd(modelId, static _ => new ModelGate());

        // Registration is gated on the mutation lock so a generation can never start mid-mutation
        // (e.g. while the model is being unloaded). The generation itself does NOT hold the lock.
        await gate.MutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            gate.Enter();
        }
        finally
        {
            gate.MutationLock.Release();
        }

        return new GenerationLease(gate);
    }

    public async Task MutateAsync(string modelId, MutationPolicy policy, Func<Task> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelId);
        ArgumentNullException.ThrowIfNull(mutation);
        var gate = _gates.GetOrAdd(modelId, static _ => new ModelGate());

        // Holding the mutation lock serializes concurrent mutations AND blocks new generations from
        // registering, so the active count can only fall to zero while we hold it.
        await gate.MutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (gate.ActiveGenerations > 0)
            {
                if (policy == MutationPolicy.Reject)
                {
                    throw new ModelBusyException(modelId);
                }

                // Drain: wait for in-flight generations to complete (no new ones can start).
                await gate.WaitDrainedAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            await mutation().ConfigureAwait(false);
        }
        finally
        {
            gate.MutationLock.Release();
        }
    }

    /// <summary>Per-model coordination primitive: a mutation lock plus a drain-aware generation count.</summary>
    private sealed class ModelGate
    {
        public readonly SemaphoreSlim MutationLock = new(1, 1);

        private readonly object _sync = new();
        private int _active;
        private TaskCompletionSource _drained = CreateCompleted();

        public int ActiveGenerations
        {
            get
            {
                lock (_sync)
                {
                    return _active;
                }
            }
        }

        public void Enter()
        {
            lock (_sync)
            {
                if (_active == 0)
                {
                    _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                _active++;
            }
        }

        public void Exit()
        {
            lock (_sync)
            {
                if (_active == 0)
                {
                    return;
                }

                _active--;
                if (_active == 0)
                {
                    _drained.TrySetResult();
                }
            }
        }

        public Task WaitDrainedAsync()
        {
            lock (_sync)
            {
                return _drained.Task;
            }
        }

        private static TaskCompletionSource CreateCompleted()
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult();
            return tcs;
        }
    }

    private sealed class GenerationLease : IAsyncDisposable
    {
        private readonly ModelGate _gate;
        private int _disposed;

        public GenerationLease(ModelGate gate) => _gate = gate;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _gate.Exit();
            }

            return ValueTask.CompletedTask;
        }
    }
}
