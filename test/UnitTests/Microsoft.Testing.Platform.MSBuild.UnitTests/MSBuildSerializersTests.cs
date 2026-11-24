// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ModuleInfoRequest and RunSummaryInfoRequest serializers are source-linked into both referenced MSBuild assemblies.
extern alias PlatformMSBuild;

using System.Reflection;
using System.Text;

using Microsoft.Testing.Extensions.MSBuild;

using FailedTestInfoRequest = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.FailedTestInfoRequest;
using FailedTestInfoRequestSerializer = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.FailedTestInfoRequestSerializer;
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

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ModuleInfoRequestSerializer_UsesLengthPrefixedUtf8FieldsInProtocolOrder(bool extensionAssembly)
    {
        object request = CreateRequest(extensionAssembly, nameof(ModuleInfoRequest), ["framework Ω", "x64", @"Q:\结果"]);
        object serializer = CreateSerializer(extensionAssembly, nameof(ModuleInfoRequestSerializer));

        AssertWirePayload(serializer, request, writer =>
        {
            WriteWireString(writer, "framework Ω");
            WriteWireString(writer, "x64");
            WriteWireString(writer, @"Q:\结果");
        });
    }

    [TestMethod]
    [DataRow(false, false, 0)]
    [DataRow(false, true, int.MaxValue)]
    [DataRow(true, false, int.MinValue)]
    [DataRow(true, true, 42)]
    public void FailedTestInfoRequestSerializer_PreservesAllFieldsAndIntegerBooleanEncoding(bool extensionAssembly, bool canceled, int line)
    {
        object request = CreateRequest(
            extensionAssembly,
            nameof(FailedTestInfoRequest),
            ["测试", canceled, "1s 002ms", "failure Ω", "stack", "expected", "actual", @"Q:\tests.cs", line]);
        object serializer = CreateSerializer(extensionAssembly, nameof(FailedTestInfoRequestSerializer));

        Assert.AreEqual(2, serializer.GetType().GetProperty("Id")!.GetValue(serializer));
        AssertWirePayload(serializer, request, writer =>
        {
            WriteWireString(writer, "测试");
            writer.Write(canceled ? 1 : 0);
            WriteWireString(writer, "1s 002ms");
            WriteWireString(writer, "failure Ω");
            WriteWireString(writer, "stack");
            WriteWireString(writer, "expected");
            WriteWireString(writer, "actual");
            WriteWireString(writer, @"Q:\tests.cs");
            writer.Write(line);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedTestInfoRequestSerializer_NullOptionalFieldsBecomeEmptyStringsOnWire(bool extensionAssembly)
    {
        object request = CreateRequest(extensionAssembly, nameof(FailedTestInfoRequest), ["test", false, null, null, null, null, null, null, 0]);
        object serializer = CreateSerializer(extensionAssembly, nameof(FailedTestInfoRequestSerializer));

        using var stream = new MemoryStream();
        Serialize(serializer, request, stream);
        stream.Position = 0;
        object result = Deserialize<object>(serializer, stream);

        Assert.AreEqual("test", GetMessageProperty(result, nameof(FailedTestInfoRequest.DisplayName)));
        Assert.IsFalse((bool)GetMessageProperty(result, nameof(FailedTestInfoRequest.IsCanceled))!);
        foreach (string property in new[]
        {
            nameof(FailedTestInfoRequest.Duration),
            nameof(FailedTestInfoRequest.ErrorMessage),
            nameof(FailedTestInfoRequest.ErrorStackTrace),
            nameof(FailedTestInfoRequest.Expected),
            nameof(FailedTestInfoRequest.Actual),
            nameof(FailedTestInfoRequest.CodeFilePath),
        })
        {
            Assert.AreEqual(string.Empty, GetMessageProperty(result, property), property);
        }

        Assert.AreEqual(0, GetMessageProperty(result, nameof(FailedTestInfoRequest.LineNumber)));
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void RunSummaryInfoRequestSerializer_PreservesCounterOrderAndNormalizesMissingDuration(bool extensionAssembly, bool allowSkipped)
    {
        object request = CreateRequest(extensionAssembly, nameof(RunSummaryInfoRequest), [19, 3, 11, 5, null, allowSkipped]);
        object serializer = CreateSerializer(extensionAssembly, nameof(RunSummaryInfoRequestSerializer));

        AssertWirePayload(serializer, request, writer =>
        {
            writer.Write(19);
            writer.Write(3);
            writer.Write(11);
            writer.Write(5);
            WriteWireString(writer, string.Empty);
            writer.Write(allowSkipped ? 1 : 0);
        }, nameof(RunSummaryInfoRequest.Duration));
    }

    [TestMethod]
    [DataRow(false, 0, false)]
    [DataRow(false, 1, true)]
    [DataRow(false, -1, true)]
    [DataRow(true, 0, false)]
    [DataRow(true, 1, true)]
    [DataRow(true, -1, true)]
    public void RunSummaryInfoRequestSerializer_Deserialize_TreatsAnyNonzeroAllowSkippedAsTrue(bool extensionAssembly, int wireValue, bool expected)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        WriteWireString(writer, string.Empty);
        writer.Write(wireValue);
        stream.Position = 0;
        object serializer = CreateSerializer(extensionAssembly, nameof(RunSummaryInfoRequestSerializer));

        object result = Deserialize<object>(serializer, stream);

        Assert.AreEqual(expected, GetMessageProperty(result, nameof(RunSummaryInfoRequest.AllowSkipped)));
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    [DataRow(false, 0, false)]
    [DataRow(false, 1, true)]
    [DataRow(false, 2, false)]
    [DataRow(true, 0, false)]
    [DataRow(true, 1, true)]
    [DataRow(true, 2, false)]
    public void FailedTestInfoRequestSerializer_Deserialize_TreatsOnlyOneAsCanceled(bool extensionAssembly, int wireValue, bool expected)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteWireString(writer, "test");
        writer.Write(wireValue);
        for (int i = 0; i < 6; i++)
        {
            WriteWireString(writer, string.Empty);
        }

        writer.Write(0);
        stream.Position = 0;
        object serializer = CreateSerializer(extensionAssembly, nameof(FailedTestInfoRequestSerializer));

        object result = Deserialize<object>(serializer, stream);

        Assert.AreEqual(expected, GetMessageProperty(result, nameof(FailedTestInfoRequest.IsCanceled)));
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    [DataRow(false, nameof(ModuleInfoRequestSerializer))]
    [DataRow(false, nameof(FailedTestInfoRequestSerializer))]
    [DataRow(false, nameof(RunSummaryInfoRequestSerializer))]
    [DataRow(true, nameof(ModuleInfoRequestSerializer))]
    [DataRow(true, nameof(FailedTestInfoRequestSerializer))]
    [DataRow(true, nameof(RunSummaryInfoRequestSerializer))]
    public void Serializer_Deserialize_RejectsMissingPayload(bool extensionAssembly, string serializerName)
    {
        using var stream = new MemoryStream();
        object serializer = CreateSerializer(extensionAssembly, serializerName);

        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(
            () => Deserialize<object>(serializer, stream));

        Assert.IsInstanceOfType<EndOfStreamException>(exception.InnerException);
    }

    private static object CreateSerializer(bool extensionAssembly, string name)
        => Activator.CreateInstance(GetProtocolType(extensionAssembly, name))!;

    private static object CreateRequest(bool extensionAssembly, string name, object?[] arguments)
        => Activator.CreateInstance(GetProtocolType(extensionAssembly, name), arguments)!;

    private static Type GetProtocolType(bool extensionAssembly, string name)
    {
        Assembly assembly = extensionAssembly ? typeof(MSBuildConsumer).Assembly : typeof(ModuleInfoRequest).Assembly;
        return assembly.GetType($"Microsoft.Testing.Extensions.MSBuild.Serializers.{name}", throwOnError: true)!;
    }

    private static object? GetMessageProperty(object message, string name)
        => message.GetType().GetProperty(name)!.GetValue(message);

    private static void AssertWirePayload(object serializer, object request, Action<BinaryWriter> writeExpected, string? normalizedProperty = null)
    {
        using var expected = new MemoryStream();
        using (var writer = new BinaryWriter(expected, Encoding.UTF8, leaveOpen: true))
        {
            writeExpected(writer);
        }

        using var actual = new MemoryStream();
        Serialize(serializer, request, actual);
        Assert.AreSequenceEqual(expected.ToArray(), actual.ToArray());

        // Deserialize independently constructed bytes, not just the serializer's own output.
        expected.Position = 0;
        object result = Deserialize<object>(serializer, expected);
        foreach (PropertyInfo property in request.GetType().GetProperties())
        {
            object? expectedValue = property.Name == normalizedProperty ? string.Empty : property.GetValue(request);
            Assert.AreEqual(expectedValue, GetMessageProperty(result, property.Name), property.Name);
        }

        Assert.AreEqual(expected.Length, expected.Position);
    }

    private static void WriteWireString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
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
                && messageType == message!.GetType()
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
