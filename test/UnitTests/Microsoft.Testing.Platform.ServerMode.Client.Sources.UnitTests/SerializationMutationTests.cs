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
using ClientMessages = serverclient::Microsoft.Testing.Platform.Extensions.Messages;
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
/// The unregistered and blocked-registration probes change the process-global serializer registry and
/// registration flag. Their method-level <see cref="DoNotParallelizeAttribute"/> keeps those changes isolated
/// from parallel readers.
/// The already-registered probe holds the registration lock without changing the registry.
/// </remarks>
[TestClass]
public sealed class SerializationMutationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void FormatterUtilities_ReportsTheSelectedFrameworkFormatter()
#if NETCOREAPP
        => Assert.AreEqual("System.Text.Json", FormatterUtilities.CreateFormatter().Id);
#else
        => Assert.AreEqual("Jsonite", FormatterUtilities.CreateFormatter().Id);
#endif

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
    [DoNotParallelize]
    public void RegisterClientSerializers_PublishedWhileCallerWaits_PreservesThePublishedTable()
    {
        FieldInfo registered = GetStaticField(typeof(SerializerUtilities), "s_clientSerializersRegistered");
        object registrationLock = GetStaticField<object>(typeof(SerializerUtilities), "ClientSerializersLock");
        IDictionary serializers = GetStaticField<IDictionary>(typeof(SerializerUtilities), "Serializers");
        IDictionary deserializers = GetStaticField<IDictionary>(typeof(SerializerUtilities), "Deserializers");
        var savedSerializers = new Hashtable(serializers);
        var savedDeserializers = new Hashtable(deserializers);
        object? savedFlag = registered.GetValue(null);
        using var started = new ManualResetEventSlim();
        Exception? workerFailure = null;
        var worker = new Thread(() =>
        {
            started.Set();
            try
            {
                SerializerUtilities.RegisterClientSerializers();
            }
            catch (Exception exception)
            {
                workerFailure = exception;
            }
        });
        bool lockHeld = false;
        bool workerStarted = false;
        try
        {
            Monitor.Enter(registrationLock);
            lockHeld = true;
            registered.SetValue(null, false);
            worker.Start();
            workerStarted = true;
            var rendezvousLimit = TimeSpan.FromSeconds(30);
            Assert.IsTrue(started.Wait(rendezvousLimit, TestContext.CancellationToken));
            Assert.IsTrue(
                SpinWait.SpinUntil(
                    () => (worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,
                    rendezvousLimit),
                "The dedicated caller must be blocked on the held registration lock before publication.");

            serializers[typeof(ClientInfo)] = new ObjectSerializer<ClientInfo>(info => new Dictionary<string, object?>
            {
                ["name"] = info.Name,
                ["version"] = info.Version,
                ["published"] = true,
            });
            registered.SetValue(null, true);
            Monitor.Exit(registrationLock);
            lockHeld = false;
            Assert.IsTrue(worker.Join(rendezvousLimit), "The registration caller must finish after the lock is released.");
            Assert.IsNull(workerFailure, $"Registration failed: {workerFailure}");

            IDictionary<string, object?> serialized = SerializerUtilities.Serialize(new ClientInfo("client", "1"));
            Assert.HasCount(3, serialized);
            Assert.AreEqual("client", serialized["name"]);
            Assert.AreEqual("1", serialized["version"]);
            Assert.IsTrue(Assert.IsInstanceOfType<bool>(serialized["published"]));
        }
        finally
        {
            if (lockHeld)
            {
                Monitor.Exit(registrationLock);
            }

            if (workerStarted)
            {
                Assert.IsTrue(worker.Join(TimeSpan.FromSeconds(30)), "The dedicated registration caller must terminate before the table is restored.");
            }

            lock (registrationLock)
            {
                serializers.Clear();
                foreach (DictionaryEntry entry in savedSerializers)
                {
                    serializers[entry.Key] = entry.Value;
                }

                deserializers.Clear();
                foreach (DictionaryEntry entry in savedDeserializers)
                {
                    deserializers[entry.Key] = entry.Value;
                }

                registered.SetValue(null, savedFlag);
            }
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
    [DataRow(null)]
    [DataRow("1.0")]
    public void SerializerUtilities_SerializeInitializeResponse_PreservesIdentityAndOptionalProtocolVersion(string? protocolVersion)
    {
        var response = new InitializeResponseArgs(
            42,
            new ServerInfo("test-server", "4.3"),
            new ServerCapabilities(new ServerTestingCapabilities(true, false, true, false, true) { ShowMessage = false }))
        {
            ProtocolVersion = protocolVersion,
        };

        IDictionary<string, object?> serialized = SerializerUtilities.Serialize(response);

        Assert.AreEqual(42, serialized[JsonRpcStrings.ProcessId]);
        IDictionary<string, object?> server = Assert.IsInstanceOfType<IDictionary<string, object?>>(serialized[JsonRpcStrings.ServerInfo]);
        Assert.HasCount(2, server);
        Assert.AreEqual("test-server", server[JsonRpcStrings.Name]);
        Assert.AreEqual("4.3", server[JsonRpcStrings.Version]);
        IDictionary<string, object?> capabilities = Assert.IsInstanceOfType<IDictionary<string, object?>>(serialized[JsonRpcStrings.Capabilities]);
        Assert.HasCount(1, capabilities);
        IDictionary<string, object?> testing = Assert.IsInstanceOfType<IDictionary<string, object?>>(capabilities[JsonRpcStrings.Testing]);
        Assert.HasCount(7, testing);
        Assert.IsTrue(Assert.IsInstanceOfType<bool>(testing[JsonRpcStrings.SupportsDiscovery]));
        Assert.IsFalse(Assert.IsInstanceOfType<bool>(testing[JsonRpcStrings.MultiRequestSupport]));
        Assert.IsTrue(Assert.IsInstanceOfType<bool>(testing[JsonRpcStrings.VSTestProviderSupport]));
        Assert.IsFalse(Assert.IsInstanceOfType<bool>(testing[JsonRpcStrings.AttachmentsSupport]));
        Assert.IsTrue(Assert.IsInstanceOfType<bool>(testing[JsonRpcStrings.MultiConnectionProvider]));
        Assert.IsFalse(Assert.IsInstanceOfType<bool>(testing[JsonRpcStrings.SupportsTestCoverageMessages]));
        Assert.IsFalse(Assert.IsInstanceOfType<bool>(testing[JsonRpcStrings.ShowMessage]));
        if (protocolVersion is null)
        {
            Assert.HasCount(3, serialized);
            Assert.IsFalse(serialized.ContainsKey(JsonRpcStrings.ProtocolVersion));
        }
        else
        {
            Assert.HasCount(4, serialized);
            Assert.AreEqual("1.0", serialized[JsonRpcStrings.ProtocolVersion]);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SerializerUtilities_SerializeRpcEnvelopes_PreservesIdsAndPayloads(bool stringId)
    {
        var request = new RequestMessage(17, "testing/run", new ServerInfo("payload", "version")) { StringId = stringId ? "17" : null };
        IDictionary<string, object?> serialized = SerializerUtilities.Serialize(request);
        Assert.HasCount(4, serialized);
        Assert.AreEqual("2.0", serialized[JsonRpcStrings.JsonRpc]);
        Assert.AreEqual(stringId ? "17" : 17, serialized[JsonRpcStrings.Id]);
        Assert.AreEqual("testing/run", serialized[JsonRpcStrings.Method]);
        IDictionary<string, object?> parameters = Assert.IsInstanceOfType<IDictionary<string, object?>>(serialized[JsonRpcStrings.Params]);
        Assert.AreEqual("payload", parameters[JsonRpcStrings.Name]);
        Assert.AreEqual("version", parameters[JsonRpcStrings.Version]);

        serialized = SerializerUtilities.Serialize(new ResponseMessage(23, new ServerInfo("result", "2")) { StringId = stringId ? "23" : null });
        Assert.HasCount(3, serialized);
        Assert.AreEqual("2.0", serialized[JsonRpcStrings.JsonRpc]);
        Assert.AreEqual(stringId ? "23" : 23, serialized[JsonRpcStrings.Id]);
        IDictionary<string, object?> result = Assert.IsInstanceOfType<IDictionary<string, object?>>(serialized[JsonRpcStrings.Result]);
        Assert.AreEqual("result", result[JsonRpcStrings.Name]);
        Assert.AreEqual("2", result[JsonRpcStrings.Version]);

        serialized = SerializerUtilities.Serialize(new CancelRequestArgs(31) { StringId = stringId ? "31" : null });
        Assert.HasCount(1, serialized);
        Assert.AreEqual(stringId ? "31" : 31, serialized[JsonRpcStrings.Id]);

        Assert.IsNull(SerializerUtilities.Serialize(new RequestMessage(17, "custom", null))[JsonRpcStrings.Params]);
        Assert.IsNull(SerializerUtilities.Serialize(new ResponseMessage(23, null))[JsonRpcStrings.Result]);
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
    public void JsonRpcResponse_OlderPeerCanceledState_PreservesIdentityAndDiagnostics()
    {
        const string Payload = """
            {"jsonrpc":"2.0","id":"17","result":{"uid":"older-peer-node","display-name":"Older peer cancellation","node-type":"action","execution-state":"canceled","error.message":"Canceled by older peer"}}
            """;
        IMessageFormatter formatter = FormatterUtilities.CreateFormatter();
        // Exercise the shipping System.Text.Json path on modern .NET and Jsonite on net462.
#if NETCOREAPP
        RpcMessage message = formatter.Deserialize<RpcMessage>(Encoding.UTF8.GetBytes(Payload));
#else
        RpcMessage message = formatter.Deserialize<RpcMessage>(Payload);
#endif

        ResponseMessage response = Assert.IsInstanceOfType<ResponseMessage>(message);
        Assert.AreEqual(17, response.Id);
        Assert.AreEqual("17", response.StringId);
        IDictionary<string, object?> result = Assert.IsInstanceOfType<IDictionary<string, object?>>(response.Result);
        Assert.HasCount(5, result);
        Assert.AreEqual("older-peer-node", result["uid"]);
        Assert.AreEqual("Older peer cancellation", result["display-name"]);
        Assert.AreEqual("action", result["node-type"]);
        Assert.AreEqual("canceled", result["execution-state"]);
        Assert.AreEqual("Canceled by older peer", result["error.message"]);
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
        using var stream = new DelayedFlushMemoryStream();
        var pool = new ObjectPool<MemoryStream>(() => stream, size: 1);
        var json = new PlatformJson();
        GetInstanceField(json.GetType(), "_memoryStreamPool").SetValue(json, pool);
        var context = new RecordingSynchronizationContext();
        var completionTimeout = TimeSpan.FromSeconds(30);

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

        try
        {
            await stream.WriteStarted.Task.WaitAsync(completionTimeout, TestContext.CancellationToken);
        }
        finally
        {
            stream.AllowWrite.TrySetResult();
            await serialization.WaitAsync(completionTimeout, CancellationToken.None);
        }

        Assert.AreEqual("\"value\"", await serialization);
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

    [TestMethod]
    [DataRow(false)]
#if NETCOREAPP
    [DataRow(true)]
#endif
    public async Task TestNodeWireFields_PreserveLocationsOutputTimingRetryAndAttachmentAssociations(bool modern)
    {
        string firstPath = Path.GetFullPath("first attachment.txt");
        string secondPath = Path.GetFullPath("second attachment.txt");
        var start = new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.FromHours(2));
        TestNode node = CreateNode(
            new ClientMessages.TestFileLocationProperty("source.cs", new ClientMessages.LinePositionSpan(
                new ClientMessages.LinePosition(7, 2), new ClientMessages.LinePosition(9, 4))),
            new ClientMessages.TestMethodIdentifierProperty("assembly", "Tests", "Case", "Run", 1, ["System.String", "System.Int32"], "System.Void"),
            new ClientMessages.StandardOutputProperty("output <tag> & \"quoted\""),
            new ClientMessages.StandardErrorProperty("error"),
            new ClientMessages.TimingProperty(new ClientMessages.TimingInfo(start, start.AddSeconds(1), TimeSpan.FromTicks(35_000))),
            new ClientMessages.RetryAttemptProperty(2, true),
            new ClientMessages.SerializableKeyValuePairStringProperty("custom", "value"),
            new ClientMessages.FileArtifactProperty(new FileInfo(firstPath), "First", "first details"),
            new ClientMessages.FileArtifactProperty(new FileInfo(secondPath), "Second"),
            PassedTestNodeStateProperty.CachedInstance);

        IDictionary<string, object?> fields = await SerializeNodeFieldsAsync(node, modern);
        var expected = new Dictionary<string, object?>
        {
            ["uid"] = "node",
            ["display-name"] = "Node",
            ["node-type"] = "action",
            ["execution-state"] = "passed",
            ["location.file"] = "source.cs",
            ["location.line-start"] = 7,
            ["location.line-end"] = 9,
            ["location.type"] = "Tests.Case",
            ["location.method"] = "Run(System.String,System.Int32)",
            ["location.method-arity"] = 1,
            ["standardOutput"] = "output <tag> & \"quoted\"",
            ["standardError"] = "error",
            ["time.duration-ms"] = 3.5d,
            ["time.start-utc"] = "2025-02-03T02:05:06.0000000+00:00",
            ["time.stop-utc"] = "2025-02-03T02:05:07.0000000+00:00",
            ["retry.attempt"] = 2,
            ["retry.is-superseded"] = true,
            ["custom"] = "value",
            ["attachments.0.uri"] = secondPath,
            ["attachments.0.display-name"] = "Second",
            ["attachments.0.description"] = null,
            ["attachments.1.uri"] = firstPath,
            ["attachments.1.display-name"] = "First",
            ["attachments.1.description"] = "first details",
        };
        Assert.HasCount(expected.Count, fields);
        foreach (KeyValuePair<string, object?> pair in expected)
        {
            Assert.AreEqual(pair.Value, fields[pair.Key], $"Wire field '{pair.Key}'");
        }
    }

    [TestMethod]
    [DataRow(false, "discovered", "discovered", false)]
    [DataRow(false, "in-progress", "in-progress", false)]
    [DataRow(false, "skipped", "skipped", true)]
    [DataRow(false, "skipped-empty", "skipped", false)]
    [DataRow(false, "failed", "failed", true)]
    [DataRow(false, "timeout", "timed-out", true)]
    [DataRow(false, "error", "error", true)]
    [DataRow(false, "completed", "discovered", false)]
#if NETCOREAPP
    [DataRow(true, "discovered", "discovered", false)]
    [DataRow(true, "in-progress", "in-progress", false)]
    [DataRow(true, "skipped", "skipped", true)]
    [DataRow(true, "skipped-empty", "skipped", false)]
    [DataRow(true, "failed", "failed", true)]
    [DataRow(true, "timeout", "timed-out", true)]
    [DataRow(true, "error", "error", true)]
    [DataRow(true, "completed", "discovered", false)]
#endif
    public async Task TestNodeWireState_MapsEachOutcomeAndOnlyItsApplicableError(bool modern, string state, string expectedState, bool hasError)
    {
        IProperty property = state switch
        {
            "discovered" => ClientMessages.DiscoveredTestNodeStateProperty.CachedInstance,
            "in-progress" => ClientMessages.InProgressTestNodeStateProperty.CachedInstance,
            "skipped" => new ClientMessages.SkippedTestNodeStateProperty("reason"),
            "skipped-empty" => new ClientMessages.SkippedTestNodeStateProperty(string.Empty),
            "failed" => new FailedTestNodeStateProperty("reason"),
            "timeout" => new ClientMessages.TimeoutTestNodeStateProperty("reason"),
            "error" => new ClientMessages.ErrorTestNodeStateProperty("reason"),
            "completed" => ClientMessages.TestNodeExecutionCompletedProperty.CachedInstance,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
        IDictionary<string, object?> fields = await SerializeNodeFieldsAsync(CreateNode(property), modern);

        Assert.HasCount(hasError ? 5 : 4, fields);
        Assert.AreEqual("node", fields["uid"]);
        Assert.AreEqual("Node", fields["display-name"]);
        Assert.AreEqual("action", fields["node-type"]);
        Assert.AreEqual(expectedState, fields["execution-state"]);
        Assert.AreEqual(hasError, fields.ContainsKey("error.message"));
        if (hasError)
        {
            Assert.AreEqual("reason", fields["error.message"]);
        }

        Assert.IsFalse(fields.ContainsKey("error.stacktrace"));
        Assert.IsFalse(fields.ContainsKey("assert.expected"));
        Assert.IsFalse(fields.ContainsKey("assert.actual"));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
#if NETCOREAPP
    [DataRow(true, false)]
    [DataRow(true, true)]
#endif
    public async Task TestNodeWireAssertion_UsesOneStructuredProducerWithoutMixingLegacyValues(bool modern, bool expectedOnly)
    {
        var exception = new FixedStackTraceException("failure", "trace");
        exception.Data["assert.expected"] = "legacy expected";
        exception.Data["assert.actual"] = "legacy actual";
        TestNode node = CreateNode(
            new FailedTestNodeStateProperty(exception),
            new AssertionFailureProperty(expectedOnly ? "expected" : null, expectedOnly ? null : "actual"));

        IDictionary<string, object?> fields = await SerializeNodeFieldsAsync(node, modern);

        Assert.AreEqual(expectedOnly ? "expected" : string.Empty, fields["assert.expected"]);
        Assert.AreEqual(expectedOnly ? string.Empty : "actual", fields["assert.actual"]);
        Assert.AreEqual("failure", fields["error.message"]);
        Assert.AreEqual("trace", fields["error.stacktrace"]);

        fields = await SerializeNodeFieldsAsync(CreateNode(new FailedTestNodeStateProperty(exception)), modern);
        Assert.AreEqual("legacy expected", fields["assert.expected"]);
        Assert.AreEqual("legacy actual", fields["assert.actual"]);
        exception.Data.Clear();
        fields = await SerializeNodeFieldsAsync(CreateNode(new FailedTestNodeStateProperty(exception)), modern);
        Assert.AreEqual(string.Empty, fields["assert.expected"]);
        Assert.AreEqual(string.Empty, fields["assert.actual"]);
    }

    [TestMethod]
    [DataRow(false)]
#if NETCOREAPP
    [DataRow(true)]
#endif
    public async Task TestNodeWireMethod_WithoutNamespaceOrParameters_DoesNotInventSeparators(bool modern)
    {
        TestNode node = CreateNode(new ClientMessages.TestMethodIdentifierProperty("assembly", string.Empty, "Case", "Run", 0, [], "System.Void"));
        IDictionary<string, object?> fields = await SerializeNodeFieldsAsync(node, modern);

        Assert.AreEqual("Case", fields["location.type"]);
        Assert.AreEqual("Run", fields["location.method"]);
        Assert.AreEqual(0, fields["location.method-arity"]);
        Assert.AreEqual("group", fields["node-type"]);
    }

    [TestMethod]
    public void SerializerUtilities_EventPayloads_PreserveAllMetadataAndPerAttachmentIdentity()
    {
        var metrics = new Dictionary<string, object> { ["count"] = 2, ["label"] = "metric" };
        IDictionary<string, object?> telemetry = SerializerUtilities.Serialize(new TelemetryEventArgs("event", metrics));
        Assert.HasCount(2, telemetry);
        Assert.AreEqual("event", telemetry[JsonRpcStrings.EventName]);
        IDictionary<string, object> serializedMetrics = Assert.IsInstanceOfType<IDictionary<string, object>>(telemetry[JsonRpcStrings.Metrics]);
        Assert.HasCount(2, serializedMetrics);
        Assert.AreEqual(2, serializedMetrics["count"]);
        Assert.AreEqual("metric", serializedMetrics["label"]);

        IDictionary<string, object?> debugger = SerializerUtilities.Serialize(new AttachDebuggerInfoArgs(42));
        Assert.HasCount(1, debugger);
        Assert.AreEqual(42, debugger[JsonRpcStrings.ProcessId]);
        IDictionary<string, object?> log = SerializerUtilities.Serialize(new LogEventArgs(
            new serverclient::Microsoft.Testing.Platform.Logging.ServerLogMessage(serverclient::Microsoft.Testing.Platform.Logging.LogLevel.Warning, "diagnostic")));
        Assert.HasCount(2, log);
        Assert.AreEqual("Warning", log[JsonRpcStrings.Level]);
        Assert.AreEqual("diagnostic", log[JsonRpcStrings.Message]);

        var first = new Artifact("first", "collector-one", "type-one", "First", "details-one");
        var second = new Artifact("second", "collector-two", "type-two", "Second", "details-two");
        IDictionary<string, object?> result = SerializerUtilities.Serialize(new RunResponseArgs([first, second]));
        IList attachments = Assert.IsInstanceOfType<IList>(result[JsonRpcStrings.Attachments]);
        Assert.HasCount(2, attachments);
        AssertAttachmentFields(Assert.IsInstanceOfType<IDictionary<string, object?>>(attachments[0]), "first", "collector-one", "type-one", "First", "details-one");
        AssertAttachmentFields(Assert.IsInstanceOfType<IDictionary<string, object?>>(attachments[1]), "second", "collector-two", "type-two", "Second", "details-two");

        result = SerializerUtilities.Serialize(new TestsAttachments(
        [
            new RunTestAttachment("first", "collector-one", "type-one", "First", "details-one"),
            new RunTestAttachment("second", "collector-two", "type-two", "Second", "details-two"),
        ]));
        attachments = Assert.IsInstanceOfType<IList>(result[JsonRpcStrings.Attachments]);
        Assert.HasCount(2, attachments);
        AssertAttachmentFields(Assert.IsInstanceOfType<IDictionary<string, object?>>(attachments[0]), "first", "collector-one", "type-one", "First", "details-one");
        AssertAttachmentFields(Assert.IsInstanceOfType<IDictionary<string, object?>>(attachments[1]), "second", "collector-two", "type-two", "Second", "details-two");
        Assert.IsEmpty(SerializerUtilities.Serialize(new DiscoverResponseArgs()));
        Assert.IsEmpty(SerializerUtilities.Serialize(new object()));
        Assert.Contains(typeof(TelemetryEventArgs), SerializerUtilities.SerializerTypes);
        Assert.Contains(typeof(InitializeRequestArgs), SerializerUtilities.DeserializerTypes);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SerializerUtilities_ProcessPayload_PreservesArgumentsDirectoryAndOptionalEnvironment(bool withEnvironment)
    {
        IDictionary<string, string?>? environment = withEnvironment
            ? new Dictionary<string, string?> { ["first"] = "value", ["second"] = null }
            : null;
        IDictionary<string, object?> fields = SerializerUtilities.Serialize(new ProcessInfoArgs("runner", "--run", "work", environment));

        Assert.AreEqual("runner", fields[JsonRpcStrings.Program]);
        Assert.AreEqual("--run", fields[JsonRpcStrings.Args]);
        Assert.AreEqual("work", fields[JsonRpcStrings.WorkingDirectory]);
        Assert.HasCount(withEnvironment ? 4 : 3, fields);
        Assert.AreEqual(withEnvironment, fields.ContainsKey(JsonRpcStrings.EnvironmentVariables));
        if (withEnvironment)
        {
#if NETCOREAPP
            IDictionary<string, string?> values = Assert.IsInstanceOfType<IDictionary<string, string?>>(fields[JsonRpcStrings.EnvironmentVariables]);
            Assert.HasCount(2, values);
            Assert.AreEqual("value", values["first"]);
            Assert.IsNull(values["second"]);
#else
            IList values = Assert.IsInstanceOfType<IList>(fields[JsonRpcStrings.EnvironmentVariables]);
            Assert.HasCount(2, values);
            Assert.AreEqual("value", Assert.IsInstanceOfType<IDictionary<string, object?>>(values[0])["first"]);
            Assert.IsNull(Assert.IsInstanceOfType<IDictionary<string, object?>>(values[1])["second"]);
#endif
        }
    }

    [TestMethod]
    [DataRow("minimum", int.MinValue)]
    [DataRow("maximum", int.MaxValue)]
    [DataRow("zero", 0)]
    [DataRow("below", null)]
    [DataRow("above", null)]
    [DataRow("fraction", null)]
    public void SerializerUtilities_DecimalIds_RequireExactInt32Values(string representation, int? expected)
    {
        decimal value = representation switch
        {
            "minimum" => int.MinValue,
            "maximum" => int.MaxValue,
            "zero" => 0,
            "below" => (decimal)int.MinValue - 1,
            "above" => (decimal)int.MaxValue + 1,
            _ => 1.5m,
        };
        var properties = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "custom/request",
            ["id"] = value,
        };
        if (expected is { } id)
        {
            RequestMessage request = Assert.IsInstanceOfType<RequestMessage>(SerializerUtilities.Deserialize<RpcMessage>(properties));
            Assert.AreEqual(id, request.Id);
            Assert.IsNull(request.StringId);
            Assert.AreEqual("custom/request", request.Method);
        }
        else
        {
            MessageFormatException exception = Assert.ThrowsExactly<MessageFormatException>(() => SerializerUtilities.Deserialize<RpcMessage>(properties));
            Assert.AreEqual("id field should be a string or an int", exception.Message);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public void SerializerUtilities_OptionalBoolean_PreservesAbsenceNullAndBothValues(bool? showMessage)
    {
        var testing = new Dictionary<string, object?> { ["debuggerProvider"] = true, ["showMessage"] = showMessage };
        var properties = new Dictionary<string, object?>
        {
            ["capabilities"] = new Dictionary<string, object?> { ["testing"] = testing },
        };

        ClientCapabilities capabilities = SerializerUtilities.Deserialize<ClientCapabilities>(properties);
        Assert.IsTrue(capabilities.DebuggerProvider);
        Assert.AreEqual(showMessage, capabilities.ShowMessage);
        Assert.IsNull(capabilities.IsStateful);
        testing.Remove("showMessage");
        Assert.IsNull(SerializerUtilities.Deserialize<ClientCapabilities>(properties).ShowMessage);
        testing["showMessage"] = "invalid";
        MessageFormatException exception = Assert.ThrowsExactly<MessageFormatException>(() => SerializerUtilities.Deserialize<ClientCapabilities>(properties));
        Assert.AreEqual("'showMessage' field has wrong type (expected Boolean)", exception.Message);
        testing.Remove("debuggerProvider");
        Assert.AreEqual(
            "'debuggerProvider' field is missing",
            Assert.ThrowsExactly<MessageFormatException>(() => SerializerUtilities.Deserialize<ClientCapabilities>(properties)).Message);
        testing["debuggerProvider"] = "invalid";
        Assert.AreEqual(
            "'debuggerProvider' field has wrong type (expected Boolean)",
            Assert.ThrowsExactly<MessageFormatException>(() => SerializerUtilities.Deserialize<ClientCapabilities>(properties)).Message);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SerializerUtilities_ErrorEnvelopes_PreserveHeaderIdAndOptionalData(bool withData)
    {
        IDictionary<string, object?> fields = SerializerUtilities.Serialize(
            new ErrorMessage(7, -42, "failure", withData ? new Dictionary<string, object?> { ["detail"] = "value" } : null)
            {
                StringId = withData ? "7" : null,
            });

        Assert.HasCount(3, fields);
        Assert.AreEqual("2.0", fields["jsonrpc"]);
        Assert.AreEqual(withData ? "7" : 7, fields["id"]);
        IDictionary<string, object?> error = Assert.IsInstanceOfType<IDictionary<string, object?>>(fields["error"]);
        Assert.HasCount(3, error);
        Assert.AreEqual(-42, error["code"]);
        Assert.AreEqual("failure", error["message"]);
        IDictionary<string, object?> data = Assert.IsInstanceOfType<IDictionary<string, object?>>(error["data"]);
        Assert.HasCount(withData ? 1 : 0, data);
        if (withData)
        {
            Assert.AreEqual("value", data["detail"]);
        }

        ErrorMessage decoded = Assert.IsInstanceOfType<ErrorMessage>(SerializerUtilities.Deserialize<RpcMessage>(
            new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = "17",
                ["error"] = new Dictionary<string, object?> { ["code"] = -42, ["message"] = "remote failure" },
            }));
        Assert.AreEqual(17, decoded.Id);
        Assert.AreEqual("17", decoded.StringId);
        Assert.AreEqual(-42, decoded.ErrorCode);
        Assert.AreEqual("remote failure", decoded.Message);
        Assert.IsNull(decoded.Data);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(2)]
    public void SerializerUtilities_TestUpdates_PreserveNullEmptyAndEachNodeParentAssociation(int count)
    {
        var first = new TestNodeUpdateMessage(
            new SessionUid("session"),
            new TestNode { Uid = "first", DisplayName = "First" }, "parent-one");
        var second = new TestNodeUpdateMessage(
            new SessionUid("session"),
            new TestNode { Uid = "second", DisplayName = "Second" });
        TestNodeUpdateMessage[]? changes = count switch { -1 => null, 0 => [], _ => [first, second] };
        var runId = new Guid("11111111-2222-3333-4444-555555555555");

        IDictionary<string, object?> fields = SerializerUtilities.Serialize(new TestNodeStateChangedEventArgs(runId, changes));

        Assert.HasCount(2, fields);
        Assert.AreEqual(runId, fields["runId"]);
        if (count < 0)
        {
            Assert.IsNull(fields["changes"]);
        }
        else
        {
            IList updates = Assert.IsInstanceOfType<IList>(fields["changes"]);
            Assert.HasCount(count, updates);
            if (count > 0)
            {
                IDictionary<string, object?> firstUpdate = Assert.IsInstanceOfType<IDictionary<string, object?>>(updates[0]);
                IDictionary<string, object?> secondUpdate = Assert.IsInstanceOfType<IDictionary<string, object?>>(updates[1]);
                Assert.AreEqual("parent-one", firstUpdate["parent"]);
                Assert.IsNull(secondUpdate["parent"]);
                Assert.AreEqual("first", Assert.IsInstanceOfType<IDictionary<string, object?>>(firstUpdate["node"])["uid"]);
                Assert.AreEqual("second", Assert.IsInstanceOfType<IDictionary<string, object?>>(secondUpdate["node"])["uid"]);
            }
        }
    }

    [TestMethod]
    public void SerializerUtilities_OptionalFilterWithWrongType_ReportsTheOffendingField()
    {
        var fields = new Dictionary<string, object?>
        {
            ["runId"] = "11111111-2222-3333-4444-555555555555",
            ["filter"] = 42,
        };

        Assert.AreEqual(
            "'filter' field has wrong type (expected String)",
            Assert.ThrowsExactly<MessageFormatException>(() => SerializerUtilities.Deserialize<DiscoverRequestArgs>(fields)).Message);
    }

    [TestMethod]
    [DataRow(false, "timeout")]
    [DataRow(false, "error")]
#if NETCOREAPP
    [DataRow(true, "timeout")]
    [DataRow(true, "error")]
#endif
    public async Task TestNodeWireExceptionalStates_PreserveExplanationAndStackTrace(bool modern, string state)
    {
        var exception = new FixedStackTraceException("failure", "trace");
        IProperty property = state switch
        {
            "timeout" => new ClientMessages.TimeoutTestNodeStateProperty(exception, "reason"),
            "error" => new ClientMessages.ErrorTestNodeStateProperty(exception, "reason"),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

        IDictionary<string, object?> fields = await SerializeNodeFieldsAsync(CreateNode(property), modern);

        Assert.AreEqual(state == "timeout" ? "timed-out" : state, fields["execution-state"]);
        Assert.AreEqual("reason", fields["error.message"]);
        Assert.AreEqual("trace", fields["error.stacktrace"]);
        Assert.IsFalse(fields.ContainsKey("assert.expected"));
        Assert.IsFalse(fields.ContainsKey("assert.actual"));
    }

    [TestMethod]
    [DataRow(false)]
#if NETCOREAPP
    [DataRow(true)]
#endif
    public async Task TestNodeWireUnknownState_IsRejectedRatherThanDropped(bool modern)
    {
        NotSupportedException exception = await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            SerializeNodeFieldsAsync(CreateNode(new UnsupportedState()), modern));

        Assert.AreEqual($"Unsupported TestNodeStateProperty '{typeof(UnsupportedState)}'", exception.Message);
    }

#if NETCOREAPP
    [TestMethod]
    [DataRow("initialize")]
    [DataRow("testing/discoverTests")]
    [DataRow("testing/runTests")]
    [DataRow("$/cancelRequest")]
    public void Json_RequiredParamsMissing_PreservesTheRequestAndReportsInvalidParams(string method)
    {
        var json = new PlatformJson();
        RequestMessage request = Assert.IsInstanceOfType<RequestMessage>(json.Deserialize<RpcMessage>(Encoding.UTF8.GetBytes(
            $$$"""{"jsonrpc":"2.0","id":7,"method":"{{{method}}}"}""")));

        Assert.AreEqual(7, request.Id);
        Assert.AreEqual(method, request.Method);
        InvalidRequestParamsArgs invalid = Assert.IsInstanceOfType<InvalidRequestParamsArgs>(request.Params);
        Assert.AreEqual(-32602, invalid.ErrorCode);
        Assert.AreEqual("'params' field is missing", invalid.ErrorMessage);
    }

    [TestMethod]
    [DataRow("testing/discoverTests")]
    [DataRow("testing/runTests")]
    public void Json_InvalidRunId_IsReportedThroughTheRequestSentinel(string method)
    {
        var json = new PlatformJson();
        RequestMessage request = Assert.IsInstanceOfType<RequestMessage>(json.Deserialize<RpcMessage>(Encoding.UTF8.GetBytes(
            $$$"""{"jsonrpc":"2.0","id":"7","method":"{{{method}}}","params":{"runId":"invalid"}}""")));

        Assert.AreEqual(7, request.Id);
        Assert.AreEqual("7", request.StringId);
        Assert.AreEqual(method, request.Method);
        InvalidRequestParamsArgs invalid = Assert.IsInstanceOfType<InvalidRequestParamsArgs>(request.Params);
        Assert.AreEqual(-32602, invalid.ErrorCode);
        Assert.AreEqual("'runId' field is not a valid Guid", invalid.ErrorMessage);
    }

    [TestMethod]
    [DataRow("uid-null", "'uid' field cannot be null")]
    [DataRow("name-null", "'display-name' field cannot be null")]
    [DataRow("uid-empty", "'uid' field cannot be empty or whitespace")]
    [DataRow("uid-whitespace", "'uid' field cannot be empty or whitespace")]
    [DataRow("location-null", "'location.file' field cannot be null")]
    public void Json_InvalidNodeFields_RejectsEachInvalidPartition(string scenario, string expectedMessage)
    {
        string payload = scenario switch
        {
            "uid-null" => """{"uid":null,"display-name":"Node"}""",
            "name-null" => """{"uid":"node","display-name":null}""",
            "uid-empty" => """{"uid":"","display-name":"Node"}""",
            "uid-whitespace" => """{"uid":"  ","display-name":"Node"}""",
            _ => """{"uid":"node","display-name":"Node","location.file":null}""",
        };
        var json = new PlatformJson();

        Assert.AreEqual(expectedMessage, Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<TestNode>(Encoding.UTF8.GetBytes(payload))).Message);
    }

    [TestMethod]
    [DataRow("showMessage")]
    [DataRow("isStateful")]
    public void Json_InvalidBooleanCapability_ReportsTheOffendingField(string field)
    {
        var json = new PlatformJson();
        string payload = $$$"""{"testing":{"debuggerProvider":false,"{{{field}}}":42}}""";

        Assert.AreEqual(
            $"'{field}' field has wrong type (expected Boolean)",
            Assert.ThrowsExactly<MessageFormatException>(() => json.Deserialize<ClientCapabilities>(Encoding.UTF8.GetBytes(payload))).Message);
    }

    [TestMethod]
    public void Json_MissingOrNullIdsAndNullErrorMessage_PreserveTheirDistinctContracts()
    {
        var json = new PlatformJson();
        Assert.AreEqual("id field is missing", Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<CancelRequestArgs>(Encoding.UTF8.GetBytes("{}"))).Message);
        Assert.AreEqual("'id' field cannot be null", Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<RpcMessage>(Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":null,"method":"custom"}"""))).Message);
        Assert.AreEqual("'id' field is missing", Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Deserialize<RpcMessage>(Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","result":{}}"""))).Message);

        ErrorMessage error = json.Deserialize<ErrorMessage>(Encoding.UTF8.GetBytes(
            """{"jsonrpc":"2.0","id":7,"error":{"code":-42,"message":null}}"""));
        Assert.AreEqual(7, error.Id);
        Assert.AreEqual(-42, error.ErrorCode);
        Assert.AreEqual(string.Empty, error.Message);
    }

    [TestMethod]
    public async Task Json_PrimitiveCharacterAndTimestamps_UseTheExpectedWireRepresentations()
    {
        var json = new PlatformJson();

        Assert.AreEqual("\"X\"", await json.SerializeAsync('X'));
        Assert.AreEqual("\"2025-02-03T04:05:06.0000000Z\"", await json.SerializeAsync(new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc)));
        Assert.AreEqual(
            "\"2025-02-03T04:05:06.0000000+02:00\"",
            await json.SerializeAsync(new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.FromHours(2))));
    }

    [TestMethod]
    public void Json_UntypedValues_PreserveNestedKindsAndNumericBoundaries()
    {
        var json = new PlatformJson();
        object[] values = json.Deserialize<object[]>(Encoding.UTF8.GetBytes(
            """["text",42,2147483648,9223372036854775808,79228162514264337593543950335,1e100,true,false,null,["inner",false],{"flag":true}]"""));

        Assert.HasCount(11, values);
        Assert.AreEqual("text", values[0]);
        Assert.AreEqual(42, Assert.IsInstanceOfType<int>(values[1]));
        Assert.AreEqual(2147483648L, Assert.IsInstanceOfType<long>(values[2]));
        Assert.AreEqual(9223372036854775808UL, Assert.IsInstanceOfType<ulong>(values[3]));
        Assert.AreEqual(decimal.MaxValue, Assert.IsInstanceOfType<decimal>(values[4]));
        Assert.AreEqual(1e100, Assert.IsInstanceOfType<double>(values[5]));
        Assert.IsTrue(Assert.IsInstanceOfType<bool>(values[6]));
        Assert.IsFalse(Assert.IsInstanceOfType<bool>(values[7]));
        Assert.IsNull(values[8]);
        object[] inner = Assert.IsInstanceOfType<object[]>(values[9]);
        Assert.AreSequenceEqual<object>(["inner", false], inner);
        IDictionary<string, object?> properties = Assert.IsInstanceOfType<IDictionary<string, object?>>(values[10]);
        Assert.HasCount(1, properties);
        Assert.IsTrue(Assert.IsInstanceOfType<bool>(properties["flag"]));
    }

    [TestMethod]
    [DataRow("element", 42)]
    [DataRow("collection", 2)]
    [DataRow("base-element", 42)]
    [DataRow("base-collection", 2)]
    public void Json_CustomDeserializers_HandleTypedAndObjectBackedConverters(string kind, int expected)
    {
        JsonDeserializer deserializer = kind switch
        {
            "element" => new JsonElementDeserializer<Marker>((_, element) => new Marker(element.GetProperty("value").GetInt32())),
            "collection" => new MarkerCollectionDeserializer(),
            "base-element" => new JsonElementDeserializer<object>((_, element) => new Marker(element.GetProperty("value").GetInt32())),
            _ => new ObjectCollectionDeserializer(),
        };
        var json = new PlatformJson(deserializers: new Dictionary<Type, JsonDeserializer>
        {
            [typeof(Marker)] = deserializer,
            [typeof(int)] = new JsonElementDeserializer<int>((_, _) => -1),
        });
        string payload = kind.EndsWith("collection", StringComparison.Ordinal) ? "[1,2]" : """{"value":42}""";

        Assert.AreEqual(expected, json.Deserialize<Marker>(Encoding.UTF8.GetBytes(payload)).Value);
        Assert.AreEqual(42, json.Deserialize<int>(Encoding.UTF8.GetBytes("42")));
    }

    [TestMethod]
    public async Task Json_UnsupportedTypesAndMissingMembers_ReportTheActualContractFailure()
    {
        var json = new PlatformJson();
        InvalidOperationException serialize = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => json.SerializeAsync(new Marker(42)));
        Assert.AreEqual($"Flattener missing {typeof(Marker)}.", serialize.Message);
        Assert.AreEqual("\"valid\"", await json.SerializeAsync("valid"));
        InvalidOperationException deserialize = Assert.ThrowsExactly<InvalidOperationException>(() =>
            json.Deserialize<Marker>(Encoding.UTF8.GetBytes("{}")));
        Assert.AreEqual($"Cannot find deserializer for {typeof(Marker)}.", deserialize.Message);
        using var document = JsonDocument.Parse("{}");
        Assert.AreEqual("'missing' field is missing", Assert.ThrowsExactly<MessageFormatException>(() =>
            json.Bind<int>(document.RootElement, "missing")).Message);
        Assert.IsFalse(json.TryBind(document.RootElement, out int missing, "missing"));
        Assert.AreEqual(0, missing);
        using var nullArray = JsonDocument.Parse("""{"items":null}""");
        Assert.IsFalse(json.TryArrayBind(nullArray.RootElement, out int[]? array, "items"));
        Assert.IsNull(array);
        Assert.AreEqual("[null,\"value\"]", await json.SerializeAsync(new object?[] { null, "value" }));

        var serializer = new JsonObjectSerializer<Marker>(_ => []) { Properties = null };
        InvalidOperationException uninitialized = Assert.ThrowsExactly<InvalidOperationException>(() => serializer.GetProperties(new Marker(42)));
        Assert.AreEqual(
            "The 'Properties' delegate on 'JsonObjectSerializer`1' has not been initialized. Assign the 'Properties' delegate before calling 'GetProperties'.",
            uninitialized.Message);
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("false")]
    [DataRow("null")]
    [DataRow("string")]
    [DataRow("number")]
    [DataRow("array")]
    public void Json_ErrorData_PreservesAllSupportedJsonKinds(string kind)
    {
        string rawData = kind switch { "string" => "\"detail\"", "number" => "42", "array" => "[1,false]", _ => kind };
        var json = new PlatformJson();
        ErrorMessage error = json.Deserialize<ErrorMessage>(Encoding.UTF8.GetBytes(
            """{"jsonrpc":"2.0","id":7,"error":{"code":-1,"message":"failure","data":""" + rawData + "}}"));

        Assert.AreEqual(7, error.Id);
        Assert.AreEqual(-1, error.ErrorCode);
        Assert.AreEqual("failure", error.Message);
        if (kind == "array")
        {
            Assert.AreSequenceEqual<object>([1, false], Assert.IsInstanceOfType<object[]>(error.Data));
        }
        else
        {
            object? expected = kind switch { "true" => true, "false" => false, "null" => null, "number" => 42, _ => "detail" };
            Assert.AreEqual(expected, error.Data);
        }
    }

    [TestMethod]
    [DataRow("null", null)]
    [DataRow("\"1.0\"", "1.0")]
    public void Json_InitializeResponse_PreservesExplicitNullOrNegotiatedVersion(string rawVersion, string? expectedVersion)
    {
        var json = new PlatformJson();
        const string prefix = """{"processId":42,"serverInfo":{"name":"server","version":"4.3"},"capabilities":{"testing":{"supportsDiscovery":true,"experimental_multiRequestSupport":false,"vstestProvider":true,"attachmentsSupport":false,"multipleConnectionProvider":true,"showMessage":false}},"protocolVersion":""";
        InitializeResponseArgs response = json.Deserialize<InitializeResponseArgs>(Encoding.UTF8.GetBytes(prefix + rawVersion + "}"));

        Assert.AreEqual(42, response.ProcessId);
        Assert.AreEqual("server", response.ServerInfo.Name);
        Assert.AreEqual("4.3", response.ServerInfo.Version);
        Assert.AreEqual(expectedVersion, response.ProtocolVersion);
        Assert.AreEqual(new ServerTestingCapabilities(true, false, true, false, true) { ShowMessage = false }, response.Capabilities.TestingCapabilities);
        Assert.AreEqual(
            "'protocolVersion' field has wrong type (expected String)",
            Assert.ThrowsExactly<MessageFormatException>(() => json.Deserialize<InitializeResponseArgs>(Encoding.UTF8.GetBytes(prefix + "42}"))).Message);
    }

    private sealed class MarkerCollectionDeserializer : JsonCollectionDeserializer<Marker>
    {
        internal override Marker CreateObject(PlatformJson json, JsonElement element) => new(element.GetArrayLength());
    }

    private sealed class ObjectCollectionDeserializer : JsonCollectionDeserializer<object>
    {
        internal override object CreateObject(PlatformJson json, JsonElement element) => new Marker(element.GetArrayLength());
    }
#endif

    private sealed class UnsupportedState() : TestNodeStateProperty("unsupported");

    private static void AssertAttachmentFields(IDictionary<string, object?> fields, string uri, string producer, string type, string name, string description)
    {
        Assert.HasCount(5, fields);
        Assert.AreEqual(uri, fields[JsonRpcStrings.Uri]);
        Assert.AreEqual(producer, fields[JsonRpcStrings.Producer]);
        Assert.AreEqual(type, fields[JsonRpcStrings.Type]);
        Assert.AreEqual(name, fields[JsonRpcStrings.DisplayName]);
        Assert.AreEqual(description, fields[JsonRpcStrings.Description]);
    }

    private static async Task<IDictionary<string, object?>> SerializeNodeFieldsAsync(TestNode node, bool modern)
    {
        if (!modern)
        {
            return SerializerUtilities.Serialize(node);
        }

#if NETCOREAPP
        var json = new PlatformJson();
        return json.Deserialize<IDictionary<string, object?>>(Encoding.UTF8.GetBytes(await json.SerializeAsync(node)));
#else
        throw new PlatformNotSupportedException("The System.Text.Json formatter is only compiled on modern .NET.");
#endif
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

        // Complete the flush inline so the context-capture probe does not depend on a free pool worker.
        public TaskCompletionSource AllowWrite { get; } = new();

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
            d(state);
        }
    }
}
