// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

using static Microsoft.Testing.Platform.UnitTests.ProtocolSerializerTestHelper;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Additional edge-case coverage for the dotnet test wire protocol serializers (empty/multiple collections, null
/// optional fields, multiple exceptions, and special characters). Complements <see cref="ProtocolTests"/>.
/// </summary>
[TestClass]
public sealed class ProtocolEdgeCaseTests
{
    [TestMethod]
    public void CoverageSummaryMessages_IndependentWireFixturePinsAllFields()
    {
        // Literal IDs deliberately do not share the production constants: a matching
        // writer/reader mistake must not make this cross-repository contract test pass.
        byte[] entry = WriteFields(
            (1, WriteString("session")),
            (2, WriteString("producer")),
            (3, WriteInt(1)),
            (4, WriteString("module.dll")),
            (5, WriteInt(255)),
            (6, WriteString("custom")),
            (7, BitConverter.GetBytes(3_000_000_000L)),
            (8, BitConverter.GetBytes(long.MaxValue)));
        byte[] payload = WriteFields(
            (1, WriteString("execution")), (2, WriteString("instance")), (3, WriteList(entry)));
        var expected = new CoverageSummaryMessage("session", "producer", 1, "module.dll", 255, "custom", 3_000_000_000L, long.MaxValue);
        object serializer = new CoverageSummaryMessagesSerializer();

        CoverageSummaryMessages actual = DeserializePayload<CoverageSummaryMessages>(serializer, payload);
        using var serialized = new MemoryStream();
        Serialize(serializer, new CoverageSummaryMessages("execution", "instance", [expected]), serialized);

        Assert.AreEqual("execution", actual.ExecutionId);
        Assert.AreEqual("instance", actual.InstanceId);
        Assert.AreEqual(expected, Assert.ContainsSingle(actual.Summaries));
        Assert.AreSequenceEqual(payload, serialized.ToArray());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AbsentCoverageList")]
    [DataRow(true, DisplayName = "ExplicitEmptyCoverageList")]
    public void CoverageSummaryMessages_EmptyPayloadDoesNotInventMeasurements(bool explicitList)
    {
        byte[] payload = explicitList ? WriteFields((3, WriteList())) : WriteFields();

        CoverageSummaryMessages actual = DeserializePayload<CoverageSummaryMessages>(new CoverageSummaryMessagesSerializer(), payload);

        Assert.IsNull(actual.ExecutionId);
        Assert.IsNull(actual.InstanceId);
        Assert.IsEmpty(actual.Summaries);
    }

    [TestMethod]
    public void CoverageSummaryMessages_AbsentCountsAreNotDefaultedToZero()
    {
        byte[] payload = WriteFields((3, WriteList(
            WriteFields((2, WriteString("missing"))),
            WriteFields((7, BitConverter.GetBytes(0L))),
            WriteFields((8, BitConverter.GetBytes(0L))),
            WriteFields((7, BitConverter.GetBytes(0L)), (8, BitConverter.GetBytes(0L))))));

        CoverageSummaryMessages actual = DeserializePayload<CoverageSummaryMessages>(new CoverageSummaryMessagesSerializer(), payload);

        Assert.AreSequenceEqual(
            [
                new CoverageSummaryMessage(null, "missing", null, null, null, null, null, null),
                new CoverageSummaryMessage(null, null, null, null, null, null, 0, null),
                new CoverageSummaryMessage(null, null, null, null, null, null, null, 0),
                new CoverageSummaryMessage(null, null, null, null, null, null, 0, 0),
            ],
            actual.Summaries);
    }

    [TestMethod]
    public void CoverageSummaryMessages_UnknownFieldsBeforeAndAfterKnownFieldsAreSkipped()
    {
        byte[] entry = WriteFields(
            (ushort.MaxValue, [0xAA, 0xBB]),
            (1, WriteString("session")),
            (2, WriteString("producer")),
            (7, BitConverter.GetBytes(42L)),
            (8, BitConverter.GetBytes(99L)),
            (ushort.MaxValue - 1, [0xCC]));
        byte[] payload = WriteFields(
            (ushort.MaxValue, [0xDD]),
            (1, WriteString("execution")),
            (3, WriteList(entry, WriteFields((2, WriteString("following-producer"))))),
            (2, WriteString("instance")),
            (ushort.MaxValue - 1, [0xEE, 0xFF]));

        CoverageSummaryMessages actual = DeserializePayload<CoverageSummaryMessages>(new CoverageSummaryMessagesSerializer(), payload);

        Assert.AreEqual("execution", actual.ExecutionId);
        Assert.AreEqual("instance", actual.InstanceId);
        Assert.AreSequenceEqual(
            [
                new CoverageSummaryMessage("session", "producer", null, null, null, null, 42, 99),
                new CoverageSummaryMessage(null, "following-producer", null, null, null, null, null, null),
            ],
            actual.Summaries);
    }

    [TestMethod]
    public void CoverageSummaryMessages_PaddedNumericFieldsPreserveFollowingValues()
    {
        byte[] entry = WriteFields(
            (3, [.. WriteInt(1), 0xAA, 0xBB]),
            (5, [.. WriteInt(255), 0xCC]),
            (7, [.. BitConverter.GetBytes(42L), 0xDD, 0xEE]),
            (8, [.. BitConverter.GetBytes(99L), 0xFF]),
            (4, WriteString("after padding")),
            (6, WriteString("custom")));

        CoverageSummaryMessages actual = DeserializePayload<CoverageSummaryMessages>(
            new CoverageSummaryMessagesSerializer(), WriteFields((3, WriteList(entry))));

        Assert.AreEqual(
            new CoverageSummaryMessage(null, null, 1, "after padding", 255, "custom", 42, 99),
            Assert.ContainsSingle(actual.Summaries));
    }

    [TestMethod]
    [DataRow(false, -1, DisplayName = "RawNegativeLength")]
    [DataRow(false, 1, DisplayName = "RawLengthExceedsPayload")]
    [DataRow(false, 1_000_001, DisplayName = "RawLengthExceedsLimit")]
    [DataRow(false, int.MaxValue, DisplayName = "RawMaximumLength")]
    [DataRow(true, -1, DisplayName = "ArtifactNegativeLength")]
    [DataRow(true, 1, DisplayName = "ArtifactLengthExceedsPayload")]
    [DataRow(true, 1_000_001, DisplayName = "ArtifactLengthExceedsLimit")]
    [DataRow(true, int.MaxValue, DisplayName = "ArtifactMaximumLength")]
    public void CoverageSummaryPayload_InvalidCollectionLengthIsBounded(bool artifact, int count)
    {
        // Isolate the collection cap from the remaining-byte guard: these zero-field
        // entries otherwise fit, so removing the cap would incorrectly accept them.
        byte[] summaries = count == 1_000_001
            ? [.. WriteInt(count), .. new byte[count * sizeof(ushort)]]
            : WriteInt(count);

        AssertDeserializeThrows<InvalidDataException>(
            artifact ? new FileArtifactMessagesSerializer() : new CoverageSummaryMessagesSerializer(),
            WriteCoveragePayload(summaries, artifact));
    }

    [TestMethod]
    [DataRow(false, 3, 0, DisplayName = "RawScopeFieldTooSmall")]
    [DataRow(false, 5, 3, DisplayName = "RawMetricFieldTooSmall")]
    [DataRow(false, 7, 4, DisplayName = "RawCoveredFieldTooSmall")]
    [DataRow(false, 8, 7, DisplayName = "RawCoverableFieldTooSmall")]
    [DataRow(true, 3, 0, DisplayName = "ArtifactScopeFieldTooSmall")]
    [DataRow(true, 5, 3, DisplayName = "ArtifactMetricFieldTooSmall")]
    [DataRow(true, 7, 4, DisplayName = "ArtifactCoveredFieldTooSmall")]
    [DataRow(true, 8, 7, DisplayName = "ArtifactCoverableFieldTooSmall")]
    public void CoverageSummaryPayload_UndersizedNumericFieldIsRejected(bool artifact, int fieldId, int declaredSize)
    {
        byte[] primitive = fieldId is 7 or 8 ? BitConverter.GetBytes(42L) : WriteInt(1);
        byte[] entry = WriteFieldsWithDeclaredSizes(((ushort)fieldId, declaredSize, primitive));

        AssertDeserializeThrows<InvalidDataException>(
            artifact ? new FileArtifactMessagesSerializer() : new CoverageSummaryMessagesSerializer(),
            WriteCoveragePayload(WriteList(entry), artifact));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RawNegativeFieldSize")]
    [DataRow(true, DisplayName = "ArtifactNegativeFieldSize")]
    public void CoverageSummaryPayload_NegativeFieldSizeIsRejected(bool artifact)
    {
        byte[] entry = WriteFieldsWithDeclaredSizes((7, -1, []));

        AssertDeserializeThrows<InvalidDataException>(
            artifact ? new FileArtifactMessagesSerializer() : new CoverageSummaryMessagesSerializer(),
            WriteCoveragePayload(WriteList(entry), artifact));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RawTruncatedEntryIsBounded")]
    [DataRow(true, DisplayName = "ArtifactTruncatedEntryIsBounded")]
    public void CoverageSummaryPayload_TruncatedEntryCannotReadPastCollection(bool artifact)
    {
        // Enough bytes exist in the neighboring field, but not inside the counted list.
        byte[] entry = WriteFieldsWithDeclaredSizes((7, sizeof(long), WriteInt(42)));

        AssertDeserializeThrows<EndOfStreamException>(
            artifact ? new FileArtifactMessagesSerializer() : new CoverageSummaryMessagesSerializer(),
            WriteCoveragePayload(WriteList(entry), artifact));
    }

    [TestMethod]
    [DataRow(false, 0, DisplayName = "RawMissingCollectionHeader")]
    [DataRow(false, 1, DisplayName = "RawOneByteCollectionHeader")]
    [DataRow(false, 2, DisplayName = "RawTwoByteCollectionHeader")]
    [DataRow(false, 3, DisplayName = "RawThreeByteCollectionHeader")]
    [DataRow(true, 0, DisplayName = "ArtifactMissingCollectionHeader")]
    [DataRow(true, 1, DisplayName = "ArtifactOneByteCollectionHeader")]
    [DataRow(true, 2, DisplayName = "ArtifactTwoByteCollectionHeader")]
    [DataRow(true, 3, DisplayName = "ArtifactThreeByteCollectionHeader")]
    public void CoverageSummaryPayload_TruncatedCollectionHeaderIsRejected(bool artifact, int byteCount)
        => AssertDeserializeThrows<EndOfStreamException>(
            artifact ? new FileArtifactMessagesSerializer() : new CoverageSummaryMessagesSerializer(),
            WriteCoveragePayload(new byte[byteCount], artifact));

    [TestMethod]
    public void FileArtifactMessages_LegacyReaderSkipsCoverageAndPreservesFollowingArtifacts()
    {
        var summary = new CoverageSummaryMessage(null, "producer", 0, null, 0, null, 2, 4);
        var message = new FileArtifactMessages("execution", "instance",
        [
            new("merged.fixture", "merged", null, null, null, null, "fixture.kind", ["first.fixture", "second.fixture"], [summary]),
            new("following.fixture", "following", null, null, null, null, "legacy.kind", ["third.fixture"]),
        ]);
        byte[] expected = WriteFields(
            (1, WriteString("execution")),
            (2, WriteString("instance")),
            (3, WriteList(
                WriteFields(
                    (1, WriteString("merged.fixture")), (2, WriteString("merged")), (7, WriteString("fixture.kind")),
                    (8, WriteStringList("first.fixture", "second.fixture")),
                    (9, WriteList(WriteFields(
                        (2, WriteString("producer")), (3, WriteInt(0)), (5, WriteInt(0)),
                        (7, BitConverter.GetBytes(2L)), (8, BitConverter.GetBytes(4L)))))),
                WriteFields(
                    (1, WriteString("following.fixture")), (2, WriteString("following")), (7, WriteString("legacy.kind")),
                    (8, WriteStringList("third.fixture"))))));
        using var stream = new MemoryStream();

        Serialize(new FileArtifactMessagesSerializer(), message, stream);

        Assert.AreSequenceEqual(expected, stream.ToArray());
        (string? Path, string? Kind, string[]? Inputs)[] legacy = ReadLegacyArtifacts(stream.ToArray());
        Assert.HasCount(2, legacy);
        Assert.AreEqual("merged.fixture", legacy[0].Path);
        Assert.AreEqual("fixture.kind", legacy[0].Kind);
        Assert.AreSequenceEqual(["first.fixture", "second.fixture"], legacy[0].Inputs);
        Assert.AreEqual("following.fixture", legacy[1].Path);
        Assert.AreEqual("legacy.kind", legacy[1].Kind);
        Assert.AreSequenceEqual(["third.fixture"], legacy[1].Inputs);
    }

    [TestMethod]
    public void TestResultMessages_WhenBothListsEmpty_RoundTrips()
    {
        var message = new TestResultMessages("exec", "inst", [], []);

        TestResultMessages actual = RoundTrip(new TestResultMessagesSerializer(), message);

        Assert.AreEqual("exec", actual.ExecutionId);
        Assert.AreEqual("inst", actual.InstanceId);
        Assert.IsEmpty(actual.SuccessfulTestMessages);
        Assert.IsEmpty(actual.FailedTestMessages);
    }

    [TestMethod]
    public void TestResultMessages_WhenMultipleResults_RoundTripsAllInOrder()
    {
        SuccessfulTestResultMessage[] passed =
        [
            new("p1", "Passed 1", TestStates.Passed, 10, "r1", "out1", "err1", "s"),
            new("p2", "Passed 2", TestStates.Passed, 20, null, null, null, null),
            new("p3", "Skipped 3", TestStates.Skipped, 0, "skip reason", null, null, "s"),
        ];
        FailedTestResultMessage[] failed =
        [
            new("f1", "Failed 1", TestStates.Failed, 30, "boom", [new ExceptionMessage("m1", "t1", "st1")], "out", "err", "s", "exp1", "act1"),
            new("f2", "Errored 2", TestStates.Error, 40, null, [new ExceptionMessage("m2", "t2", "st2"), new ExceptionMessage("m3", "t3", "st3")], null, null, null, null, null),
        ];
        var message = new TestResultMessages("exec", "inst", passed, failed);

        TestResultMessages actual = RoundTrip(new TestResultMessagesSerializer(), message);

        Assert.HasCount(3, actual.SuccessfulTestMessages);
        Assert.HasCount(2, actual.FailedTestMessages);
        Assert.AreEqual("inst", actual.InstanceId);
        Assert.AreEqual("p2", actual.SuccessfulTestMessages[1].Uid);
        Assert.IsNull(actual.SuccessfulTestMessages[1].Reason);
        Assert.AreEqual(TestStates.Skipped, actual.SuccessfulTestMessages[2].State);

        // The second failed test carries two exceptions and they must round-trip in order.
        Assert.IsNotNull(actual.FailedTestMessages[1].Exceptions);
        Assert.HasCount(2, actual.FailedTestMessages[1].Exceptions!);
        Assert.AreEqual("m2", actual.FailedTestMessages[1].Exceptions![0].ErrorMessage);
        Assert.AreEqual("st3", actual.FailedTestMessages[1].Exceptions![1].StackTrace);
    }

    [TestMethod]
    public void FailedTestResultMessage_WhenNoExceptions_RoundTrips()
    {
        var message = new TestResultMessages("exec", "inst", [], [new FailedTestResultMessage("f", "F", TestStates.Failed, 1, "reason", [], null, null, null, null, null)]);

        TestResultMessages actual = RoundTrip(new TestResultMessagesSerializer(), message);

        Assert.HasCount(1, actual.FailedTestMessages);
        Assert.AreEqual("inst", actual.InstanceId);
        Assert.IsNotNull(actual.FailedTestMessages[0].Exceptions);
        Assert.IsEmpty(actual.FailedTestMessages[0].Exceptions!);
        Assert.IsNull(actual.FailedTestMessages[0].Expected);
        Assert.IsNull(actual.FailedTestMessages[0].Actual);
    }

    [TestMethod]
    public void FailedTestResultMessage_WithExpectedAndActual_RoundTrips()
    {
        var message = new TestResultMessages("exec", "inst", [], [new FailedTestResultMessage("f", "F", TestStates.Failed, 1, "reason", [], null, null, null, "expected value", "actual value")]);

        TestResultMessages actual = RoundTrip(new TestResultMessagesSerializer(), message);

        Assert.HasCount(1, actual.FailedTestMessages);
        Assert.AreEqual("expected value", actual.FailedTestMessages[0].Expected);
        Assert.AreEqual("actual value", actual.FailedTestMessages[0].Actual);
    }

    [TestMethod]
    public void SuccessfulAndFailedTestResultMessage_WithRetryAttemptMetadata_RoundTrips()
    {
        // A superseded (non-final) retry attempt that failed, followed by the final attempt that passed - the
        // typical shape produced by MSTest's [Retry] attribute for a flaky test that eventually succeeds.
        var message = new TestResultMessages(
            "exec",
            "inst",
            [new SuccessfulTestResultMessage("uid", "F", TestStates.Passed, 1, "reason", null, null, "s", RetryAttemptNumber: 2, IsSuperseded: false)],
            [new FailedTestResultMessage("uid", "F", TestStates.Failed, 1, "reason", [], null, null, "s", null, null, RetryAttemptNumber: 1, IsSuperseded: true)]);

        TestResultMessages actual = RoundTrip(new TestResultMessagesSerializer(), message);

        Assert.AreEqual(2, actual.SuccessfulTestMessages[0].RetryAttemptNumber);
        Assert.IsFalse(actual.SuccessfulTestMessages[0].IsSuperseded);
        Assert.AreEqual(1, actual.FailedTestMessages[0].RetryAttemptNumber);
        Assert.IsTrue(actual.FailedTestMessages[0].IsSuperseded);
    }

    [TestMethod]
    public void TestResultMessages_WhenPayloadPredatesRetryAttemptFields_DeserializesWithNullRetryMetadata()
    {
        // Simulates a payload written by an older SDK/runner that predates the RetryAttemptNumber/IsSuperseded
        // fields: the field ids are simply absent from the wire, rather than present with a null/default value.
        // A new reader must treat that absence as "no retry metadata" instead of throwing or defaulting to zero.
        byte[] successfulResult = WriteFields(
            (SuccessfulTestResultMessageFieldsId.Uid, WriteString("p")),
            (SuccessfulTestResultMessageFieldsId.DisplayName, WriteString("passed test")));
        byte[] failedResult = WriteFields(
            (FailedTestResultMessageFieldsId.Uid, WriteString("f")),
            (FailedTestResultMessageFieldsId.DisplayName, WriteString("failed test")));
        byte[] payload = WriteFields(
            (TestResultMessagesFieldsId.InstanceId, WriteString("instance")),
            (TestResultMessagesFieldsId.SuccessfulTestMessageList, WriteList(successfulResult)),
            (TestResultMessagesFieldsId.FailedTestMessageList, WriteList(failedResult)));

        TestResultMessages actual = DeserializePayload<TestResultMessages>(new TestResultMessagesSerializer(), payload);

        Assert.HasCount(1, actual.SuccessfulTestMessages);
        Assert.AreEqual("passed test", actual.SuccessfulTestMessages[0].DisplayName);
        Assert.IsNull(actual.SuccessfulTestMessages[0].RetryAttemptNumber);
        Assert.IsNull(actual.SuccessfulTestMessages[0].IsSuperseded);
        Assert.HasCount(1, actual.FailedTestMessages);
        Assert.AreEqual("failed test", actual.FailedTestMessages[0].DisplayName);
        Assert.IsNull(actual.FailedTestMessages[0].RetryAttemptNumber);
        Assert.IsNull(actual.FailedTestMessages[0].IsSuperseded);
    }

    [TestMethod]
    public void TestResultMessages_WhenFieldsContainSpecialCharacters_RoundTripsExactly()
    {
        // Unicode, newlines, tabs, and a null character must survive the round-trip unchanged because failure
        // messages and captured output frequently contain them.
        const string Tricky = "líne1\r\nlíne2\tTabbed \u2728 \u0000 end \"quoted\"";
        var message = new TestResultMessages(
            Tricky,
            "inst",
            [new SuccessfulTestResultMessage("uid", Tricky, TestStates.Passed, 1, Tricky, Tricky, Tricky, "s")],
            [new FailedTestResultMessage("uid", Tricky, TestStates.Failed, 1, Tricky, [new ExceptionMessage(Tricky, Tricky, Tricky)], Tricky, Tricky, "s", Tricky, Tricky)]);

        TestResultMessages actual = RoundTrip(new TestResultMessagesSerializer(), message);

        Assert.AreEqual(Tricky, actual.ExecutionId);
        Assert.AreEqual(Tricky, actual.SuccessfulTestMessages[0].DisplayName);
        Assert.AreEqual(Tricky, actual.SuccessfulTestMessages[0].StandardOutput);
        Assert.AreEqual(Tricky, actual.FailedTestMessages[0].Exceptions![0].StackTrace);
        Assert.AreEqual(Tricky, actual.FailedTestMessages[0].Expected);
        Assert.AreEqual(Tricky, actual.FailedTestMessages[0].Actual);
    }

    [TestMethod]
    public void TestResultMessages_WhenUnknownFieldsPrecedeKnownFields_DeserializesRemainingFields()
    {
        byte[] successfulResult = WriteFields(
            (ushort.MaxValue, [0x99]),
            (SuccessfulTestResultMessageFieldsId.DisplayName, WriteString("passed test")));
        byte[] exception = WriteFields(
            (ushort.MaxValue, [0xAA, 0xBB]),
            (ExceptionMessageFieldsId.ErrorMessage, WriteString("boom")));
        byte[] failedResult = WriteFields(
            (ushort.MaxValue, [0xCC]),
            (FailedTestResultMessageFieldsId.DisplayName, WriteString("failed test")),
            (FailedTestResultMessageFieldsId.ExceptionMessageList, WriteList(exception)));
        byte[] payload = WriteFields(
            (ushort.MaxValue, [0xDD, 0xEE]),
            (TestResultMessagesFieldsId.InstanceId, WriteString("instance")),
            (TestResultMessagesFieldsId.SuccessfulTestMessageList, WriteList(successfulResult)),
            (TestResultMessagesFieldsId.FailedTestMessageList, WriteList(failedResult)));

        TestResultMessages actual = DeserializePayload<TestResultMessages>(new TestResultMessagesSerializer(), payload);

        Assert.AreEqual("instance", actual.InstanceId);
        Assert.HasCount(1, actual.SuccessfulTestMessages);
        Assert.AreEqual("passed test", actual.SuccessfulTestMessages[0].DisplayName);
        Assert.HasCount(1, actual.FailedTestMessages);
        Assert.AreEqual("failed test", actual.FailedTestMessages[0].DisplayName);
        Assert.HasCount(1, actual.FailedTestMessages[0].Exceptions!);
        Assert.AreEqual("boom", actual.FailedTestMessages[0].Exceptions![0].ErrorMessage);
    }

    [TestMethod]
    public void DiscoveredTestMessages_WhenEmptyList_RoundTrips()
    {
        var message = new DiscoveredTestMessages("exec", "inst", []);

        DiscoveredTestMessages actual = RoundTrip(new DiscoveredTestMessagesSerializer(), message);

        Assert.AreEqual("exec", actual.ExecutionId);
        Assert.AreEqual("inst", actual.InstanceId);
        Assert.IsEmpty(actual.DiscoveredMessages);
    }

    [TestMethod]
    public void DiscoveredTestMessages_WhenUnknownFieldsPrecedeKnownFields_DeserializesRemainingFields()
    {
        byte[] trait = WriteFields(
            (ushort.MaxValue, [0xAA]),
            (TraitMessageFieldsId.Key, WriteString("category")),
            (TraitMessageFieldsId.Value, WriteString("unit")));
        byte[] discoveredTest = WriteFields(
            (ushort.MaxValue, [0xBB, 0xCC]),
            (DiscoveredTestMessageFieldsId.DisplayName, WriteString("discovered test")),
            (DiscoveredTestMessageFieldsId.Traits, WriteList(trait)));
        byte[] payload = WriteFields(
            (ushort.MaxValue, [0xDD]),
            (DiscoveredTestMessagesFieldsId.InstanceId, WriteString("instance")),
            (DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList, WriteList(discoveredTest)));

        DiscoveredTestMessages actual = DeserializePayload<DiscoveredTestMessages>(new DiscoveredTestMessagesSerializer(), payload);

        Assert.AreEqual("instance", actual.InstanceId);
        Assert.HasCount(1, actual.DiscoveredMessages);
        Assert.AreEqual("discovered test", actual.DiscoveredMessages[0].DisplayName);
        Assert.HasCount(1, actual.DiscoveredMessages[0].Traits);
        Assert.AreEqual("category", actual.DiscoveredMessages[0].Traits[0].Key);
        Assert.AreEqual("unit", actual.DiscoveredMessages[0].Traits[0].Value);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    [DataRow(1_000_001)]
    [DataRow(int.MaxValue)]
    public void DiscoveredTestMessages_WhenParameterTypeCountIsInvalid_ThrowsInvalidDataException(int count)
    {
        byte[] discoveredTest = WriteFields(
            (DiscoveredTestMessageFieldsId.ParameterTypeFullNames, WriteInt(count)));
        byte[] payload = WriteFields(
            (DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList, WriteList(discoveredTest)));

        AssertDeserializeThrows<InvalidDataException>(new DiscoveredTestMessagesSerializer(), payload);
    }

    [TestMethod]
    public void DiscoveredTestMessages_WhenIntFieldIsWiderThanPrimitive_SkipsPadding()
    {
        byte[] discoveredTest = WriteFields(
            (DiscoveredTestMessageFieldsId.LineNumber, [.. BitConverter.GetBytes(42), 0xAA, 0xBB, 0xCC, 0xDD]),
            (DiscoveredTestMessageFieldsId.DisplayName, WriteString("after padding")));
        byte[] payload = WriteFields(
            (DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList, WriteList(discoveredTest)));

        DiscoveredTestMessages actual = DeserializePayload<DiscoveredTestMessages>(new DiscoveredTestMessagesSerializer(), payload);

        Assert.AreEqual(42, actual.DiscoveredMessages[0].LineNumber);
        Assert.AreEqual("after padding", actual.DiscoveredMessages[0].DisplayName);
    }

    [TestMethod]
    public void FileArtifactMessages_WhenEmptyList_RoundTrips()
    {
        var message = new FileArtifactMessages("exec", "inst", []);

        FileArtifactMessages actual = RoundTrip(new FileArtifactMessagesSerializer(), message);

        Assert.AreEqual("exec", actual.ExecutionId);
        Assert.AreEqual("inst", actual.InstanceId);
        Assert.IsEmpty(actual.FileArtifacts);
    }

    [TestMethod]
    public void FileArtifactMessages_WhenMultipleArtifactsWithMixedNulls_RoundTrips()
    {
        var message = new FileArtifactMessages(
            "exec",
            "inst",
            [
                new FileArtifactMessage("/a/b.trx", "b.trx", "a trx", "uid", "Test", "session", "microsoft.testing.trx"),
                new FileArtifactMessage("/c/d.coverage", "d.coverage", null, null, null, null, null),
                new FileArtifactMessage("/e/f.txt", null, null, "uid2", null, "session2", null),
            ]);

        FileArtifactMessages actual = RoundTrip(new FileArtifactMessagesSerializer(), message);

        Assert.HasCount(3, actual.FileArtifacts);
        Assert.AreEqual("inst", actual.InstanceId);
        Assert.AreEqual("/c/d.coverage", actual.FileArtifacts[1].FullPath);
        Assert.IsNull(actual.FileArtifacts[1].Description);
        Assert.AreEqual("microsoft.testing.trx", actual.FileArtifacts[0].Kind);
        Assert.IsNull(actual.FileArtifacts[1].Kind);
        Assert.AreEqual("session2", actual.FileArtifacts[2].SessionUid);
        Assert.IsNull(actual.FileArtifacts[2].DisplayName);
        Assert.IsNull(actual.FileArtifacts[2].InputArtifactPaths);
    }

    [TestMethod]
    public void FileArtifactMessages_WhenUnknownFieldsPrecedeKnownFields_DeserializesRemainingFields()
    {
        byte[] fileArtifact = WriteFields(
            (ushort.MaxValue, [0xAA, 0xBB]),
            (FileArtifactMessageFieldsId.FullPath, WriteString("/a/b.trx")),
            (FileArtifactMessageFieldsId.Kind, WriteString("microsoft.testing.trx")));
        byte[] payload = WriteFields(
            (ushort.MaxValue, [0xCC]),
            (FileArtifactMessagesFieldsId.InstanceId, WriteString("instance")),
            (FileArtifactMessagesFieldsId.FileArtifactMessageList, WriteList(fileArtifact)));

        FileArtifactMessages actual = DeserializePayload<FileArtifactMessages>(new FileArtifactMessagesSerializer(), payload);

        Assert.AreEqual("instance", actual.InstanceId);
        Assert.HasCount(1, actual.FileArtifacts);
        Assert.AreEqual("/a/b.trx", actual.FileArtifacts[0].FullPath);
        Assert.AreEqual("microsoft.testing.trx", actual.FileArtifacts[0].Kind);
        Assert.IsNull(actual.FileArtifacts[0].InputArtifactPaths);
    }

    [TestMethod]
    public void FileArtifactMessages_WhenInputArtifactPathCountIsTooLarge_ThrowsInvalidDataException()
    {
        byte[] fileArtifact = WriteFields(
            (FileArtifactMessageFieldsId.InputArtifactPaths, WriteInt(int.MaxValue)));
        byte[] payload = WriteFields(
            (FileArtifactMessagesFieldsId.FileArtifactMessageList, WriteList(fileArtifact)));

        AssertDeserializeThrows<InvalidDataException>(new FileArtifactMessagesSerializer(), payload);
    }

    [TestMethod]
    public void DisplayMessage_WhenByteFieldIsWiderThanPrimitive_SkipsPadding()
    {
        byte[] payload = WriteFields(
            (DisplayMessageFieldsId.Level, [DisplayMessageLevels.Warning, 0xFF]),
            (DisplayMessageFieldsId.Text, WriteString("after padding")));

        DisplayMessage actual = DeserializePayload<DisplayMessage>(new DisplayMessageSerializer(), payload);

        Assert.AreEqual(DisplayMessageLevels.Warning, actual.Level);
        Assert.AreEqual("after padding", actual.Text);
    }

    [TestMethod]
    public void DisplayMessage_WhenByteFieldDeclaresNoPayload_ThrowsInvalidDataException()
    {
        byte[] payload = WriteFieldsWithDeclaredSizes(
            (DisplayMessageFieldsId.Level, 0, [DisplayMessageLevels.Warning]),
            (DisplayMessageFieldsId.Text, "after malformed field".Length, WriteString("after malformed field")));

        AssertDeserializeThrows<InvalidDataException>(new DisplayMessageSerializer(), payload);
    }

    [TestMethod]
    public void TestResultMessages_WhenDurationExceedsDeclaredFieldSize_ThrowsInvalidDataException()
    {
        byte[] successfulResult = WriteFieldsWithDeclaredSizes(
            (SuccessfulTestResultMessageFieldsId.Duration, sizeof(int), BitConverter.GetBytes(42L)));
        byte[] payload = WriteFields(
            (TestResultMessagesFieldsId.SuccessfulTestMessageList, WriteList(successfulResult)));

        AssertDeserializeThrows<InvalidDataException>(new TestResultMessagesSerializer(), payload);
    }

    [TestMethod]
    public void CommandLineOptionMessages_WhenEmptyList_RoundTrips()
    {
        var message = new CommandLineOptionMessages("module.dll", []);

        CommandLineOptionMessages actual = RoundTrip(new CommandLineOptionMessagesSerializer(), message);

        Assert.AreEqual("module.dll", actual.ModulePath);
        Assert.IsNotNull(actual.CommandLineOptionMessageList);
        Assert.IsEmpty(actual.CommandLineOptionMessageList);
    }

    [TestMethod]
    public void TestInProgressMessages_WhenAllFieldsNull_RoundTrips()
    {
        var message = new TestInProgressMessages("exec", "inst", [new TestInProgressMessage(null, null)]);

        TestInProgressMessages actual = RoundTrip(new TestInProgressMessagesSerializer(), message);

        Assert.HasCount(1, actual.InProgressMessages);
        Assert.AreEqual("inst", actual.InstanceId);
        Assert.IsNull(actual.InProgressMessages[0].Uid);
        Assert.IsNull(actual.InProgressMessages[0].DisplayName);
    }

    [TestMethod]
    public void TestInProgressMessages_WhenUnknownFieldsPrecedeKnownFields_DeserializesRemainingFields()
    {
        byte[] inProgressTest = WriteFields(
            (ushort.MaxValue, [0xAA, 0xBB]),
            (TestInProgressMessageFieldsId.DisplayName, WriteString("running test")));
        byte[] payload = WriteFields(
            (ushort.MaxValue, [0xCC]),
            (TestInProgressMessagesFieldsId.InstanceId, WriteString("instance")),
            (TestInProgressMessagesFieldsId.TestInProgressMessageList, WriteList(inProgressTest)));

        TestInProgressMessages actual = DeserializePayload<TestInProgressMessages>(new TestInProgressMessagesSerializer(), payload);

        Assert.AreEqual("instance", actual.InstanceId);
        Assert.HasCount(1, actual.InProgressMessages);
        Assert.AreEqual("running test", actual.InProgressMessages[0].DisplayName);
    }

    [TestMethod]
    public void TestSessionEvent_End_RoundTrips()
    {
        var message = new TestSessionEvent(SessionEventTypes.TestSessionEnd, "session", "exec");

        TestSessionEvent actual = RoundTrip(new TestSessionEventSerializer(), message);

        Assert.AreEqual(SessionEventTypes.TestSessionEnd, actual.SessionType);
        Assert.AreEqual("session", actual.SessionUid);
        Assert.AreEqual("exec", actual.ExecutionId);
    }

    private static TMessage DeserializePayload<TMessage>(object serializer, byte[] payload)
    {
        using var stream = new MemoryStream(payload);
        return (TMessage)Deserialize(serializer, stream);
    }

    private static void AssertDeserializeThrows<TException>(object serializer, byte[] payload)
        where TException : Exception
    {
        using var stream = new MemoryStream(payload);
        TargetInvocationException wrapper = Assert.ThrowsExactly<TargetInvocationException>(
            () => Deserialize(serializer, stream));
        Assert.IsInstanceOfType<TException>(wrapper.InnerException);
    }

    private static byte[] WriteCoveragePayload(byte[] summaries, bool artifact)
        => artifact
            ? WriteFields((3, WriteList(WriteFields((9, summaries), (1, WriteString("neighbor.fixture"))))))
            : WriteFields((3, summaries), (2, WriteString("neighbor-instance")));

    private static byte[] WriteStringList(params string[] values)
    {
        using var stream = new MemoryStream();
        WriteInt(stream, values.Length);
        foreach (string value in values)
        {
            byte[] bytes = WriteString(value);
            WriteInt(stream, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        return stream.ToArray();
    }

    private static (string? Path, string? Kind, string[]? Inputs)[] ReadLegacyArtifacts(byte[] payload)
    {
        // This intentionally knows only the pre-coverage field IDs, not production serializers.
        using var reader = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
        List<(string? Path, string? Kind, string[]? Inputs)> artifacts = [];
        int fields = reader.ReadUInt16();
        for (int i = 0; i < fields; i++)
        {
            ushort id = reader.ReadUInt16();
            int size = reader.ReadInt32();
            if (id != 3)
            {
                reader.BaseStream.Seek(size, SeekOrigin.Current);
                continue;
            }

            int count = reader.ReadInt32();
            for (int j = 0; j < count; j++)
            {
                string? path = null, kind = null;
                string[]? inputs = null;
                int artifactFields = reader.ReadUInt16();
                for (int k = 0; k < artifactFields; k++)
                {
                    ushort fieldId = reader.ReadUInt16();
                    int fieldSize = reader.ReadInt32();
                    switch (fieldId)
                    {
                        case 1:
                            path = Encoding.UTF8.GetString(reader.ReadBytes(fieldSize));
                            break;
                        case 7:
                            kind = Encoding.UTF8.GetString(reader.ReadBytes(fieldSize));
                            break;
                        case 8:
                            inputs = new string[reader.ReadInt32()];
                            for (int index = 0; index < inputs.Length; index++)
                            {
                                inputs[index] = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
                            }

                            break;
                        default:
                            reader.BaseStream.Seek(fieldSize, SeekOrigin.Current);
                            break;
                    }
                }

                artifacts.Add((path, kind, inputs));
            }
        }

        return [.. artifacts];
    }

    private static byte[] WriteFields(params (ushort Id, byte[] Payload)[] fields)
    {
        using var stream = new MemoryStream();
        WriteUShort(stream, (ushort)fields.Length);

        foreach ((ushort id, byte[] payload) in fields)
        {
            WriteUShort(stream, id);
            WriteInt(stream, payload.Length);
            stream.Write(payload, 0, payload.Length);
        }

        return stream.ToArray();
    }

    private static byte[] WriteList(params byte[][] items)
    {
        using var stream = new MemoryStream();
        WriteInt(stream, items.Length);
        foreach (byte[] item in items)
        {
            stream.Write(item, 0, item.Length);
        }

        return stream.ToArray();
    }

    private static byte[] WriteFieldsWithDeclaredSizes(params (ushort Id, int DeclaredSize, byte[] Payload)[] fields)
    {
        using var stream = new MemoryStream();
        WriteUShort(stream, (ushort)fields.Length);

        foreach ((ushort id, int declaredSize, byte[] payload) in fields)
        {
            WriteUShort(stream, id);
            WriteInt(stream, declaredSize);
            stream.Write(payload, 0, payload.Length);
        }

        return stream.ToArray();
    }

    private static byte[] WriteString(string value) => Encoding.UTF8.GetBytes(value);

    private static byte[] WriteInt(int value) => BitConverter.GetBytes(value);

    private static void WriteInt(Stream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteUShort(Stream stream, ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
