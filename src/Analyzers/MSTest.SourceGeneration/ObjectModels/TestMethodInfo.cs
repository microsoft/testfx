// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Framework.SourceGeneration.Helpers;

using MSTest.SourceGeneration.Helpers;

namespace Microsoft.Testing.Framework.SourceGeneration.ObjectModels;

internal sealed record class TestMethodInfo
{
    internal const string TestArgumentsEntryTypeName = "MSTF::InternalUnsafeTestArgumentsEntry";
    private const string CtorVariableName = "instance";
    private const string TestExecutionContextVariableName = "testExecutionContext";
    private const string DataVariableName = "data";
    private const string DataDotArgumentsMemberAccessName = DataVariableName + ".Arguments";
    private readonly EquatableArray<(string FilePath, int StartLine, int EndLine)> _declarationReferences;
    private readonly string _methodName;
    private readonly int _methodArity;
    private readonly string _declaringAssemblyName;
    private readonly string _usingTypeFullyQualifiedName;
    private readonly bool _isAsync;
    private readonly EquatableArray<(string Key, string? Value)> _testProperties;
    private readonly TimeSpan? _testExecutionTimeout;
    private readonly EquatableArray<(string RuleId, string Description)> _invocationPragmas;
    private readonly string _methodIdentifierAssemblyName;
    private readonly string _methodIdentifierNamespace;
    private readonly string _methodIdentifierTypeName;
    private readonly string _methodIdentifierReturnFullyQualifiedTypeName;

    private TestMethodInfo(IMethodSymbol methodSymbol, INamedTypeSymbol typeUsingMethod, ImmutableArray<(string Key, string? Value)> testProperties,
        TestMethodParametersInfo parametersInfo, ITestMethodArgumentsInfo? argumentsInfo,
        IEnumerable<(string RuleId, string Description)> invocationPragmas, TimeSpan? testExecutionTimeout)
    {
        // 'SymbolDisplayFormat.CSharpShortErrorMessageFormat' gives us the minimal name while preserving sub-classes
        _methodIdentifierTypeName = methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        _methodIdentifierNamespace = methodSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : methodSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        _methodIdentifierReturnFullyQualifiedTypeName = methodSymbol.ReturnType.ToDisplayString(TestMethods.MethodIdentifierFullyQualifiedTypeFormat);
        ArgumentsInfo = argumentsInfo;
        _testExecutionTimeout = testExecutionTimeout;
        _invocationPragmas = invocationPragmas.ToImmutableArray();
        _usingTypeFullyQualifiedName = typeUsingMethod.ToDisplayString();
        // NOTE: the method symbol containing type is the type declaring the method, not the type using the method.
        string fullyQualifiedDisplayName = _usingTypeFullyQualifiedName
            + "."
            + methodSymbol.ToDisplayString().Substring(methodSymbol.ContainingType.ToDisplayString().Length + 1);
        _declarationReferences = methodSymbol.DeclaringSyntaxReferences
            .Select(x => (x.SyntaxTree.FilePath, x.SyntaxTree.GetLineSpan(x.Span)))
            .Select(tuple => (tuple.FilePath, tuple.Item2.StartLinePosition.Line + 1, tuple.Item2.EndLinePosition.Line + 1))
            .ToImmutableArray();
        _methodName = methodSymbol.Name;
        _methodArity = methodSymbol.Arity;
        // 'SymbolDisplayFormat.FullyQualifiedFormat' would add version, culture and public key token to the assembly name.
        _declaringAssemblyName = methodSymbol.ContainingAssembly.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        _methodIdentifierAssemblyName = methodSymbol.ContainingAssembly.ToDisplayString();
        _isAsync = !methodSymbol.ReturnsVoid;
        _testProperties = testProperties;
        ParametersInfo = parametersInfo;

        TestMethodStableUid = $"\"{_declaringAssemblyName}.{fullyQualifiedDisplayName}\"";
    }

    internal string TestMethodStableUid { get; }

    internal ITestMethodArgumentsInfo? ArgumentsInfo { get; }

