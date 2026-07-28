// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.ServerMode.Json;

using JsonDocument = System.Text.Json.JsonDocument;
using JsonElement = System.Text.Json.JsonElement;

using TestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class JsonTests
{
    private readonly Json _json;

    public JsonTests()
    {
        Dictionary<Type, JsonSerializer> serializers = [];
        Dictionary<Type, JsonDeserializer> deserializers = [];

        foreach (Type serializableType in SerializerUtilities.SerializerTypes)
        {
            serializers[serializableType] = new JsonObjectSerializer<object>(
                o => [.. SerializerUtilities.Serialize(serializableType, o).Select(kvp => (kvp.Key, kvp.Value))]);
        }

        foreach (Type deserializableType in SerializerUtilities.DeserializerTypes)
        {
            // By default we wrap the jsonite serialization, we can override specific types inside the Json .NET runtime implementation.
            deserializers[deserializableType] = new JsonElementDeserializer<object>((json, doc)
                => SerializerUtilities.Deserialize(deserializableType, json.Bind<JsoniteProperties>(doc)!));
        }

        _json = new Json(serializers, deserializers);
    }

    [TestMethod]
    public async Task Serialize_TestNodeAsync()
    {
        // Arrange
        PropertyBag bag = new(new SerializableKeyValuePairStringProperty("hello", "my friend"));

        TestNode testNode = new()
        {
            DisplayName = "test",
            Properties = bag,
            Uid = new TestNodeUid("11111"),
        };

        // Act
        string actual = await _json.SerializeAsync(testNode);

        // Assert
        Assert.AreEqual("""{"uid":"11111","display-name":"test","hello":"my friend","node-type":"group"}""", actual);
    }

    [TestMethod]
    public async Task Serialize_Array()
    {
        // Arrange
        Json json = new();

        // Act
        string actual = await json.SerializeAsync(new int[] { 1, 2, 3 });

        // Assert
        Assert.AreEqual("[1,2,3]", actual);
    }

    [TestMethod]
    public async Task Serialize_DateTimeOffset()
    {
        // Arrange
        Json json = new();

        // Act
        string actual = await json.SerializeAsync(new DateTimeOffset(2023, 01, 01, 01, 01, 01, 01, TimeSpan.Zero));

        // Assert
        Assert.AreEqual("2023-01-01T01:01:01.0010000+00:00", actual.Trim('"'));
    }

    [TestMethod]
    public async Task Serialize_ArrayOfObjects()
    {
        // Arrange
        Dictionary<Type, JsonSerializer> converters = new()
        {
            [typeof(Person)] = new JsonObjectSerializer<Person>(
                n =>
                [
                    ("name", n.Name),
                    ("children", n.Children)
                ]),
        };

        Person person = new()
        {
            Name = "Thomas",
            Children =
            [
                new()
                {
                    Name = "Ruth",
                },
            ],
        };

        Json json = new(converters);

        // Act
        string actual = await json.SerializeAsync(new object[] { person, new[] { 2 }, 3 });

        // Assert
        Assert.AreEqual("""[{"name":"Thomas","children":[{"name":"Ruth","children":null}]},[2],3]""", actual);
    }

    [TestMethod]
    public async Task Serialize_ErrorMessage_EmitsJsonRpc20CompliantShape()
    {
        // Arrange
        ErrorMessage errorMessage = new(
            Id: 42,
            ErrorCode: -32601,
            Message: "Method not found",
            Data: null);

        // Act
        string actual = await _json.SerializeAsync(errorMessage);

        // Assert
        // Per JSON-RPC 2.0 §5.1, "code" MUST live inside the "error" object and MUST NOT
        // appear at the top level of the response envelope.
        using var document = JsonDocument.Parse(actual);
        JsonElement root = document.RootElement;

        Assert.AreEqual("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.AreEqual(42, root.GetProperty("id").GetInt32());
        Assert.IsFalse(
            root.TryGetProperty("code", out _),
            "Top-level 'code' must not be present; it belongs inside the 'error' object per JSON-RPC 2.0 §5.1.");

        JsonElement error = root.GetProperty("error");
        Assert.AreEqual(-32601, error.GetProperty("code").GetInt32());
        Assert.AreEqual("Method not found", error.GetProperty("message").GetString());
    }

    [TestMethod]
    public async Task Serialize_ErrorMessage_RoundTripsViaDeserializer()
    {
        // Arrange
        ErrorMessage original = new(
            Id: 7,
            ErrorCode: -32000,
            Message: "Server error",
            Data: null);

        // Act
        string serialized = await _json.SerializeAsync(original);
        ErrorMessage roundTripped = _json.Deserialize<ErrorMessage>(serialized.AsMemory());

        // Assert
        Assert.AreEqual(original.Id, roundTripped.Id);
        Assert.AreEqual(original.ErrorCode, roundTripped.ErrorCode);
        Assert.AreEqual(original.Message, roundTripped.Message);
    }

    [TestMethod]
    public void DeserializePerson()
    {
        // Arrange
        Json json = new(null, new Dictionary<Type, JsonDeserializer>
        {
            [typeof(Person)] = new JsonElementDeserializer<Person>((json, jsonElement) => new Person
            {
                Name = json.Bind<string?>(jsonElement, "name"),
            }),
        });

        // Act
        Person actual = json.Deserialize<Person>(new("""{"name":"Thomas"}""".ToCharArray()));

        // Assert
        Assert.AreEqual("Thomas", actual.Name);
    }

    [TestMethod]
    public void GetProperties_WhenPropertiesDelegateIsNotSet_ThrowsInvalidOperationException()
    {
        // Arrange
        var serializer = new TestJsonObjectSerializer();

        // Act
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => serializer.GetProperties(new object()));

        // Assert
        Assert.Contains(nameof(JsonObjectSerializer.Properties), exception.Message);
        Assert.Contains(nameof(TestJsonObjectSerializer), exception.Message);
    }

    [TestMethod]
    public void Deserialize_InitializeRequest_WithIsStatefulTrue_StjPath_SurfacesStatefulClient()
    {
        // Arrange
        Json json = new();
        const string initializeParams = """
            {
                "processId": 1,
                "clientInfo": { "name": "client", "version": "1.0.0" },
                "capabilities": { "testing": { "debuggerProvider": true, "isStateful": true } }
            }
            """;

        // Act
        InitializeRequestArgs args = json.Deserialize<InitializeRequestArgs>(initializeParams.AsMemory());

        // Assert
        Assert.IsTrue(args.Capabilities.IsStateful);
    }

    [TestMethod]
    public void Deserialize_InitializeRequest_WithoutIsStateful_StjPath_DefaultsToStateless()
    {
        // Arrange
        Json json = new();
        const string initializeParams = """
            {
                "processId": 1,
                "clientInfo": { "name": "client", "version": "1.0.0" },
                "capabilities": { "testing": { "debuggerProvider": true } }
            }
            """;

        // Act
        InitializeRequestArgs args = json.Deserialize<InitializeRequestArgs>(initializeParams.AsMemory());

        // Assert
        Assert.IsFalse(args.Capabilities.IsStateful);
    }

    [TestMethod]
    public void Deserialize_ClientCapabilities_WithIsStatefulTrue_JsonitePath_SurfacesStatefulClient()
    {
        // Arrange
        Dictionary<string, object?> properties = new()
        {
            ["capabilities"] = new Dictionary<string, object?>
            {
                ["testing"] = new Dictionary<string, object?>
                {
                    ["debuggerProvider"] = true,
                    ["isStateful"] = true,
                },
            },
        };

        // Act
        ClientCapabilities capabilities = SerializerUtilities.Deserialize<ClientCapabilities>(properties);

        // Assert
        Assert.IsTrue(capabilities.IsStateful);
    }

    [TestMethod]
    public void Deserialize_ClientCapabilities_WithoutIsStateful_JsonitePath_DefaultsToStateless()
    {
        // Arrange
        Dictionary<string, object?> properties = new()
        {
            ["capabilities"] = new Dictionary<string, object?>
            {
                ["testing"] = new Dictionary<string, object?>
                {
                    ["debuggerProvider"] = true,
                },
            },
        };

        // Act
        ClientCapabilities capabilities = SerializerUtilities.Deserialize<ClientCapabilities>(properties);

        // Assert
        Assert.IsFalse(capabilities.IsStateful);
    }

    private sealed class TestJsonObjectSerializer : JsonObjectSerializer;

    private sealed class Person
    {
        public string? Name { get; set; }

        public List<Person>? Children { get; set; }
    }
}
