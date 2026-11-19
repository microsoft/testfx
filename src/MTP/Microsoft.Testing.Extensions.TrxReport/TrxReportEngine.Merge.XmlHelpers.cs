// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private static XElement? FindChild(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.Ordinal));

    private static void CloneChildrenInto(XElement? source, XElement destination)
    {
        if (source is null)
        {
            return;
        }

        foreach (XElement child in source.Elements())
        {
            destination.Add(new XElement(child));
        }
    }

    /// <summary>
    /// Merges one input's <c>TestDefinitions</c> into the accumulator and returns the id remap to apply to
    /// that input's <c>testId</c> references. An id not seen before is kept; an id whose definition is
    /// identical to the one already kept is deduplicated (the schema forbids duplicate ids); an id whose
    /// definition differs (e.g. the same test from another TFM with different storage) is remapped to a
    /// fresh deterministic id and kept, so module-specific definitions are preserved.
    /// </summary>
    private static Dictionary<string, string> MergeTestDefinitions(XElement? testDefinitions, XElement mergedTestDefinitions, Dictionary<string, XElement> definitionsById, int inputIndex)
    {
        var remap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (testDefinitions is null)
        {
            return remap;
        }

        foreach (XElement definition in testDefinitions.Elements())
        {
            string? id = definition.Attribute("id")?.Value;
            if (id is null)
            {
                mergedTestDefinitions.Add(new XElement(definition));
                continue;
            }

            if (!definitionsById.TryGetValue(id, out XElement? existing))
            {
                definitionsById[id] = definition;
                mergedTestDefinitions.Add(new XElement(definition));
            }
            else if (XNode.DeepEquals(existing, definition))
            {
                // Identical definition already kept — deduplicate.
            }
            else
            {
                string newId = RemapDefinitionId(id, inputIndex);
                while (definitionsById.ContainsKey(newId))
                {
                    newId = RemapDefinitionId(newId, inputIndex);
                }

                var clone = new XElement(definition);
                clone.SetAttributeValue("id", newId);
                definitionsById[newId] = clone;
                mergedTestDefinitions.Add(clone);
                remap[id] = newId;
            }
        }

        return remap;
    }

    /// <summary>
    /// Clones the children of <paramref name="source"/> into <paramref name="destination"/>, rewriting any
    /// <c>testId</c> attribute (on the child or its descendants) through <paramref name="remap"/> so a
    /// remapped TestDefinition's results/entries reference the right definition.
    /// </summary>
    private static void CloneWithRemappedTestIds(XElement? source, XElement destination, Dictionary<string, string> remap)
    {
        if (source is null)
        {
            return;
        }

        foreach (XElement child in source.Elements())
        {
            var clone = new XElement(child);
            if (remap.Count > 0)
            {
                foreach (XElement element in clone.DescendantsAndSelf())
                {
                    if (element.Attribute("testId") is { } testId && remap.TryGetValue(testId.Value, out string? newId))
                    {
                        testId.Value = newId;
                    }
                }
            }

            destination.Add(clone);
        }
    }

    /// <summary>
    /// Derives a stable, distinct id for a TestDefinition that collides with a materially-different one,
    /// from the original id and the input index, so the remap is deterministic (RFC 018 idempotency).
    /// </summary>
    private static string RemapDefinitionId(string originalId, int inputIndex)
    {
        const ulong fnvPrime = 1099511628211UL;
        ulong low = 14695981039346656037UL;
        ulong high = 0x9E3779B97F4A7C15UL;
        foreach (char c in originalId + "|" + inputIndex.ToString(CultureInfo.InvariantCulture))
        {
            low = (low ^ c) * fnvPrime;
            high = (high ^ c) * fnvPrime;
        }

        byte[] bytes = new byte[16];
        BitConverter.GetBytes(low).CopyTo(bytes, 0);
        BitConverter.GetBytes(high).CopyTo(bytes, 8);
        return new Guid(bytes).ToString();
    }
}
