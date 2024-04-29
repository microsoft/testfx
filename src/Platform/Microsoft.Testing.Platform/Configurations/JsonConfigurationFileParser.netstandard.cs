// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP

using System.Globalization;

using Jsonite;

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed class JsonConfigurationFileParser
{
    public static readonly string KeyDelimiter = ":";

    private readonly Dictionary<string, string?> _singleValueData = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _propertyToAllChildren = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> _paths = new();
    private readonly JsonSettings _settings = new()
    {
        AllowTrailingCommas = true,
    };

    private JsonConfigurationFileParser()
    {
    }

    public static (Dictionary<string, string?> SingleValueData, Dictionary<string, string?> PropertyToAllChildren) Parse(Stream input)
        => new JsonConfigurationFileParser().ParseStream(input);

    private (Dictionary<string, string?> SingleValueData, Dictionary<string, string?> PropertyToAllChildren) ParseStream(Stream input)
    {
        using StreamReader reader = new(input);
        var doc = (JsonObject)Jsonite.Json.Deserialize(reader.ReadToEnd(), _settings);
        if (doc is not null)
        {
            VisitObjectElement(doc);
            return (_singleValueData, _propertyToAllChildren);
        }

        throw new FormatException(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonConfigurationFileParserTopLevelElementIsNotAnObjectErrorMessage, "null"));
    }

    private void VisitObjectElement(JsonObject obj)
    {
        bool isEmpty = true;
        foreach (KeyValuePair<string, object?> property in obj)
        {
            isEmpty = false;
            EnterContext(property.Key);
            SavePropertyToAllChildren(property.Value);
            VisitValue(property.Value);
            ExitContext();
        }

        SetNullIfElementIsEmpty(isEmpty);
    }

    private void SavePropertyToAllChildren(object? property)
    {
        string key = _paths.Peek();
        if (_propertyToAllChildren.ContainsKey(key))
        {
            throw new FormatException(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonConfigurationFileParserDuplicateKeyErrorMessage, key));
        }

        _propertyToAllChildren[key] = Jsonite.Json.Serialize(property, _settings);
    }

    private void VisitArrayElement(JsonArray array)
    {
        int index = 0;

        foreach (object arrayElement in array)
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

    private void VisitValue(object? value)
    {
        RoslynDebug.Assert(_paths.Count > 0, "We should have at least one path");

        switch (value)
        {
            case JsonObject:
                VisitObjectElement((JsonObject)value);
                break;

            case JsonArray:
                VisitArrayElement((JsonArray)value);
                break;

            default:
                string key = _paths.Peek();
                if (_singleValueData.ContainsKey(key))
                {
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonConfigurationFileParserDuplicateKeyErrorMessage, key));
                }

                // Adapt to the System.Text.Json serialization outcome
                _singleValueData[key] = value is bool boolean
                    ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(boolean.ToString())
                    : value is string stringValue ? stringValue.Trim('\"') : Jsonite.Json.Serialize(value, _settings);

                break;
        }
    }

    private void EnterContext(string context) =>
        _paths.Push(_paths.Count > 0 ?
            _paths.Peek() + KeyDelimiter + context :
            context);

    private void ExitContext() => _paths.Pop();
}

#endif
