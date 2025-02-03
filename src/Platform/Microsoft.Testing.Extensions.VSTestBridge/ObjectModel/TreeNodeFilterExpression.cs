// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

internal sealed class TreeNodeFilterExpression : ITestCaseFilterExpression
{
    private TreeNodeFilter _treeNodeFilter;
    private IEnumerable<string>? _supportedProperties;
    private Func<string, TestProperty?> _propertyProvider;

    public TreeNodeFilterExpression(TreeNodeFilter treeNodeFilter, IEnumerable<string>? supportedProperties, Func<string, TestProperty?> propertyProvider)
    {
        _treeNodeFilter = treeNodeFilter;
        _supportedProperties = supportedProperties;
        _propertyProvider = propertyProvider;
    }

    public string TestCaseFilterValue => _treeNodeFilter.Filter;

    public bool MatchTestCase(TestCase testCase, Func<string, object?> propertyValueProvider)
        => true;
}
