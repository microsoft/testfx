// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class DiscoveredTestsJsonSerializerTests
{
    [TestMethod]
    public void Serialize_Empty_ProducesEnvelopeWithNoTests()
    {
        string json = DiscoveredTestsJsonSerializer.Serialize([]);

        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.AreEqual(DiscoveredTestsJsonSerializer.SchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual(JsonValueKind.Array, root.GetProperty("tests").ValueKind);
        Assert.AreEqual(0, root.GetProperty("tests").GetArrayLength());
    }

    [TestMethod]
    public void Serialize_TestNodeWithOnlyUidAndDisplayName_IncludesNoOptionalFields()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("abc"),
            DisplayName = "MyDisplay",
        };

        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        JsonElement test = document.RootElement.GetProperty("tests")[0];

        Assert.AreEqual("abc", test.GetProperty("uid").GetString());
        Assert.AreEqual("MyDisplay", test.GetProperty("displayName").GetString());
        Assert.IsFalse(test.TryGetProperty("type", out _));
        Assert.IsFalse(test.TryGetProperty("location", out _));
        Assert.IsFalse(test.TryGetProperty("traits", out _));
        Assert.IsFalse(test.TryGetProperty("properties", out _));
    }

    [TestMethod]
    public void Serialize_TestNodeWithMethodIdentifier_EmitsTypeBlock()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid1"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                    @namespace: "My.Tests",
                    typeName: "MyClass+Nested`1",
                    methodName: "MyMethod",
                    methodArity: 2,
                    parameterTypeFullNames: ["System.Int32", "System.String"],
                    returnTypeFullName: "System.Threading.Tasks.Task")),
        };

        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        JsonElement type = document.RootElement.GetProperty("tests")[0].GetProperty("type");

        Assert.AreEqual("MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", type.GetProperty("assemblyFullName").GetString());
        Assert.AreEqual("My.Tests", type.GetProperty("namespace").GetString());
        Assert.AreEqual("MyClass+Nested`1", type.GetProperty("typeName").GetString());
        Assert.AreEqual("MyMethod", type.GetProperty("methodName").GetString());
        Assert.AreEqual(2, type.GetProperty("methodArity").GetInt32());
        Assert.AreEqual("System.Threading.Tasks.Task", type.GetProperty("returnTypeFullName").GetString());

        JsonElement parameters = type.GetProperty("parameterTypeFullNames");
        Assert.AreEqual(2, parameters.GetArrayLength());
        Assert.AreEqual("System.Int32", parameters[0].GetString());
        Assert.AreEqual("System.String", parameters[1].GetString());
    }

    [TestMethod]
    public void Serialize_TestNodeWithFileLocation_EmitsLocationBlock()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                new TestFileLocationProperty("/repo/src/Test.cs", new LinePositionSpan(new LinePosition(10, 0), new LinePosition(20, 0)))),
        };

        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        JsonElement location = document.RootElement.GetProperty("tests")[0].GetProperty("location");

        Assert.AreEqual("/repo/src/Test.cs", location.GetProperty("file").GetString());
        Assert.AreEqual(10, location.GetProperty("lineStart").GetInt32());
        Assert.AreEqual(20, location.GetProperty("lineEnd").GetInt32());
    }

    [TestMethod]
    public void Serialize_TestNodeWithTraits_EmitsTraitsArray()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                new TestMetadataProperty("Category", "Integration"),
                new TestMetadataProperty("Priority", "1")),
        };

        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        JsonElement traits = document.RootElement.GetProperty("tests")[0].GetProperty("traits");

        Assert.AreEqual(2, traits.GetArrayLength());

        // The serializer reverses PropertyBag.OfType so the JSON reflects insertion order.
        Assert.AreEqual("Category", traits[0].GetProperty("key").GetString());
        Assert.AreEqual("Integration", traits[0].GetProperty("value").GetString());
        Assert.AreEqual("Priority", traits[1].GetProperty("key").GetString());
        Assert.AreEqual("1", traits[1].GetProperty("value").GetString());
    }

    [TestMethod]
    public void Serialize_TestNodeWithKeyValueProperties_EmitsPropertiesArray()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                new SerializableKeyValuePairStringProperty("ExecutionId", "exec-123"),
                new SerializableKeyValuePairStringProperty("Other", "value")),
        };

        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        JsonElement properties = document.RootElement.GetProperty("tests")[0].GetProperty("properties");

        // Emitted as array of {key, value} to preserve insertion order and allow duplicates.
        Assert.AreEqual(JsonValueKind.Array, properties.ValueKind);
        Assert.AreEqual(2, properties.GetArrayLength());
        Assert.AreEqual("ExecutionId", properties[0].GetProperty("key").GetString());
        Assert.AreEqual("exec-123", properties[0].GetProperty("value").GetString());
        Assert.AreEqual("Other", properties[1].GetProperty("key").GetString());
        Assert.AreEqual("value", properties[1].GetProperty("value").GetString());
    }

    [TestMethod]
    public void Serialize_TestNodeWithDuplicatePropertyKeys_PreservesBothEntries()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                new SerializableKeyValuePairStringProperty("Tag", "first"),
                new SerializableKeyValuePairStringProperty("Tag", "second")),
        };

        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        JsonElement properties = document.RootElement.GetProperty("tests")[0].GetProperty("properties");

        Assert.AreEqual(2, properties.GetArrayLength());
        Assert.AreEqual("Tag", properties[0].GetProperty("key").GetString());
        Assert.AreEqual("first", properties[0].GetProperty("value").GetString());
        Assert.AreEqual("Tag", properties[1].GetProperty("key").GetString());
        Assert.AreEqual("second", properties[1].GetProperty("value").GetString());
    }

    [TestMethod]
    public void Serialize_TestNodeWithGlobalNamespace_OmitsNamespaceField()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly",
                    @namespace: string.Empty,
                    typeName: "GlobalType",
                    methodName: "GlobalMethod",
                    methodArity: 0,
                    parameterTypeFullNames: [],
                    returnTypeFullName: "System.Void")),
        };

        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        JsonElement type = document.RootElement.GetProperty("tests")[0].GetProperty("type");

        Assert.IsFalse(type.TryGetProperty("namespace", out _));
        Assert.AreEqual("GlobalType", type.GetProperty("typeName").GetString());
    }

    [TestMethod]
    public void Serialize_DisplayNameWithSpecialCharacters_EscapesProperly()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "Quotes\" Backslash\\ Newline\n Tab\t",
        };

        // Should round-trip through JsonDocument without throwing.
        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        Assert.AreEqual(
            "Quotes\" Backslash\\ Newline\n Tab\t",
            document.RootElement.GetProperty("tests")[0].GetProperty("displayName").GetString());
    }

    [TestMethod]
    public void Serialize_DisplayNameWithLoneSurrogate_ReplacesWithReplacementCharacter()
    {
        // Lone high surrogate followed by a non-surrogate.
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "\uD83DZ",
        };

        // Must still parse as valid JSON; the lone surrogate is replaced by U+FFFD.
        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        Assert.AreEqual(
            "\uFFFDZ",
            document.RootElement.GetProperty("tests")[0].GetProperty("displayName").GetString());
    }

    [TestMethod]
    public void Serialize_DisplayNameWithValidSurrogatePair_PreservesPair()
    {
        // 😀 = U+1F600 encoded as surrogate pair U+D83D U+DE00.
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "Smile \uD83D\uDE00",
        };

        using var document = JsonDocument.Parse(DiscoveredTestsJsonSerializer.Serialize([node]));
        Assert.AreEqual(
            "Smile \uD83D\uDE00",
            document.RootElement.GetProperty("tests")[0].GetProperty("displayName").GetString());
    }

    [TestMethod]
    public void Serialize_DisplayNameWithLineAndParagraphSeparators_EscapesThem()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "line\u2028para\u2029end",
        };

        string json = DiscoveredTestsJsonSerializer.Serialize([node]);
        Assert.Contains("\\u2028", json);
        Assert.Contains("\\u2029", json);

        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(
            "line\u2028para\u2029end",
            document.RootElement.GetProperty("tests")[0].GetProperty("displayName").GetString());
    }
}
