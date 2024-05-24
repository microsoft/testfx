// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// Taken and adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Configuration.Json/src/JsonConfigurationFileParser.cs
/// We added also the _propertyToAllChildren dictionary to keep the whole json structure because we don't support by default the serialization/deserialization
/// and we need to keep the whole json structure per json property to be able to easily serialize it to some other format.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class JsonConfigurationFileParser
{
    private readonly Dictionary<string, string?> _singleValueData = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _propertyToAllChildren = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> _paths = new();

    private JsonConfigurationFileParser()
    {
    }

    public static (Dictionary<string, string?> SingleValueData, Dictionary<string, string?> PropertyToAllChildren) Parse(Stream input)
        => new JsonConfigurationFileParser().ParseStream(input);

    private (Dictionary<string, string?> SingleValueData, Dictionary<string, string?> PropertyToAllChildren) ParseStream(Stream input)
    {
        JsonDocumentOptions jsonDocumentOptions = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        using (StreamReader reader = new(input))
        using (var doc = JsonDocument.Parse(reader.ReadToEnd(), jsonDocumentOptions))
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException(string.Format(CultureInfo.CurrentCulture, PlatformResources.JsonConfigurationFileParserTopLevelElementIsNotAnObjectErrorMessage, doc.RootElement.ValueKind));
            }

            VisitObjectElement(doc.RootElement);
        }

        return (_singleValueData, _propertyToAllChildren);
    }

    private void VisitObjectElement(JsonElement element)
    {
        bool isEmpty = true;

        foreach (JsonProperty property in element.EnumerateObject())
        {
            isEmpty = false;
            EnterContext(property.Name);
            SavePropertyToAllChildren(property);
            VisitValue(property.Value);
            ExitContext();
        }

        SetNullIfElementIsEmpty(isEmpty);
    }

    private void SavePropertyToAllChildren(JsonProperty property)
    {
        string key = _paths.Peek();
        if (_propertyToAllChildren.ContainsKey(key))
        {
            throw new FormatException(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonConfigurationFileParserDuplicateKeyErrorMessage, key));
        }

        _propertyToAllChildren[key] = property.Value.ToString();
    }

    private void VisitArrayElement(JsonElement element)
    {
        int index = 0;

        foreach (JsonElement arrayElement in element.EnumerateArray())
        {
            EnterContext(index.ToString(CultureInfo.InvariantCulture));
            VisitValue(arrayElement);
            ExitContext();
            index++;
        }

        SetNullIfElementIsEmpty(isEmpty: index == 0);
    }

    private void SetNullIfElementIsEmpty(bool isEmpty)
    {
        if (isEmpty && _paths.Count > 0)
        {
            _singleValueData[_paths.Peek()] = null;
        }
    }

    private void VisitValue(JsonElement value)
    {
        RoslynDebug.Assert(_paths.Count > 0);

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                VisitObjectElement(value);
                break;

            case JsonValueKind.Array:
                VisitArrayElement(value);
                break;

            case JsonValueKind.Number:
            case JsonValueKind.String:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                string key = _paths.Peek();
                if (_singleValueData.ContainsKey(key))
                {
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonConfigurationFileParserDuplicateKeyErrorMessage, key));
                }

                _singleValueData[key] = value.ToString();
                break;

            default:
                throw new FormatException(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonConfigurationFileParserUnsupportedTokenErrorMessage, value.ValueKind));
        }
    }

    private void EnterContext(string context) => _paths.Push(_paths.Count > 0 ? _paths.Peek() + PlatformConfigurationConstants.KeyDelimiter + context : context);

    private void ExitContext() => _paths.Pop();
}
