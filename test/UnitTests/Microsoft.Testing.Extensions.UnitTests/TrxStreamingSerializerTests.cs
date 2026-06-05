// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class TrxStreamingSerializerTests
{
    [TestMethod]
    public void RoundTrip_AllFieldsPopulated_ReadsBackEqual()
    {
        var original = new TrxTestResult
        {
            Uid = "uid-1",
            DisplayName = "My Test",
            Outcome = TrxTestOutcome.Failed,
            StartTime = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)),
            EndTime = new DateTimeOffset(2025, 1, 2, 3, 4, 6, TimeSpan.FromHours(2)),
            Duration = TimeSpan.FromMilliseconds(1234),
            TrxTestDefinitionName = "DefName",
            TrxFullyQualifiedTypeName = "Ns.Type",
            TestMethodIdentifier = new TrxTestMethodIdentifier { Namespace = "Ns", TypeName = "Type", MethodName = "Method" },
            ExceptionMessage = "boom",
            ExceptionStackTrace = "at Ns.Type.Method()",
            Messages =
            [
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardOutput, Message = "out" },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardError, Message = "err" },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.DebugOrTrace, Message = null },
            ],
            Categories = ["cat-a", "cat-b"],
            Metadata = [new TrxTestMetadata { Key = "k", Value = "v" }],
            FileArtifacts = [new TrxTestFileArtifact { FullPath = @"c:\artifact.txt" }],
        };

        TrxTestResult round = WriteAndReadOne(original);

        Assert.AreEqual(original.Uid, round.Uid);
        Assert.AreEqual(original.DisplayName, round.DisplayName);
        Assert.AreEqual(original.Outcome, round.Outcome);
        Assert.AreEqual(original.StartTime, round.StartTime);
        Assert.AreEqual(original.EndTime, round.EndTime);
        Assert.AreEqual(original.Duration, round.Duration);
        Assert.AreEqual(original.TrxTestDefinitionName, round.TrxTestDefinitionName);
        Assert.AreEqual(original.TrxFullyQualifiedTypeName, round.TrxFullyQualifiedTypeName);
        Assert.IsNotNull(round.TestMethodIdentifier);
        Assert.AreEqual("Ns", round.TestMethodIdentifier.Namespace);
        Assert.AreEqual("Type", round.TestMethodIdentifier.TypeName);
        Assert.AreEqual("Method", round.TestMethodIdentifier.MethodName);
        Assert.AreEqual(original.ExceptionMessage, round.ExceptionMessage);
        Assert.AreEqual(original.ExceptionStackTrace, round.ExceptionStackTrace);
        Assert.IsNotNull(round.Messages);
        Assert.HasCount(3, round.Messages);
        Assert.AreEqual(TrxStreamMessageKind.StandardOutput, round.Messages[0].Kind);
        Assert.AreEqual("out", round.Messages[0].Message);
        Assert.AreEqual(TrxStreamMessageKind.DebugOrTrace, round.Messages[2].Kind);
        Assert.IsNull(round.Messages[2].Message);
        Assert.IsNotNull(round.Categories);
        Assert.AreSequenceEqual(new[] { "cat-a", "cat-b" }, round.Categories.ToArray());
        Assert.IsNotNull(round.Metadata);
        Assert.AreEqual("k", round.Metadata[0].Key);
        Assert.AreEqual("v", round.Metadata[0].Value);
        Assert.IsNotNull(round.FileArtifacts);
        Assert.AreEqual(@"c:\artifact.txt", round.FileArtifacts[0].FullPath);
    }

    [TestMethod]
    public void RoundTrip_AllOutcomes_RoundTrip()
    {
        foreach (TrxTestOutcome outcome in (TrxTestOutcome[])[TrxTestOutcome.Passed, TrxTestOutcome.Skipped, TrxTestOutcome.Failed, TrxTestOutcome.Timeout])
        {
            var original = new TrxTestResult { Uid = "u", DisplayName = "d", Outcome = outcome };
            TrxTestResult round = WriteAndReadOne(original);
            Assert.AreEqual(outcome, round.Outcome);
        }
    }

    [TestMethod]
    public void RoundTrip_MinimalRecord_NullCollectionsRemainNull()
    {
        var original = new TrxTestResult { Uid = "u", DisplayName = "d", Outcome = TrxTestOutcome.Passed };
        TrxTestResult round = WriteAndReadOne(original);
        Assert.IsNull(round.Messages);
        Assert.IsNull(round.Categories);
        Assert.IsNull(round.Metadata);
        Assert.IsNull(round.FileArtifacts);
        Assert.IsNull(round.TestMethodIdentifier);
        Assert.IsNull(round.ExceptionMessage);
        Assert.IsNull(round.ExceptionStackTrace);
        Assert.IsNull(round.StartTime);
        Assert.IsNull(round.EndTime);
        Assert.IsNull(round.Duration);
    }

    [TestMethod]
    public void RoundTrip_EmptyCollections_NormalizeToNull()
    {
        // Documented behavior in TrxTestResultSerializer xmldoc: empty collections round-trip as null.
        var original = new TrxTestResult
        {
            Uid = "u",
            DisplayName = "d",
            Outcome = TrxTestOutcome.Passed,
            Messages = [],
            Categories = [],
            Metadata = [],
            FileArtifacts = [],
        };

        TrxTestResult round = WriteAndReadOne(original);
        Assert.IsNull(round.Messages);
        Assert.IsNull(round.Categories);
        Assert.IsNull(round.Metadata);
        Assert.IsNull(round.FileArtifacts);
    }

    [TestMethod]
    public void ReadAll_EmptyStream_YieldsNothing()
    {
        using var ms = new MemoryStream();
        TrxTestResult[] results = TrxTestResultSerializer.ReadAll(ms).ToArray();
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void ReadAll_TruncatedLengthPrefix_StopsCleanly()
    {
        using var ms = new MemoryStream([0x01, 0x02]); // less than 4 bytes
        TrxTestResult[] results = TrxTestResultSerializer.ReadAll(ms).ToArray();
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void ReadAll_TruncatedPayload_StopsCleanly()
    {
        // Length says 100 bytes, but only 5 bytes of payload follow.
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(100);
            w.Write([1, 2, 3, 4, 5]);
        }

        ms.Position = 0;
        TrxTestResult[] results = TrxTestResultSerializer.ReadAll(ms).ToArray();
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void ReadAll_ImplausibleLength_StopsAndDoesNotEmitGarbage()
    {
        // First a valid record, then a corrupt length prefix. The valid record must come back;
        // the second record must NOT be skipped past (no sync marker available) so reading stops
        // at the corrupt length.
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            TrxTestResultSerializer.Write(w, new TrxTestResult { Uid = "ok", DisplayName = "d", Outcome = TrxTestOutcome.Passed });
            w.Write(int.MaxValue); // implausible length
            w.Write([0xFF, 0xFF, 0xFF, 0xFF]);
        }

        ms.Position = 0;
        TrxTestResult[] results = TrxTestResultSerializer.ReadAll(ms).ToArray();
        Assert.HasCount(1, results);
        Assert.AreEqual("ok", results[0].Uid);
    }

    [TestMethod]
    public void ReadAll_NegativeLength_StopsAndDoesNotEmitGarbage()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(-1);
            w.Write([0, 0, 0, 0]);
        }

        ms.Position = 0;
        TrxTestResult[] results = TrxTestResultSerializer.ReadAll(ms).ToArray();
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void ReadAll_MultipleRecords_ReturnsInOrder()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            for (int i = 0; i < 5; i++)
            {
                TrxTestResultSerializer.Write(w, new TrxTestResult { Uid = $"u{i}", DisplayName = "d", Outcome = TrxTestOutcome.Passed });
            }
        }

        ms.Position = 0;
        TrxTestResult[] results = TrxTestResultSerializer.ReadAll(ms).ToArray();
        Assert.HasCount(5, results);
        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual($"u{i}", results[i].Uid);
        }
    }

    [TestMethod]
    public void RoundTrip_StringWithControlAndSurrogateChars_PreservesBytes()
    {
        // BinaryWriter writes UTF-8; surrogate pairs and control characters must round-trip exactly.
        var original = new TrxTestResult
        {
            Uid = "u",
            DisplayName = "name with \u0001 control \u0007 bell",
            Outcome = TrxTestOutcome.Passed,
            ExceptionMessage = "emoji \uD83D\uDE00 surrogate pair",
            ExceptionStackTrace = "tab\there\rline\nfeed",
        };
        TrxTestResult round = WriteAndReadOne(original);
        Assert.AreEqual(original.DisplayName, round.DisplayName);
        Assert.AreEqual(original.ExceptionMessage, round.ExceptionMessage);
        Assert.AreEqual(original.ExceptionStackTrace, round.ExceptionStackTrace);
    }

    [TestMethod]
    public void RoundTrip_DateTimeOffsetWithNonStandardOffset_PreservesOffsetMinutes()
    {
        // Offsets aren't always whole hours (India = +05:30, Nepal = +05:45).
        var original = new TrxTestResult
        {
            Uid = "u",
            DisplayName = "d",
            Outcome = TrxTestOutcome.Passed,
            StartTime = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.FromMinutes(345)),
        };
        TrxTestResult round = WriteAndReadOne(original);
        Assert.AreEqual(original.StartTime, round.StartTime);
        Assert.AreEqual(TimeSpan.FromMinutes(345), round.StartTime!.Value.Offset);
    }

    private static TrxTestResult WriteAndReadOne(TrxTestResult record)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            TrxTestResultSerializer.Write(w, record);
        }

        ms.Position = 0;
        return TrxTestResultSerializer.ReadAll(ms).Single();
    }
}
