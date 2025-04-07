// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode.Json;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class JsonTests
{
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

    [TestMethod]
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
