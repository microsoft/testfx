// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers;

public sealed class DictionaryHelperMutationTests : TestContainer
{
    private const string SourceName = "left-side";
    private const string OverwriteName = "right-side";

    private const string BothEmptyMessage = "DictionaryHelper.ConcatWithOverwrites: Both {0} and {1} dictionaries are null or empty, returning empty dictionary.";
    private const string EmptyMessage = "DictionaryHelper.ConcatWithOverwrites: The {0} is null or empty, returning the {1} dictionary.";
    private const string MergingMessage = "DictionaryHelper.ConcatWithOverwrites: The {0} has {1} keys. And {2} has {3} keys. Merging them.";
    private const string TakingMessage = "DictionaryHelper.ConcatWithOverwrites: Taking all keys from {0}: {1}.";
    private const string OverwritingMessage = "DictionaryHelper.ConcatWithOverwrites: The {0} already contains key {1}. Overwriting it with value from {2}.";
    private const string AddingMessage = "DictionaryHelper.ConcatWithOverwrites: The {0} does not contain key {1}. Adding it from {2}.";
    private const string DoneMessage = "DictionaryHelper.ConcatWithOverwrites: Merging done: Resulting dictionary has keys {0}, overwrites {1}.";

    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public DictionaryHelperMutationTests()
    {
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void ConcatWithOverwrites_LogsNothingWhenInfoTracingIsDisabled()
    {
        IDictionary<string, string>? none = null;
        var source = new Dictionary<string, string> { ["k1"] = "s1", ["k2"] = "s2" };
        var overwrite = new Dictionary<string, string> { ["k2"] = "o2", ["k3"] = "o3" };

        none.ConcatWithOverwrites(none, SourceName, OverwriteName);
        source.ConcatWithOverwrites(none, SourceName, OverwriteName);
        none.ConcatWithOverwrites(overwrite, SourceName, OverwriteName);
        source.ConcatWithOverwrites(overwrite, SourceName, OverwriteName);

        _provider.MockTraceLogger.Verify(logger => logger.Info(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never, "tracing is disabled for every branch");
    }

    public void ConcatWithOverwrites_LogsTheEmptyResultWhenBothSidesAreEmpty()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        IDictionary<string, string>? none = null;

        IDictionary<string, string> result = none.ConcatWithOverwrites(new Dictionary<string, string>(), SourceName, OverwriteName);

        result.Should().BeEmpty(because: "nothing is merged");
        _provider.MockTraceLogger.Verify(logger => logger.Info(BothEmptyMessage, SourceName, OverwriteName), Times.Once);
        VerifyInfoCallCount(1);
    }

    public void ConcatWithOverwrites_LogsTheSourceCopyWhenOverwriteIsEmpty()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        var source = new Dictionary<string, string> { ["k1"] = "s1" };

        IDictionary<string, string> result = source.ConcatWithOverwrites(new Dictionary<string, string>(), SourceName, OverwriteName);

        result.Should().BeEquivalentTo(source, because: "the source is returned as a copy");
        _provider.MockTraceLogger.Verify(logger => logger.Info(EmptyMessage, OverwriteName, SourceName), Times.Once);
        VerifyInfoCallCount(1);
    }

    public void ConcatWithOverwrites_LogsTheOverwriteCopyWhenSourceIsEmpty()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        var overwrite = new Dictionary<string, string> { ["k1"] = "o1" };
        IDictionary<string, string>? none = null;

        IDictionary<string, string> result = none.ConcatWithOverwrites(overwrite, SourceName, OverwriteName);

        result.Should().BeEquivalentTo(overwrite, because: "the overwrite is returned as a copy");
        _provider.MockTraceLogger.Verify(logger => logger.Info(EmptyMessage, SourceName, OverwriteName), Times.Once);
        VerifyInfoCallCount(1);
    }

    public void ConcatWithOverwrites_LogsEveryMergeStepWithTheKeysThatWereOverwrittenAndAdded()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        var source = new Dictionary<string, string> { ["k1"] = "s1", ["k2"] = "s2", ["k3"] = "s3", ["k4"] = "s4" };
        var overwrite = new Dictionary<string, string> { ["k3"] = "o3", ["k4"] = "o4", ["k5"] = "o5" };

        IDictionary<string, string> result = source.ConcatWithOverwrites(overwrite, SourceName, OverwriteName);

        result.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["k1"] = "s1", ["k2"] = "s2", ["k3"] = "o3", ["k4"] = "o4", ["k5"] = "o5" },
            because: "the overwrite wins on the keys both sides have");
        _provider.MockTraceLogger.Verify(logger => logger.Info(MergingMessage, SourceName, 4, OverwriteName, 3), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info(TakingMessage, SourceName, "k1, k2, k3, k4"), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info(OverwritingMessage, SourceName, "k3", OverwriteName), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info(OverwritingMessage, SourceName, "k4", OverwriteName), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info(AddingMessage, SourceName, "k5", OverwriteName), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info(DoneMessage, "k1, k2, k3, k4, k5", "k3, k4"), Times.Once);
        VerifyInfoCallCount(6);
    }

    private void VerifyInfoCallCount(int expected)
        => _provider.MockTraceLogger.Verify(logger => logger.Info(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Exactly(expected), "every branch logs once per step");
}
