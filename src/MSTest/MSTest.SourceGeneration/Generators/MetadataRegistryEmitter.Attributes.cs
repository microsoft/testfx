// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

internal static partial class MetadataRegistryEmitter
{
    private static void EmitAssemblyAttributesProperty(IndentedStringBuilder sb, EquatableArray<AttributeApplicationModel> attributes)
    {
        if (attributes.Length == 0)
        {
            sb.AppendLine("public static object[] AssemblyAttributes { get; } = Array.Empty<object>();");
            return;
        }

        sb.AppendLine("public static object[] AssemblyAttributes { get; } = new object[]");
        using (sb.Block(null))
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                AttributeApplicationModel attr = attributes[i];
                sb.Append($"(Attribute){BuildAttributeExpression(attr)}");
                if (i < attributes.Length - 1)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine(";");
    }

    private static void EmitAttributesProperty(IndentedStringBuilder sb, string propertyName, EquatableArray<AttributeApplicationModel> attributes)
    {
        if (attributes.Length == 0)
        {
            sb.Append($"{propertyName} = Array.Empty<Attribute>()");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"{propertyName} = new Attribute[]");
        using (sb.Block(null))
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                AttributeApplicationModel attr = attributes[i];
                sb.Append(BuildAttributeExpression(attr));
                if (i < attributes.Length - 1)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine();
                }
            }
        }
    }
}
