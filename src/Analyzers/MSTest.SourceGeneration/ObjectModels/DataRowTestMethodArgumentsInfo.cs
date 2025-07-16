// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Testing.Framework.SourceGeneration.Helpers;

namespace Microsoft.Testing.Framework.SourceGeneration.ObjectModels;

internal sealed class DataRowTestMethodArgumentsInfo : ITestMethodArgumentsInfo
{
    private readonly ImmutableArray<ImmutableArray<string>> _argumentsRows;
    private readonly TestMethodParametersInfo _parametersInfo;

    public bool IsTestArgumentsEntryReturnType => true;

    public string? GeneratorMethodFullName { get; }

    public DataRowTestMethodArgumentsInfo(ImmutableArray<ImmutableArray<string>> argumentsRows, TestMethodParametersInfo parametersInfo)
    {
        _argumentsRows = argumentsRows;
        _parametersInfo = parametersInfo;
    }

    public static DataRowTestMethodArgumentsInfo? TryBuild(IMethodSymbol methodSymbol, IEnumerable<AttributeData> argumentsAttributes,
        TestMethodParametersInfo parametersInfo)
    {
        var argumentsRows = argumentsAttributes.Select(attr => GetInlineArguments(methodSymbol, attr).ToImmutableArray()).ToImmutableArray();

        return argumentsRows.IsEmpty
            ? null
            : new(argumentsRows, parametersInfo);
    }

    public void AppendArguments(IndentedStringBuilder nodeBuilder)
    {
        using (nodeBuilder.AppendBlock($"GetArguments = static () => new {TestMethodInfo.TestArgumentsEntryTypeName}<{_parametersInfo.ParametersTuple}>[]", closingBraceSuffixChar: ','))
        {
            foreach (ImmutableArray<string> arguments in _argumentsRows)
            {
                string argumentsEntry = arguments.Length > 1
                    ? "(" + string.Join(", ", arguments) + ")"
                    : arguments[0];
                string argumentsUid = GetArgumentsUid([.. _parametersInfo.Parameters.Select(x => x.Name)], arguments);
                nodeBuilder.AppendLine($"new {TestMethodInfo.TestArgumentsEntryTypeName}<{_parametersInfo.ParametersTuple}>({argumentsEntry}, \"{argumentsUid}\"),");
            }
        }
    }

    private static string GetArgumentsUid(string[] parameterNames, IList<string> arguments)
    {
        StringBuilder argumentsUidBuilder = new();

        for (int i = 0; i < arguments.Count; i++)
        {
            if (i < parameterNames.Length)
            {
                if (i != 0)
                {
                    argumentsUidBuilder.Append(", ");
                }

                argumentsUidBuilder.Append(parameterNames[i]);
                argumentsUidBuilder.Append(": ");
            }

            EscapeArgument(arguments[i], argumentsUidBuilder);
        }

        return argumentsUidBuilder.ToString();
    }

    internal /* for testing purposes */ static void EscapeArgument(string argument, StringBuilder argumentsUidBuilder)
    {
        int escapeCharCount = 0;
        for (int i = 0; i < argument.Length; i++)
        {
            char currentChar = argument[i];

            if (currentChar == '\\')
            {
                escapeCharCount++;
            }
            else if (currentChar == '"' && escapeCharCount % 2 == 0)
            {
                argumentsUidBuilder.Append('\\');
                escapeCharCount = 0;
            }
            else
            {
                escapeCharCount = 0;
            }

            argumentsUidBuilder.Append(argument[i]);
        }
    }

    private static IEnumerable<string> GetInlineArguments(IMethodSymbol methodSymbol, AttributeData attr)
    {
        TypedConstant argumentsAttributeArguments = attr.ConstructorArguments[0];
        if (argumentsAttributeArguments.IsNull)
        {
            yield return "null";
            yield break;
        }

        StringBuilder argumentsBuilder = new();
        Stack<(TypedConstant Argument, bool HasNextArg, bool IsInArray, int ClosingCurlyBraceCount)> argumentStack = new();

        bool hasManyArgsButExpectsSingleArray =
            methodSymbol.Parameters is [{ Type.TypeKind: TypeKind.Array }]
            && argumentsAttributeArguments.Values.Length > 1;

        if (hasManyArgsButExpectsSingleArray)
        {
            argumentStack.Push((argumentsAttributeArguments, HasNextArg: false, IsInArray: false, ClosingCurlyBraceCount: 0));
        }
        else
        {
            // We are using the ctor with params object[];
            if (argumentsAttributeArguments.Kind == TypedConstantKind.Array)
            {
                for (int i = argumentsAttributeArguments.Values.Length - 1; i >= 0; i--)
                {
                    argumentStack.Push((argumentsAttributeArguments.Values[i],
                        HasNextArg: i < argumentsAttributeArguments.Values.Length - 1,
                        IsInArray: false,
                        ClosingCurlyBraceCount: 0));
                }
            }
            else
            {
                argumentStack.Push((argumentsAttributeArguments,
                    HasNextArg: false,
                    IsInArray: false,
                    ClosingCurlyBraceCount: 0));
            }
        }

        while (argumentStack.Count > 0)
        {
            (TypedConstant argument, bool hasNextArg, bool isInArray, int closingCurlyBraceCount) = argumentStack.Pop();

            if (argument.Kind == TypedConstantKind.Array)
            {
                argumentsBuilder.Append("new ");
                if (argument.Type is not null)
                {
                    argumentsBuilder.Append(argument.Type.ToDisplayString());
                }
                else
                {
                    argumentsBuilder.Append("[]");
                }

                argumentsBuilder.Append(" { ");

                for (int i = argument.Values.Length - 1; i >= 0; i--)
                {
                    argumentStack.Push((argument.Values[i],
                        HasNextArg: i < argument.Values.Length - 1 || hasNextArg,
                        IsInArray: true,
                        ClosingCurlyBraceCount: i == argument.Values.Length - 1 ? closingCurlyBraceCount + 1 : 0));
                }
            }
            else
            {
                if (argument.Kind == TypedConstantKind.Enum)
                {
                    // We could cast the argument to TypedConstant and get the full name of the type
                    // with the global:: prefix from e.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    // but then we don't have an easy way to get the value in CSharp format without the type.
                    // So we just prepend.
                    argumentsBuilder.Append("global::" + argument.ToCSharpString());
                }
                else
                {
                    argumentsBuilder.Append(argument.ToCSharpString());
                }

                for (int i = 0; i < closingCurlyBraceCount; i++)
                {
                    argumentsBuilder.Append(" }");
                }

                if (hasNextArg)
                {
                    if (isInArray && closingCurlyBraceCount == 0)
                    {
                        argumentsBuilder.Append(", ");
                    }
                    else
                    {
                        yield return argumentsBuilder.ToString();
                        argumentsBuilder.Clear();
                    }
                }
            }
        }

        yield return argumentsBuilder.ToString();
    }
}
