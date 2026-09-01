// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestAssetFixtureBaseTests
{
    [TestMethod]
    [DataRow("Cache Hit Count:", "  Cache Hit Count: 12 cache hits", 12)]
    [DataRow("Cache Miss Count:", "Cache Miss Count: 3 cache misses", 3)]
    [DataRow("Cache Hit Count:", "Cache Hit Count: 0 cache hits", 0)]
    public void TryReadCacheCount_ValidStatistic_ReturnsCount(string label, string line, int expectedCount)
    {
        bool result = TryReadCacheCount([line], label, out int count);

        Assert.IsTrue(result);
        Assert.AreEqual(expectedCount, count);
    }

    [TestMethod]
    [DataRow("unrelated output")]
    [DataRow("Cache Hit Count: invalid")]
    [DataRow("Cache Hit Count:")]
    [DataRow("Cache Hit Count: -1")]
    public void TryReadCacheCount_MissingOrMalformedStatistic_ReturnsFalse(string line)
    {
        bool result = TryReadCacheCount([line], "Cache Hit Count:", out int count);

        Assert.IsFalse(result);
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    [DataRow("(saved 1.5 project-seconds)", 1.5)]
    [DataRow("(saved 2 project-minutes)", 120)]
    [DataRow("(saved 0.5 project-hours)", 1800)]
    [DataRow("(saved 0 project-hours)", 0)]
    public void TryReadSavedProjectSeconds_ValidStatistic_ConvertsToSeconds(string line, double expectedSeconds)
    {
        bool result = TryReadSavedProjectSeconds([line], out double savedSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(expectedSeconds, savedSeconds);
    }

    [TestMethod]
    [DataRow("unrelated output")]
    [DataRow("(saved invalid project-seconds)")]
    [DataRow("(saved 2 project-days)")]
    [DataRow("(saved 0 project-days)")]
    [DataRow("(saved -1 project-seconds)")]
    [DataRow("(saved 1E308 project-hours)")]
    public void TryReadSavedProjectSeconds_MissingOrMalformedStatistic_ReturnsFalse(string line)
    {
        bool result = TryReadSavedProjectSeconds([line], out double savedSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, savedSeconds);
    }

    [TestMethod]
    [DataRow(3, true, true)]
    [DataRow(3, false, false)]
    [DataRow(0, true, true)]
    [DataRow(0, false, true)]
    public void TryReadCacheStatistics_SavedTimeCompleteness_IsRequiredOnlyForHits(
        int hitCount,
        bool includeSavedTime,
        bool expectedResult)
    {
        string savedTime = includeSavedTime ? " (saved 1.5 project-seconds)" : string.Empty;
        string[] outputLines =
        [
            $"Cache Hit Count: {hitCount}{savedTime}",
            "Cache Miss Count: 2",
        ];

        bool result = TryReadCacheStatistics(outputLines, out int parsedHitCount, out int missCount, out double savedSeconds);

        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(hitCount, parsedHitCount);
        Assert.AreEqual(2, missCount);
        Assert.AreEqual(includeSavedTime ? 1.5 : 0, savedSeconds);
    }

    [TestMethod]
    public void TryReadCacheStatistics_ZeroHitsWithMalformedSavedTime_ReturnsFalse()
    {
        string[] outputLines =
        [
            "Cache Hit Count: 0 (saved invalid project-seconds)",
            "Cache Miss Count: 2",
        ];

        bool result = TryReadCacheStatistics(outputLines, out int hitCount, out int missCount, out double savedSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hitCount);
        Assert.AreEqual(2, missCount);
        Assert.AreEqual(0, savedSeconds);
    }

    [TestMethod]
    public async Task AcceptanceCacheCircuitBreaker_SuccessfulProbeEnablesWaitingAttempts()
    {
        using TempDirectory cacheRoot = new();
        using CircuitBreakerProxy firstCircuitBreaker = new(cacheRoot.Path);
        using CircuitBreakerProxy secondCircuitBreaker = new(cacheRoot.Path);
        using CacheLeaseProxy probe = await firstCircuitBreaker.EnterAsync();
        Task<CacheLeaseProxy> waitingAttempt = secondCircuitBreaker.EnterAsync();

        Assert.IsTrue(probe.ShouldUseCache);
        Assert.IsFalse(waitingAttempt.IsCompleted);

        probe.Complete(succeeded: true);

        using CacheLeaseProxy attempt = await waitingAttempt;
        Assert.IsTrue(attempt.ShouldUseCache);
        attempt.Complete(succeeded: true);
    }

    [TestMethod]
    public async Task AcceptanceCacheCircuitBreaker_FailedProbeDisablesWaitingAndFutureAttempts()
    {
        using TempDirectory cacheRoot = new();
        using CircuitBreakerProxy firstCircuitBreaker = new(cacheRoot.Path);
        using CircuitBreakerProxy secondCircuitBreaker = new(cacheRoot.Path);
        using CacheLeaseProxy probe = await firstCircuitBreaker.EnterAsync();
        Task<CacheLeaseProxy> waitingAttempt = secondCircuitBreaker.EnterAsync();

        probe.Complete(succeeded: false);

        using CacheLeaseProxy attempt = await waitingAttempt;
        using CacheLeaseProxy laterAttempt = await firstCircuitBreaker.EnterAsync();
        Assert.IsFalse(attempt.ShouldUseCache);
        Assert.IsFalse(laterAttempt.ShouldUseCache);
    }

    [TestMethod]
    public async Task AcceptanceCacheCircuitBreaker_FailureAfterProbeDisablesFutureAttempts()
    {
        using TempDirectory cacheRoot = new();
        using CircuitBreakerProxy firstCircuitBreaker = new(cacheRoot.Path);
        using CircuitBreakerProxy secondCircuitBreaker = new(cacheRoot.Path);
        using CacheLeaseProxy probe = await firstCircuitBreaker.EnterAsync();
        probe.Complete(succeeded: true);
        using CacheLeaseProxy attempt = await secondCircuitBreaker.EnterAsync();

        attempt.Complete(succeeded: false);

        using CacheLeaseProxy laterAttempt = await firstCircuitBreaker.EnterAsync();
        Assert.IsFalse(laterAttempt.ShouldUseCache);
    }

    [TestMethod]
    public async Task AcceptanceCacheCircuitBreaker_AbandonedProbeDisablesWaitingAttempts()
    {
        using TempDirectory cacheRoot = new();
        using CircuitBreakerProxy firstCircuitBreaker = new(cacheRoot.Path);
        using CircuitBreakerProxy secondCircuitBreaker = new(cacheRoot.Path);
        CacheLeaseProxy probe = await firstCircuitBreaker.EnterAsync();
        Task<CacheLeaseProxy> waitingAttempt = secondCircuitBreaker.EnterAsync();

        probe.Dispose();

        using CacheLeaseProxy attempt = await waitingAttempt;
        Assert.IsFalse(attempt.ShouldUseCache);
    }

    private static bool TryReadCacheStatistics(
        IReadOnlyList<string> outputLines,
        out int hitCount,
        out int missCount,
        out double savedSeconds)
    {
        object?[] arguments = [outputLines, 0, 0, 0d];
        bool result = InvokeParser<bool>(nameof(TryReadCacheStatistics), arguments);
        hitCount = (int)arguments[1]!;
        missCount = (int)arguments[2]!;
        savedSeconds = (double)arguments[3]!;
        return result;
    }

    private static bool TryReadCacheCount(IReadOnlyList<string> outputLines, string label, out int count)
    {
        object?[] arguments = [outputLines, label, 0];
        bool result = InvokeParser<bool>(nameof(TryReadCacheCount), arguments);
        count = (int)arguments[2]!;
        return result;
    }

    private static bool TryReadSavedProjectSeconds(IReadOnlyList<string> outputLines, out double savedSeconds)
    {
        object?[] arguments = [outputLines, 0d];
        bool result = InvokeParser<bool>(nameof(TryReadSavedProjectSeconds), arguments);
        savedSeconds = (double)arguments[1]!;
        return result;
    }

    private static T InvokeParser<T>(string methodName, object?[] arguments)
    {
        MethodInfo method = typeof(TestAssetFixtureBase).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Could not find {nameof(TestAssetFixtureBase)}.{methodName}.");
        return (T)method.Invoke(null, arguments)!;
    }

    private sealed class CircuitBreakerProxy : IDisposable
    {
        private static readonly Type CircuitBreakerType = typeof(TestAssetFixtureBase).Assembly.GetType(
            "Microsoft.Testing.TestInfrastructure.AcceptanceCacheCircuitBreaker",
            throwOnError: true)!;

        private static readonly MethodInfo EnterAsyncMethod = CircuitBreakerType.GetMethod("EnterAsync")!;

        private readonly object _instance;

        public CircuitBreakerProxy(string cacheRoot)
            => _instance = Activator.CreateInstance(CircuitBreakerType, cacheRoot)!;

        public async Task<CacheLeaseProxy> EnterAsync()
        {
            object resultTask = EnterAsyncMethod.Invoke(_instance, [CancellationToken.None])!;
            await (Task)resultTask;
            object lease = resultTask.GetType().GetProperty("Result")!.GetValue(resultTask)!;
            return new CacheLeaseProxy(lease);
        }

        public void Dispose() => ((IDisposable)_instance).Dispose();
    }

    private sealed class CacheLeaseProxy : IDisposable
    {
        private readonly object _instance;
        private readonly MethodInfo _completeMethod;

        public CacheLeaseProxy(object instance)
        {
            _instance = instance;
            Type leaseType = instance.GetType();
            _completeMethod = leaseType.GetMethod("Complete")!;
            ShouldUseCache = (bool)leaseType.GetProperty("ShouldUseCache")!.GetValue(instance)!;
        }

        public bool ShouldUseCache { get; }

        public void Complete(bool succeeded) => _completeMethod.Invoke(_instance, [succeeded]);

        public void Dispose() => ((IDisposable)_instance).Dispose();
    }
}
