// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// NOTE: This file is copied as-is from VSTest source code.
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// Implements ITestCaseFilterExpression, providing test case filtering functionality.
/// </summary>
internal sealed class TestCaseFilterExpression : ITestCaseFilterExpression
{
    private readonly FilterExpressionWrapper? _filterWrapper;
    private readonly TreeNodeFilter? _treeNodeFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestCaseFilterExpression"/> class.
    /// Adapter specific filter expression.
    /// </summary>
    public TestCaseFilterExpression(FilterExpressionWrapper? filterWrapper, TreeNodeFilter? treeNodeFilter)
    {
        _filterWrapper = filterWrapper;
        _treeNodeFilter = treeNodeFilter;
        if (RoslynString.IsNullOrEmpty(filterWrapper?.ParseError))
        {
            throw new UnreachableException();
        }
    }

    /// <summary>
    /// Gets user specified filter criteria.
    /// </summary>
    public string TestCaseFilterValue => _filterWrapper?.FilterString ?? string.Empty;

    /// <summary>
    /// Match test case with filter criteria.
    /// </summary>
    public bool MatchTestCase(TestCase testCase, Func<string, object?> propertyValueProvider)
    {
        Ensure.NotNull(testCase);
        Ensure.NotNull(propertyValueProvider);

        bool vstestFilterMatch = _filterWrapper is null ||
            _filterWrapper.Evaluate(propertyValueProvider);

        bool treeNodeFilterMatch = _treeNodeFilter is null ||
            _treeNodeFilter.MatchesFilter(string.Empty /*TODO*/, new Platform.Extensions.Messages.PropertyBag() /*TODO*/);

        // TODO: To be defined: Use AND or OR.
        return vstestFilterMatch && treeNodeFilterMatch;
    }
}
