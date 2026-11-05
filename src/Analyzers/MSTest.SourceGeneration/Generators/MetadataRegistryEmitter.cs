// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

/// <summary>
/// Emits the registry file (a single C# file per compilation) that mirrors what
/// <c>IReflectionOperations</c> would return at runtime.
///
/// The emitted code shape is intentionally close to what the MSTest adapter needs:
/// per-test-class records holding the materialized <c>Attribute[]</c>, parameter types,
/// and delegate-based constructor / method invokers. A future change to the adapter
/// can iterate over <c>MSTestReflectionMetadata.TestClasses</c> instead of calling
/// <c>Assembly.GetTypes()</c> + reflection.
/// </summary>
internal static partial class MetadataRegistryEmitter
{
    private const string GeneratedNamespace = "MSTest.SourceGenerated";
    private const string RegistryClassName = "MSTestReflectionMetadata";
    private const string UnitTestingNamespaceGlobal = "global::" + MSTestAttributeNames.UnitTestingNamespace;

    public static string EmitRegistry(string assemblyName, AssemblyMetadataModel assemblyMetadata, IReadOnlyList<TestClassModel> testClasses)
    {
        var sb = new IndentedStringBuilder();
        AppendHeader(sb);

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        sb.AppendLine("#pragma warning disable MSTESTEXP");
        sb.AppendLine();

        using (sb.Block($"namespace {GeneratedNamespace}"))
        {
            using (sb.Block($"internal static class {RegistryClassName}"))
            {
                sb.AppendLine($"public const string AssemblyName = \"{Escape(assemblyName)}\";");
                sb.AppendLine();

                // Emit assembly-level [assembly: ...] attributes so the consumer never has to call
                // Assembly.GetCustomAttributes for attributes declared in the same compilation.
                EmitAssemblyAttributesProperty(sb, assemblyMetadata.Attributes);
                sb.AppendLine();

                sb.AppendLine("public static IReadOnlyList<TestClassReflectionInfo> TestClasses { get; } = new TestClassReflectionInfo[]");
                using (sb.Block(null))
                {
                    for (int i = 0; i < testClasses.Count; i++)
                    {
                        EmitTestClass(sb, testClasses[i]);
                        if (i < testClasses.Count - 1)
                        {
                            sb.AppendLine(",");
                        }
                    }
                }

                sb.AppendLine(";");
            }
        }

        return sb.ToString();
    }

    private static void EmitTestClass(IndentedStringBuilder sb, TestClassModel model)
    {
        string fqn = model.FullyQualifiedTypeName;
        sb.AppendLine("new TestClassReflectionInfo");
        using (sb.Block(null))
        {
            sb.AppendLine($"Type = typeof({fqn}),");
            EmitAttributesProperty(sb, "Attributes", model.Attributes);
            sb.AppendLine(",");
            sb.AppendLine($"AreAttributesComplete = {Bool(model.AreAttributesComplete)},");
            sb.AppendLine($"SupportsGeneratedDescriptors = {Bool(model.SupportsGeneratedDescriptors)},");
            sb.AppendLine($"AreGeneratedDescriptorsComplete = {Bool(model.AreGeneratedDescriptorsComplete)},");

            EmitConstructors(sb, fqn, model);
            sb.AppendLine(",");

            EmitMethods(sb, fqn, model);
            sb.AppendLine(",");

            EmitProperties(sb, fqn, model);
        }
    }

    internal static string BuildAttributeExpression(AttributeApplicationModel attribute)
    {
        string ctorArgs = string.Join(", ", attribute.ConstructorArguments.AsImmutableArray().Select(BuildConstantExpression));
        string ctorCall = $"new {attribute.FullyQualifiedAttributeType}({ctorArgs})";

        if (attribute.NamedArguments.Length == 0)
        {
            return ctorCall;
        }

        string initializers = string.Join(", ", attribute.NamedArguments.AsImmutableArray()
            .Select(na => $"{na.Name} = {BuildConstantExpression(na.Value)}"));
        return $"{ctorCall} {{ {initializers} }}";
    }

