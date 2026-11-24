// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

internal static partial class MetadataRegistryEmitter
{
    private static void EmitDynamicDataSources(IndentedStringBuilder sb, EquatableArray<DynamicDataSourceModel> sources)
    {
        if (sources.Length == 0)
        {
            sb.Append("DynamicDataSources = Array.Empty<DynamicDataSourceReflectionInfo>()");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("DynamicDataSources = new DynamicDataSourceReflectionInfo[]");
        using (sb.Block(null))
        {
            for (int i = 0; i < sources.Length; i++)
            {
                DynamicDataSourceModel source = sources[i];
                sb.AppendLine("new DynamicDataSourceReflectionInfo");
                using (sb.Block(null))
                {
                    sb.AppendLine($"DeclaringType = typeof({source.DeclaringTypeFullyQualifiedName}),");
                    sb.AppendLine($"SourceName = \"{Escape(source.SourceName)}\",");
                    sb.AppendLine($"SourceType = {UnitTestingNamespaceGlobal}.DynamicDataSourceType.{source.RequestedSourceType},");
                    EmitDataProvider(sb, source);

                    if (source.DisplayNameMethodName is { } displayNameMethod && source.DisplayNameDeclaringTypeFullyQualifiedName is { } displayNameType)
                    {
                        sb.AppendLine($"DisplayNameDeclaringType = typeof({displayNameType}),");
                        sb.AppendLine($"DisplayNameMethodName = \"{Escape(displayNameMethod)}\",");
                        EmitDisplayNameProvider(sb, displayNameType, EscapeIdentifier(displayNameMethod));
                    }
                }

                if (i < sources.Length - 1)
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

    private static void EmitDataProvider(IndentedStringBuilder sb, DynamicDataSourceModel source)
    {
        string member = $"(object?){source.DeclaringTypeFullyQualifiedName}.{EscapeIdentifier(source.SourceName)}";
        switch (source.MemberKind)
        {
            // A field read cannot run user code, so it needs no exception translation (and reflection reads
            // fields via FieldInfo.GetValue without TargetInvocationException wrapping anyway).
            case DynamicDataMemberKind.Field:
                sb.AppendLine($"GetData = static args => {member},");
                break;

            // Property getters and (parameterless) methods can run user code. The reflection fallback invokes
            // them via PropertyInfo.GetValue / MethodInfo.Invoke, which wrap a thrown user exception in a
            // TargetInvocationException. Mirror that so a throwing source surfaces the same exception type in
            // source-generated mode as in reflection mode.
            case DynamicDataMemberKind.Property:
                EmitInvocationWrappedLambda(sb, "GetData = static args =>", $"return {member};");
                break;

            case DynamicDataMemberKind.Method:
                EmitInvocationWrappedLambda(sb, "GetData = static args =>", $"return {member}();");
                break;
        }
    }

    private static void EmitDisplayNameProvider(IndentedStringBuilder sb, string declaringType, string methodName)

        // The custom display-name method is user code; reflection calls it via MethodInfo.Invoke, which wraps
        // a thrown exception in TargetInvocationException. Preserve that wrapping here too.
        => EmitInvocationWrappedLambda(sb, "GetDisplayName = static (methodInfo, data) =>", $"return {declaringType}.{methodName}(methodInfo, data!);");

    private static void EmitInvocationWrappedLambda(IndentedStringBuilder sb, string header, string bodyStatement)
    {
        sb.AppendLine(header);
        using (sb.Block(null))
        {
            using (sb.Block("try"))
            {
                sb.AppendLine(bodyStatement);
            }

            using (sb.Block("catch (global::System.Exception ex)"))
            {
                sb.AppendLine("throw new global::System.Reflection.TargetInvocationException(ex);");
            }
        }

        sb.AppendLine(",");
    }
}
