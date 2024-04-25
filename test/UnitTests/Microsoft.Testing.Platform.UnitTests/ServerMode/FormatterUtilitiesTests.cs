// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.TestInfrastructure;

using TestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;
using TestNodeUid = Microsoft.Testing.Platform.Extensions.Messages.TestNodeUid;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class FormatterUtilitiesTests : TestBase
{
    private readonly IMessageFormatter _formatter = FormatterUtilities.CreateFormatter();

    public FormatterUtilitiesTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
#if NETCOREAPP
        Assert.AreEqual("System.Text.Json", _formatter.Id);
#else
        Assert.AreEqual("Jsonite", _formatter.Id);
#endif
    }

    public void CanDeserializeTaskResponse()
    {
#pragma warning disable SA1009 // Closing parenthesis should be spaced correctly
#pragma warning disable SA1111 // Closing parenthesis should be on line of last parameter
        RpcMessage msg = _formatter.Deserialize<RpcMessage>("""
            {
                "jsonrpc": "2.0",
                "id": 1,
                "result": null
            }
            """
#if NETCOREAPP
            .AsMemory()
#endif
            );
#pragma warning restore SA1111 // Closing parenthesis should be on line of last parameter
#pragma warning restore SA1009 // Closing parenthesis should be spaced correctly

        var response = (ResponseMessage)msg;
        Assert.AreEqual(1, response.Id);
        Assert.AreEqual(null, response.Result);
    }

    [ArgumentsProvider(memberName: nameof(SerializerUtilities.SerializerTypes), memberType: typeof(SerializerUtilities),
        TestArgumentsEntryProviderMethodName = nameof(FormatSerializerTypes))]
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

    [Arguments(typeof(DiscoverRequestArgs))]
    [Arguments(typeof(RunRequestArgs))]
    public void DeserializeSpecificTypes(Type type)
    {
        var json = CreateSerializedInstance(type);
        Type? deserializer = SerializerUtilities.DeserializerTypes.SingleOrDefault(x => x == type);

        if (deserializer is not null)
        {
            var actual = Deserialize(deserializer, json);
            object expected = CreateInstance(type);

            if (type == typeof(DiscoverRequestArgs) || type == typeof(RunRequestArgs))
            {
                AssertRequestArgs(type, actual, expected);
            }
            else
            {
                Assert.AreEqual(actual, expected);
            }
        }
    }

    private void AssertRequestArgs(Type type, object actual, object expected)
    {
        RequestArgsBase? actualRequest = null;
        RequestArgsBase? expectedRequest = null;
        if (type == typeof(DiscoverRequestArgs))
        {
            actualRequest = (DiscoverRequestArgs)actual;
            expectedRequest = (DiscoverRequestArgs)expected;
        }
        else if (type == typeof(RunRequestArgs))
        {
            actualRequest = (RunRequestArgs)actual;
            expectedRequest = (RunRequestArgs)expected;
        }

        Assert.AreEqual(expectedRequest?.RunId, actualRequest?.RunId);
        Assert.AreEqual(expectedRequest?.TestNodes?.Count, actualRequest?.TestNodes?.Count);

        var actualTestNodes = actualRequest?.TestNodes?.ToArray();
        var expectedTestNodes = expectedRequest?.TestNodes?.ToArray();

        for (int i = 0; i < actualRequest?.TestNodes?.Count; i++)
        {
            CustomAssert(typeof(TestNode), actualTestNodes?[i]!, expectedTestNodes?[i]!);
        }

        Assert.AreEqual(expectedRequest?.GraphFilter, actualRequest?.GraphFilter);
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

    internal static TestArgumentsEntry<Type> FormatSerializerTypes(TestArgumentsContext testArgumentsContext)
        => new((Type)testArgumentsContext.Arguments, ((Type)testArgumentsContext.Arguments).Name);

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
            Assert.AreEqual("""{"EventName":"eventName","metrics":{"key":1}}""".Replace(" ", string.Empty), instanceSerialized, because);
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
            Assert.AreEqual("""{"uid":"uid","display-name":"DisplayName","tuples-key":[{"a":"1"},{"b":"2"}],"array-key":["1","2"],"time.start-utc":"2023-01-01T01:01:01.0000000+00:00","time.stop-utc":"2023-01-01T01:01:01.0000000+00:00","time.duration-ms":0,"location.namespace":"namespace","location.type":"typeName","location.method":"methodName(param1,param2)","location.file":"filePath","location.line-start":1,"location.line-end":2,"key":"value","node-type":"action","execution-state":"failed","error.message":"sample","error.stacktrace":"","assert.actual":"","assert.expected":""}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(TestNodeStateChangedEventArgs))
        {
            Assert.AreEqual("""{"runId":"00000000-0000-0000-0000-000000000000","changes":null}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(TestNodeUpdateMessage))
        {
            Assert.AreEqual("""{"node":{"uid":"uid","display-name":"DisplayName","tuples-key":[{"a":"1"},{"b":"2"}],"array-key":["1","2"],"time.start-utc":"2023-01-01T01:01:01.0000000+00:00","time.stop-utc":"2023-01-01T01:01:01.0000000+00:00","time.duration-ms":0,"location.namespace":"namespace","location.type":"typeName","location.method":"methodName(param1,param2)","location.file":"filePath","location.line-start":1,"location.line-end":2,"key":"value","node-type":"action","execution-state":"failed","error.message":"sample","error.stacktrace":"","assert.actual":"","assert.expected":""},"parent":"parent-uid"}""".Replace(" ", string.Empty), instanceSerialized, because);
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
            Assert.AreEqual("""{"supportsDiscovery":true,"experimental_multiRequestSupport":true,"vstestProvider":true}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(ServerCapabilities))
        {
            Assert.AreEqual("""{"testing":{"supportsDiscovery":true,"experimental_multiRequestSupport":true,"vstestProvider":true}}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(ServerInfo))
        {
            Assert.AreEqual("""{"name":"ServerInfo","version":"Version"}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(InitializeResponseArgs))
        {
            Assert.AreEqual("""{"serverInfo":{"name":"ServerInfoName","version":"Version"},"capabilities":{"testing":{"supportsDiscovery":true,"experimental_multiRequestSupport":true,"vstestProvider":true}}}""".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        if (type == typeof(ErrorMessage))
        {
            Assert.AreEqual("""{"jsonrpc":"2.0","code":2,"id":1,"error":{"code":2,"data":{},"message":"This is error"}}""".Replace(" ", string.Empty), instanceSerialized, because);
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

        if (type == typeof(object))
        {
            Assert.AreEqual("{}".Replace(" ", string.Empty), instanceSerialized, because);
            return;
        }

        throw new NotImplementedException($"Assertion not implemented '{type}', value to assert:\n{instanceSerialized}");
    }

    private static string CreateSerializedInstance(Type type)
    {
        return type == typeof(DiscoverRequestArgs) || type == typeof(RunRequestArgs)
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
    }

    private static object CreateInstance(Type type)
    {
        if (type == typeof(AttachDebuggerInfoArgs))
        {
            return new AttachDebuggerInfoArgs(1);
        }

        if (type == typeof(ProcessInfoArgs))
        {
            return new ProcessInfoArgs("program", "arts", "workingDir", new Dictionary<string, string?>() { { "key", "value" } });
        }

        if (type == typeof(KeyValuePair<string, string>))
        {
            return new KeyValuePair<string, string>("key", "value");
        }

        if (type == typeof(TelemetryEventArgs))
        {
            return new TelemetryEventArgs("eventName", new Dictionary<string, object>() { { "key", 1 } });
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
                new Extensions.Messages.TestNodeUid("parent-uid"));
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

        if (type == typeof(ServerTestingCapabilities))
        {
            return new ServerTestingCapabilities(true, true, true);
        }

        if (type == typeof(ServerCapabilities))
        {
            return new ServerCapabilities(new ServerTestingCapabilities(true, true, true));
        }

        if (type == typeof(ServerInfo))
        {
            return new ServerInfo("ServerInfo", "Version");
        }

        if (type == typeof(InitializeResponseArgs))
        {
            return new InitializeResponseArgs(new ServerInfo("ServerInfoName", "Version"), new ServerCapabilities(new ServerTestingCapabilities(true, true, true)));
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
                new TestNode[]
                {
                    new()
                    {
                        Uid = new TestNodeUid("UnitTest1.TestMethod1"),
                        DisplayName = "test1",
                        Properties = new PropertyBag(new TestFileLocationProperty("filePath", new LinePositionSpan(new(1, 0), new(2, 0)))),
                    },
                },
                null);
        }

        if (type == typeof(RunRequestArgs))
        {
            return new RunRequestArgs(
                Guid.Empty,
                new TestNode[]
                {
                    new()
                    {
                        Uid = new TestNodeUid("UnitTest1.TestMethod1"),
                        DisplayName = "test1",
                        Properties = new PropertyBag(new TestFileLocationProperty("filePath", new LinePositionSpan(new(1, 0), new(2, 0)))),
                    },
                },
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
                Uid = new Extensions.Messages.TestNodeUid("uid"),
            };

            testNode.Properties.Add(new SerializableKeyValuePairStringProperty("key", "value"));
            testNode.Properties.Add(new TestFileLocationProperty("filePath", new LinePositionSpan(new(1, 0), new(2, 0))));
            testNode.Properties.Add(new TestMethodIdentifierProperty("assemblyFullName", "namespace", "typeName", "methodName", ["param1", "param2"], "returnTypeFullName"));
            testNode.Properties.Add(new TimingProperty(new TimingInfo(new DateTimeOffset(2023, 01, 01, 01, 01, 01, TimeSpan.Zero), new DateTimeOffset(2023, 01, 01, 01, 01, 01, TimeSpan.Zero), TimeSpan.Zero)));
            testNode.Properties.Add(new FailedTestNodeStateProperty(new InvalidOperationException("sample")));
            testNode.Properties.Add(new SerializableNamedArrayStringProperty("array-key", ["1", "2"]));
            testNode.Properties.Add(new SerializableNamedKeyValuePairsStringProperty("tuples-key", [new KeyValuePair<string, string>("a", "1"), new KeyValuePair<string, string>("b", "2"),]));

            return testNode;
        }
    }

    private object Deserialize(Type type, string instanceSerialized)
    {
        if (type == typeof(ErrorMessage))
        {
#if NETCOREAPP
            return _formatter.Deserialize<ErrorMessage>(instanceSerialized.AsMemory());
#else
            return _formatter.Deserialize<ErrorMessage>(instanceSerialized);
#endif
        }

        if (type == typeof(InitializeResponseArgs))
        {
#if NETCOREAPP
            return _formatter.Deserialize<InitializeResponseArgs>(instanceSerialized.AsMemory());
#else
            return _formatter.Deserialize<InitializeResponseArgs>(instanceSerialized);
#endif
        }

        if (type == typeof(ServerInfo))
        {
#if NETCOREAPP
            return _formatter.Deserialize<ServerInfo>(instanceSerialized.AsMemory());
#else
            return _formatter.Deserialize<ServerInfo>(instanceSerialized);
#endif
        }

        if (type == typeof(CancelRequestArgs))
        {
#if NETCOREAPP
            return _formatter.Deserialize<CancelRequestArgs>(instanceSerialized.AsMemory());
#else
            return _formatter.Deserialize<CancelRequestArgs>(instanceSerialized);
#endif
        }

        if (type == typeof(TestNode))
        {
#if NETCOREAPP
            return _formatter.Deserialize<TestNode>(instanceSerialized.AsMemory());
#else
            return _formatter.Deserialize<TestNode>(instanceSerialized);
#endif
        }

        if (type == typeof(DiscoverRequestArgs))
        {
#if NETCOREAPP
            return _formatter.Deserialize<DiscoverRequestArgs>(instanceSerialized.AsMemory());
#else
            return _formatter.Deserialize<DiscoverRequestArgs>(instanceSerialized);
#endif
        }

        if (type == typeof(RunRequestArgs))
        {
#if NETCOREAPP
            return _formatter.Deserialize<RunRequestArgs>(instanceSerialized.AsMemory());
#else
            return _formatter.Deserialize<RunRequestArgs>(instanceSerialized);
#endif
        }

        if (type == typeof(ServerCapabilities))
        {
#if NETCOREAPP
            return _formatter.Deserialize<ServerCapabilities>(instanceSerialized.AsMemory());
#else
            return _formatter.Deserialize<ServerCapabilities>(instanceSerialized);
#endif
        }

        throw new NotImplementedException($"Deserializer for type not implemented '{type}'");
    }
}
