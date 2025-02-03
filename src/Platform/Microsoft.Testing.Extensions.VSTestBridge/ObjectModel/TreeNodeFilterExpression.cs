// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

internal sealed class TreeNodeFilterExpression : ITestCaseFilterExpression
{
    private readonly TreeNodeFilter _treeNodeFilter;
    private readonly IEnumerable<string>? _supportedProperties;
    private readonly Func<string, TestProperty?> _propertyProvider;

    public TreeNodeFilterExpression(TreeNodeFilter treeNodeFilter, IEnumerable<string>? supportedProperties, Func<string, TestProperty?> propertyProvider)
    {
        _treeNodeFilter = treeNodeFilter;
        _supportedProperties = supportedProperties;
        _propertyProvider = propertyProvider;
    }

    public string TestCaseFilterValue => _treeNodeFilter.Filter;

    public bool MatchTestCase(TestCase testCase, Func<string, object?> propertyValueProvider)
    {
        // TODO
        string assemblyName = Path.GetFileNameWithoutExtension(testCase.Source);
        ReadOnlySpan<char> fullyQualifiedName = testCase.FullyQualifiedName.AsSpan();

        int lastDot = fullyQualifiedName.LastIndexOf('.');
        ReadOnlySpan<char> methodName = fullyQualifiedName.Slice(lastDot + 1);
        fullyQualifiedName = fullyQualifiedName.Slice(0, lastDot);

        lastDot = fullyQualifiedName.LastIndexOf('.');
        ReadOnlySpan<char> className = fullyQualifiedName.Slice(lastDot + 1);
        fullyQualifiedName = fullyQualifiedName.Slice(0, lastDot);

        ReadOnlySpan<char> @namespace = fullyQualifiedName;

        // TODO: PropertyBag argument
        return _treeNodeFilter.MatchesFilter($"/{assemblyName}/{@namespace.ToString()}/{className.ToString()}/{methodName.ToString()}", new());
    }
}
