// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Formats test names based on a user-provided format string with placeholders.
/// </summary>
internal sealed class TestNameFormatter
{
    private readonly string _format;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestNameFormatter"/> class.
    /// </summary>
    /// <param name="format">The format string containing placeholders like &lt;fqn&gt;, &lt;display&gt;, etc.</param>
    public TestNameFormatter(string format)
    {
        Guard.NotNull(format);
        _format = format;
    }

    /// <summary>
    /// Formats a test name based on the configured format string.
    /// </summary>
    /// <param name="testNode">The test node containing the test information.</param>
    /// <returns>The formatted test name.</returns>
    public string Format(TestNode testNode)
    {
        Guard.NotNull(testNode);
        Guard.NotNull(testNode.DisplayName);

        string result = _format;

        // Extract TestMethodIdentifierProperty if available
        TestMethodIdentifierProperty? methodIdentifier = testNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>();

        // <display> - Display name
        result = result.Replace("<display>", testNode.DisplayName, StringComparison.Ordinal);

        if (methodIdentifier is not null)
        {
            // <fqn> - Fully qualified name (namespace.type.method with parameters)
            string fqn = BuildFullyQualifiedName(methodIdentifier);
            result = result.Replace("<fqn>", fqn, StringComparison.Ordinal);

            // <ns> - Namespace
            result = result.Replace("<ns>", methodIdentifier.Namespace, StringComparison.Ordinal);

            // <type> - Type name
            result = result.Replace("<type>", methodIdentifier.TypeName, StringComparison.Ordinal);

            // <method> - Method name
            result = result.Replace("<method>", methodIdentifier.MethodName, StringComparison.Ordinal);

            // <asm> - Assembly name (short name without version/culture/token)
            string assemblyName = GetShortAssemblyName(methodIdentifier.AssemblyFullName);
            result = result.Replace("<asm>", assemblyName, StringComparison.Ordinal);
        }
        else
        {
            // If TestMethodIdentifierProperty is not available, replace with empty or display name
            result = result.Replace("<fqn>", testNode.DisplayName, StringComparison.Ordinal);
            result = result.Replace("<ns>", string.Empty, StringComparison.Ordinal);
            result = result.Replace("<type>", string.Empty, StringComparison.Ordinal);
            result = result.Replace("<method>", string.Empty, StringComparison.Ordinal);
            result = result.Replace("<asm>", string.Empty, StringComparison.Ordinal);
        }

        return result;
    }

    private static string BuildFullyQualifiedName(TestMethodIdentifierProperty methodIdentifier)
    {
        StringBuilder fqnBuilder = new();

        // Add namespace
        if (!RoslynString.IsNullOrEmpty(methodIdentifier.Namespace))
        {
            fqnBuilder.Append(methodIdentifier.Namespace);
            fqnBuilder.Append('.');
        }

        // Add type name
        fqnBuilder.Append(methodIdentifier.TypeName);
        fqnBuilder.Append('.');

        // Add method name
        fqnBuilder.Append(methodIdentifier.MethodName);

        // Add parameters if any
        if (methodIdentifier.ParameterTypeFullNames.Length > 0)
        {
            fqnBuilder.Append('(');
            fqnBuilder.Append(string.Join(", ", methodIdentifier.ParameterTypeFullNames));
            fqnBuilder.Append(')');
        }

        return fqnBuilder.ToString();
    }

    private static string GetShortAssemblyName(string assemblyFullName)
    {
        // Extract just the assembly name without version, culture, publicKeyToken
        int commaIndex = assemblyFullName.IndexOf(',');
        return commaIndex > 0 ? assemblyFullName[..commaIndex] : assemblyFullName;
    }
}

#if NETSTANDARD
internal static class StringExtensions
{
#pragma warning disable IDE0060 // Remove unused parameter
    public static string Replace(this string str, string oldValue, string newValue, StringComparison comparisonType)
#pragma warning restore IDE0060 // Remove unused parameter
        => str.Replace(oldValue, newValue);
}
#endif
