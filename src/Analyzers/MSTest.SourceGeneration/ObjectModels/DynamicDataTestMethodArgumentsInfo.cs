// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Analyzers.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Framework.SourceGeneration.Helpers;

namespace Microsoft.Testing.Framework.SourceGeneration.ObjectModels;

/// <summary>
/// Clone of ArgumentsProvider, that is implementing very similar functionality for DynamicData, because there we don't know what types the user is using, so we do basically the same
/// but we cast their data to the assumed types.
/// </summary>
internal sealed class DynamicDataTestMethodArgumentsInfo : ITestMethodArgumentsInfo
{
    // Based on DynamicDataSourceType in:
    // https://github.com/microsoft/testfx/blob/8cf945ba740034e37e0a16efddad85f6b0fb67bc/src/TestFramework/TestFramework/Attributes/DataSource/DynamicDataAttribute.cs#L17-L36
    private const int DynamicDataSourceTypeProperty = 0;
    private const int DynamicDataSourceTypeMethod = 1;
    private const int DynamicDataSourceTypeAutoDetect = 2;
    private const int DynamicDataSourceTypeField = 3;

    internal const string TestArgumentsEntryTypeName = "MSTF::InternalUnsafeTestArgumentsEntry";
    internal const string DynamicDataNameProviderTypeName = "MSTF::DynamicDataNameProvider";
    private const string TestArgumentsEntryProviderMethodName = nameof(TestArgumentsEntryProviderMethodName);
    private const string TestArgumentsEntryProviderMethodType = nameof(TestArgumentsEntryProviderMethodType);
    private readonly string _memberName;
    private readonly string _memberFullType;
    private readonly SymbolKind _memberKind;
    private readonly TestMethodParametersInfo _testMethodParameters;
    private readonly bool _targetMethodReturnsCollectionOfTestArgumentsEntry;

    private DynamicDataTestMethodArgumentsInfo(string memberName, string memberFullType, SymbolKind memberKind, TestMethodParametersInfo testMethodParameters,
        bool targetMemberReturnsCollectionOfTestArgumentsEntry, string? generatorMethodFullName)
    {
        _memberName = memberName;
        _memberFullType = memberFullType;
        // This is always true, because it tells the source gen that GetParameters will return collection of TestArgumentsEntry.
        // If the target member does not return that type, we will write code to adapt the result to this collection in AppendArguments method below.
        IsTestArgumentsEntryReturnType = true;
        _targetMethodReturnsCollectionOfTestArgumentsEntry = targetMemberReturnsCollectionOfTestArgumentsEntry;
        _memberKind = memberKind;
        GeneratorMethodFullName = generatorMethodFullName;
        _testMethodParameters = testMethodParameters;
    }

    public bool IsTestArgumentsEntryReturnType { get; }

    public string? GeneratorMethodFullName { get; }

    public static DynamicDataTestMethodArgumentsInfo? TryBuild(IMethodSymbol methodSymbol, List<AttributeData> argumentsProviderAttributes,
        WellKnownTypes wellKnownTypes)
    {
        // We don't support more than one provider on method at the same time.
        if (argumentsProviderAttributes.Count != 1)
        {
            return null;
        }

        // We collect all the providers that match, and they might conflict by parameters, but this also ties us to the actual type
        // so user cannot subclass.
        if (!SymbolEqualityComparer.Default.Equals(argumentsProviderAttributes[0].AttributeClass, wellKnownTypes.DynamicDataAttributeSymbol))
        {
            return null;
        }

        AttributeData attribute = argumentsProviderAttributes[0];
        INamedTypeSymbol memberTypeSymbol = methodSymbol.ContainingType;
        string? memberName = null;
        int memberKind = DynamicDataSourceTypeAutoDetect;

        foreach (TypedConstant arg in attribute.ConstructorArguments)
        {
            if (arg.Type?.SpecialType == SpecialType.System_String)
            {
                memberName = arg.Value?.ToString();
            }
            else if (arg.Value is INamedTypeSymbol argTypeSymbol)
            {
                memberTypeSymbol = argTypeSymbol;
            }
            else if (arg.Value is int argValueAsInt)
            {
                memberKind = argValueAsInt;
            }
        }

        return memberName is null
            ? null
            : TryBuildFromDynamicData(memberTypeSymbol, memberName, ToSymbolKind(memberKind), wellKnownTypes, methodSymbol);

        static SymbolKind? ToSymbolKind(int memberKind) =>
            memberKind switch
            {
                DynamicDataSourceTypeProperty => SymbolKind.Property,
                DynamicDataSourceTypeMethod => SymbolKind.Method,
                DynamicDataSourceTypeField => SymbolKind.Field,
                DynamicDataSourceTypeAutoDetect => null,
                _ => throw ApplicationStateGuard.Unreachable(),
            };
    }

