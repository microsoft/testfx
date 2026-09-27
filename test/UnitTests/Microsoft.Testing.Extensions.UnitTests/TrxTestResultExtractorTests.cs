// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class TrxTestResultExtractorTests
{
    private const int MaxCapturedFieldChars = 1 * 1024 * 1024;

    [TestMethod]
    public void Extract_LargeExceptionMessage_TruncatesWithSuffix()
    {
        string huge = new('x', MaxCapturedFieldChars + 100);
        var bag = new PropertyBag(
            new PassedTestNodeStateProperty(),
            new TrxExceptionProperty(huge, null));

        (TrxTestResult result, bool wasTruncated) = TrxTestResultExtractor.Extract(new TestNodeUpdateMessage(
            new SessionUid("1"),
            new TestNode { Uid = "u", DisplayName = "d", Properties = bag }));

        Assert.IsTrue(wasTruncated);
        Assert.IsNotNull(result.ExceptionMessage);
        Assert.IsLessThan(huge.Length, result.ExceptionMessage.Length);
        Assert.Contains("[truncated by TRX streaming store]", result.ExceptionMessage);
    }

    [TestMethod]
    public void Extract_SmallExceptionMessage_NotTruncated()
    {
        var bag = new PropertyBag(
            new PassedTestNodeStateProperty(),
            new TrxExceptionProperty("small", "stack"));

        (TrxTestResult result, bool wasTruncated) = TrxTestResultExtractor.Extract(new TestNodeUpdateMessage(
            new SessionUid("1"),
            new TestNode { Uid = "u", DisplayName = "d", Properties = bag }));

        Assert.IsFalse(wasTruncated);
        Assert.AreEqual("small", result.ExceptionMessage);
        Assert.AreEqual("stack", result.ExceptionStackTrace);
    }

    [TestMethod]
    public void Extract_StateProperties_MapToExpectedOutcomes()
    {
        Assert.AreEqual(TrxTestOutcome.Passed, ExtractResult(new PassedTestNodeStateProperty()).Outcome);
        Assert.AreEqual(TrxTestOutcome.Skipped, ExtractResult(new SkippedTestNodeStateProperty()).Outcome);
        Assert.AreEqual(TrxTestOutcome.Timeout, ExtractResult(new TimeoutTestNodeStateProperty()).Outcome);
        Assert.AreEqual(TrxTestOutcome.Failed, ExtractResult(new FailedTestNodeStateProperty()).Outcome);
        Assert.AreEqual(TrxTestOutcome.Failed, ExtractResult(new ErrorTestNodeStateProperty()).Outcome);
#pragma warning disable CS0618, MTP0001 // CancelledTestNodeStateProperty is obsolete
        Assert.AreEqual(TrxTestOutcome.Failed, ExtractResult(new CancelledTestNodeStateProperty()).Outcome);
#pragma warning restore CS0618, MTP0001
    }

    [TestMethod]
    public void Extract_InProgressState_ThrowsUnreachableException()
    {
        // UnreachableException is an internal, per-assembly polyfill on non-NETCOREAPP TFMs, so asserting on the
        // generic type parameter would compare against this test assembly's copy and fail due to type identity.
        Exception exception = Assert.Throws<Exception>(() => ExtractResult(new InProgressTestNodeStateProperty()));

        Assert.AreEqual(typeof(UnreachableException).FullName, exception.GetType().FullName);
    }

    [TestMethod]
    public void Extract_IdentityAndTimingProperties_ProjectsValues()
    {
        DateTimeOffset startTime = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset endTime = startTime.AddSeconds(12);
        var duration = TimeSpan.FromSeconds(10);

        TrxTestResult result = ExtractResult(
            new PassedTestNodeStateProperty(),
            new TimingProperty(new TimingInfo(startTime, endTime, duration)),
            new TrxFullyQualifiedTypeNameProperty("My.Namespace.MyType"),
            new TestMethodIdentifierProperty("tests.dll", "My.Namespace", "MyType", "MyMethod", 0, [], "System.Void"),
            new TrxTestDefinitionName("My test definition"));

        Assert.AreEqual(startTime, result.StartTime);
        Assert.AreEqual(endTime, result.EndTime);
        Assert.AreEqual(duration, result.Duration);
        Assert.AreEqual("My.Namespace.MyType", result.TrxFullyQualifiedTypeName);
        Assert.AreEqual("My test definition", result.TrxTestDefinitionName);
        Assert.IsNotNull(result.TestMethodIdentifier);
        Assert.AreEqual("My.Namespace", result.TestMethodIdentifier.Namespace);
        Assert.AreEqual("MyType", result.TestMethodIdentifier.TypeName);
        Assert.AreEqual("MyMethod", result.TestMethodIdentifier.MethodName);
    }

    [TestMethod]
    public void Extract_MultipleMetadataAndArtifacts_EnumeratesInReverseInsertionOrder()
    {
        var firstArtifact = new FileInfo("first.txt");
        var secondArtifact = new FileInfo("second.txt");

        TrxTestResult result = ExtractResult(
            new PassedTestNodeStateProperty(),
            new TestMetadataProperty("first-key", "first-value"),
            new TestMetadataProperty("second-key", "second-value"),
            new FileArtifactProperty(firstArtifact, "first"),
            new FileArtifactProperty(secondArtifact, "second"));

        Assert.IsNotNull(result.Metadata);
        Assert.HasCount(2, result.Metadata);
        Assert.AreEqual("second-key", result.Metadata[0].Key);
        Assert.AreEqual("second-value", result.Metadata[0].Value);
        Assert.AreEqual("first-key", result.Metadata[1].Key);
        Assert.AreEqual("first-value", result.Metadata[1].Value);
        Assert.IsNotNull(result.FileArtifacts);
        Assert.HasCount(2, result.FileArtifacts);
        Assert.AreEqual(secondArtifact.FullName, result.FileArtifacts[0].FullPath);
        Assert.AreEqual(firstArtifact.FullName, result.FileArtifacts[1].FullPath);
    }

    [TestMethod]
    public void Extract_WorkItems_CopiesIds()
    {
        string[] workItemIds = ["123", "456"];
        var bag = new PropertyBag(
            new PassedTestNodeStateProperty(),
            new TrxWorkItemsProperty(workItemIds));

        (TrxTestResult result, _) = TrxTestResultExtractor.Extract(new TestNodeUpdateMessage(
            new SessionUid("1"),
            new TestNode { Uid = "u", DisplayName = "d", Properties = bag }));

        Assert.IsNotNull(result.WorkItemIds);
        Assert.AreSequenceEqual(workItemIds, result.WorkItemIds.ToArray());
        Assert.AreNotSame(workItemIds, result.WorkItemIds);
    }

    [TestMethod]
    public void Extract_Categories_CopiesValues()
    {
        string[] categories = ["unit", "fast"];

        TrxTestResult result = ExtractResult(
            new PassedTestNodeStateProperty(),
            new TrxCategoriesProperty(categories));

        Assert.IsNotNull(result.Categories);
        Assert.AreSequenceEqual(categories, result.Categories.ToArray());
        Assert.AreNotSame(categories, result.Categories);
    }

    [TestMethod]
    public void Extract_LargeStandardOutMessage_TruncatesPerMessage()
    {
        string huge = new('y', MaxCapturedFieldChars + 50);
        var bag = new PropertyBag(
            new PassedTestNodeStateProperty(),
            new TrxMessagesProperty([new StandardOutputTrxMessage(huge), new StandardOutputTrxMessage("short")]));

        (TrxTestResult result, bool wasTruncated) = TrxTestResultExtractor.Extract(new TestNodeUpdateMessage(
            new SessionUid("1"),
            new TestNode { Uid = "u", DisplayName = "d", Properties = bag }));

        Assert.IsTrue(wasTruncated);
        Assert.IsNotNull(result.Messages);
        Assert.HasCount(2, result.Messages);
        Assert.IsNotNull(result.Messages[0].Message);
        Assert.IsLessThan(huge.Length, result.Messages[0].Message!.Length);
        Assert.AreEqual("short", result.Messages[1].Message);
    }

    [TestMethod]
    public void Extract_Messages_MapsKinds()
    {
        TrxTestResult result = ExtractResult(
            new PassedTestNodeStateProperty(),
            new TrxMessagesProperty(
            [
                new StandardOutputTrxMessage("standard output"),
                new StandardErrorTrxMessage("standard error"),
                new DebugOrTraceTrxMessage("debug"),
            ]));

        Assert.IsNotNull(result.Messages);
        Assert.HasCount(3, result.Messages);
        Assert.AreEqual(TrxStreamMessageKind.StandardOutput, result.Messages[0].Kind);
        Assert.AreEqual("standard output", result.Messages[0].Message);
        Assert.AreEqual(TrxStreamMessageKind.StandardError, result.Messages[1].Kind);
        Assert.AreEqual("standard error", result.Messages[1].Message);
        Assert.AreEqual(TrxStreamMessageKind.DebugOrTrace, result.Messages[2].Kind);
        Assert.AreEqual("debug", result.Messages[2].Message);
    }

    [TestMethod]
    public void Extract_TruncationLandsOnSurrogateBoundary_DoesNotSplitPair()
    {
        // Build a string where char index (MaxCapturedFieldChars - 1) is a high surrogate. The
        // truncator must back off by one so the resulting string contains no unpaired surrogates.
        const string Surrogate = "\uD83D\uDE00"; // U+1F600 grinning face
        var sb = new StringBuilder(MaxCapturedFieldChars + 2);
        sb.Append('a', MaxCapturedFieldChars - 1);
        sb.Append(Surrogate);
        string input = sb.ToString();

        Assert.IsTrue(char.IsHighSurrogate(input[MaxCapturedFieldChars - 1]), "Test setup failed: boundary char must be a high surrogate.");

        var bag = new PropertyBag(
            new PassedTestNodeStateProperty(),
            new TrxExceptionProperty(input, null));

        (TrxTestResult result, bool wasTruncated) = TrxTestResultExtractor.Extract(new TestNodeUpdateMessage(
            new SessionUid("1"),
            new TestNode { Uid = "u", DisplayName = "d", Properties = bag }));

        Assert.IsTrue(wasTruncated);
        Assert.IsNotNull(result.ExceptionMessage);

        // The truncated portion (the prefix before the suffix) must not end with an unpaired high surrogate.
        // The suffix starts with '\n', which is not a low surrogate, so we just check the last char of the prefix.
        string truncated = result.ExceptionMessage;
        int suffixStart = truncated.IndexOf("\n... [truncated", StringComparison.Ordinal);
        Assert.IsGreaterThan(0, suffixStart);
        char lastPrefixChar = truncated[suffixStart - 1];
        Assert.IsFalse(char.IsHighSurrogate(lastPrefixChar), "Truncation must not leave a dangling high surrogate.");
    }

    [TestMethod]
    public void Extract_DuplicateSingletonProperty_Throws()
    {
        // PropertyBag allows multiple properties of the same runtime type. The pre-refactor
        // implementation used SingleOrDefault<T>() which throws InvalidOperationException in
        // that case; the single-pass switch must preserve the same invariant so upstream bugs
        // that add a singleton-typed property twice are surfaced (rather than silently keeping
        // the first or last one and producing nondeterministic TRX output).
        var bag = new PropertyBag(
            new PassedTestNodeStateProperty(),
            new TimingProperty(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero)),
            new TimingProperty(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero)));

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            TrxTestResultExtractor.Extract(new TestNodeUpdateMessage(
                new SessionUid("1"),
                new TestNode { Uid = "u", DisplayName = "d", Properties = bag })));

        Assert.Contains(nameof(TimingProperty), ex.Message);
    }

    private static TrxTestResult ExtractResult(params IProperty[] properties)
        => TrxTestResultExtractor.Extract(new TestNodeUpdateMessage(
            new SessionUid("1"),
            new TestNode { Uid = "u", DisplayName = "d", Properties = new PropertyBag(properties) })).Result;
}
