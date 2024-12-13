// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// NOTE: This file is copied as-is from VSTest source code.
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

[ExcludeFromCodeCoverage] // Helper copied from VSTest source code
internal sealed class FilterExpressionWrapper
{
    /// <summary>
    /// FilterExpression corresponding to filter criteria.
    /// </summary>
    private readonly FilterExpression? _filterExpression;

    /// <remarks>
    /// Exposed for testing purpose.
    /// </remarks>
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1604 // Element documentation should have summary
    internal readonly FastFilter? _fastFilter;
#pragma warning restore SA1604 // Element documentation should have summary
#pragma warning restore SA1401 // Fields should be private

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterExpressionWrapper"/> class.
    /// Initializes FilterExpressionWrapper with given filterString and options.
    /// </summary>
    public FilterExpressionWrapper(string filterString, FilterOptions? options)
    {
        Guard.NotNullOrEmpty(filterString);

        FilterString = filterString;
        FilterOptions = options;

        try
        {
            // We prefer fast filter when it's available.
            _filterExpression = FilterExpression.Parse(filterString, out _fastFilter);

            if (UseFastFilter)
            {
                _filterExpression = null;

                // Property value regex is only supported for fast filter,
                // so we ignore it if no fast filter is constructed.

                // TODO: surface an error message to suer.
                string? regexString = options?.FilterRegEx;
                if (!RoslynString.IsNullOrEmpty(regexString))
                {
                    RoslynDebug.Assert(options!.FilterRegExReplacement == null || options.FilterRegEx != null);
                    _fastFilter.PropertyValueRegex = new Regex(regexString, RegexOptions.Compiled);
                    _fastFilter.PropertyValueRegexReplacement = options.FilterRegExReplacement;
                }
            }
        }
        catch (FormatException ex)
        {
            ParseError = ex.Message;
        }
        catch (ArgumentException ex)
        {
            _fastFilter = null;
            ParseError = ex.Message;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterExpressionWrapper"/> class.
    /// Initializes FilterExpressionWrapper with given filterString.
    /// </summary>
    public FilterExpressionWrapper(string filterString)
        : this(filterString, null)
    {
    }

    [MemberNotNullWhen(true, nameof(_fastFilter))]
    private bool UseFastFilter => _fastFilter != null;

    /// <summary>
    /// Gets user specified filter criteria.
    /// </summary>
    public string FilterString { get; }

    /// <summary>
    /// Gets user specified additional filter options.
    /// </summary>
    public FilterOptions? FilterOptions { get; }

    /// <summary>
    /// Gets parsing error (if any), when parsing 'FilterString' with built-in parser.
    /// </summary>
    public string? ParseError { get; }

    /// <summary>
    /// Validate if underlying filter expression is valid for given set of supported properties.
    /// </summary>
    public string[]? ValidForProperties(IEnumerable<string>? supportedProperties, Func<string, TestProperty?>? propertyProvider)
        => UseFastFilter
            ? _fastFilter.ValidForProperties(supportedProperties)
            : _filterExpression?.ValidForProperties(supportedProperties, propertyProvider);

    /// <summary>
    /// Evaluate filterExpression with given propertyValueProvider.
    /// </summary>
    public bool Evaluate(Func<string, object?> propertyValueProvider)
    {
        Guard.NotNull(propertyValueProvider);

        return UseFastFilter
            ? _fastFilter.Evaluate(propertyValueProvider)
            : _filterExpression != null && _filterExpression.Evaluate(propertyValueProvider);
    }
}
