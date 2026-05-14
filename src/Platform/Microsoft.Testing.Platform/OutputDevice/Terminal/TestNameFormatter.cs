// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Formats test names based on a user-provided template containing <c>{placeholder}</c> tokens.
/// Resolution is delegated to <see cref="ArtifactNamingHelper"/>, so the template syntax matches
/// the one used by artifact naming.
/// </summary>
internal sealed class TestNameFormatter
{
    public const string DisplayPlaceholder = "display";
    public const string FullyQualifiedNamePlaceholder = "fqn";
    public const string NamespacePlaceholder = "ns";
    public const string TypePlaceholder = "type";
    public const string MethodPlaceholder = "method";
    public const string AssemblyPlaceholder = "asm";

    private readonly string _template;

    public TestNameFormatter(string template)
        => _template = template;

    public string Format(TestNode testNode)
        => ArtifactNamingHelper.ResolveTemplate(_template, BuildReplacements(testNode));

    private static Dictionary<string, string> BuildReplacements(TestNode testNode)
    {
        string displayName = testNode.DisplayName;
        TestMethodIdentifierProperty? methodIdentifier = testNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>();

        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DisplayPlaceholder] = displayName,
        };

        if (methodIdentifier is null)
        {
            // Fall back to the display name for {fqn} and to empty strings for the more granular tokens
            // so unknown placeholders are never left unresolved.
            replacements[FullyQualifiedNamePlaceholder] = displayName;
            replacements[NamespacePlaceholder] = string.Empty;
            replacements[TypePlaceholder] = string.Empty;
            replacements[MethodPlaceholder] = string.Empty;
            replacements[AssemblyPlaceholder] = string.Empty;
        }
        else
        {
            replacements[FullyQualifiedNamePlaceholder] = BuildFullyQualifiedName(methodIdentifier);
            replacements[NamespacePlaceholder] = methodIdentifier.Namespace;
            replacements[TypePlaceholder] = methodIdentifier.TypeName;
            replacements[MethodPlaceholder] = methodIdentifier.MethodName;
            replacements[AssemblyPlaceholder] = GetShortAssemblyName(methodIdentifier.AssemblyFullName);
        }

        return replacements;
    }

    private static string BuildFullyQualifiedName(TestMethodIdentifierProperty methodIdentifier)
    {
        var builder = new StringBuilder();

        if (!RoslynString.IsNullOrEmpty(methodIdentifier.Namespace))
        {
            builder.Append(methodIdentifier.Namespace);
            builder.Append('.');
        }

        builder.Append(methodIdentifier.TypeName);
        builder.Append('.');
        builder.Append(methodIdentifier.MethodName);

        if (methodIdentifier.ParameterTypeFullNames.Length > 0)
        {
            builder.Append('(');
            builder.Append(string.Join(", ", methodIdentifier.ParameterTypeFullNames));
            builder.Append(')');
        }

        return builder.ToString();
    }

    private static string GetShortAssemblyName(string assemblyFullName)
    {
        int commaIndex = assemblyFullName.IndexOf(',');
        return commaIndex > 0 ? assemblyFullName[..commaIndex] : assemblyFullName;
    }
}