    private static string BuildConstantExpression(TypedConstantModel constant)
        => constant.Kind switch
        {
            ConstantValueKind.Null => constant.FullyQualifiedType is null
                ? "null"
                : $"({constant.FullyQualifiedType})null!",
            ConstantValueKind.Type => constant.FullyQualifiedType is null
                ? "typeof(object)"
                : $"typeof({constant.FullyQualifiedType})",
            ConstantValueKind.Enum => BuildEnumLiteral(constant),
            ConstantValueKind.Array => BuildArrayLiteral(constant),
            _ => BuildPrimitiveLiteral(constant),
        };

    private static string BuildEnumLiteral(TypedConstantModel constant)
    {
        string? typeName = constant.FullyQualifiedType;
        string raw = FormatPrimitive(constant.PrimitiveValue);
        return typeName is null ? raw : $"({typeName})({raw})";
    }

    private static string BuildArrayLiteral(TypedConstantModel constant)
    {
        // The element type comes back as e.g. "global::System.Object[]" — strip the trailing [].
        string elementType = "object";
        if (constant.FullyQualifiedType is { } typeName && typeName.EndsWith("[]", System.StringComparison.Ordinal))
        {
            elementType = typeName.Substring(0, typeName.Length - 2);
        }

        if (constant.ArrayElements.Length == 0)
        {
            return $"Array.Empty<{elementType}>()";
        }

        string values = string.Join(", ", constant.ArrayElements.AsImmutableArray().Select(BuildConstantExpression));
        return $"new {elementType}[] {{ {values} }}";
    }

    private static string BuildPrimitiveLiteral(TypedConstantModel constant)
    {
        string literal = FormatPrimitive(constant.PrimitiveValue);
        return constant.PrimitiveValue is byte or sbyte or short or ushort
            && constant.FullyQualifiedType is { } typeName
                ? $"({typeName}){(literal.StartsWith("-", StringComparison.Ordinal) ? $"({literal})" : literal)}"
                : literal;
    }

    private static string FormatPrimitive(object? value)
        => value switch
        {
            null => "null",
            string s => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(s, quote: true),
            bool b => Bool(b),
            char c => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(c, quote: true),
            float f when float.IsNaN(f) => "global::System.Single.NaN",
            float f when float.IsPositiveInfinity(f) => "global::System.Single.PositiveInfinity",
            float f when float.IsNegativeInfinity(f) => "global::System.Single.NegativeInfinity",
            float f => f.ToString("R", CultureInfo.InvariantCulture) + "F",
            double d when double.IsNaN(d) => "global::System.Double.NaN",
            double d when double.IsPositiveInfinity(d) => "global::System.Double.PositiveInfinity",
            double d when double.IsNegativeInfinity(d) => "global::System.Double.NegativeInfinity",
            double d => d.ToString("R", CultureInfo.InvariantCulture) + "D",
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
            long l => l.ToString(CultureInfo.InvariantCulture) + "L",
            ulong ul => ul.ToString(CultureInfo.InvariantCulture) + "UL",
            uint u => u.ToString(CultureInfo.InvariantCulture) + "U",
            byte by => by.ToString(CultureInfo.InvariantCulture),
            sbyte sb => sb.ToString(CultureInfo.InvariantCulture),
            short sh => sh.ToString(CultureInfo.InvariantCulture),
            ushort us => us.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null",
        };

    private static string BuildArgumentsFromObjectArray(EquatableArray<TestParameterModel> parameters)
        => parameters.Length == 0
            ? string.Empty
            : string.Join(", ", parameters.AsImmutableArray()
                .Select((p, i) => $"({p.FullyQualifiedType})args![{i}]!"));

    private static string Bool(bool value) => value ? "true" : "false";

    internal static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // Escapes a member/type identifier so a source name that happens to be a C# reserved keyword (e.g. a
    // member declared as `@class`) is emitted as a valid identifier rather than breaking the generated code.
    private static string EscapeIdentifier(string name)
        => Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None
            ? "@" + name
            : name;
}
