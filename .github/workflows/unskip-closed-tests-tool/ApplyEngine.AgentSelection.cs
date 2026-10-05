using System.Text.Json;

namespace UnskipClosedTests.Tool;

internal static partial class ApplyEngine
{
    private static List<string> ReadAgentSelection(string path, string expectedManifestDigest)
    {
        using JsonDocument document = JsonSupport.ReadDocument(path);
        JsonElement root = document.RootElement;
        List<JsonElement> items = [];
        if (root.ValueKind == JsonValueKind.Array)
        {
            items.AddRange(root.EnumerateArray());
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            JsonElement collection;
            if (root.TryGetProperty("items", out collection) ||
                root.TryGetProperty("outputs", out collection) ||
                root.TryGetProperty("safe_outputs", out collection))
            {
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.Name is not ("items" or "outputs" or "safe_outputs"))
                    {
                        throw new ContractException($"Unknown agent output root field '{property.Name}'.");
                    }
                }

                if (collection.ValueKind != JsonValueKind.Array)
                {
                    throw new ContractException("Agent output item collection must be an array.");
                }

                items.AddRange(collection.EnumerateArray());
            }
            else if (root.TryGetProperty("type", out _))
            {
                items.Add(root);
            }
            else
            {
                throw new ContractException("Agent output must contain an item array.");
            }
        }
        else
        {
            throw new ContractException("Agent output root must be an object or array.");
        }

        if (items.Count != 1 || items[0].ValueKind != JsonValueKind.Object)
        {
            throw new ContractException("Agent output must contain exactly one output item.");
        }

        JsonElement item = items[0];
        HashSet<string> allowed = ["type", "manifest_digest", "candidate_ids_json"];
        foreach (JsonProperty property in item.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new ContractException($"Unknown apply_verified_unskips field '{property.Name}'.");
            }
        }

        string type = RequiredString(item, "type");
        if (type != "apply_verified_unskips")
        {
            throw new ContractException($"Unexpected output item type '{type}'.");
        }

        string manifestDigest = RequiredString(item, "manifest_digest");
        if (!string.Equals(manifestDigest, expectedManifestDigest, StringComparison.Ordinal))
        {
            throw new ContractException("Agent output manifest_digest does not match the resolved manifest.");
        }

        if (!item.TryGetProperty("candidate_ids_json", out JsonElement idsElement))
        {
            throw new ContractException("candidate_ids_json is required.");
        }

        JsonElement array;
        JsonDocument? parsedString = null;
        if (idsElement.ValueKind == JsonValueKind.String)
        {
            try
            {
                parsedString = JsonDocument.Parse(idsElement.GetString()!);
                array = parsedString.RootElement;
            }
            catch (JsonException ex)
            {
                throw new ContractException($"candidate_ids_json string is malformed JSON: {ex.Message}");
            }
        }
        else
        {
            array = idsElement;
        }

        try
        {
            if (array.ValueKind != JsonValueKind.Array)
            {
                throw new ContractException("candidate_ids_json must be a JSON array.");
            }

            List<string> ids = [];
            foreach (JsonElement id in array.EnumerateArray())
            {
                if (id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                {
                    throw new ContractException("candidate_ids_json must contain only non-empty strings.");
                }

                ids.Add(id.GetString()!);
            }

            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            {
                throw new ContractException("candidate_ids_json contains duplicate candidate IDs.");
            }

            return ids;
        }
        finally
        {
            parsedString?.Dispose();
        }
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new ContractException($"{name} must be a string.");
        }

        return value.GetString()!;
    }
}
