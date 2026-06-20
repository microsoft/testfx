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
}
