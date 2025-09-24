// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;
using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0025: <inheritdoc cref="Resources.PreferAssertFailOverAlwaysFalseConditionsTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PreferAssertFailOverAlwaysFalseConditionsAnalyzer : DiagnosticAnalyzer
{
    private enum EqualityStatus
    {
        Unknown,
        Equal,
        NotEqual,
    }

    private const string ExpectedParameterName = "expected";
    private const string NotExpectedParameterName = "notExpected";
    private const string ActualParameterName = "actual";
    private const string ConditionParameterName = "condition";
    private const string ValueParameterName = "value";
    private const string MessageParameterName = "message";
    private const string ParametersParameterName = "parameters";

    private static readonly LocalizableResourceString Title = new(nameof(Resources.PreferAssertFailOverAlwaysFalseConditionsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PreferAssertFailOverAlwaysFalseConditionsMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PreferAssertFailOverAlwaysFalseConditionsRuleId,
        Title,
        MessageFormat,
        null,
        Category.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? assertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert);
            if (assertSymbol is not null)
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;

        if (assertSymbol.Equals(operation.TargetMethod.ContainingType, SymbolEqualityComparer.Default) &&
            IsAlwaysFalse(operation, context.Operation.SemanticModel))
        {
            context.ReportDiagnostic(operation.CreateDiagnostic(Rule, GetAdditionalLocations(operation), properties: null, operation.TargetMethod.Name));
        }
    }

    private static ImmutableArray<Location> GetAdditionalLocations(IInvocationOperation operation)
    {
        IArgumentOperation? messageArg = operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == MessageParameterName);
        if (messageArg is null)
        {
            return ImmutableArray<Location>.Empty;
        }

        IArgumentOperation? parametersArg = operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == ParametersParameterName);
        if (parametersArg is null)
        {
            return ImmutableArray.Create(messageArg.Syntax.GetLocation());
        }

        if (parametersArg.ArgumentKind == ArgumentKind.ParamArray)
        {
            ImmutableArray<Location>.Builder builder = ImmutableArray.CreateBuilder<Location>();
            builder.Add(messageArg.Syntax.GetLocation());
            if (parametersArg.Value is IArrayCreationOperation { Initializer.ElementValues: { } elements })
            {
                foreach (IOperation element in elements)
                {
                    builder.Add(element.Syntax.GetLocation());
                }
            }

            return builder.ToImmutable();
        }

        return ImmutableArray.Create(messageArg.Syntax.GetLocation(), parametersArg.Syntax.GetLocation());
    }

    private static bool IsAlwaysFalse(IInvocationOperation operation, SemanticModel? semanticModel)
        => operation.TargetMethod.Name switch
        {
            "IsTrue" => GetConditionArgument(operation) is { Value.ConstantValue: { HasValue: true, Value: false } },
            "IsFalse" => GetConditionArgument(operation) is { Value.ConstantValue: { HasValue: true, Value: true } },
            "AreEqual" => GetEqualityStatus(operation, ExpectedParameterName) == EqualityStatus.NotEqual,
            "AreNotEqual" => GetEqualityStatus(operation, NotExpectedParameterName) == EqualityStatus.Equal,
            "IsNotNull" => GetValueArgument(operation) is { Value.ConstantValue: { HasValue: true, Value: null } },
            "IsNull" => GetValueArgument(operation) is { } valueArgumentOperation && IsNotNullableType(valueArgumentOperation, semanticModel),
            _ => false,
        };

    private static bool IsNotNullableType(IArgumentOperation valueArgumentOperation, SemanticModel? semanticModel)
    {
        ITypeSymbol? valueArgType = valueArgumentOperation.Value.GetReferencedMemberOrLocalOrParameter().GetReferencedMemberOrLocalOrParameter();
        if (valueArgType is null)
        {
            return false;
        }

        // If this is a value type, check if it's not System.Nullable<T>
        if (valueArgType.IsValueType)
        {
            return valueArgType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
        }

        // For reference types, we need to check both the nullable annotation and the nullable context
        if (valueArgType.IsReferenceType)
        {
            // If nullable annotation is explicitly Annotated (e.g., string?), then it's nullable
            if (valueArgType.NullableAnnotation == NullableAnnotation.Annotated)
            {
                return false;
            }

            // If nullable annotation is NotAnnotated, we need to check if nullable reference types are enabled
            if (valueArgType.NullableAnnotation == NullableAnnotation.NotAnnotated)
            {
                // If we have a semantic model, check the nullable context at this location
                if (semanticModel is not null)
                {
                    // Get the nullable context at the argument's syntax location
                    var syntaxNode = valueArgumentOperation.Syntax;
                    
                    // Check if nullable reference types are enabled at this location
                    // In a #nullable disable context, we should treat reference types as potentially nullable
                    // and not report the diagnostic
                    var nullableContext = semanticModel.GetNullableContext(syntaxNode.SpanStart);
                    if (nullableContext == Microsoft.CodeAnalysis.NullableContext.Disabled)
                    {
                        // In a disabled nullable context, reference types should be treated as potentially nullable
                        return false;
                    }
                }

                // If nullable reference types are enabled (or we can't determine), 
                // and the annotation is NotAnnotated, then it's a non-nullable reference type
                return true;
            }
        }

        return false;
    }

    private static IArgumentOperation? GetArgumentWithName(IInvocationOperation operation, string name)
        => operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == name);

    private static IArgumentOperation? GetConditionArgument(IInvocationOperation operation)
        => GetArgumentWithName(operation, ConditionParameterName);

    private static IArgumentOperation? GetValueArgument(IInvocationOperation operation)
        => GetArgumentWithName(operation, ValueParameterName);

    private static EqualityStatus GetEqualityStatus(IInvocationOperation operation, string expectedOrNotExpectedParameterName)
    {
        if (GetArgumentWithName(operation, expectedOrNotExpectedParameterName) is { } expectedOrNotExpectedArgument &&
            GetArgumentWithName(operation, ActualParameterName) is { } actualArgument &&
            expectedOrNotExpectedArgument.Value.ConstantValue.HasValue &&
            actualArgument.Value.ConstantValue.HasValue)
        {
            return Equals(expectedOrNotExpectedArgument.Value.ConstantValue.Value, actualArgument.Value.ConstantValue.Value) ? EqualityStatus.Equal : EqualityStatus.NotEqual;
        }

        // We are not sure about the equality status
        return EqualityStatus.Unknown;
    }
}
