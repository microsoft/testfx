// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias serverclient;

using System.Collections;
using System.Reflection;
using System.Text;
#if NETCOREAPP
using System.Text.Json;

using Microsoft.Testing.Platform.Helpers;
#endif

using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.ServerMode.Json;

using AssertionFailureProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.AssertionFailureProperty;
using FailedTestNodeStateProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.FailedTestNodeStateProperty;
using IProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.IProperty;
using PassedTestNodeStateProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.PassedTestNodeStateProperty;
#if NETCOREAPP
using PlatformJson = Microsoft.Testing.Platform.ServerMode.Json.Json;
using PlatformJsonSerializer = Microsoft.Testing.Platform.ServerMode.Json.JsonSerializer;
#endif

using PropertyBag = serverclient::Microsoft.Testing.Platform.Extensions.Messages.PropertyBag;
using SessionUid = serverclient::Microsoft.Testing.Platform.TestHost.SessionUid;
using TestFileLocationProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestFileLocationProperty;
using TestMetadataProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestMetadataProperty;
using TestNode = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestNode;
using TestNodeStateProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestNodeStateProperty;
using TestNodeUid = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestNodeUid;
using TestNodeUpdateMessage = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestNodeUpdateMessage;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

/// <remarks>
/// Only <see cref="RegisterClientSerializers_WhenUnregistered_PopulatesTablesAndPublishesRegistration"/> changes
/// the process-global serializer registry and registration flag. Its method-level
/// <see cref="DoNotParallelizeAttribute"/> keeps those changes isolated from parallel readers.
/// The already-registered probe holds the registration lock without changing the registry.
/// </remarks>
[TestClass]
public sealed class SerializationMutationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void RegisterClientSerializers_WhenAlreadyRegistered_DoesNotAcquireRegistrationLock()
    {
        SerializerUtilities.RegisterClientSerializers();
        object registrationLock = GetStaticField<object>(typeof(SerializerUtilities), "ClientSerializersLock");
        Task? registration = null;

        Monitor.Enter(registrationLock);
        try
        {
            // A pool-backed probe can starve while this test blocks a parallel worker holding the lock.
            registration = Task.Factory.StartNew(
                SerializerUtilities.RegisterClientSerializers,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Assert.IsTrue(
                registration.Wait(5_000, TestContext.CancellationToken),
                "Registration should return before the held lock is released.");
        }
        finally
        {
            Monitor.Exit(registrationLock);
            if (registration is not null)
            {
                // Drain the probe even if the test was canceled while waiting with the lock held.
                Assert.IsTrue(
                    registration.Wait(5_000, CancellationToken.None),
                    "Registration should complete after the lock is released.");
            }
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void RegisterClientSerializers_WhenUnregistered_PopulatesTablesAndPublishesRegistration()
    {
        FieldInfo registeredField = GetStaticField(typeof(SerializerUtilities), "s_clientSerializersRegistered");
        IDictionary serializers = GetStaticField<IDictionary>(typeof(SerializerUtilities), "Serializers");
        MethodInfo registerCore = GetStaticMethod(typeof(SerializerUtilities), "RegisterClientSerializersCore");

        registeredField.SetValue(null, false);
        serializers.Remove(typeof(ClientInfo));
        try
        {
            SerializerUtilities.RegisterClientSerializers();

            Assert.IsTrue(serializers.Contains(typeof(ClientInfo)));
            Assert.IsTrue(Assert.IsInstanceOfType<bool>(registeredField.GetValue(null)));
        }
        finally
        {
            registerCore.Invoke(null, null);
            registeredField.SetValue(null, true);
        }
    }

    [TestMethod]
    public void SerializerUtilities_StaticRegistration_ContainsRpcTestNodeAndDeserializerEntries()
    {
        IDictionary<string, object?> pair = SerializerUtilities.Serialize(new KeyValuePair<string, string>("key", "value"));
        Assert.HasCount(1, pair);
        Assert.AreEqual("value", pair["key"]);

        IDictionary<string, object?> notification = SerializerUtilities.Serialize(
            new NotificationMessage("custom/notification", new Dictionary<string, object?>()));
        Assert.AreEqual("2.0", notification[JsonRpcStrings.JsonRpc]);
        Assert.AreEqual("custom/notification", notification[JsonRpcStrings.Method]);

        ClientInfo clientInfo = SerializerUtilities.Deserialize<ClientInfo>(new Dictionary<string, object?>
        {
            [JsonRpcStrings.ClientInfo] = new Dictionary<string, object?>
            {
                [JsonRpcStrings.Name] = "client",
                [JsonRpcStrings.Version] = "1.2.3",
            },
        });
        Assert.AreEqual("client", clientInfo.Name);
        Assert.AreEqual("1.2.3", clientInfo.Version);
    }

    [TestMethod]
    public void SerializerUtilities_SerializeNotification_PreservesNullAndNonNullParams()
    {
        IDictionary<string, object?> withoutParams = SerializerUtilities.Serialize(
            new NotificationMessage("custom/withoutParams", null));
        Assert.IsNull(withoutParams[JsonRpcStrings.Params]);

        var parameters = new Dictionary<string, object?>
        {
            ["value"] = 42,
        };
        IDictionary<string, object?> withParams = SerializerUtilities.Serialize(
            new NotificationMessage("custom/withParams", parameters));
        Assert.AreSame(parameters, withParams[JsonRpcStrings.Params]);
    }

    [TestMethod]
    public void SerializerUtilities_DeserializeRpcMessage_NonCanonicalStringIdThrows()
    {
        var properties = new Dictionary<string, object?>
        {
            [JsonRpcStrings.JsonRpc] = "2.0",
            [JsonRpcStrings.Id] = "01",
            [JsonRpcStrings.Method] = "client/request",
        };

        Assert.ThrowsExactly<MessageFormatException>(() => SerializerUtilities.Deserialize<RpcMessage>(properties));
    }

    [TestMethod]
    public void SerializerUtilities_DeserializeRpcMessage_NullMethodThrows()
    {
        var properties = new Dictionary<string, object?>
        {
            [JsonRpcStrings.JsonRpc] = "2.0",
            [JsonRpcStrings.Method] = null,
        };

        Assert.ThrowsExactly<MessageFormatException>(() => SerializerUtilities.Deserialize<RpcMessage>(properties));
    }

    [TestMethod]
    public void SerializerUtilities_SerializeGroupTestNode_UsesOnlyExpectedWireProperties()
    {
        IDictionary<string, object?> serialized = SerializerUtilities.Serialize(CreateNode());

        Assert.HasCount(3, serialized);
        Assert.AreEqual("node", serialized[JsonRpcStrings.Uid]);
        Assert.AreEqual("Node", serialized[JsonRpcStrings.DisplayName]);
        Assert.AreEqual("group", serialized["node-type"]);
        Assert.IsFalse(serialized.ContainsKey("traits"));
        Assert.IsFalse(serialized.ContainsKey(string.Empty));
    }

    [TestMethod]
    public void SerializerUtilities_SerializeTestNodeWithTrait_IncludesTrait()
    {
        TestNode node = CreateNode(
            new TestMetadataProperty("category", "fast"),
            new TestMetadataProperty("owner", "platform"));

        IDictionary<string, object?> serialized = SerializerUtilities.Serialize(node);

        Assert.HasCount(4, serialized);
#if NETCOREAPP
        List<KeyValuePair<string, string>> traits = Assert.IsInstanceOfType<List<KeyValuePair<string, string>>>(serialized["traits"]);
        Assert.HasCount(2, traits);
        Assert.Contains(new KeyValuePair<string, string>("category", "fast"), traits);
        Assert.Contains(new KeyValuePair<string, string>("owner", "platform"), traits);
#else
        IList traits = Assert.IsInstanceOfType<IList>(serialized["traits"]);
        Assert.HasCount(2, traits);
        string serializedTraits = string.Join(Environment.NewLine, traits.Cast<object>());
        Assert.Contains("category", serializedTraits);
        Assert.Contains("fast", serializedTraits);
        Assert.Contains("owner", serializedTraits);
        Assert.Contains("platform", serializedTraits);
#endif
        Assert.IsFalse(serialized.ContainsKey(string.Empty));
    }

    [TestMethod]
    public void SerializerUtilities_SerializePassedTestNode_DoesNotInspectAssertionFailureProperties()
    {
        TestNode node = CreateNode(
            PassedTestNodeStateProperty.CachedInstance,
            new AssertionFailureProperty("expected-1", "actual-1"),
            new AssertionFailureProperty("expected-2", "actual-2"));

        IDictionary<string, object?> serialized = SerializerUtilities.Serialize(node);

        Assert.AreEqual("action", serialized["node-type"]);
        Assert.AreEqual("passed", serialized["execution-state"]);
        Assert.IsFalse(serialized.ContainsKey("assert.expected"));
        Assert.IsFalse(serialized.ContainsKey("assert.actual"));
    }

    [TestMethod]
    public void SerializerUtilities_SerializeFailedTestNode_UsesStructuredAssertionFailure()
    {
        TestNode node = CreateNode(
            new FailedTestNodeStateProperty(new InvalidOperationException("exception"), "failure"),
            new AssertionFailureProperty("expected", "actual"));

        IDictionary<string, object?> serialized = SerializerUtilities.Serialize(node);

        Assert.AreEqual("action", serialized["node-type"]);
        Assert.AreEqual("failed", serialized["execution-state"]);
        Assert.AreEqual("failure", serialized["error.message"]);
        Assert.AreEqual(string.Empty, serialized["error.stacktrace"]);
        Assert.AreEqual("expected", serialized["assert.expected"]);
        Assert.AreEqual("actual", serialized["assert.actual"]);
        Assert.IsFalse(serialized.ContainsKey(string.Empty));
    }

    [TestMethod]
    public void SerializerUtilities_FormatException_PreservesExplanationStackTraceAndInnerExceptions()
    {
        Exception thrown = CaptureException();
        (string? message, string? stackTrace) = SerializerUtilities.FormatException("explanation", thrown);

        Assert.AreEqual("explanation", message);
        Assert.IsNotNull(stackTrace);
        Assert.Contains(nameof(CaptureException), stackTrace);

        var firstException = new FixedStackTraceException("first", "first-stack");
        var secondException = new FixedStackTraceException("second", "second-stack");
        var aggregateException = new AggregateException("aggregate", firstException, secondException);
        (message, stackTrace) = SerializerUtilities.FormatException("aggregate explanation", aggregateException);

        Assert.AreEqual(
            string.Join(
                Environment.NewLine,
                "aggregate explanation",
                $" ---> {typeof(FixedStackTraceException).FullName}: first",
                $" ---> {typeof(FixedStackTraceException).FullName}: second"),
            message);
        Assert.AreEqual(
            string.Join(
                Environment.NewLine,
                $"--- Inner exception stack trace ({typeof(FixedStackTraceException).FullName}) ---",
                "first-stack",
                $"--- Inner exception stack trace ({typeof(FixedStackTraceException).FullName}) ---",
                "second-stack"),
            stackTrace);

        var innerException = new FixedStackTraceException("inner", "inner-stack");
        var outerException = new FixedStackTraceException(string.Empty, "outer-stack", innerException);
        (message, stackTrace) = SerializerUtilities.FormatException(null, outerException);

        Assert.AreEqual($" ---> {typeof(FixedStackTraceException).FullName}: inner", message);
        Assert.AreEqual(
            string.Join(
                Environment.NewLine,
                "outer-stack",
                $"--- Inner exception stack trace ({typeof(FixedStackTraceException).FullName}) ---",
                "inner-stack"),
            stackTrace);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task Json_Constructor_PreservesDefaultSerializerAndAddsCustomDeserializer()
    {
        var serializers = new Dictionary<Type, PlatformJsonSerializer>
        {
            [typeof(string)] = new JsonValueSerializer<string>((writer, _) => writer.WriteStringValue("replacement")),
        };
        var deserializers = new Dictionary<Type, JsonDeserializer>
        {
            [typeof(Marker)] = new JsonElementDeserializer<Marker>((_, element) => new Marker(element.GetProperty("value").GetInt32())),
        };
        var json = new PlatformJson(serializers, deserializers);

        Assert.AreEqual("\"original\"", await json.SerializeAsync("original"));
        Marker marker = json.Deserialize<Marker>(Encoding.UTF8.GetBytes("""{"value":42}"""));
        Assert.AreEqual(42, marker.Value);
    }

    [TestMethod]
    public async Task Json_SerializeAsync_ReturnsMemoryStreamToPool()
    {
        var json = new PlatformJson();
        object pool = GetInstanceField<object>(json, "_memoryStreamPool");
        FieldInfo firstItem = GetInstanceField(pool.GetType(), "_firstItem");

        Assert.IsNull(firstItem.GetValue(pool));
        Assert.AreEqual("\"value\"", await json.SerializeAsync("value"));
        Assert.IsNotNull(firstItem.GetValue(pool));
    }

    [TestMethod]
    public async Task Json_SerializeAsync_DoesNotCaptureSynchronizationContextForFlush()
    {
        var stream = new DelayedFlushMemoryStream();
        var pool = new ObjectPool<MemoryStream>(() => stream, size: 1);
        var json = new PlatformJson();
        GetInstanceField(json.GetType(), "_memoryStreamPool").SetValue(json, pool);
        var context = new RecordingSynchronizationContext();

        SynchronizationContext? previous = SynchronizationContext.Current;
        Task<string> serialization;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            serialization = json.SerializeAsync("value");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await stream.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        stream.AllowWrite.SetResult();

        Assert.AreEqual("\"value\"", await serialization.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        Assert.AreEqual(0, context.PostCount);
    }

    [TestMethod]
    public void Json_TryArrayBind_ReportsMissingAndPresentArraysAccurately()
    {
        var json = new PlatformJson();
        using var missingDocument = JsonDocument.Parse("{}");
        using var presentDocument = JsonDocument.Parse("[1,2]");

        Assert.IsFalse(json.TryArrayBind(missingDocument.RootElement, out int[]? missing, "items"));
        Assert.IsNull(missing);
        Assert.IsTrue(json.TryArrayBind(presentDocument.RootElement, out int[]? present));
        Assert.AreSequenceEqual([1, 2], present);
    }

    [TestMethod]
    public void Json_DeserializeRpcMessage_ValidatesHeaderAndUnknownScalarParams()
    {
        var json = new PlatformJson();

        MessageFormatException invalidHeader = Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<RpcMessage>(Encoding.UTF8.GetBytes("""{"jsonrpc":"1.0","method":"custom"}""")));
        Assert.AreEqual("jsonrpc field is not valid", invalidHeader.Message);

        RpcMessage message = json.Deserialize<RpcMessage>(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"custom","params":"text"}"""));
        NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(message);
        Assert.IsNull(notification.Params);
    }

    [TestMethod]
    public void Json_DeserializeRpcMessage_PreservesObjectParamsAndDistinguishesRequestsAndResponses()
    {
        var json = new PlatformJson();

        RpcMessage notificationMessage = json.Deserialize<RpcMessage>(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"custom","params":{"value":42}}"""));
        NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(notificationMessage);
        IDictionary<string, object?> notificationParams = Assert.IsInstanceOfType<IDictionary<string, object?>>(notification.Params);
        Assert.AreEqual(42, notificationParams["value"]);

        RpcMessage requestMessage = json.Deserialize<RpcMessage>(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":"7","method":"custom","params":{"value":42}}"""));
        RequestMessage request = Assert.IsInstanceOfType<RequestMessage>(requestMessage);
        Assert.AreEqual(7, request.Id);
        Assert.AreEqual("7", request.StringId);

        RpcMessage responseMessage = json.Deserialize<RpcMessage>(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":"8","result":{"success":true}}"""));
        ResponseMessage response = Assert.IsInstanceOfType<ResponseMessage>(responseMessage);
        Assert.AreEqual(8, response.Id);
        Assert.AreEqual("8", response.StringId);
        IDictionary<string, object?> result = Assert.IsInstanceOfType<IDictionary<string, object?>>(response.Result);
        Assert.IsTrue(Assert.IsInstanceOfType<bool>(result["success"]));

        RpcMessage nullResponseMessage = json.Deserialize<RpcMessage>(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":9,"result":null}"""));
        ResponseMessage nullResponse = Assert.IsInstanceOfType<ResponseMessage>(nullResponseMessage);
        Assert.AreEqual(9, nullResponse.Id);
        Assert.IsNull(nullResponse.Result);
    }

    [TestMethod]
    public void Json_DeserializeClientCapabilities_PreservesDeclaredStatefulness()
    {
        var json = new PlatformJson();

        ClientCapabilities capabilities = json.Deserialize<ClientCapabilities>(
            Encoding.UTF8.GetBytes("""{"testing":{"debuggerProvider":false,"isStateful":true}}"""));

        Assert.IsFalse(capabilities.DebuggerProvider);
        Assert.IsTrue(capabilities.IsStateful);
    }

    [TestMethod]
    public void Json_DeserializeInitializeRequest_ValidatesProtocolVersionEntries()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            """{"processId":1,"clientInfo":{"name":"client","version":"1"},"capabilities":{"testing":{"debuggerProvider":false}},"protocolVersions":["1.0",null]}""");

        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(
            () => DeserializeClientJson<InitializeRequestArgs>(payload));
        Assert.IsNotNull(exception.InnerException);
        Assert.AreEqual(
            "Microsoft.Testing.Platform.ServerMode.MessageFormatException",
            exception.InnerException.GetType().FullName);
        Assert.AreEqual(
            $"'{JsonRpcStrings.ProtocolVersions}' entries must be strings",
            exception.InnerException.Message);

        object valid = DeserializeClientJson<InitializeRequestArgs>(Encoding.UTF8.GetBytes(
            """{"processId":1,"clientInfo":{"name":"client","version":"1"},"capabilities":{"testing":{"debuggerProvider":false}},"protocolVersions":["1.0"]}"""));
        Assert.AreSequenceEqual(
            ["1.0"],
            (string[]?)valid.GetType().GetProperty(nameof(InitializeRequestArgs.ProtocolVersions))!.GetValue(valid));
    }

    [TestMethod]
    public void Json_DeserializeTestNode_RequiresCompleteLocationAndAcceptsCompleteLocation()
    {
        var json = new PlatformJson();
        const string prefix = """{"uid":"node","display-name":"Node",""";

        MessageFormatException missingFile = Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<TestNode>(Encoding.UTF8.GetBytes(prefix + "\"location.line-start\":1}")));
        Assert.AreEqual(
            "'location.file', 'location.line-start', and 'location.line-end' fields must be specified together",
            missingFile.Message);
        Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<TestNode>(Encoding.UTF8.GetBytes(prefix + "\"location.line-end\":2}")));
        MessageFormatException missingEnd = Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<TestNode>(Encoding.UTF8.GetBytes(prefix + "\"location.file\":\"test.cs\",\"location.line-start\":1}")));
        Assert.AreEqual(
            "'location.file', 'location.line-start', and 'location.line-end' fields must be specified together",
            missingEnd.Message);
        Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<TestNode>(Encoding.UTF8.GetBytes(prefix + "\"location.file\":\"test.cs\",\"location.line-end\":2}")));

        TestNode node = json.Deserialize<TestNode>(
            Encoding.UTF8.GetBytes(prefix + "\"location.file\":\"test.cs\",\"location.line-start\":1,\"location.line-end\":2}"));
        TestFileLocationProperty location = node.Properties.Single<TestFileLocationProperty>();
        Assert.AreEqual("test.cs", location.FilePath);
        Assert.AreEqual(1, location.LineSpan.Start.Line);
        Assert.AreEqual(2, location.LineSpan.End.Line);
    }

    [TestMethod]
    public void Json_DeserializeCancelAndErrorMessages_PreservesStringIdsAndNormalizesData()
    {
        var json = new PlatformJson();

        CancelRequestArgs cancel = json.Deserialize<CancelRequestArgs>(Encoding.UTF8.GetBytes("""{"id":"17"}"""));
        Assert.AreEqual(17, cancel.CancelRequestId);
        Assert.AreEqual("17", cancel.StringId);
        CancelRequestArgs numericCancel = json.Deserialize<CancelRequestArgs>(Encoding.UTF8.GetBytes("""{"id":18}"""));
        Assert.AreEqual(18, numericCancel.CancelRequestId);
        Assert.IsNull(numericCancel.StringId);

        ErrorMessage emptyData = json.Deserialize<ErrorMessage>(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":"23","error":{"code":-1,"message":"failure","data":{}}}"""));
        Assert.AreEqual("23", emptyData.StringId);
        Assert.AreEqual("failure", emptyData.Message);
        Assert.IsNull(emptyData.Data);

        ErrorMessage populatedData = json.Deserialize<ErrorMessage>(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":24,"error":{"code":-1,"message":"failure","data":{"detail":"value"}}}"""));
        IDictionary<string, object?> data = Assert.IsInstanceOfType<IDictionary<string, object?>>(populatedData.Data);
        Assert.AreEqual("value", data["detail"]);

        Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<ErrorMessage>(Encoding.UTF8.GetBytes("""{"jsonrpc":"1.0","id":1,"error":{"code":-1,"message":"failure"}}""")));
    }

    [TestMethod]
    public void Json_DeserializeUntypedNumbers_PreservesNarrowestNumericTypes()
    {
        var json = new PlatformJson();
        IDictionary<string, object?> values = json.Deserialize<IDictionary<string, object?>>(
            Encoding.UTF8.GetBytes("""{"int":42,"long":2147483648,"ulong":9223372036854775808}"""));

        Assert.IsInstanceOfType<int>(values["int"]);
        Assert.AreEqual(42, values["int"]);
        Assert.IsInstanceOfType<long>(values["long"]);
        Assert.AreEqual(2147483648L, values["long"]);
        Assert.IsInstanceOfType<ulong>(values["ulong"]);
        Assert.AreEqual(9223372036854775808UL, values["ulong"]);
    }

    [TestMethod]
    public void Json_DeserializeRpcMessage_RejectsNonCanonicalStringId()
    {
        var json = new PlatformJson();

        MessageFormatException exception = Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<RpcMessage>(Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":"01","method":"custom"}""")));
        Assert.AreEqual($"'{JsonRpcStrings.Id}' field should be an int or a numeric string", exception.Message);
    }

    [TestMethod]
    public async Task Json_SerializeMessages_PreservesOptionalValuesAndStringIds()
    {
        var json = new PlatformJson();
        var data = new Dictionary<string, object> { ["detail"] = "value" };

        string errorJson = await json.SerializeAsync(new ErrorMessage(1, -1, "failure", data) { StringId = "001" });
        using var errorDocument = JsonDocument.Parse(errorJson);
        Assert.AreEqual("001", errorDocument.RootElement.GetProperty(JsonRpcStrings.Id).GetString());
        Assert.AreEqual("value", errorDocument.RootElement.GetProperty(JsonRpcStrings.Error).GetProperty(JsonRpcStrings.Data).GetProperty("detail").GetString());

        var capabilities = new ServerCapabilities(new ServerTestingCapabilities(false, false, false, false, false));
        string initializeJson = await json.SerializeAsync(new InitializeResponseArgs(1, new ServerInfo("server", "1"), capabilities));
        using var initializeDocument = JsonDocument.Parse(initializeJson);
        Assert.IsFalse(initializeDocument.RootElement.TryGetProperty(JsonRpcStrings.ProtocolVersion, out _));

        string cancelJson = await json.SerializeAsync(new CancelRequestArgs(2) { StringId = "002" });
        using var cancelDocument = JsonDocument.Parse(cancelJson);
        Assert.AreEqual("002", cancelDocument.RootElement.GetProperty(JsonRpcStrings.Id).GetString());
    }

    [TestMethod]
    public async Task Json_SerializeGroupAndTraitTestNodes_UsesExpectedWireProperties()
    {
        var json = new PlatformJson(new Dictionary<Type, PlatformJsonSerializer>
        {
            [typeof(KeyValuePair<string, string>)] = new JsonObjectSerializer<KeyValuePair<string, string>>(pair =>
            [
                (pair.Key, pair.Value),
            ]),
        });

        using var groupDocument = JsonDocument.Parse(await json.SerializeAsync(CreateNode()));
        JsonElement group = groupDocument.RootElement;
        Assert.HasCount(3, group.EnumerateObject());
        Assert.AreEqual("group", group.GetProperty("node-type").GetString());
        Assert.IsFalse(group.TryGetProperty("traits", out _));

        using var traitDocument = JsonDocument.Parse(
            await json.SerializeAsync(CreateNode(
                new TestMetadataProperty("category", "fast"),
                new TestMetadataProperty("owner", "platform"))));
        JsonElement traitNode = traitDocument.RootElement;
        JsonElement traits = traitNode.GetProperty("traits");
        Assert.AreEqual(2, traits.GetArrayLength());
        var traitValues = traits
            .EnumerateArray()
            .SelectMany(element => element.EnumerateObject())
            .ToDictionary(property => property.Name, property => property.Value.GetString());
        Assert.AreEqual("fast", traitValues["category"]);
        Assert.AreEqual("platform", traitValues["owner"]);
    }

    [TestMethod]
    public async Task Json_SerializePassedTestNode_DoesNotInspectAssertionFailureProperties()
    {
        TestNode node = CreateNode(
            PassedTestNodeStateProperty.CachedInstance,
            new AssertionFailureProperty("expected-1", "actual-1"),
            new AssertionFailureProperty("expected-2", "actual-2"));
        var json = new PlatformJson();

        using var document = JsonDocument.Parse(await json.SerializeAsync(node));

        Assert.AreEqual("action", document.RootElement.GetProperty("node-type").GetString());
        Assert.AreEqual("passed", document.RootElement.GetProperty("execution-state").GetString());
        Assert.IsFalse(document.RootElement.TryGetProperty("assert.expected", out _));
        Assert.IsFalse(document.RootElement.TryGetProperty("assert.actual", out _));
    }

    [TestMethod]
    public async Task Json_SerializeFailedTestNode_IncludesStructuredAssertionAndStackTrace()
    {
        TestNode node = CreateNode(
            new FailedTestNodeStateProperty(new InvalidOperationException("failure")),
            new AssertionFailureProperty("expected", "actual"));
        var json = new PlatformJson();

        using var document = JsonDocument.Parse(await json.SerializeAsync(node));
        JsonElement root = document.RootElement;

        Assert.AreEqual("failed", root.GetProperty("execution-state").GetString());
        Assert.AreEqual("failure", root.GetProperty("error.message").GetString());
        Assert.AreEqual("expected", root.GetProperty("assert.expected").GetString());
        Assert.AreEqual("actual", root.GetProperty("assert.actual").GetString());
        Assert.AreEqual(string.Empty, root.GetProperty("error.stacktrace").GetString());
    }
