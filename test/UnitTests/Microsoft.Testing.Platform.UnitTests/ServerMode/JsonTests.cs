// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.ServerMode.Json;

using TestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class JsonTests : TestBase
{
    private readonly Json _json;

    public JsonTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        Dictionary<Type, JsonSerializer> serializers = new();
        Dictionary<Type, JsonDeserializer> deserializers = new();

        foreach (Type serializableType in SerializerUtilities.SerializerTypes)
        {
            serializers[serializableType] = new JsonObjectSerializer<object>(
                o => SerializerUtilities.Serialize(serializableType, o).Select(kvp => (kvp.Key, kvp.Value)).ToArray());
        }

        foreach (Type deserializableType in SerializerUtilities.DeserializerTypes)
        {
            // By default we wrap the jsonite serialization, we can override specific types inside the Json .NET runtime implementation.
            deserializers[deserializableType] = new JsonElementDeserializer<object>((json, doc)
                => SerializerUtilities.Deserialize(deserializableType, json.Bind<JsoniteProperties>(doc)!));
        }

        _json = new Json(serializers, deserializers);
    }

    public async Task Serialize_TestNodeAsync()
    {
        // Arrange
        PropertyBag bag = new(new SerializableKeyValuePairStringProperty("hello", "my friend"));

        TestNode testNode = new()
        {
            DisplayName = "test",
            Properties = bag,
            Uid = new Extensions.Messages.TestNodeUid("11111"),
        };

        // Act
        string actual = await _json.SerializeAsync(testNode);

        // Assert
        Assert.AreEqual("""{"uid":"11111","display-name":"test","hello":"my friend","node-type":"group"}""", actual);
    }

    public async Task Serialize_Array()
    {
        // Arrange
        Json json = new();

        // Act
        string actual = await json.SerializeAsync(new int[] { 1, 2, 3 });

        // Assert
        Assert.AreEqual("[1,2,3]", actual);
    }

    public async Task Serialize_DateTimeOffset()
    {
        // Arrange
        Json json = new();

        // Act
        string actual = await json.SerializeAsync(new DateTimeOffset(2023, 01, 01, 01, 01, 01, 01, TimeSpan.Zero));

        // Assert
        Assert.AreEqual("2023-01-01T01:01:01.0010000+00:00", actual.Trim('"'));
    }

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

    public void DeserializePerson()
    {
        // Arrange
        Json json = new(null, new Dictionary<Type, JsonDeserializer>
        {
            [typeof(Person)] = new JsonElementDeserializer<Person>((json, jsonElement) => new Person
            {
                Name = json.Bind<string?>(jsonElement, "name"),
                Children = json.Bind<List<Person>>(jsonElement, "children"),
            }),

            [typeof(List<Person>)] = new JsonCollectionDeserializer<List<Person>, Person>(_ => [], (c, i) => c.Add(i)),
        });

        // Act
        Person actual = json.Deserialize<Person>(new("""{"name":"Thomas","children":[{"name":"Ruth","children":null}]}""".ToCharArray()));

        // Assert
        Assert.AreEqual("Thomas", actual.Name);
        Assert.AreEqual(1, actual.Children!.Count);
        Assert.AreEqual("Ruth", actual.Children![0].Name);
        Assert.IsNull(actual.Children![0].Children);
    }

    public void DeserializePersonList()
    {
        // Arrange
        Json json = new(null, new Dictionary<Type, JsonDeserializer>
        {
            [typeof(Person)] = new JsonElementDeserializer<Person>((json, jsonElement) => new Person
            {
                Name = json.Bind<string?>(jsonElement, "name"),
                Children = json.Bind<List<Person>>(jsonElement, "children"),
            }),

            [typeof(List<Person>)] = new JsonCollectionDeserializer<List<Person>, Person>(_ => [], (c, i) => c.Add(i)),
        });

        // Act
        List<Person> actual = json.Deserialize<List<Person>>(new("""[{"name":"Thomas","children":[{"name":"Ruth","children":null}]}]""".ToCharArray()));

        // Assert
        Assert.AreEqual(1, actual.Count);
        Assert.AreEqual("Thomas", actual[0].Name);
    }

    private sealed class Person
    {
        public string? Name { get; set; }

        public List<Person>? Children { get; set; }
    }
}