    private static DynamicDataTestMethodArgumentsInfo? TryBuildFromDynamicData(INamedTypeSymbol memberTypeSymbol, string memberName, SymbolKind? symbolKind,
    WellKnownTypes wellKnownTypes, IMethodSymbol testMethodSymbol)
    {
        // Dynamic data supports Properties, Methods, and Fields.
        // null is also possible and means "AutoDetect"
        if (symbolKind is not (SymbolKind.Property or SymbolKind.Method or SymbolKind.Field or null))
        {
            return null;
        }

        ISymbol? firstMatchingMember = memberTypeSymbol.GetAllMembers(memberName)
            .SelectMany(x => x)
            // DynamicData supports properties, methods, and fields.
            // .Where(s => s.IsStatic && (s.Kind is SymbolKind.Field or SymbolKind.Property or SymbolKind.Method))
            // .Where(s => symbolKind == null || s.Kind == symbolKind)
            .Where(s => s.IsStatic && (s.Kind == symbolKind || (symbolKind is null && s.Kind is SymbolKind.Property or SymbolKind.Method or SymbolKind.Field)))
            .Select(s => s switch
            {
                IPropertySymbol propertySymbol => (ISymbol)propertySymbol,
                IMethodSymbol methodSymbol => methodSymbol,
                IFieldSymbol fieldSymbol => fieldSymbol,
                _ => throw ApplicationStateGuard.Unreachable(),
            })
            .OrderBy(tuple => tuple.Kind switch
            {
                SymbolKind.Property => 1,
                SymbolKind.Method => 2,
                SymbolKind.Field => 3,
                _ => throw ApplicationStateGuard.Unreachable(),
            })
            .FirstOrDefault();

        if (firstMatchingMember is null
            || firstMatchingMember.GetMemberType() is not { } returnMemberTypeSymbol)
        {
            return null;
        }

        // We want to check if the member returns a type that implements IEnumerable<InternalUnsafeTestArgumentsEntry<...>>
        var allInterfacesAndSelfIfInterface = new List<INamedTypeSymbol>(returnMemberTypeSymbol.AllInterfaces);
        if (returnMemberTypeSymbol.TypeKind == TypeKind.Interface
            && returnMemberTypeSymbol is INamedTypeSymbol namedInterfaceSymbol)
        {
            allInterfacesAndSelfIfInterface.Add(namedInterfaceSymbol);
        }

        // If the return type is not IEnumerable<InternalUnsafeTestArgumentsEntry<...>> we will adapt it later.
        // The implementation of https://github.com/microsoft/testfx/blob/main/src/TestFramework/TestFramework/Attributes/DataSource/DynamicDataAttribute.cs#L87
        // only allows IEnumerable<object[]> so we will assume that is true.
        bool targetMemberReturnsCollectionOfTestArgumentsEntry = allInterfacesAndSelfIfInterface.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, wellKnownTypes.IEnumerable1Symbol)
            && i.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0].OriginalDefinition, wellKnownTypes.TestArgumentsEntrySymbol));

        string? generatorMethodFullName = null;

        // This way we could handle the additional named parameters on [DynamicData
        // DynamicDataDisplayName
        // and DynamicDataDisplayNameDeclaringType, but this is imperfect implementation for now.
        // KeyValuePair<string, TypedConstant> argumentsEntryProviderMethodType = namedArguments.FirstOrDefault(x => x.Key == TestArgumentsEntryProviderMethodType);
        // if (argumentsEntryProviderMethodType.Key == TestArgumentsEntryProviderMethodType
        //     && argumentsEntryProviderMethodType.Value.Value is INamedTypeSymbol generatorMethodTypeSymbol)
        // {
        //     generatorMethodFullName = generatorMethodTypeSymbol.ToDisplayString();
        // }
        //
        // KeyValuePair<string, TypedConstant> argumentsEntryProviderMethodName = namedArguments.FirstOrDefault(x => x.Key == TestArgumentsEntryProviderMethodName);
        // if (argumentsEntryProviderMethodName.Key == TestArgumentsEntryProviderMethodName
        //     && argumentsEntryProviderMethodName.Value.Value is string generatorMethodName)
        // {
        //     generatorMethodFullName ??= methodTypeSymbol.ToDisplayString();
        //     generatorMethodFullName = $"{generatorMethodFullName}.{generatorMethodName}";
        // }
        var testMethodParameters = new TestMethodParametersInfo(testMethodSymbol.Parameters);

        return new(firstMatchingMember.Name, firstMatchingMember.ContainingType.ToDisplayString(),
            firstMatchingMember.Kind, testMethodParameters, targetMemberReturnsCollectionOfTestArgumentsEntry, generatorMethodFullName);
    }

    public void AppendArguments(IndentedStringBuilder nodeBuilder)
    {
        nodeBuilder.Append("GetArguments = static () => ");

        using (nodeBuilder.AppendBlock())
        {
            if (_targetMethodReturnsCollectionOfTestArgumentsEntry)
            {
                // We just return the data as is.
                nodeBuilder.Append(" return ");
            }
            else
            {
                // We need to convert the data to TestArgumentsEntry.
                nodeBuilder.Append("var data = ");
            }

            // Call the member.
            nodeBuilder.Append($"{_memberFullType}.{_memberName}");

            if (_memberKind is SymbolKind.Method)
            {
                nodeBuilder.Append("()");
            }

            nodeBuilder.AppendLine(';');

            if (!_targetMethodReturnsCollectionOfTestArgumentsEntry)
            {
                string tupleType = $"{TestArgumentsEntryTypeName}<{_testMethodParameters.ParametersTuple}>";
                nodeBuilder.AppendLine($"var dataCollection = new ColGen.List<{tupleType}>();");
                nodeBuilder.AppendLine($"var index = 0;");
                string expand = string.Join(", ", _testMethodParameters.Parameters.Select((p, i) => $"({p.FullyQualifiedType}) item[{i}]"));
                using (nodeBuilder.AppendBlock("foreach (var item in data)"))
                {
                    IEnumerable<string> parameterNames = _testMethodParameters.Parameters.Select(p => p.Name);
                    nodeBuilder.AppendLine($$"""string uidFragment = {{DynamicDataNameProviderTypeName}}.GetUidFragment(new string[] {"{{string.Join("\", \"", parameterNames)}}"}, item, index);""");
                    nodeBuilder.AppendLine("index++;");
                    nodeBuilder.AppendLine($"""dataCollection.Add(new(({expand}), uidFragment));""");
                }

                nodeBuilder.AppendLine("return dataCollection;");
            }
        }

        nodeBuilder.AppendLine(',');
    }
}
