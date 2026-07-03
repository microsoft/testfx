// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions;

internal static class TestNodeIdentity
{
    private const string FullyQualifiedNamePropertyKey = "vstest.TestCase.FullyQualifiedName";

    /// <summary>
    /// Resolves the stable, fully-qualified test name for a <see cref="TestNode"/>. Falls back to the
    /// display name when the fully-qualified name property is unavailable.
    /// </summary>
    /// <remarks>
    /// The identity is resolved in the same priority order used by the TRX and VSTest-bridge converters:
    /// <list type="number">
    /// <item><description><see cref="TestMethodIdentifierProperty"/> — the canonical, ECMA-335 compliant identity added by
    /// frameworks running natively on the platform. This is the property that maps to Azure DevOps' <c>AutomatedTestName</c>.</description></item>
    /// <item><description>The non-standard <c>vstest.TestCase.FullyQualifiedName</c> property (only present for tests flowing
    /// through the VSTest bridge in server mode).</description></item>
    /// <item><description>The display name, as a last resort.</description></item>
    /// </list>
    /// In practice a given <see cref="TestNode"/> carries at most one of the first two properties, so the walk order does
    /// not matter; we still prefer <see cref="TestMethodIdentifierProperty"/> when both are somehow present.
    /// </remarks>
    public static string GetTestName(TestNode testNode)
    {
        // Walk the PropertyBag once with the zero-allocation struct enumerator, avoiding the LINQ
        // iterator/boxed-enumerator allocations that OfType<T>().FirstOrDefault(...) would incur.
        // Short-circuit as soon as the preferred TestMethodIdentifierProperty is found; otherwise
        // remember the first VSTest fully-qualified name as a fallback.
        string? fullyQualifiedName = null;
        using PropertyBag.PropertyBagEnumerator enumerator = testNode.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TestMethodIdentifierProperty testMethodIdentifier:
                    return FormatFullyQualifiedName(testMethodIdentifier);

                case SerializableKeyValuePairStringProperty { Key: FullyQualifiedNamePropertyKey } kvp when fullyQualifiedName is null:
                    fullyQualifiedName = kvp.Value;
                    break;
            }
        }

        return fullyQualifiedName ?? testNode.DisplayName;
    }

    private static string FormatFullyQualifiedName(TestMethodIdentifierProperty testMethodIdentifier)
        => RoslynString.IsNullOrEmpty(testMethodIdentifier.Namespace)
            ? $"{testMethodIdentifier.TypeName}.{testMethodIdentifier.MethodName}"
            : $"{testMethodIdentifier.Namespace}.{testMethodIdentifier.TypeName}.{testMethodIdentifier.MethodName}";
}
