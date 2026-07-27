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

    private static byte[] WriteString(string value) => Encoding.UTF8.GetBytes(value);

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
