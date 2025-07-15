// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Helpers;
using Microsoft.Testing.Platform.Helpers;

using Polyfills;

namespace Microsoft.Testing.Framework;

internal sealed class TestFixtureManager : ITestFixtureManager
{
    private readonly Dictionary<FixtureId, Dictionary<Type, AsyncLazy<object>>> _fixtureInstancesByFixtureId = [];
    private readonly Dictionary<TestNode, FixtureId[]> _fixtureIdsUsedByTestNode = [];

    // We could improve this by doing some optimistic lock but we expect a rather low contention on this.
    // We use a dictionary as performance improvement because we know that when the registration is complete
    // we will only read the collection (so no need for concurrency handling).
    private readonly Dictionary<FixtureId, CountHolder> _fixtureUses = [];
    private readonly CancellationToken _cancellationToken;
    private bool _isRegistrationFrozen;
    private bool _isUsageRegistrationFrozen;

    public TestFixtureManager(CancellationToken cancellationToken)
        => _cancellationToken = cancellationToken;

    public void RegisterFixture<TFixture>(string fixtureId, Func<TFixture> factory)
        where TFixture : notnull
        => TryRegisterFixture(fixtureId, () => Task.FromResult(factory()),
            () => throw new InvalidOperationException($"Fixture with ID '{fixtureId}' is already registering a fixture of type {typeof(TFixture)}"));

    public void RegisterFixture<TFixture>(string fixtureId, Func<Task<TFixture>> asyncFactory)
        where TFixture : notnull
        => TryRegisterFixture(fixtureId, asyncFactory,
            () => throw new InvalidOperationException($"Fixture with ID '{fixtureId}' is already registering a fixture of type {typeof(TFixture)}"));

    public void TryRegisterFixture<TFixture>(string fixtureId, Func<TFixture> factory)
        where TFixture : notnull
        => TryRegisterFixture(fixtureId, () => Task.FromResult(factory()), () => { });

    public void TryRegisterFixture<TFixture>(string fixtureId, Func<Task<TFixture>> asyncFactory)
        where TFixture : notnull
        => TryRegisterFixture(fixtureId, asyncFactory, () => { });

    public async Task<TFixture> GetFixtureAsync<TFixture>(string fixtureId)
        where TFixture : notnull
    {
        Guard.NotNullOrWhiteSpace(fixtureId);
        if (!_fixtureInstancesByFixtureId.TryGetValue(fixtureId, out Dictionary<Type, AsyncLazy<object>>? fixtureInstancesPerType))
        {
            throw new InvalidOperationException($"Fixture with ID '{fixtureId}' is not registered");
        }

        if (!fixtureInstancesPerType.TryGetValue(typeof(TFixture), out AsyncLazy<object>? fixture))
        {
            throw new InvalidOperationException($"Fixture with ID '{fixtureId}' is not registered for type {typeof(TFixture)}");
        }

        if (!fixture.IsValueCreated || !fixture.Value.IsCompleted)
        {
            await fixture.Value.ConfigureAwait(false);
        }

        // We can safely cast here because we know that the fixture is of type TFixture and is awaited.
#pragma warning disable VSTHRD103 // Call async methods when in an async method
        return (TFixture)fixture.Value.Result;
#pragma warning restore VSTHRD103 // Call async methods when in an async method
    }

    internal void RegisterFixtureUsage(TestNode testNode, string[] fixtureIds)
    {
        if (_isUsageRegistrationFrozen)
        {
            throw new InvalidOperationException("Cannot register fixture usage after registration is frozen");
        }

        if (fixtureIds.Length == 0)
        {
            return;
        }

        _fixtureIdsUsedByTestNode.Add(testNode, [.. fixtureIds.Select(x => new FixtureId(x))]);
        foreach (string fixtureId in fixtureIds)
        {
            if (!_fixtureUses.TryGetValue(fixtureId, out CountHolder? uses))
            {
                uses = new();
                _fixtureUses.Add(fixtureId, uses);
            }

            uses.Value++;
        }
    }

    internal void FreezeRegistration() => _isRegistrationFrozen = true;

    internal void FreezeUsageRegistration() => _isUsageRegistrationFrozen = true;

