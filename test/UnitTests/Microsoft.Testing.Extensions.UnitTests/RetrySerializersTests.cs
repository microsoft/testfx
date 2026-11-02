// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetrySerializersTests
{
    private const string SerializeMethodName = "Serialize";
    private const string DeserializeMethodName = "Deserialize";

    [TestMethod]
    public void FailedTestRequest_RoundTrips()
    {
        var request = new FailedTestRequest("test-uid", "Test display name");

        FailedTestRequest result = RoundTrip(new FailedTestRequestSerializer(), request);

        Assert.AreEqual(request.Uid, result.Uid);
        Assert.AreEqual(request.DisplayName, result.DisplayName);
    }

    [TestMethod]
    public void FailedTestRequest_SerializesUidBeforeDisplayName()
    {
        var serializer = new FailedTestRequestSerializer();
        using var stream = new MemoryStream();

        Serialize(serializer, new FailedTestRequest("test-uid", "display"), stream);
        stream.Position = 0;

        Assert.AreEqual("test-uid", ReadString(stream));
        Assert.AreEqual("display", ReadString(stream));
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    public void GetListOfFailedTestsRequest_SerializesAsEmptyRecordAndDeserializes()
    {
        var serializer = new GetListOfFailedTestsRequestSerializer();
        using var stream = new MemoryStream();

        Serialize(serializer, new GetListOfFailedTestsRequest(), stream);

        Assert.AreEqual(0, stream.Length);
        stream.Position = 0;
        Assert.IsNotNull(Deserialize<GetListOfFailedTestsRequest>(serializer, stream));
    }

    [TestMethod]
    public void GetListOfFailedTestsResponse_RoundTripsMultipleIds()
    {
        var response = new GetListOfFailedTestsResponse(["uid-1", "uid-2"]);

        GetListOfFailedTestsResponse result = RoundTrip(new GetListOfFailedTestsResponseSerializer(), response);

        Assert.AreSequenceEqual(response.FailedTestIds, result.FailedTestIds);
    }

    [TestMethod]
    public void GetListOfFailedTestsResponse_RoundTripsEmptyArray()
    {
        var response = new GetListOfFailedTestsResponse([]);

        GetListOfFailedTestsResponse result = RoundTrip(new GetListOfFailedTestsResponseSerializer(), response);

        Assert.IsEmpty(result.FailedTestIds);
    }

    [TestMethod]
    public void TestRunCountsRequest_RoundTripsCountsAndRecoveredUids()
    {
        var request = new TestRunCountsRequest(7, 2, 1, ["uid-1", "uid-2"]);

        TestRunCountsRequest result = RoundTrip(new TestRunCountsRequestSerializer(), request);

        Assert.AreEqual(request.PassedTests, result.PassedTests);
        Assert.AreEqual(request.FailedTests, result.FailedTests);
        Assert.AreEqual(request.SkippedTests, result.SkippedTests);
        Assert.AreSequenceEqual(request.RecoveredTestUids, result.RecoveredTestUids);
    }

    [TestMethod]
    public void TestRunCountsRequest_RoundTripsEmptyRecoveredUids()
    {
        var request = new TestRunCountsRequest(7, 2, 1, []);

        TestRunCountsRequest result = RoundTrip(new TestRunCountsRequestSerializer(), request);

        Assert.IsEmpty(result.RecoveredTestUids);
    }

    [DataRow(7, 2, 1, 9, 10)]
    [DataRow(2, 7, 3, 9, 12)]
    [TestMethod]
    public void TestRunCountsRequest_ComputesExecutedAndTotalTests(
        int passed,
        int failed,
        int skipped,
        int expectedExecuted,
        int expectedTotal)
    {
        var request = new TestRunCountsRequest(passed, failed, skipped, []);

        Assert.AreEqual(expectedExecuted, request.ExecutedTests);
        Assert.AreEqual(expectedTotal, request.TotalTests);
    }

    [TestMethod]
    public void TestRunCountsRequest_SerializesExactWireFormat()
    {
        var serializer = new TestRunCountsRequestSerializer();
        using var stream = new MemoryStream();

        Serialize(serializer, new TestRunCountsRequest(7, 2, 3, ["recovered"]), stream);
        stream.Position = 0;

        Assert.AreEqual(7, ReadInt(stream));
        Assert.AreEqual(2, ReadInt(stream));
        Assert.AreEqual(3, ReadInt(stream));
        Assert.AreEqual(1, ReadInt(stream));
        Assert.AreEqual("recovered", ReadString(stream));
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    public void TestRunCountsRequest_DeserializesEmptyRecoveredSetWithoutReadingPastCount()
    {
        var serializer = new TestRunCountsRequestSerializer();
        using var stream = new MemoryStream();
        WriteInt(stream, 1);
        WriteInt(stream, 2);
        WriteInt(stream, 3);
        WriteInt(stream, 0);
        stream.Position = 0;

        TestRunCountsRequest result = Deserialize<TestRunCountsRequest>(serializer, stream);

        Assert.IsEmpty(result.RecoveredTestUids);
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    public void ArtifactRequest_RoundTripsKind()
    {
        var request = new ArtifactRequest("results.trx", "microsoft.testing.trx");

        ArtifactRequest result = RoundTrip(new ArtifactRequestSerializer(), request);

        Assert.AreEqual(request.Path, result.Path);
        Assert.AreEqual(request.Kind, result.Kind);
    }

    [TestMethod]
    public void ArtifactRequest_SerializesExplicitKindPresenceMarker()
    {
        var serializer = new ArtifactRequestSerializer();
        using var stream = new MemoryStream();

        Serialize(serializer, new ArtifactRequest("results.trx", "microsoft.testing.trx"), stream);
        stream.Position = 0;

        Assert.AreEqual("results.trx", ReadString(stream));
        Assert.AreEqual(1, ReadInt(stream));
        Assert.AreEqual("microsoft.testing.trx", ReadString(stream));
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    public void ArtifactRequest_RoundTripsNullKind()
    {
        var request = new ArtifactRequest("results.xml", null);

        ArtifactRequest result = RoundTrip(new ArtifactRequestSerializer(), request);

        Assert.AreEqual(request.Path, result.Path);
        Assert.IsNull(result.Kind);
    }

    // The serializer base type and interface are [Embedded], so their members are hidden from consumer compilation
    // even though the Retry assembly grants this test assembly internals access.
    private static TMessage RoundTrip<TMessage>(object serializer, TMessage message)
    {
        using var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Position = 0;
        return Deserialize<TMessage>(serializer, stream);
    }

    private static void Serialize<TMessage>(object serializer, TMessage message, Stream stream)
        => serializer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == SerializeMethodName
                && method.GetParameters() is [{ ParameterType: var messageType }, { ParameterType: var streamType }]
                && messageType == typeof(TMessage)
                && streamType == typeof(Stream))
            .Invoke(serializer, [message!, stream]);

    private static TMessage Deserialize<TMessage>(object serializer, Stream stream)
        => (TMessage)serializer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == DeserializeMethodName
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(Stream))
            .Invoke(serializer, [stream])!;

    private static string ReadString(Stream stream)
    {
        int length = ReadInt(stream);
        byte[] valueBytes = new byte[length];
        Assert.AreEqual(valueBytes.Length, stream.Read(valueBytes, 0, valueBytes.Length));
        return Encoding.UTF8.GetString(valueBytes);
    }

    private static int ReadInt(Stream stream)
    {
        byte[] bytes = new byte[sizeof(int)];
        Assert.AreEqual(bytes.Length, stream.Read(bytes, 0, bytes.Length));
        return BitConverter.ToInt32(bytes, 0);
    }

    private static void WriteInt(Stream stream, int value)
        => stream.Write(BitConverter.GetBytes(value), 0, sizeof(int));
}
