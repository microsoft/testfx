// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HangDump.Serializers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpSerializersTests
{
    private const string SerializeMethodName = "Serialize";
    private const string DeserializeMethodName = "Deserialize";

    [TestMethod]
    public void Serializers_HaveExpectedIds()
    {
        Assert.AreEqual(7, new ActivitySignalRequestSerializer().Id);
        Assert.AreEqual(3, new ConsumerPipeNameRequestSerializer().Id);
        Assert.AreEqual(4, new GetInProgressTestsRequestSerializer().Id);
        Assert.AreEqual(5, new GetInProgressTestsResponseSerializer().Id);
    }

    [TestMethod]
    public void ActivitySignalRequest_RoundTripsSingletonWithEmptyPayload()
    {
        var serializer = new ActivitySignalRequestSerializer();
        using var stream = new MemoryStream();

        Serialize(serializer, ActivitySignalRequest.Instance, stream);

        Assert.AreEqual(0, stream.Length);
        stream.Position = 0;
        ActivitySignalRequest result = Deserialize<ActivitySignalRequest>(serializer, stream);
        Assert.AreSame(ActivitySignalRequest.Instance, result);
    }

    [TestMethod]
    public void ConsumerPipeNameRequest_RoundTripsPipeName()
    {
        var request = new ConsumerPipeNameRequest("hang-dump-consumer-pipe");

        ConsumerPipeNameRequest result = RoundTrip(new ConsumerPipeNameRequestSerializer(), request);

        Assert.AreEqual("hang-dump-consumer-pipe", result.PipeName);
    }

    [TestMethod]
    public void ConsumerPipeNameRequest_RoundTripsEmptyPipeName()
    {
        var request = new ConsumerPipeNameRequest(string.Empty);

        ConsumerPipeNameRequest result = RoundTrip(new ConsumerPipeNameRequestSerializer(), request);

        Assert.AreEqual(string.Empty, result.PipeName);
    }

    [TestMethod]
    public void GetInProgressTestsRequest_SerializesAsEmptyRecordAndDeserializes()
    {
        var serializer = new GetInProgressTestsRequestSerializer();
        using var stream = new MemoryStream();

        Serialize(serializer, new GetInProgressTestsRequest(), stream);

        Assert.AreEqual(0, stream.Length);
        stream.Position = 0;
        Assert.IsNotNull(Deserialize<GetInProgressTestsRequest>(serializer, stream));
    }

    [TestMethod]
    public void GetInProgressTestsResponse_RoundTripsMultipleEntries()
    {
        (string, int)[] expectedTests =
        [
            ("first test", 0),
            ("second test", int.MaxValue),
        ];
        var response = new GetInProgressTestsResponse(expectedTests);

        GetInProgressTestsResponse result = RoundTrip(new GetInProgressTestsResponseSerializer(), response);

        Assert.AreSequenceEqual(expectedTests, result.Tests);
    }

    [TestMethod]
    public void GetInProgressTestsResponse_RoundTripsEmptyArray()
    {
        var response = new GetInProgressTestsResponse([]);

        GetInProgressTestsResponse result = RoundTrip(new GetInProgressTestsResponseSerializer(), response);

        Assert.IsEmpty(result.Tests);
    }

    [TestMethod]
    public void GetInProgressTestsResponse_RoundTripsUnicodeTestName()
    {
        const string ExpectedTestName = "测试方法_Grüße_🚀";
        const int ExpectedUnixTimeSeconds = 1_700_000_000;
        var response = new GetInProgressTestsResponse([(ExpectedTestName, ExpectedUnixTimeSeconds)]);

        GetInProgressTestsResponse result = RoundTrip(new GetInProgressTestsResponseSerializer(), response);

        Assert.HasCount(1, result.Tests);
        (string testName, int unixTimeSeconds) = result.Tests[0];
        Assert.AreEqual(ExpectedTestName, testName);
        Assert.AreEqual(ExpectedUnixTimeSeconds, unixTimeSeconds);
    }

    // The serializer base type and interface are [Embedded], so their members are hidden from consumer compilation
    // even though the HangDump assembly grants this test assembly internals access.
    private static TMessage RoundTrip<TMessage>(object serializer, TMessage message)
    {
        using var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Position = 0;
        return Deserialize<TMessage>(serializer, stream);
    }

    private static void Serialize<TMessage>(object serializer, TMessage message, Stream stream)
    {
        MethodInfo method = GetSerializerMethod(
            serializer,
            SerializeMethodName,
            candidate => candidate.Name == SerializeMethodName
                && candidate.GetParameters() is [{ ParameterType: var messageType }, { ParameterType: var streamType }]
                && messageType == typeof(TMessage)
                && streamType == typeof(Stream));
        _ = method.Invoke(serializer, [message!, stream]);
    }

    private static TMessage Deserialize<TMessage>(object serializer, Stream stream)
    {
        MethodInfo method = GetSerializerMethod(
            serializer,
            DeserializeMethodName,
            candidate => candidate.Name == DeserializeMethodName
                && candidate.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(Stream));
        return (TMessage)method.Invoke(serializer, [stream])!;
    }

    private static MethodInfo GetSerializerMethod(object serializer, string methodName, Func<MethodInfo, bool> predicate)
    {
        MethodInfo[] methods = serializer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(predicate)
            .ToArray();

        return methods.Length switch
        {
            1 => methods[0],
            0 => throw new InvalidOperationException(
                $"No matching '{methodName}' method was found on serializer '{serializer.GetType().FullName}'."),
            _ => throw new InvalidOperationException(
                $"Multiple matching '{methodName}' methods were found on serializer '{serializer.GetType().FullName}'."),
        };
    }
}
