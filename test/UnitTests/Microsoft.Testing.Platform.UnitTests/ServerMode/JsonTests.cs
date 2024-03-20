// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.ServerMode.Json;
using Microsoft.Testing.TestInfrastructure;

using TestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class JsonTests : TestBase
{
    private readonly Json _json;

    public JsonTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        var serializers = new Dictionary<Type, JsonSerializer>();
        var deserializers = new Dictionary<Type, JsonDeserializer>();

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
        var bag = new PropertyBag(new SerializableKeyValuePairStringProperty("hello", "my friend"));

        var testNode = new TestNode
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
        var json = new Json();

        // Act
        string actual = await json.SerializeAsync(new int[] { 1, 2, 3 });

        // Assert
        Assert.AreEqual("[1,2,3]", actual);
    }

    public async Task Serialize_DateTimeOffset()
    {
        // Arrange
        var json = new Json();

        // Act
        string actual = await json.SerializeAsync(new DateTimeOffset(2023, 01, 01, 01, 01, 01, 01, TimeSpan.Zero));

        // Assert
        Assert.AreEqual("2023-01-01T01:01:01.0010000+00:00", actual.Trim('"'));
    }

    public async Task Serialize_ArrayOfObjects()
    {
        // Arrange
        var converters = new Dictionary<Type, JsonSerializer>
        {
            [typeof(Person)] = new JsonObjectSerializer<Person>(
                n => new (string Key, object? Value)[]
                {
                    ("name", n.Name),
                    ("children", n.Children),
                }),
        };

        var person = new Person
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

        var json = new Json(converters);

        // Act
        string actual = await json.SerializeAsync(new object[] { person, new[] { 2 }, 3 });

        // Assert
        Assert.AreEqual("""[{"name":"Thomas","children":[{"name":"Ruth","children":null}]},[2],3]""", actual);
    }

    public void DeserializePerson()
    {
        // Arrange
        var json = new Json(null, new Dictionary<Type, JsonDeserializer>
        {
            [typeof(Person)] = new JsonElementDeserializer<Person>((json, jsonElement) => new Person
            {
                Name = json.Bind<string?>(jsonElement, "name"),
                Children = json.Bind<List<Person>>(jsonElement, "children"),
            }),

            [typeof(List<Person>)] = new JsonCollectionDeserializer<List<Person>, Person>(_ => new List<Person>(), (c, i) => c.Add(i)),
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
        var json = new Json(null, new Dictionary<Type, JsonDeserializer>
        {
            [typeof(Person)] = new JsonElementDeserializer<Person>((json, jsonElement) => new Person
            {
                Name = json.Bind<string?>(jsonElement, "name"),
                Children = json.Bind<List<Person>>(jsonElement, "children"),
            }),

            [typeof(List<Person>)] = new JsonCollectionDeserializer<List<Person>, Person>(_ => new List<Person>(), (c, i) => c.Add(i)),
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
