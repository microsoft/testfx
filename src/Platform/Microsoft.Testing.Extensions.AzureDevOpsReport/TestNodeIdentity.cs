// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal static class TestNodeIdentity
{
    private const string FullyQualifiedNamePropertyKey = "vstest.TestCase.FullyQualifiedName";

    /// <summary>
    /// Resolves the stable test name used to match a <see cref="TestNode"/> against Azure DevOps history
    /// (which keys results by <c>AutomatedTestName</c>). Falls back to the display name when the
    /// fully-qualified name property is unavailable.
    /// </summary>
    public static string GetTestName(TestNode testNode)
        => testNode.Properties
            .OfType<SerializableKeyValuePairStringProperty>()
            .FirstOrDefault(static property => property.Key == FullyQualifiedNamePropertyKey)?.Value
            ?? testNode.DisplayName;
}
