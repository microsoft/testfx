// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ModuleInfoRequest and RunSummaryInfoRequest serializers are source-linked into both referenced MSBuild assemblies.
extern alias PlatformMSBuild;

using System.Reflection;

using ModuleInfoRequest = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.ModuleInfoRequest;
using ModuleInfoRequestSerializer = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.ModuleInfoRequestSerializer;
using RunSummaryInfoRequest = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.RunSummaryInfoRequest;
using RunSummaryInfoRequestSerializer = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.RunSummaryInfoRequestSerializer;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class MSBuildSerializersTests
{
    private const string SerializeMethodName = "Serialize";
    private const string DeserializeMethodName = "Deserialize";

    [TestMethod]
    public void ModuleInfoRequestSerializer_HasExpectedId()
        => Assert.AreEqual(1, new ModuleInfoRequestSerializer().Id);

    [TestMethod]
    public void RunSummaryInfoRequestSerializer_HasExpectedId()
        => Assert.AreEqual(3, new RunSummaryInfoRequestSerializer().Id);

    [TestMethod]
    public void ModuleInfoRequest_RoundTripsAllFields()
    {
        var request = new ModuleInfoRequest("framework", "architecture", "results");

        ModuleInfoRequest result = RoundTrip(new ModuleInfoRequestSerializer(), request);

        Assert.AreEqual(request.FrameworkDescription, result.FrameworkDescription);
        Assert.AreEqual(request.ProcessArchitecture, result.ProcessArchitecture);
        Assert.AreEqual(request.TestResultFolder, result.TestResultFolder);
    }

    [TestMethod]
    public void ModuleInfoRequest_RoundTripsEmptyStrings()
    {
        var request = new ModuleInfoRequest(string.Empty, string.Empty, string.Empty);

        ModuleInfoRequest result = RoundTrip(new ModuleInfoRequestSerializer(), request);

        Assert.AreEqual(string.Empty, result.FrameworkDescription);
        Assert.AreEqual(string.Empty, result.ProcessArchitecture);
        Assert.AreEqual(string.Empty, result.TestResultFolder);
    }

    [TestMethod]
    public void RunSummaryInfoRequest_RoundTripsAllFieldsWithDuration()
    {
        var request = new RunSummaryInfoRequest(10, 2, 6, 2, "00:00:01.234", AllowSkipped: true);

        RunSummaryInfoRequest result = RoundTrip(new RunSummaryInfoRequestSerializer(), request);

        Assert.AreEqual(request.Total, result.Total);
        Assert.AreEqual(request.TotalFailed, result.TotalFailed);
        Assert.AreEqual(request.TotalPassed, result.TotalPassed);
        Assert.AreEqual(request.TotalSkipped, result.TotalSkipped);
        Assert.AreEqual(request.Duration, result.Duration);
        Assert.AreEqual(request.AllowSkipped, result.AllowSkipped);
    }

    [TestMethod]
    public void RunSummaryInfoRequest_NormalizesNullDurationToEmptyString()
    {
        var request = new RunSummaryInfoRequest(10, 2, 6, 2, null, AllowSkipped: true);

        RunSummaryInfoRequest result = RoundTrip(new RunSummaryInfoRequestSerializer(), request);

        // Null duration is normalized to an empty string on the wire, so this is intentionally not a full round-trip.
        Assert.AreEqual(string.Empty, result.Duration);
    }

    [TestMethod]
    public void RunSummaryInfoRequest_RoundTripsAllowSkippedFalse()
    {
        var request = new RunSummaryInfoRequest(10, 2, 6, 2, "00:00:01.234", AllowSkipped: false);

        RunSummaryInfoRequest result = RoundTrip(new RunSummaryInfoRequestSerializer(), request);

        Assert.IsFalse(result.AllowSkipped);
    }

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
