// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        bool result = TestAssetFixtureBase.TryReadCacheCount([line], label, out int count);

        Assert.IsTrue(result);
        Assert.AreEqual(expectedCount, count);
    }

    [TestMethod]
    [DataRow("unrelated output")]
    [DataRow("Cache Hit Count: invalid")]
    [DataRow("Cache Hit Count:")]
    public void TryReadCacheCount_MissingOrMalformedStatistic_ReturnsFalse(string line)
    {
        bool result = TestAssetFixtureBase.TryReadCacheCount([line], "Cache Hit Count:", out int count);

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
        bool result = TestAssetFixtureBase.TryReadSavedProjectSeconds([line], out double savedSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(expectedSeconds, savedSeconds);
    }

    [TestMethod]
    [DataRow("unrelated output")]
    [DataRow("(saved invalid project-seconds)")]
    [DataRow("(saved 2 project-days)")]
    [DataRow("(saved 0 project-days)")]
    [DataRow("(saved -1 project-seconds)")]
    public void TryReadSavedProjectSeconds_MissingOrMalformedStatistic_ReturnsFalse(string line)
    {
        bool result = TestAssetFixtureBase.TryReadSavedProjectSeconds([line], out double savedSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, savedSeconds);
    }
}
