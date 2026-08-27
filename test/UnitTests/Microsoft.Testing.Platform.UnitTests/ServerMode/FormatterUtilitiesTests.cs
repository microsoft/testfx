// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;

using TestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;
using TestNodeUid = Microsoft.Testing.Platform.Extensions.Messages.TestNodeUid;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class FormatterUtilitiesTests
{
    private readonly IMessageFormatter _formatter = FormatterUtilities.CreateFormatter();

    public static IEnumerable<object[]> SerializerTypesForDynamicData
        => SerializerUtilities.SerializerTypes.Select(x => new object[] { x });

    /// <summary>
    /// Feeds a JSON string to the formatter the way the transport does. On .NET the formatter consumes UTF-8
    /// bytes (Content-Length counts bytes, and that is what System.Text.Json parses); the Jsonite formatter
    /// used elsewhere only accepts a string. Wrapping it here keeps that split out of every call site.
    /// </summary>
    private T Deserialize<T>(string json)
#if NETCOREAPP
        => _formatter.Deserialize<T>(Encoding.UTF8.GetBytes(json).AsMemory());
#else
        => _formatter.Deserialize<T>(json);
#endif

    public FormatterUtilitiesTests()
        =>
#if NETCOREAPP
        Assert.AreEqual("System.Text.Json", _formatter.Id);
#else
        Assert.AreEqual("Jsonite", _formatter.Id);
#endif

    [TestMethod]
    public async Task Serialize_TestNodeStateChangedEventArgs_NullChanges_PreservesNull()
    {
        TestNodeStateChangedEventArgs message = new(Guid.Empty, null);

        IDictionary<string, object?> properties = SerializerUtilities.Serialize(message);
        string serialized = (await _formatter.SerializeAsync(message)).Replace(" ", string.Empty);

        Assert.IsNull(properties[JsonRpcStrings.Changes]);
        Assert.AreEqual("""{"runId":"00000000-0000-0000-0000-000000000000","changes":null}""", serialized);
    }

    [TestMethod]
    public async Task Serialize_TestNodeStateChangedEventArgs_EmptyChanges_PreservesEmptyObjectList()
    {
        TestNodeStateChangedEventArgs message = new(Guid.Empty, []);

        IDictionary<string, object?> properties = SerializerUtilities.Serialize(message);
        string serialized = (await _formatter.SerializeAsync(message)).Replace(" ", string.Empty);

        List<object> changes = Assert.IsInstanceOfType<List<object>>(properties[JsonRpcStrings.Changes]);
        Assert.IsEmpty(changes);
        Assert.AreEqual("""{"runId":"00000000-0000-0000-0000-000000000000","changes":[]}""", serialized);
    }

    [TestMethod]
    public async Task Serialize_TestNodeStateChangedEventArgs_Changes_PreservesOrderObjectTypesAndSource()
    {
        TestNodeUpdateMessage first = CreateTestNodeUpdate("first");
        TestNodeUpdateMessage second = CreateTestNodeUpdate("second");
        TestNodeUpdateMessage[] source = [first, second];
        TestNodeStateChangedEventArgs message = new(Guid.Empty, source);

        IDictionary<string, object?> properties = SerializerUtilities.Serialize(message);
        string serialized = (await _formatter.SerializeAsync(message)).Replace(" ", string.Empty);

        List<object> changes = Assert.IsInstanceOfType<List<object>>(properties[JsonRpcStrings.Changes]);
        Assert.HasCount(2, changes);
        IDictionary<string, object?> firstSerializedChange = Assert.IsInstanceOfType<IDictionary<string, object?>>(changes[0]);
        IDictionary<string, object?> secondSerializedChange = Assert.IsInstanceOfType<IDictionary<string, object?>>(changes[1]);
        IDictionary<string, object?> firstSerializedNode = Assert.IsInstanceOfType<IDictionary<string, object?>>(firstSerializedChange[JsonRpcStrings.Node]);
        IDictionary<string, object?> secondSerializedNode = Assert.IsInstanceOfType<IDictionary<string, object?>>(secondSerializedChange[JsonRpcStrings.Node]);
        Assert.AreEqual("first", firstSerializedNode[JsonRpcStrings.Uid]);
        Assert.AreEqual("second", secondSerializedNode[JsonRpcStrings.Uid]);
        Assert.AreSame(first, source[0]);
        Assert.AreSame(second, source[1]);
        Assert.AreEqual(
            """{"runId":"00000000-0000-0000-0000-000000000000","changes":[{"node":{"uid":"first","display-name":"first","node-type":"group"},"parent":null},{"node":{"uid":"second","display-name":"second","node-type":"group"},"parent":null}]}""",
            serialized);
    }

    [TestMethod]
    public void CanDeserializeTaskResponse()
    {
        RpcMessage msg = Deserialize<RpcMessage>("""
            {
                "jsonrpc": "2.0",
                "id": 1,
                "result": null
            }
            """);

        var response = (ResponseMessage)msg;
        Assert.AreEqual(1, response.Id);
        Assert.IsNull(response.Result);
    }

    [TestMethod]
    public void CanDeserializeNumericStringRequestId()
    {
        RpcMessage message = Deserialize<RpcMessage>(
            """
            {
                "jsonrpc": "2.0",
                "id": "42",
                "method": "testing/unknown"
            }
            """);

        RequestMessage request = Assert.IsInstanceOfType<RequestMessage>(message);
        Assert.AreEqual(42, request.Id);
        Assert.AreEqual("42", request.StringId);
    }

    [DataRow("1.0", 1)]
    [DataRow("1e0", 1)]
    [DataRow("-2.000", -2)]
    [TestMethod]
    public void CanDeserializeIntegralNumericRequestId(string serializedId, int expectedId)
    {
        RpcMessage message = Deserialize<RpcMessage>(
            $$"""
            {
                "jsonrpc": "2.0",
                "id": {{serializedId}},
                "method": "testing/unknown"
            }
            """);

        RequestMessage request = Assert.IsInstanceOfType<RequestMessage>(message);
        Assert.AreEqual(expectedId, request.Id);
        Assert.IsNull(request.StringId);
    }

    [DataRow("1.00000000000000001")]
    [DataRow("1.0000000000000000000000000000001")]
    [DataRow("1e-1")]
    [DataRow("2147483648.0")]
    [TestMethod]
    public void RejectsNonIntegralOrOutOfRangeNumericRequestId(string serializedId)
        => Assert.ThrowsExactly<MessageFormatException>(() => Deserialize<RpcMessage>(
            $$"""
            {
                "jsonrpc": "2.0",
                "id": {{serializedId}},
                "method": "testing/unknown"
            }
            """));

    [TestMethod]
    public async Task NumericStringId_IsPreservedInResponsesAndErrors()
    {
        ResponseMessage response = new(42, Result: null) { StringId = "42" };
        ErrorMessage error = new(42, ErrorCodes.InvalidRequest, "invalid", Data: null) { StringId = "42" };

        string serializedResponse = (await _formatter.SerializeAsync(response)).Replace(" ", string.Empty);
        string serializedError = (await _formatter.SerializeAsync(error)).Replace(" ", string.Empty);

        Assert.Contains("\"id\":\"42\"", serializedResponse);
        Assert.Contains("\"id\":\"42\"", serializedError);
    }

    [TestMethod]
    public void CanDeserializeNumericStringCancellationId()
    {
        RpcMessage message = Deserialize<RpcMessage>(
            """
            {
                "jsonrpc": "2.0",
                "method": "$/cancelRequest",
                "params": {
                    "id": "42"
                }
            }
            """);

        NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(message);
        CancelRequestArgs args = Assert.IsInstanceOfType<CancelRequestArgs>(notification.Params);
        Assert.AreEqual(42, args.CancelRequestId);
        Assert.AreEqual("42", args.StringId);
    }

    [TestMethod]
    public void CanDeserializeIntegralNumericCancellationId()
    {
        RpcMessage message = Deserialize<RpcMessage>(
            """
            {
                "jsonrpc": "2.0",
                "method": "$/cancelRequest",
                "params": {
                    "id": 42.0
                }
            }
            """);

        NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(message);
        CancelRequestArgs args = Assert.IsInstanceOfType<CancelRequestArgs>(notification.Params);
        Assert.AreEqual(42, args.CancelRequestId);
        Assert.IsNull(args.StringId);
    }

    [TestMethod]
    public void NullRequestId_IsRejected()
        => Assert.ThrowsExactly<MessageFormatException>(() => Deserialize<RpcMessage>(
            """
            {
                "jsonrpc": "2.0",
                "id": null,
                "method": "testing/unknown"
            }
            """));

    [TestMethod]
    public void DeserializeInitializeRequest_NullProtocolVersions_UsesLegacyNegotiation()
    {
        RpcMessage message = Deserialize<RpcMessage>(
            """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "test-client", "version": "1.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": false
                        }
                    },
                    "protocolVersions": null
                }
            }
            """);

        InitializeRequestArgs request = Assert.IsInstanceOfType<RequestMessage>(message).Params
            as InitializeRequestArgs
            ?? throw new InvalidOperationException("Expected typed initialize request arguments.");
        Assert.IsNull(request.ProtocolVersions);
    }

    [DataRow("\"1.0.0\"")]
    [DataRow("[1]")]
    [DataRow("[null]")]
    [TestMethod]
    public void DeserializeInitializeRequest_InvalidProtocolVersions_CapturesInvalidParams(string protocolVersions)
    {
        RpcMessage message = Deserialize<RpcMessage>(
            $$"""
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "test-client", "version": "1.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": false
                        }
                    },
                    "protocolVersions": {{protocolVersions}}
                }
            }
            """);

        RequestMessage request = Assert.IsInstanceOfType<RequestMessage>(message);
        InvalidRequestParamsArgs invalidParams = Assert.IsInstanceOfType<InvalidRequestParamsArgs>(request.Params);
        Assert.AreEqual(ErrorCodes.InvalidParams, invalidParams.ErrorCode);
    }

    [DataRow(JsonRpcMethods.Initialize)]
    [DataRow(JsonRpcMethods.TestingDiscoverTests)]
    [DataRow(JsonRpcMethods.TestingRunTests)]
    [DataRow(JsonRpcMethods.CancelRequest)]
    [TestMethod]
    public void DeserializeKnownRequest_MissingParams_CapturesInvalidParams(string method)
    {
        RpcMessage message = Deserialize<RpcMessage>(
            $$"""
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "{{method}}"
            }
            """);

        RequestMessage request = Assert.IsInstanceOfType<RequestMessage>(message);
        InvalidRequestParamsArgs invalidParams = Assert.IsInstanceOfType<InvalidRequestParamsArgs>(request.Params);
        Assert.AreEqual(ErrorCodes.InvalidParams, invalidParams.ErrorCode);
    }

    [TestMethod]
    public void DeserializeUnknownNotification_NonObjectParams_DropsParams()
    {
        RpcMessage message = Deserialize<RpcMessage>(
            """
            {
                "jsonrpc": "2.0",
                "method": "testing/unknown",
                "params": "not-an-object"
            }
            """);

        Assert.IsNull(Assert.IsInstanceOfType<NotificationMessage>(message).Params);
    }

    [DataRow("\"filter\": 42")]
    [DataRow("\"tests\": \"not-an-array\"")]
    [DataRow("\"tests\": [42]")]
    [DataRow("\"tests\": [{}]")]
    [DataRow("\"tests\": [{\"uid\": 42, \"display-name\": \"Test\"}]")]
    [DataRow("\"tests\": [{\"uid\": \"test\"}]")]
    [DataRow("\"tests\": [{\"uid\": null, \"display-name\": \"Test\"}]")]
    [DataRow("\"tests\": [{\"uid\": \"test\", \"display-name\": null}]")]
    [DataRow("\"tests\": [{\"uid\": \"\", \"display-name\": \"Test\"}]")]
    [DataRow("\"tests\": [{\"uid\": \"   \", \"display-name\": \"Test\"}]")]
    [DataRow("\"tests\": [{\"uid\": \"test\", \"display-name\": \"Test\", \"location.file\": null}]")]
    [DataRow("\"tests\": [{\"uid\": \"test\", \"display-name\": \"Test\", \"location.file\": \"file\", \"location.line-start\": null, \"location.line-end\": 2}]")]
    [DataRow("\"tests\": [{\"uid\": \"test\", \"display-name\": \"Test\", \"location.file\": \"file\"}]")]
    [DataRow("\"tests\": [{\"uid\": \"test\", \"display-name\": \"Test\", \"location.file\": \"file\", \"location.line-start\": 1}]")]
    [DataRow("\"tests\": [{\"uid\": \"test\", \"display-name\": \"Test\", \"location.line-start\": 1, \"location.line-end\": 2}]")]
    [TestMethod]
    public void DeserializeRunRequest_InvalidOptionalPropertyType_CapturesInvalidParams(string property)
    {
        RpcMessage message = Deserialize<RpcMessage>(
            $$"""
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "testing/runTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000001",
                    {{property}}
                }
            }
            """);

        RequestMessage request = Assert.IsInstanceOfType<RequestMessage>(message);
        InvalidRequestParamsArgs invalidParams = Assert.IsInstanceOfType<InvalidRequestParamsArgs>(request.Params);
        Assert.AreEqual(ErrorCodes.InvalidParams, invalidParams.ErrorCode);
    }

    [TestMethod]
    public void DeserializeError_PrimitiveData_PreservesValue()
    {
        RpcMessage message = Deserialize<RpcMessage>(
            """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "error": {
                    "code": -32600,
                    "message": "invalid",
                    "data": "detail"
                }
            }
            """);

        Assert.AreEqual("detail", Assert.IsInstanceOfType<ErrorMessage>(message).Data);
    }

    [TestMethod]
    public void DeserializeInitializeResponse_NonStringProtocolVersion_Throws()
    {
        const string Json = """
            {
                "processId": 1,
                "serverInfo": {
                    "name": "server",
                    "version": "1.2.3"
                },
                "capabilities": {
                    "testing": {
                        "supportsDiscovery": true,
                        "experimental_multiRequestSupport": false,
                        "vstestProvider": false,
                        "attachmentsSupport": true,
                        "multipleConnectionProvider": false
                    }
                },
                "protocolVersion": 1
            }
            """;

        Assert.Throws<MessageFormatException>(() => Deserialize<InitializeResponseArgs>(Json));
    }

    [TestMethod]
    public async Task Serialize_TestNodeWithRetryAttempt_EmitsRetryProperties()
    {
        // An unhandled property falls through the serializer's type chain and is silently dropped, which would
        // leave a server-mode client unable to tell repeated updates for the same uid apart. Both serializer
        // implementations (System.Text.Json on .NET, Jsonite on .NET Framework) must carry the attribution.
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "DisplayName",
            Properties = new PropertyBag(
                PassedTestNodeStateProperty.CachedInstance,
                new RetryAttemptProperty(2, isSuperseded: false)),
        };

        string serialized = (await _formatter.SerializeAsync(testNode)).Replace(" ", string.Empty);

        Assert.Contains("\"retry.attempt\":2", serialized, serialized);
        Assert.Contains("\"retry.is-superseded\":false", serialized, serialized);
    }

    [TestMethod]
    public async Task Serialize_TestNodeWithSupersededRetryAttempt_EmitsIsSupersededTrue()
    {
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "DisplayName",
            Properties = new PropertyBag(
                new FailedTestNodeStateProperty("boom"),
                new RetryAttemptProperty(1, isSuperseded: true)),
        };

        string serialized = (await _formatter.SerializeAsync(testNode)).Replace(" ", string.Empty);

        Assert.Contains("\"retry.attempt\":1", serialized, serialized);
        Assert.Contains("\"retry.is-superseded\":true", serialized, serialized);
    }

    [TestMethod]
    public async Task Serialize_TestsAttachmentsWithoutRunId_PreservesLegacyShape()
    {
        TestsAttachments attachments = new([new RunTestAttachment("uri", "producer", "type", "name", null)]);

        string serialized = (await _formatter.SerializeAsync(attachments)).Replace(" ", string.Empty);

        Assert.AreEqual(
            """{"attachments":[{"uri":"uri","producer":"producer","type":"type","display-name":"name","description":null}]}""",
            serialized);
    }

    [DynamicData(nameof(SerializerTypesForDynamicData), DynamicDataDisplayName = nameof(FormatSerializerTypes))]
    [TestMethod]
    public async Task SerializeDeserialize_Succeed(Type type)
    {
        object instanceToSerialize = CreateInstance(type);
        string instanceSerialized = await _formatter.SerializeAsync(instanceToSerialize);
        AssertSerialize(type, instanceSerialized.Replace(" ", string.Empty));
        Type? deserializer = SerializerUtilities.DeserializerTypes.SingleOrDefault(x => x == type);
        if (deserializer is not null)
        {
            object instanceDeserialized = Deserialize(deserializer, instanceSerialized);
            if (!HasCustomDeserializeAssert(type))
            {
                Assert.AreEqual(instanceToSerialize, instanceDeserialized);
            }
            else
            {
                CustomAssert(type, instanceDeserialized, instanceToSerialize);
            }
        }

        static bool HasCustomDeserializeAssert(Type type) => type == typeof(TestNode);
    }

    [TestMethod]
    public async Task Serialize_ExecutionCompletedTestNode_RemainsActionWithoutOutcome()
    {
        var testNode = new TestNode
        {
            Uid = "dropped-test",
            DisplayName = "DroppedTest",
            Properties = new PropertyBag(TestNodeExecutionCompletedProperty.CachedInstance),
        };

        string serialized = (await _formatter.SerializeAsync(testNode)).Replace(" ", string.Empty);

        Assert.AreEqual(
            """{"uid":"dropped-test","display-name":"DroppedTest","node-type":"action","execution-state":"discovered"}""",
            serialized);
    }

    [TestMethod]
    public async Task Serialize_FailedTestNodeWithAssertionFailureProperty_EmitsAssertValues()
    {
        // No exception at all: the assertion diff must still reach the client. This is the case the
        // Exception.Data channel structurally cannot express.
        var testNode = new TestNode
        {
            Uid = "failing-test",
            DisplayName = "FailingTest",
            Properties = new PropertyBag(
                new FailedTestNodeStateProperty("Assert.AreEqual failed."),
                new AssertionFailureProperty("5", "2")),
        };

        string serialized = (await _formatter.SerializeAsync(testNode)).Replace(" ", string.Empty);

        Assert.AreEqual(
            """{"uid":"failing-test","display-name":"FailingTest","node-type":"action","execution-state":"failed","error.message":"Assert.AreEqualfailed.","assert.actual":"2","assert.expected":"5"}""",
            serialized);
    }

    [TestMethod]
    public async Task Serialize_FailedTestNodeWithAssertionFailureProperty_PrefersPropertyOverExceptionData()
    {
        var exception = new InvalidOperationException("boom");
        exception.Data["assert.expected"] = "legacy expected";
        exception.Data["assert.actual"] = "legacy actual";

        var testNode = new TestNode
        {
            Uid = "failing-test",
            DisplayName = "FailingTest",
            Properties = new PropertyBag(
                new FailedTestNodeStateProperty(exception),
                new AssertionFailureProperty("5", "2")),
        };

        string serialized = (await _formatter.SerializeAsync(testNode)).Replace(" ", string.Empty);

        Assert.Contains("\"assert.actual\":\"2\"", serialized);
        Assert.Contains("\"assert.expected\":\"5\"", serialized);

        // Proves the property won rather than merely being present: an implementation that emitted both
        // channels would satisfy the two assertions above.
        Assert.DoesNotContain("legacy", serialized);
    }

    [TestMethod]
    public async Task Serialize_OneSidedAssertionFailureProperty_DoesNotSpliceInExceptionData()
    {
        // The property is authoritative as a whole; the missing half must serialize as empty rather than
        // falling back to an unrelated Exception.Data entry.
        var exception = new InvalidOperationException("boom");
        exception.Data["assert.expected"] = "legacy expected";
        exception.Data["assert.actual"] = "legacy actual";

        var testNode = new TestNode
        {
            Uid = "failing-test",
            DisplayName = "FailingTest",
            Properties = new PropertyBag(
                new FailedTestNodeStateProperty(exception),
                new AssertionFailureProperty("5", null)),
        };

        string serialized = (await _formatter.SerializeAsync(testNode)).Replace(" ", string.Empty);

        Assert.Contains("\"assert.expected\":\"5\"", serialized);
        Assert.Contains("\"assert.actual\":\"\"", serialized);
        Assert.DoesNotContain("legacy", serialized);
    }

    [DataRow(typeof(DiscoverRequestArgs))]
    [DataRow(typeof(RunRequestArgs))]
    [TestMethod]
    public void DeserializeSpecificTypes(Type type)
    {
        string json = CreateSerializedInstance(type);
        Type? deserializer = SerializerUtilities.DeserializerTypes.SingleOrDefault(x => x == type);

        if (deserializer is not null)
        {
            object actual = Deserialize(deserializer, json);
            object expected = CreateInstance(type);

            if (type == typeof(DiscoverRequestArgs))
            {
                AssertRequestArgs(type, (DiscoverRequestArgs)actual, (DiscoverRequestArgs)expected);
            }
            else if (type == typeof(RunRequestArgs))
            {
                AssertRequestArgs(type, (RunRequestArgs)actual, (RunRequestArgs)expected);
            }
            else
            {
                Assert.AreEqual(expected, actual);
            }
        }
    }

    [DataRow(typeof(DiscoverRequestArgs))]
    [DataRow(typeof(RunRequestArgs))]
    [TestMethod]
    public void Deserialize_InvalidRunId_ThrowsMessageFormatException(Type type)
    {
        const string json = """
            {
                "runId": "not-a-guid"
            }
            """;

        Assert.Throws<MessageFormatException>(() => Deserialize(type, json));
    }

    [DataRow("testing/discoverTests", "\"runId\": 42")]
    [DataRow("testing/runTests", "\"runId\": 42")]
    [DataRow("testing/discoverTests", "")]
    [DataRow("testing/runTests", "")]
    [TestMethod]
    public void DeserializeRpcMessage_InvalidRunIdShape_CapturesInvalidParamsSentinel(string method, string runIdProperty)
    {
        string json = $$"""
            {
                "jsonrpc": "2.0",
                "id": 42,
                "method": "{{method}}",
                "params": {
                    {{runIdProperty}}
                }
            }
            """;

        RpcMessage msg = Deserialize<RpcMessage>(json);

        var request = (RequestMessage)msg;
        Assert.AreEqual(42, request.Id);
        Assert.AreEqual(method, request.Method);
        var invalid = (InvalidRequestParamsArgs)request.Params!;
        Assert.AreEqual(ErrorCodes.InvalidParams, invalid.ErrorCode);
    }

    [DataRow("testing/discoverTests")]
    [DataRow("testing/runTests")]
    [TestMethod]
    public void DeserializeRpcMessage_InvalidRunId_CapturesInvalidParamsSentinel(string method)
    {
        string json = $$"""
            {
                "jsonrpc": "2.0",
                "id": 42,
                "method": "{{method}}",
                "params": {
                    "runId": "not-a-guid"
                }
            }
            """;

        RpcMessage msg = Deserialize<RpcMessage>(json);

        var request = (RequestMessage)msg;
        Assert.AreEqual(42, request.Id);
        Assert.AreEqual(method, request.Method);
        var invalid = (InvalidRequestParamsArgs)request.Params!;
        Assert.AreEqual(ErrorCodes.InvalidParams, invalid.ErrorCode);
        Assert.Contains("runId", invalid.ErrorMessage);
    }

    private static void AssertRequestArgs<TRequestArgs>(Type type, TRequestArgs actualRequest, TRequestArgs expectedRequest)
        where TRequestArgs : RequestArgsBase
    {
        Assert.AreEqual(expectedRequest.RunId, actualRequest.RunId);
        Assert.AreEqual(expectedRequest.TestNodes?.Count, actualRequest.TestNodes?.Count);

        TestNode[]? actualTestNodes = actualRequest.TestNodes?.ToArray();
        TestNode[]? expectedTestNodes = expectedRequest.TestNodes?.ToArray();

        for (int i = 0; i < actualRequest.TestNodes?.Count; i++)
        {
            CustomAssert(typeof(TestNode), actualTestNodes?[i]!, expectedTestNodes?[i]!);
        }

        Assert.AreEqual(expectedRequest.GraphFilter, actualRequest.GraphFilter);
    }

    private static void CustomAssert(Type type, object instanceDeserialized, object originalObject)
    {
        if (type == typeof(TestNode))
        {
            var deserialized = (TestNode)instanceDeserialized;
            var original = (TestNode)originalObject;
            Assert.AreEqual(original.Uid, deserialized.Uid);
            Assert.AreEqual(original.DisplayName, deserialized.DisplayName);
            Assert.AreEqual(original.Properties.Single<TestFileLocationProperty>(), deserialized.Properties.Single<TestFileLocationProperty>());
        }
    }

    public static string? FormatSerializerTypes(MethodInfo methodInfo, object?[]? data)
        => (data?[0] as Type)?.Name;

    private static void AssertSerialize(Type type, string instanceSerialized)
    {
        string because = $"type {type} should serialize to the expected value";
        if (type == typeof(AttachDebuggerInfoArgs))
        {
            Assert.AreEqual("""{"processId":1}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(ProcessInfoArgs))
        {
            Assert.AreEqual("""{"Program":"program","Args":"arts","WorkingDirectory":"workingDir","EnvironmentVariables":[{"key":"value"}]}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(KeyValuePair<string, string>))
        {
            Assert.AreEqual("""{"key":"value"}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(TelemetryEventArgs))
        {
            Assert.AreEqual("""{"eventName":"eventName","metrics":{"key":1}}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(CancelRequestArgs))
        {
            Assert.AreEqual("""{"id":1}""", instanceSerialized, because);
            return;
        }

        if (type == typeof(LogEventArgs))
        {
            Assert.AreEqual("""{"level":"Warning","message":"Warning error"}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(TestNode))
        {
            Assert.AreEqual("""{"uid":"uid","display-name":"DisplayName","traits":[{"testmetadata-key":"testmetadata-value"}],"standardError":"textProperty2","standardOutput":"textProperty","time.duration-ms":0,"location.type":"namespace.typeName","location.method":"methodName(param1,param2)","location.method-arity":0,"location.file":"filePath","location.line-start":1,"location.line-end":2,"key":"value","node-type":"action","execution-state":"failed","error.message":"sample","error.stacktrace":"","assert.actual":"","assert.expected":""}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(TestNodeStateChangedEventArgs))
        {
            Assert.AreEqual("""{"runId":"00000000-0000-0000-0000-000000000000","changes":null}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(TestNodeUpdateMessage))
        {
            Assert.AreEqual("""{"node":{"uid":"uid","display-name":"DisplayName","traits":[{"testmetadata-key":"testmetadata-value"}],"standardError":"textProperty2","standardOutput":"textProperty","time.duration-ms":0,"location.type":"namespace.typeName","location.method":"methodName(param1,param2)","location.method-arity":0,"location.file":"filePath","location.line-start":1,"location.line-end":2,"key":"value","node-type":"action","execution-state":"failed","error.message":"sample","error.stacktrace":"","assert.actual":"","assert.expected":""},"parent":"parent-uid"}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(RunResponseArgs))
        {
            Assert.AreEqual("""{"attachments":[]}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(DiscoverResponseArgs))
        {
            Assert.AreEqual("""{}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(Artifact))
        {
            Assert.AreEqual("""{"uri":"Uri","producer":"Producer","type":"Type","display-name":"DisplayName","description":"Description"}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(ServerTestingCapabilities))
        {
            Assert.AreEqual("""{"supportsDiscovery":true,"experimental_multiRequestSupport":true,"vstestProvider":true,"attachmentsSupport":true,"multipleConnectionProvider":true,"supportsTestCoverageMessages":false}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(ServerCapabilities))
        {
            Assert.AreEqual("""{"testing":{"supportsDiscovery":true,"experimental_multiRequestSupport":true,"vstestProvider":true,"attachmentsSupport":true,"multipleConnectionProvider":true,"supportsTestCoverageMessages":false}}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(ServerInfo))
        {
            Assert.AreEqual("""{"name":"ServerInfo","version":"Version"}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(InitializeResponseArgs))
        {
            Assert.AreEqual("""{"processId":1,"serverInfo":{"name":"ServerInfoName","version":"Version"},"capabilities":{"testing":{"supportsDiscovery":true,"experimental_multiRequestSupport":true,"vstestProvider":true,"attachmentsSupport":true,"multipleConnectionProvider":true,"supportsTestCoverageMessages":false}},"protocolVersion":"1.0.0"}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(ErrorMessage))
        {
            // Per JSON-RPC 2.0 §5.1, "code" must only appear inside the nested "error" object,
            // never at the top level of the response envelope.
            Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"error":{"code":2,"data":{},"message":"This is error"}}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(NotificationMessage))
        {
            Assert.AreEqual("""{"jsonrpc":"2.0","method":"testing/discoverTests","params":null}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(ResponseMessage))
        {
            Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":null}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(RequestMessage))
        {
            Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"method":"testing/discoverTests","params":null}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(TestsAttachments))
        {
            Assert.AreEqual("""{"attachments":[{"uri":"Uri","producer":"Producer","type":"Type","display-name":"DisplayName","description":"Description"}]}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(RunTestAttachment))
        {
            Assert.AreEqual("""{"uri":"Uri","producer":"Producer","type":"Type","display-name":"DisplayName","description":"Description"}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(object))
        {
            Assert.AreEqual("{}".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        throw new NotImplementedException($"Assertion not implemented '{type}', value to assert:\n{instanceSerialized}");
    }

    private static string CreateSerializedInstance(Type type) => type == typeof(DiscoverRequestArgs) || type == typeof(RunRequestArgs)
            ? """
            {
                "runId":"00000000-0000-0000-0000-000000000000",
                "tests":[
                    {
                        "uid":"UnitTest1.TestMethod1",
                        "display-name":"test1",
                        "location.file":"filePath",
                        "location.line-start":1,
                        "location.line-end":2
                    }
                    ]
                }
            """
            : throw new NotImplementedException($"Serialized instance doesn't exist for '{type}'");

    private static object CreateInstance(Type type)
    {
        if (type == typeof(AttachDebuggerInfoArgs))
        {
            return new AttachDebuggerInfoArgs(1);
        }

        if (type == typeof(ProcessInfoArgs))
        {
            return new ProcessInfoArgs("program", "arts", "workingDir", new Dictionary<string, string?> { ["key"] = "value" });
        }

        if (type == typeof(KeyValuePair<string, string>))
        {
            return new KeyValuePair<string, string>("key", "value");
        }

        if (type == typeof(TelemetryEventArgs))
        {
            return new TelemetryEventArgs("eventName", new Dictionary<string, object> { ["key"] = 1 });
        }

        if (type == typeof(CancelRequestArgs))
        {
            return new CancelRequestArgs(1);
        }

        if (type == typeof(LogEventArgs))
        {
            return new LogEventArgs(new Logging.ServerLogMessage(Logging.LogLevel.Warning, "Warning error"));
        }

        if (type == typeof(TestNode))
        {
            return GetSampleTestNode();
        }

        if (type == typeof(TestNodeStateChangedEventArgs))
        {
            return new TestNodeStateChangedEventArgs(Guid.Empty, null);
        }

        if (type == typeof(TestNodeUpdateMessage))
        {
            return new TestNodeUpdateMessage(
                default,
                GetSampleTestNode(),
                new TestNodeUid("parent-uid"));
        }

        if (type == typeof(RunResponseArgs))
        {
            return new RunResponseArgs([]);
        }

        if (type == typeof(DiscoverResponseArgs))
        {
            return new DiscoverResponseArgs();
        }

        if (type == typeof(Artifact))
        {
            return new Artifact("Uri", "Producer", "Type", "DisplayName", "Description");
        }

        if (type == typeof(TestsAttachments))
        {
            return new TestsAttachments([new("Uri", "Producer", "Type", "DisplayName", "Description")]);
        }

        if (type == typeof(RunTestAttachment))
        {
            return new RunTestAttachment("Uri", "Producer", "Type", "DisplayName", "Description");
        }

        if (type == typeof(ServerTestingCapabilities))
        {
            return new ServerTestingCapabilities(true, true, true, true, true);
        }

        if (type == typeof(ServerCapabilities))
        {
            return new ServerCapabilities(new ServerTestingCapabilities(true, true, true, true, true));
        }

        if (type == typeof(ServerInfo))
        {
            return new ServerInfo("ServerInfo", "Version");
        }

        if (type == typeof(InitializeResponseArgs))
        {
            return new InitializeResponseArgs(
                1,
                new ServerInfo("ServerInfoName", "Version"),
                new ServerCapabilities(new ServerTestingCapabilities(true, true, true, true, true)))
            {
                ProtocolVersion = JsonRpcProtocolVersions.Current,
            };
        }

        if (type == typeof(ErrorMessage))
        {
            return new ErrorMessage(1, 2, "This is error", null);
        }

        if (type == typeof(NotificationMessage))
        {
            return new NotificationMessage("testing/discoverTests", null);
        }

        if (type == typeof(ResponseMessage))
        {
            return new ResponseMessage(1, null);
        }

        if (type == typeof(RequestMessage))
        {
            return new RequestMessage(1, "testing/discoverTests", null);
        }

        if (type == typeof(DiscoverRequestArgs))
        {
            return new DiscoverRequestArgs(
                Guid.Empty,
                [
                    new()
                    {
                        Uid = new TestNodeUid("UnitTest1.TestMethod1"),
                        DisplayName = "test1",
                        Properties = new PropertyBag(new TestFileLocationProperty("filePath", new LinePositionSpan(new(1, 0), new(2, 0)))),
                    }
                ],
                null);
        }

        if (type == typeof(RunRequestArgs))
        {
            return new RunRequestArgs(
                Guid.Empty,
                [
                    new()
                    {
                        Uid = new TestNodeUid("UnitTest1.TestMethod1"),
                        DisplayName = "test1",
                        Properties = new PropertyBag(new TestFileLocationProperty("filePath", new LinePositionSpan(new(1, 0), new(2, 0)))),
                    }
                ],
                null);
        }

        // Last resort, try to create an instance of the type
        return type == typeof(object)
            ? new object()
            : throw new NotImplementedException($"Test for type not implemented '{type}'");

        static TestNode GetSampleTestNode()
        {
            // This should be kept in sync with the serialization code inside SerializerUtilities.cs and Json.cs
            TestNode testNode = new()
            {
                DisplayName = "DisplayName",
                Uid = new TestNodeUid("uid"),
            };

            testNode.Properties.Add(new SerializableKeyValuePairStringProperty("key", "value"));
            testNode.Properties.Add(new TestFileLocationProperty("filePath", new LinePositionSpan(new(1, 0), new(2, 0))));
            testNode.Properties.Add(new TestMethodIdentifierProperty("assemblyFullName", "namespace", "typeName", "methodName", 0, ["param1", "param2"], "returnTypeFullName"));
            testNode.Properties.Add(new TimingProperty(new TimingInfo(new DateTimeOffset(2023, 01, 01, 01, 01, 01, TimeSpan.Zero), new DateTimeOffset(2023, 01, 01, 01, 01, 01, TimeSpan.Zero), TimeSpan.Zero)));
            testNode.Properties.Add(new FailedTestNodeStateProperty(new InvalidOperationException("sample")));
            testNode.Properties.Add(new StandardOutputProperty("textProperty"));
            testNode.Properties.Add(new StandardErrorProperty("textProperty2"));
            testNode.Properties.Add(new TestMetadataProperty("testmetadata-key", "testmetadata-value"));

            return testNode;
        }
    }

    private static TestNodeUpdateMessage CreateTestNodeUpdate(string uid)
        => new(
            default,
            new TestNode
            {
                Uid = new TestNodeUid(uid),
                DisplayName = uid,
            });

    private object Deserialize(Type type, string instanceSerialized)
        => true switch
        {
            _ when type == typeof(ErrorMessage) => Deserialize<ErrorMessage>(instanceSerialized)!,
            _ when type == typeof(InitializeResponseArgs) => Deserialize<InitializeResponseArgs>(instanceSerialized)!,
            _ when type == typeof(ServerInfo) => Deserialize<ServerInfo>(instanceSerialized)!,
            _ when type == typeof(CancelRequestArgs) => Deserialize<CancelRequestArgs>(instanceSerialized)!,
            _ when type == typeof(TestNode) => Deserialize<TestNode>(instanceSerialized)!,
            _ when type == typeof(DiscoverRequestArgs) => Deserialize<DiscoverRequestArgs>(instanceSerialized)!,
            _ when type == typeof(RunRequestArgs) => Deserialize<RunRequestArgs>(instanceSerialized)!,
            _ when type == typeof(ServerCapabilities) => Deserialize<ServerCapabilities>(instanceSerialized)!,
            _ => throw new NotImplementedException($"Deserializer for type not implemented '{type}'"),
        };
}