#endif

    [TestMethod]
    public void PropertyBag_SingleOrDefault_ReturnsStateAndSingleRegularProperty()
    {
        var metadata = new TestMetadataProperty("category", "fast");
        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, metadata);

        Assert.AreSame(PassedTestNodeStateProperty.CachedInstance, bag.SingleOrDefault<TestNodeStateProperty>());
        Assert.AreSame(metadata, bag.SingleOrDefault<TestMetadataProperty>());
    }

    [TestMethod]
    public void PropertyBag_SingleOrDefault_WithDuplicateRegularPropertiesThrows()
    {
        var bag = new PropertyBag(
            new TestMetadataProperty("category", "fast"),
            new TestMetadataProperty("owner", "platform"));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => bag.SingleOrDefault<TestMetadataProperty>());
        Assert.AreEqual(
            $"Found multiple properties of type '{typeof(TestMetadataProperty)}'.",
            exception.Message);
    }

    [TestMethod]
    public void PropertyBag_Single_WithNoMatchingPropertyThrows()
    {
        var bag = new PropertyBag();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => bag.Single<TestMetadataProperty>());
        Assert.AreEqual(
            $"Could not find a property of type '{typeof(TestMetadataProperty)}'.",
            exception.Message);
    }

    [TestMethod]
    public void PropertyBag_SingleOrDefault_StateLookupDoesNotWalkRegularPropertyList()
    {
        var bag = new PropertyBag
        {
            _property = new PropertyBag.Property(PassedTestNodeStateProperty.CachedInstance),
        };

        Assert.IsNull(bag.SingleOrDefault<TestNodeStateProperty>());
    }

    [TestMethod]
    public void PropertyBag_Enumerator_DisposeClearsCurrent()
    {
        IEnumerator<IProperty> enumerator = new PropertyBag(new TestMetadataProperty("category", "fast")).GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.IsNotNull(enumerator.Current);

        enumerator.Dispose();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => _ = enumerator.Current);
        Assert.AreEqual("Invalid Current state, possible wrong usage.", exception.Message);
    }

    [TestMethod]
    public void TestNodeUpdateMessage_UsesStableDisplayNameAndDescription()
    {
        var message = new TestNodeUpdateMessage(new SessionUid("session"), CreateNode());

        Assert.AreEqual("TestNode update", message.DisplayName);
        Assert.AreEqual("This data is used to report a TestNode state change.", message.Description);
    }

    private static TestNode CreateNode(params IProperty[] properties)
        => new()
        {
            Uid = new TestNodeUid("node"),
            DisplayName = "Node",
            Properties = new PropertyBag(properties),
        };

    private static Exception CaptureException()
    {
        try
        {
            throw new InvalidOperationException("captured");
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static FieldInfo GetStaticField(Type type, string name)
        => type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find static field '{name}' on '{type}'.");

    private static T GetStaticField<T>(Type type, string name)
        => (T)(GetStaticField(type, name).GetValue(null)
            ?? throw new InvalidOperationException($"Static field '{name}' on '{type}' is null."));

#if NETCOREAPP
    private static FieldInfo GetInstanceField(Type type, string name)
        => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find instance field '{name}' on '{type}'.");

    private static T GetInstanceField<T>(object instance, string name)
        => (T)(GetInstanceField(instance.GetType(), name).GetValue(instance)
            ?? throw new InvalidOperationException($"Instance field '{name}' on '{instance.GetType()}' is null."));
#endif

    private static MethodInfo GetStaticMethod(Type type, string name)
        => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find static method '{name}' on '{type}'.");

#if NETCOREAPP
    private static object DeserializeClientJson<T>(byte[] payload)
    {
        Assembly clientAssembly = typeof(TestNode).Assembly;
        Type jsonType = clientAssembly.GetType(
            "Microsoft.Testing.Platform.ServerMode.Json.Json",
            throwOnError: true)!;
        Type targetType = clientAssembly.GetType(typeof(T).FullName!, throwOnError: true)!;
        MethodInfo deserialize = jsonType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == "Deserialize" && method.IsGenericMethodDefinition);
        object json = Activator.CreateInstance(jsonType, [null, null])!;

        return deserialize.MakeGenericMethod(targetType).Invoke(json, [new ReadOnlyMemory<byte>(payload)])!;
    }
#endif

    private sealed record Marker(int Value);

    private sealed class FixedStackTraceException : Exception
    {
        private readonly string _stackTrace;

        public FixedStackTraceException(string message, string stackTrace, Exception? innerException = null)
            : base(message, innerException)
            => _stackTrace = stackTrace;

        public override string StackTrace => _stackTrace;
    }

#if NETCOREAPP
    private sealed class DelayedFlushMemoryStream : MemoryStream
    {
        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteStarted.SetResult();
            await AllowWrite.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
    }
#endif

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => _postCount;

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }
}