    internal async Task SetupUsedFixturesAsync(TestNode testNode)
    {
        if (!_fixtureIdsUsedByTestNode.TryGetValue(testNode, out FixtureId[]? fixtureIds))
        {
            return;
        }

        foreach (FixtureId fixtureId in fixtureIds)
        {
            if (!_fixtureInstancesByFixtureId.TryGetValue(fixtureId, out Dictionary<Type, AsyncLazy<object>>? fixtureInstancesPerType))
            {
                throw new InvalidOperationException($"Fixture with ID '{fixtureId}' is not registered");
            }

            foreach (AsyncLazy<object> lazyFixture in fixtureInstancesPerType.Values)
            {
                if (!lazyFixture.IsValueCreated || !lazyFixture.Value.IsCompleted)
                {
                    await lazyFixture.Value.ConfigureAwait(false);
                }
            }
        }
    }

    internal async Task CleanUnusedFixturesAsync(TestNode testNode)
    {
        if (!_fixtureIdsUsedByTestNode.TryGetValue(testNode, out FixtureId[]? fixtureIds))
        {
            return;
        }

        foreach (FixtureId fixtureId in fixtureIds)
        {
            CountHolder uses = _fixtureUses[fixtureId];
            int usesCount = uses.Value;
            lock (uses)
            {
                uses.Value--;
                usesCount = uses.Value;
            }

            // It's important to use the captured value and to not check `uses.Value` again because
            // another thread could have decremented the value in the meantime. We would then end up
            // cleaning the fixture multiple times.
            if (usesCount == 0)
            {
                await CleanupAndDisposeFixtureAsync(fixtureId).ConfigureAwait(false);
            }
        }
    }

    private void TryRegisterFixture<TFixture>(string fixtureId, Func<Task<TFixture>> asyncFactory, Action onTypeExist)
        where TFixture : notnull
    {
        Guard.NotNullOrWhiteSpace(fixtureId);

        if (_isRegistrationFrozen)
        {
            throw new InvalidOperationException("Cannot register fixture usage after registration is frozen");
        }

        if (_fixtureInstancesByFixtureId.TryGetValue(fixtureId, out Dictionary<Type, AsyncLazy<object>>? fixtureInstancesPerType))
        {
            if (fixtureInstancesPerType.ContainsKey(typeof(TFixture)))
            {
                onTypeExist();
            }
            else
            {
                fixtureInstancesPerType.Add(
                    typeof(TFixture),
                    new(async () => await CreateAndInitializeFixtureAsync(asyncFactory, _cancellationToken).ConfigureAwait(false), LazyThreadSafetyMode.ExecutionAndPublication));
            }
        }
        else
        {
            _fixtureInstancesByFixtureId.Add(
                fixtureId,
                new()
                {
                    [typeof(TFixture)] = new(
                        async () => await CreateAndInitializeFixtureAsync(asyncFactory, _cancellationToken).ConfigureAwait(false),
                        LazyThreadSafetyMode.ExecutionAndPublication),
                });
        }

        static async Task<TFixture> CreateAndInitializeFixtureAsync(Func<Task<TFixture>> asyncFactory, CancellationToken cancellationToken)
        {
            TFixture fixture = await asyncFactory().ConfigureAwait(false);
            return fixture;
        }
    }

    private async Task CleanupAndDisposeFixtureAsync(FixtureId fixtureId)
    {
        if (!_fixtureInstancesByFixtureId.TryGetValue(fixtureId, out Dictionary<Type, AsyncLazy<object>>? fixtureInstancesPerType))
        {
            throw new InvalidOperationException($"Fixture with ID '{fixtureId}' is not registered");
        }

        foreach (AsyncLazy<object> lazyFixture in fixtureInstancesPerType.Values)
        {
            if (!lazyFixture.IsValueCreated || !lazyFixture.Value.IsCompleted)
            {
                throw new InvalidOperationException($"Fixture with ID '{fixtureId}' is not created");
            }

#pragma warning disable VSTHRD103 // Call async methods when in an async method
            object fixture = lazyFixture.Value.Result;
#pragma warning restore VSTHRD103 // Call async methods when in an async method

            await DisposeHelper.DisposeAsync(fixture).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Integers are value types and we need a reference type to be able to lock on it.
    /// </summary>
    private sealed class CountHolder
    {
#pragma warning disable SA1401 // Fields should be private
        public int Value;
#pragma warning restore SA1401 // Fields should be private
    }

    private sealed record FixtureId(string Value)
    {
        public static implicit operator FixtureId(string fixtureId)
            => new(fixtureId);
    }
}
