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

    /// <summary>
    /// Resolves a human-friendly label for a <see cref="TestNode"/> that distinguishes individual
    /// data-driven (parameterized) instances which otherwise share a single fully-qualified name.
    /// </summary>
    /// <remarks>
    /// The label is the fully-qualified name from <see cref="GetTestName"/>, augmented with the
    /// distinguishing part of the display name when the test is parameterized:
    /// <list type="bullet">
    /// <item><description>For a non-parameterized test the display name is just the method's simple name, which is
    /// already part of the fully-qualified name, so the label is the fully-qualified name unchanged.</description></item>
    /// <item><description>For a data-driven test the default display name is <c>MethodName (args)</c>; only the
    /// distinguishing <c>(args)</c> suffix is appended so the method name is not repeated.</description></item>
    /// <item><description>A fully custom display name that does not embed the method name is appended in parentheses.</description></item>
    /// </list>
    /// This keeps the fully-qualified name (used for history/threshold lookups) intact while giving each
    /// parameterized instance a distinct, non-duplicated line in the output.
    /// </remarks>
    public static string GetDisplayLabel(TestNode testNode)
    {
        string identity = GetTestName(testNode);
        string displayName = testNode.DisplayName;

        if (RoslynString.IsNullOrEmpty(displayName)
            || string.Equals(displayName, identity, StringComparison.Ordinal))
        {
            return identity;
        }

        string methodName = GetMethodSimpleName(identity);
        if (string.Equals(displayName, methodName, StringComparison.Ordinal))
        {
            // Non-parameterized test: the display name is the method's simple name, already in 'identity'.
            return identity;
        }

        if (methodName.Length > 0 && displayName.StartsWith(methodName, StringComparison.Ordinal))
        {
            // Data-driven test with the default 'MethodName (args)' display name: append only the
            // distinguishing suffix so the method name is not repeated.
            string suffix = displayName.Substring(methodName.Length).TrimStart();
            return suffix.Length == 0 ? identity : $"{identity} {suffix}";
        }

        // Custom display name that does not embed the method name: surface it alongside the identity.
        return $"{identity} ({displayName})";
    }

    private static string FormatFullyQualifiedName(TestMethodIdentifierProperty testMethodIdentifier)
        => RoslynString.IsNullOrEmpty(testMethodIdentifier.Namespace)
            ? $"{testMethodIdentifier.TypeName}.{testMethodIdentifier.MethodName}"
            : $"{testMethodIdentifier.Namespace}.{testMethodIdentifier.TypeName}.{testMethodIdentifier.MethodName}";

    private static string GetMethodSimpleName(string fullyQualifiedName)
    {
        int lastDot = fullyQualifiedName.LastIndexOf('.');
        return lastDot < 0 ? fullyQualifiedName : fullyQualifiedName.Substring(lastDot + 1);
    }
}
