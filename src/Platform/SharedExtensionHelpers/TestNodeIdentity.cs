// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions;

internal static class TestNodeIdentity
{
    private const string FullyQualifiedNamePropertyKey = "vstest.TestCase.FullyQualifiedName";

    /// <summary>
    /// Resolves the stable, fully-qualified test name for a <see cref="TestNode"/>. Falls back to the
    /// display name when the fully-qualified name property is unavailable.
    /// </summary>
    public static string GetTestName(TestNode testNode)
    {
        // Walk the PropertyBag once with the zero-allocation struct enumerator and short-circuit
        // on the first matching key, avoiding the LINQ iterator/boxed-enumerator allocations that
        // OfType<T>().FirstOrDefault(...) would incur.
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
