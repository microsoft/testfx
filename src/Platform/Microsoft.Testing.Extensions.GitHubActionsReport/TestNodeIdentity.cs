// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal static class TestNodeIdentity
{
    private const string FullyQualifiedNamePropertyKey = "vstest.TestCase.FullyQualifiedName";

    /// <summary>
    /// Resolves the stable, fully-qualified test name. Falls back to the display name when the
    /// fully-qualified name property is unavailable.
    /// </summary>
    public static string GetTestName(TestNode testNode)
    {
        using PropertyBag.PropertyBagEnumerator enumerator = testNode.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current is SerializableKeyValuePairStringProperty kvp
                && kvp.Key == FullyQualifiedNamePropertyKey)
            {
                return kvp.Value;
            }
        }

        return testNode.DisplayName;
    }
}
