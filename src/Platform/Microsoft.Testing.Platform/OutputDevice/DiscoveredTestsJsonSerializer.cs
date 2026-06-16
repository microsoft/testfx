// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Serializes the result of <c>--list-tests json</c> into a single JSON document with as much
/// information as is available for each <see cref="TestNode"/>.
/// </summary>
/// <remarks>
/// Schema history (bump <see cref="SchemaVersion"/> on any breaking change):
/// <list type="number">
///   <item>
///     <description>
///       Initial schema. Top-level: <c>schemaVersion</c> (int), <c>tests</c> (array of test
///       objects). Per test: <c>uid</c>, <c>displayName</c>, plus optional <c>type</c>
///       (assembly / namespace / type / method / arity / params / return — from
///       <see cref="TestMethodIdentifierProperty"/>), <c>location</c> (file path, start line,
///       end line — from <see cref="TestFileLocationProperty"/>), <c>traits</c> (array of
///       <c>{ key, value }</c> from <see cref="TestMetadataProperty"/>), <c>properties</c>
///       (array of <c>{ key, value }</c> from
///       <see cref="SerializableKeyValuePairStringProperty"/>; array — not object — so
///       duplicate keys survive serialization). Absent fields are omitted;
///       <c>type.namespace</c> is omitted for the global namespace.
///     </description>
///   </item>
/// </list>
/// </remarks>
internal static class DiscoveredTestsJsonSerializer
{
    /// <summary>
    /// Schema version of the produced JSON document. Increment when introducing a breaking change.
    /// </summary>
    internal const int SchemaVersion = 1;

    /// <summary>
    /// Serializes the discovered tests to a pretty-printed JSON document.
    /// </summary>
    /// <param name="tests">The discovered tests.</param>
    /// <returns>The JSON document.</returns>
    public static string Serialize(IEnumerable<TestNode> tests)
    {
        var writer = new JsonStringWriter();
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteStartArray("tests");
        foreach (TestNode test in tests)
        {
            WriteTestNode(writer, test);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        return writer.ToString();
    }

    private static void WriteTestNode(JsonStringWriter writer, TestNode test)
    {
        writer.WriteStartObject();
        writer.WriteString("uid", test.Uid.Value);
        writer.WriteString("displayName", test.DisplayName);

        // Collect all required properties in a single linked-list pass — replacing
        // 4 × PropertyBag traversals (2 × SingleOrDefault + 2 × OfType) with one
        // zero-allocation GetStructEnumerator() pass per discovered test.
        TestMethodIdentifierProperty? methodIdentifier = null;
        TestFileLocationProperty? fileLocation = null;
        List<TestMetadataProperty>? traits = null;
        List<SerializableKeyValuePairStringProperty>? kvps = null;

        using PropertyBag.PropertyBagEnumerator enumerator = test.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TestMethodIdentifierProperty m: methodIdentifier = GetSingleOrDefaultValue(methodIdentifier, m); break;
                case TestFileLocationProperty l: fileLocation = GetSingleOrDefaultValue(fileLocation, l); break;
                case TestMetadataProperty meta: (traits ??= []).Add(meta); break;
                case SerializableKeyValuePairStringProperty kvp: (kvps ??= []).Add(kvp); break;
            }
        }

        static TProperty GetSingleOrDefaultValue<TProperty>(TProperty? existingProperty, TProperty property)
            where TProperty : class, IProperty
            => existingProperty is not null
                ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                : property;

        if (methodIdentifier is not null)
        {
            writer.WriteStartObject("type");
            writer.WriteString("assemblyFullName", methodIdentifier.AssemblyFullName);
            if (!RoslynString.IsNullOrEmpty(methodIdentifier.Namespace))
            {
                writer.WriteString("namespace", methodIdentifier.Namespace);
            }

            writer.WriteString("typeName", methodIdentifier.TypeName);
            writer.WriteString("methodName", methodIdentifier.MethodName);
            writer.WriteNumber("methodArity", methodIdentifier.MethodArity);
            writer.WriteString("returnTypeFullName", methodIdentifier.ReturnTypeFullName);
            writer.WriteStartArray("parameterTypeFullNames");
            foreach (string parameterFullName in methodIdentifier.ParameterTypeFullNames)
            {
                writer.WriteStringValue(parameterFullName);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        if (fileLocation is not null)
        {
            writer.WriteStartObject("location");
            writer.WriteString("file", fileLocation.FilePath);
            writer.WriteNumber("lineStart", fileLocation.LineSpan.Start.Line);
            writer.WriteNumber("lineEnd", fileLocation.LineSpan.End.Line);
            writer.WriteEndObject();
        }

        if (traits is not null)
        {
            // PropertyBag prepends on Add, so GetStructEnumerator yields in reverse insertion order.
            // Reverse here so the JSON reflects the order in which the adapter recorded the traits,
            // which is what consumers will reasonably expect and what makes diffs stable across runs.
            traits.Reverse();
            writer.WriteStartArray("traits");
            foreach (TestMetadataProperty trait in traits)
            {
                writer.WriteStartObject();
                writer.WriteString("key", trait.Key);
                writer.WriteString("value", trait.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        if (kvps is not null)
        {
            // Same rationale as traits above: reverse so the JSON reflects insertion order.
            // Emit as an array of {key, value} (mirroring traits) so duplicate keys — which
            // PropertyBag allows — survive serialization. A JSON object would silently collapse them.
            kvps.Reverse();
            writer.WriteStartArray("properties");
            foreach (SerializableKeyValuePairStringProperty kvp in kvps)
            {
                writer.WriteStartObject();
                writer.WriteString("key", kvp.Key);
                writer.WriteString("value", kvp.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