    internal TestMethodParametersInfo ParametersInfo { get; }

    public static TestMethodInfo? TryBuild(IMethodSymbol methodSymbol, INamedTypeSymbol typeUsingMethod, WellKnownTypes wellKnownTypes)
    {
        try
        {
            // We don't need to be checking for resultant visibility here because we know parent is checking for it
            if (!methodSymbol.IsValidTestMethodShape(wellKnownTypes)
                || methodSymbol.IsKnownNonTestMethod(wellKnownTypes))
            {
                return null;
            }

            ImmutableArray<AttributeData> attributes = methodSymbol.GetAttributes();

            if (attributes.Length == 0 || !attributes.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, wellKnownTypes.TestMethodAttributeSymbol)))
            {
                return null;
            }

            List<AttributeData> dataRowAttributes = [];
            List<AttributeData> dynamicDataAttributes = [];
            List<AttributeData> testPropertyAttributes = [];
            List<(string RuleId, string Description)> pragmas = [];
            TimeSpan? testExecutionTimeout = null;
            foreach (AttributeData attribute in attributes)
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, wellKnownTypes.DataRowAttributeSymbol)
                    && attribute.ConstructorArguments.Length == 1)
                {
                    dataRowAttributes.Add(attribute);
                }
                else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, wellKnownTypes.DynamicDataAttributeSymbol)
                    && attribute.ConstructorArguments.Length is 1 or 2 or 3)
                {
                    dynamicDataAttributes.Add(attribute);
                }
                else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, wellKnownTypes.TestPropertyAttributeSymbol)
                    && attribute.ConstructorArguments.Length == 2)
                {
                    testPropertyAttributes.Add(attribute);
                }
                else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, wellKnownTypes.SystemObsoleteAttributeSymbol))
                {
                    if (attribute.ConstructorArguments.Length == 0)
                    {
                        pragmas.Add(("CS0612", "Type or member is obsolete"));
                    }
                    else if (attribute.ConstructorArguments.Length == 1
                        // We cannot suppress CS0619 as it's an error level
                        || (attribute.ConstructorArguments.Length == 2 && attribute.ConstructorArguments[1].Value?.Equals(false) == true))
                    {
                        pragmas.Add(("CS0618", "Type or member is obsolete"));
                    }
                }
                else if (attribute.TryGetTestExecutionTimeout(wellKnownTypes.TestExecutionTimeoutAttributeSymbol, wellKnownTypes.TimeSpanSymbol,
                    out TimeSpan maybeTestExecutionTimeout))
                {
                    testExecutionTimeout = maybeTestExecutionTimeout;
                }
            }

            TestMethodParametersInfo parametersInfo = new(methodSymbol.Parameters);

            // TODO: This code is not handling the case where both DataRow and DynamicData attributes are present.
            ITestMethodArgumentsInfo? argumentsInfo =
                (ITestMethodArgumentsInfo?)DataRowTestMethodArgumentsInfo.TryBuild(methodSymbol, dataRowAttributes, parametersInfo)
                ?? DynamicDataTestMethodArgumentsInfo.TryBuild(methodSymbol, dynamicDataAttributes, wellKnownTypes);

            ImmutableArray<(string Key, string? Value)> testProperties = testPropertyAttributes
                .Where(attr => attr.ConstructorArguments[0].Value is not null)
                .Select(attr => (attr.ConstructorArguments[0].Value!.ToString()!, attr.ConstructorArguments[1].Value?.ToString()))
                .ToImmutableArray();

            // Method is valid test method
            return new(methodSymbol, typeUsingMethod, testProperties, parametersInfo, argumentsInfo, pragmas, testExecutionTimeout);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed for method {methodSymbol.ToDisplayString()}, with {ex}", ex);
        }
    }

    public void AppendTestNode(IndentedStringBuilder sourceStringBuilder, TestTypeInfo testTypeInfo)
    {
        bool useAsyncNode = _isAsync;
        AppendTestNodeCtorDeclaration(sourceStringBuilder, useAsyncNode, ParametersInfo.ParametersTuple, ArgumentsInfo);
        using (sourceStringBuilder.AppendBlock(closingBraceSuffixChar: ','))
        {
            sourceStringBuilder.AppendLine($"StableUid = {TestMethodStableUid},");
            sourceStringBuilder.AppendLine($"DisplayName = \"{_methodName}\",");

            int propertiesCount =
                1 // properties that are always present
                + _declarationReferences.Length
                + _testProperties.Length;
            using (sourceStringBuilder.AppendBlock($"Properties = new Msg::IProperty[{propertiesCount}]", closingBraceSuffixChar: ','))
            {
                sourceStringBuilder.AppendLine("new Msg::TestMethodIdentifierProperty(");
                sourceStringBuilder.IndentationLevel++;
                sourceStringBuilder.AppendLine($"\"{_methodIdentifierAssemblyName}\",");
                sourceStringBuilder.AppendLine($"\"{_methodIdentifierNamespace}\",");
                sourceStringBuilder.AppendLine($"\"{_methodIdentifierTypeName}\",");
                sourceStringBuilder.AppendLine($"\"{_methodName}\",");
                sourceStringBuilder.AppendLine($"{_methodArity},");

                if (ParametersInfo.ParametersMethodIdentifierFullyQualifiedTypes.Length > 0)
                {
                    using (sourceStringBuilder.AppendBlock($"new string[{ParametersInfo.ParametersMethodIdentifierFullyQualifiedTypes.Length}]", closingBraceSuffixChar: ','))
                    {
                        foreach (string parameterIdentifierType in ParametersInfo.ParametersMethodIdentifierFullyQualifiedTypes)
                        {
                            sourceStringBuilder.AppendLine($"\"{parameterIdentifierType}\",");
                        }
                    }
                }
                else
                {
                    sourceStringBuilder.AppendLine("Sys::Array.Empty<string>(),");
                }

                sourceStringBuilder.AppendLine($"\"{_methodIdentifierReturnFullyQualifiedTypeName}\"),");
                sourceStringBuilder.IndentationLevel--;

                foreach ((string filePath, int startLine, int endLine) in _declarationReferences)
                {
                    sourceStringBuilder.AppendLine($"new Msg::TestFileLocationProperty(@\"{filePath}\", new(new({startLine}, -1), new({endLine}, -1))),");
                }

                foreach ((string key, string? value) in _testProperties)
                {
                    sourceStringBuilder.AppendLine($"new Msg::TestMetadataProperty(\"{key}\", \"{value}\"),");
                }
            }

            ArgumentsInfo?.AppendArguments(sourceStringBuilder);
            sourceStringBuilder.Append("Body = static ");
            if (useAsyncNode)
            {
                sourceStringBuilder.Append("async ");
            }

            sourceStringBuilder.Append(
                ParametersInfo.Parameters.IsEmpty
                    ? TestExecutionContextVariableName
                    : $"({TestExecutionContextVariableName}, {DataVariableName})");

            using (sourceStringBuilder.AppendBlock(" =>", closingBraceSuffixChar: ','))
            {
                MaybeAppendBodyCancellationTokenCreation(sourceStringBuilder, testTypeInfo);
                AppendCtorCall(sourceStringBuilder, testTypeInfo);
                AppendMethodCall(sourceStringBuilder);
            }
        }
    }

    private void MaybeAppendBodyCancellationTokenCreation(IndentedStringBuilder sourceStringBuilder, TestTypeInfo testTypeInfo)
    {
        if (testTypeInfo.TestExecutionTimeout is null && _testExecutionTimeout is null)
        {
            return;
        }

        TimeSpan minTimeout = (testTypeInfo.TestExecutionTimeout, _testExecutionTimeout) switch
        {
            (null, { } time) => time,
            ({ } time, null) => time,
            ({ } time1, { } time2) when time1 <= time2 => time1,
            ({ } time1, { } time2) when time1 > time2 => time2,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        sourceStringBuilder.AppendLine($"{TestExecutionContextVariableName}.CancelTestExecution(new global::System.TimeSpan({minTimeout.Ticks}));");
    }

    private static void AppendTestNodeCtorDeclaration(IndentedStringBuilder nodeBuilder, bool useAsyncNode,
        string? parametersTuple, ITestMethodArgumentsInfo? argumentsInfo)
    {
        if (parametersTuple != null && argumentsInfo is null)
        {
            nodeBuilder.AppendLine("// The test method is parameterized but no argument was specified.");
            nodeBuilder.AppendLine("// This is most often caused by using an unsupported arguments input.");
            nodeBuilder.AppendLine("// Possible resolutions:");
            nodeBuilder.AppendLine("// - There is a mismatch between arguments from [DataRow] and the method parameters.");
            nodeBuilder.AppendLine("// - There is a mismatch between arguments from [DynamicData] and the method parameters.");
            nodeBuilder.AppendLine("// If nothing else worked, report the error and exclude this method by using [Ignore].");
        }

        nodeBuilder.Append("new MSTF::");
        nodeBuilder.Append((useAsyncNode, argumentsInfo) switch
        {
            (true, null) => "InternalUnsafeAsyncActionTestNode",
            (true, _) => "InternalUnsafeAsyncActionParameterizedTestNode",

            (false, null) => "InternalUnsafeActionTestNode",
            (false, _) => "InternalUnsafeActionParameterizedTestNode",
        });
        nodeBuilder.AppendLine((parametersTuple, argumentsInfo?.IsTestArgumentsEntryReturnType ?? false) switch
        {
            (null, _) => string.Empty,
            (_, false) => $"<{parametersTuple}>",
            (_, true) => $"<{TestArgumentsEntryTypeName}<{parametersTuple}>>",
        });
    }

    private static void AppendCtorCall(IndentedStringBuilder nodeBuilder, TestTypeInfo testTypeInfo)
    {
        if (testTypeInfo.IsIAsyncDisposable)
        {
            nodeBuilder.Append("await using ");
        }
        else if (testTypeInfo.IsIDisposable)
        {
            nodeBuilder.Append("using ");
        }

        nodeBuilder.Append($"var {CtorVariableName} = new {testTypeInfo.ConstructorShortName}();");
    }

    private void AppendMethodCall(IndentedStringBuilder sourceStringBuilder)
    {
        sourceStringBuilder.AppendLine();
        IDisposable tryBlock = sourceStringBuilder.AppendBlock("try");

        foreach ((string ruleId, string description) in _invocationPragmas)
        {
            sourceStringBuilder.AppendUnindentedLine($"#pragma warning disable {ruleId} // {description}");
        }

        if (_isAsync)
        {
            sourceStringBuilder.Append("await ");
        }

        sourceStringBuilder.Append($"{CtorVariableName}.{_methodName}(");

        string dataVariable = ArgumentsInfo?.IsTestArgumentsEntryReturnType ?? false
            ? DataDotArgumentsMemberAccessName
            : DataVariableName;

        if (ParametersInfo.Parameters.Length == 1)
        {
            sourceStringBuilder.Append(dataVariable);
        }
        else
        {
            for (int i = 0; i < ParametersInfo.Parameters.Length; i++)
            {
                if (i > 0)
                {
                    sourceStringBuilder.Append(", ");
                }

                sourceStringBuilder.Append($"{dataVariable}.{ParametersInfo.Parameters[i].Name}");
            }
        }

        sourceStringBuilder.AppendLine(");");

        foreach ((string ruleId, string description) in _invocationPragmas)
        {
            sourceStringBuilder.AppendUnindentedLine($"#pragma warning restore {ruleId} // {description}");
        }

        tryBlock?.Dispose();

        using (sourceStringBuilder.AppendBlock("catch (global::System.Exception ex)"))
        {
            sourceStringBuilder.AppendLine($"{TestExecutionContextVariableName}.ReportException(ex, null);");
        }
    }
}
