// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// NOTE: This file is copied as-is from VSTest source code.
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

[ExcludeFromCodeCoverage] // Helper copied from VSTest source code
internal sealed class FastFilter
{
    internal FastFilter(ImmutableDictionary<string, ISet<string>> filterProperties, Operation filterOperation, Operator filterOperator)
    {
        ValidateArg.NotNullOrEmpty(filterProperties, nameof(filterProperties));

        FilterProperties = filterProperties;

        IsFilteredOutWhenMatched =
            (filterOperation != Operation.Equal || (filterOperator != Operator.Or && filterOperator != Operator.None))
            && (filterOperation == Operation.NotEqual && (filterOperator == Operator.And || filterOperator == Operator.None)
                ? true
                : throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "An error occurred while creating Fast filter.")));
    }

    internal ImmutableDictionary<string, ISet<string>> FilterProperties { get; }

    internal bool IsFilteredOutWhenMatched { get; }

    internal Regex? PropertyValueRegex { get; set; }

    internal string? PropertyValueRegexReplacement { get; set; }

    internal string[]? ValidForProperties(IEnumerable<string>? properties)
        => properties is null
            ? null
            : FilterProperties.Keys.All(name => properties.Contains(name))
                ? null
                : FilterProperties.Keys.Where(name => !properties.Contains(name)).ToArray();

    internal bool Evaluate(Func<string, object?> propertyValueProvider)
    {
        ValidateArg.NotNull(propertyValueProvider, nameof(propertyValueProvider));

        bool matched = false;
        foreach (string name in FilterProperties.Keys)
        {
            // If there is no value corresponding to given name, treat it as unmatched.
            if (!TryGetPropertyValue(name, propertyValueProvider, out string? singleValue, out string[]? multiValues))
            {
                continue;
            }

            if (singleValue != null)
            {
                string? value = PropertyValueRegex == null ? singleValue : ApplyRegex(singleValue);
                matched = value != null && FilterProperties[name].Contains(value);
            }
            else
            {
                IEnumerable<string?>? values = PropertyValueRegex == null ? multiValues : multiValues?.Select(ApplyRegex);
                matched = values?.Any(result => result != null && FilterProperties[name].Contains(result)) == true;
            }

            if (matched)
            {
                break;
            }
        }

        return IsFilteredOutWhenMatched ? !matched : matched;
    }

    /// <summary>
    /// Apply regex matching or replacement to given value.
    /// </summary>
    /// <returns>For matching, returns the result of matching, null if no match found. For replacement, returns the result of replacement.</returns>
    private string? ApplyRegex(string value)
    {
        RoslynDebug.Assert(PropertyValueRegex != null);

        string? result = null;
        if (PropertyValueRegexReplacement == null)
        {
            Match match = PropertyValueRegex!.Match(value);
            if (match.Success)
            {
                result = match.Value;
            }
        }
        else
        {
            result = PropertyValueRegex!.Replace(value, PropertyValueRegexReplacement);
        }

        return result;
    }

    /// <summary>
    /// Returns property value for Property using propertyValueProvider.
    /// </summary>
    private static bool TryGetPropertyValue(string name, Func<string, object?> propertyValueProvider, out string? singleValue, out string[]? multiValues)
    {
        object? propertyValue = propertyValueProvider(name);
        if (propertyValue != null)
        {
            multiValues = propertyValue as string[];
            singleValue = multiValues == null ? propertyValue.ToString() : null;
            return true;
        }

        singleValue = null;
        multiValues = null;
        return false;
    }

    internal static Builder CreateBuilder() => new();

    internal sealed class Builder
    {
        private readonly ImmutableDictionary<string, ImmutableHashSet<string>.Builder>.Builder _filterDictionaryBuilder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>.Builder>(StringComparer.OrdinalIgnoreCase);

        private bool _operatorEncountered;
        private Operator _fastFilterOperator = Operator.None;

        private bool _conditionEncountered;
        private Operation _fastFilterOperation;

        private bool _containsValidFilter = true;

        internal bool ContainsValidFilter => _containsValidFilter && _conditionEncountered;

        internal void AddOperator(Operator @operator)
        {
            if (_containsValidFilter && (@operator == Operator.And || @operator == Operator.Or))
            {
                if (_operatorEncountered)
                {
                    _containsValidFilter = _fastFilterOperator == @operator;
                }
                else
                {
                    _operatorEncountered = true;
                    _fastFilterOperator = @operator;
                    if ((_fastFilterOperation == Operation.NotEqual && _fastFilterOperator == Operator.Or)
                        || (_fastFilterOperation == Operation.Equal && _fastFilterOperator == Operator.And))
                    {
                        _containsValidFilter = false;
                    }
                }
            }
            else
            {
                _containsValidFilter = false;
            }
        }

        internal void AddCondition(Condition condition)
        {
            if (!_containsValidFilter)
            {
                return;
            }

            if (_conditionEncountered)
            {
                if (condition.Operation == _fastFilterOperation)
                {
                    AddProperty(condition.Name, condition.Value);
                }
                else
                {
                    _containsValidFilter = false;
                }
            }
            else
            {
                _conditionEncountered = true;
                _fastFilterOperation = condition.Operation;
                AddProperty(condition.Name, condition.Value);

                // Don't support `Contains`.
                if (_fastFilterOperation is not Operation.Equal and not Operation.NotEqual)
                {
                    _containsValidFilter = false;
                }
            }
        }

        private void AddProperty(string name, string value)
        {
            if (!_filterDictionaryBuilder.TryGetValue(name, out ImmutableHashSet<string>.Builder? values))
            {
                values = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
                _filterDictionaryBuilder.Add(name, values);
            }

            values.Add(value);
        }

        internal FastFilter? ToFastFilter() => ContainsValidFilter
                ? new FastFilter(
                    _filterDictionaryBuilder.ToImmutableDictionary(kvp => kvp.Key, kvp => (ISet<string>)_filterDictionaryBuilder[kvp.Key].ToImmutable()),
                    _fastFilterOperation,
                    _fastFilterOperator)
                : null;
    }
}
