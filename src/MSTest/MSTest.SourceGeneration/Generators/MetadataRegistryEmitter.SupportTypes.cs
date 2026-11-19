// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

internal static partial class MetadataRegistryEmitter
{
    public static string EmitSupportTypes()
    {
        var sb = new IndentedStringBuilder();
        AppendHeader(sb);

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();

        using (sb.Block($"namespace {GeneratedNamespace}"))
        {
            using (sb.Block("internal sealed class TestClassReflectionInfo"))
            {
                // The stored Type flows into the generated Initialize method's direct GetMethods /
                // GetProperties calls. Annotate the property so the trimmer keeps those members; the
                // registry assigns typeof(<concrete class>), which satisfies the requirement without
                // warnings at the assignment site.
                sb.AppendLine("[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]");
                sb.AppendLine("public Type Type { get; set; } = null!;");
                sb.AppendLine("public Attribute[] Attributes { get; set; } = Array.Empty<Attribute>();");
                sb.AppendLine("public bool AreAttributesComplete { get; set; }");
                sb.AppendLine("public bool SupportsGeneratedDescriptors { get; set; }");
                sb.AppendLine("public bool AreGeneratedDescriptorsComplete { get; set; }");
                sb.AppendLine("public IReadOnlyList<TestMethodReflectionInfo> Methods { get; set; } = Array.Empty<TestMethodReflectionInfo>();");
                sb.AppendLine("public IReadOnlyList<TestPropertyReflectionInfo> Properties { get; set; } = Array.Empty<TestPropertyReflectionInfo>();");
                sb.AppendLine("public IReadOnlyList<TestConstructorReflectionInfo> Constructors { get; set; } = Array.Empty<TestConstructorReflectionInfo>();");
            }

            sb.AppendLine();
            using (sb.Block("internal sealed class TestMethodReflectionInfo"))
            {
                sb.AppendLine("public string Name { get; set; } = string.Empty;");
                sb.AppendLine("public Type DeclaringType { get; set; } = null!;");
                sb.AppendLine("public bool IsTestMethod { get; set; }");
                sb.AppendLine("public bool IsStatic { get; set; }");
                sb.AppendLine("public bool IsAsync { get; set; }");
                sb.AppendLine("public bool ReturnsTask { get; set; }");
                sb.AppendLine("public bool ReturnsValueTask { get; set; }");
                sb.AppendLine("public bool ReturnsVoid { get; set; }");
                sb.AppendLine("public bool IsDescriptorSupported { get; set; }");
                sb.AppendLine("public Type[] ParameterTypes { get; set; } = Array.Empty<Type>();");
                sb.AppendLine("public Attribute[] Attributes { get; set; } = Array.Empty<Attribute>();");
                sb.AppendLine("public bool AreAttributesComplete { get; set; }");
                sb.AppendLine("public IReadOnlyList<DynamicDataSourceReflectionInfo> DynamicDataSources { get; set; } = Array.Empty<DynamicDataSourceReflectionInfo>();");
                sb.AppendLine("public Func<object?, object?[]?, Task> Invoke { get; set; } = static (_, _) => Task.CompletedTask;");
            }

            sb.AppendLine();
            using (sb.Block("internal sealed class DynamicDataSourceReflectionInfo"))
            {
                sb.AppendLine("public Type DeclaringType { get; set; } = null!;");
                sb.AppendLine("public string SourceName { get; set; } = string.Empty;");
                sb.AppendLine($"public {UnitTestingNamespaceGlobal}.DynamicDataSourceType SourceType {{ get; set; }}");
                sb.AppendLine("public Func<object?[], object?> GetData { get; set; } = static _ => null;");
                sb.AppendLine("public Type? DisplayNameDeclaringType { get; set; }");
                sb.AppendLine("public string? DisplayNameMethodName { get; set; }");
                sb.AppendLine("public Func<System.Reflection.MethodInfo, object?[]?, string?>? GetDisplayName { get; set; }");
            }

            sb.AppendLine();
            using (sb.Block("internal sealed class TestPropertyReflectionInfo"))
            {
                sb.AppendLine("public string Name { get; set; } = string.Empty;");
                sb.AppendLine("public Type PropertyType { get; set; } = typeof(object);");
                sb.AppendLine("public bool HasPublicSetter { get; set; }");
                sb.AppendLine("public Attribute[] Attributes { get; set; } = Array.Empty<Attribute>();");
                sb.AppendLine("public Func<object?, object?> Get { get; set; } = static _ => null;");
                sb.AppendLine("public Action<object?, object?> Set { get; set; } = static (_, _) => { };");
            }

            sb.AppendLine();
            using (sb.Block("internal sealed class TestConstructorReflectionInfo"))
            {
                sb.AppendLine("public Type[] ParameterTypes { get; set; } = Array.Empty<Type>();");
                sb.AppendLine("public Func<object?[]?, object> Invoke { get; set; } = static _ => throw new InvalidOperationException(\"No constructor registered.\");");
            }
        }

        return sb.ToString();
    }

    private static void AppendHeader(IndentedStringBuilder sb)
    {
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }
}
