// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetrySerializersTests
{
    [TestMethod]
    public void FailedTestRequest_RoundTrips()
    {
        var request = new FailedTestRequest("test-uid", "Test display name");

        FailedTestRequest result = RoundTrip(new FailedTestRequestSerializer(), request);

        Assert.AreEqual(request.Uid, result.Uid);
        Assert.AreEqual(request.DisplayName, result.DisplayName);
    }

    [TestMethod]
    public void GetListOfFailedTestsRequest_SerializesAsEmptyRecordAndDeserializes()
    {
        var serializer = new GetListOfFailedTestsRequestSerializer();
        using var stream = new MemoryStream();

        Serialize(serializer, new GetListOfFailedTestsRequest(), stream);

        Assert.AreEqual(0, stream.Length);
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

    [TestMethod]
    public void TestRunCountsRequest_ComputesExecutedAndTotalTests()
    {
        var request = new TestRunCountsRequest(7, 2, 1, []);

        Assert.AreEqual(9, request.ExecutedTests);
        Assert.AreEqual(10, request.TotalTests);
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
    public void ArtifactRequest_RoundTripsNullKind()
    {
        var request = new ArtifactRequest("results.xml", null);

        ArtifactRequest result = RoundTrip(new ArtifactRequestSerializer(), request);

        Assert.AreEqual(request.Path, result.Path);
        Assert.IsNull(result.Kind);
    }

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
            .Single(method => method.Name == nameof(Serialize)
                && method.GetParameters() is [{ ParameterType: var messageType }, { ParameterType: var streamType }]
                && messageType == typeof(TMessage)
                && streamType == typeof(Stream))
            .Invoke(serializer, [message!, stream]);

    private static TMessage Deserialize<TMessage>(object serializer, Stream stream)
        => (TMessage)serializer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == nameof(Deserialize)
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(Stream))
            .Invoke(serializer, [stream])!;
}
