// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0005: <inheritdoc cref="Resources.TestContextShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestContextShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestContextShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestContextShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestContextShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal const string TestContextPropertyName = "TestContext";

    internal static readonly DiagnosticDescriptor TestContextShouldBeValidRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestContextShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(TestContextShouldBeValidRule);

    private static IFieldSymbol? TryGetReturnedField(ImmutableArray<IOperation> operations)
    {
        foreach (IOperation operation in operations)
        {
            if (TryGetReturnedField(operation) is { } returnedMember)
            {
                return returnedMember;
            }
        }

        return null;
    }

    private static IFieldSymbol? TryGetReturnedField(IOperation operation)
    {
        if (operation is IBlockOperation blockOperation)
        {
            return TryGetReturnedField(blockOperation.Operations);
        }

        if (operation is IReturnOperation { ReturnedValue: IFieldReferenceOperation { Field: { } returnedField } })
        {
            return returnedField;
        }

        // We can't figure out exactly the field returned by this property.
        return null;
    }

    private static bool AssignsParameterToMember(IParameterSymbol parameter, ISymbol member, ImmutableArray<IOperation> operations)
    {
        foreach (IOperation operation in operations)
        {
            if (AssignsParameterToMember(parameter, member, operation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AssignsParameterToMember(IParameterSymbol parameter, ISymbol member, IOperation operation)
    {
        if (operation is IBlockOperation blockOperation)
        {
            return AssignsParameterToMember(parameter, member, blockOperation.Operations);
        }

        if (operation is IExpressionStatementOperation expressionStatementOperation)
        {
            operation = expressionStatementOperation.Operation;
        }

        if (operation is ISimpleAssignmentOperation assignmentOperation &&
            assignmentOperation.Target is IMemberReferenceOperation targetMemberReference &&
            SymbolEqualityComparer.Default.Equals(targetMemberReference.Member, member))
        {
            // Handle direct parameter assignment
            if (assignmentOperation.Value is IParameterReferenceOperation parameterReference &&
                SymbolEqualityComparer.Default.Equals(parameterReference.Parameter, parameter))
            {
                return true;
            }

            // Handle null-coalescing operator with parameter on left side
            // e.g., TestContext = testContext ?? throw new ArgumentNullException(nameof(testContext));
            if (assignmentOperation.Value is IBinaryOperation binaryOperation &&
                binaryOperation.OperatorKind == BinaryOperatorKind.Coalescing &&
                binaryOperation.LeftOperand is IParameterReferenceOperation coalescingParamRef &&
                SymbolEqualityComparer.Default.Equals(coalescingParamRef.Parameter, parameter))
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectTestContextFieldsAssignedInConstructor(
        IParameterSymbol testContextParameter,
        ImmutableArray<IOperation> operations,
        ConcurrentBag<IFieldSymbol> fieldsAssignedInConstructor)
    {
        foreach (IOperation operation in operations)
        {
            CollectTestContextFieldsAssignedInConstructor(testContextParameter, operation, fieldsAssignedInConstructor);
        }
    }

    private static void CollectTestContextFieldsAssignedInConstructor(
        IParameterSymbol testContextParameter,
        IOperation operation,
        ConcurrentBag<IFieldSymbol> fieldsAssignedInConstructor)
    {
        if (operation is IBlockOperation blockOperation)
        {
            CollectTestContextFieldsAssignedInConstructor(testContextParameter, blockOperation.Operations, fieldsAssignedInConstructor);
        }
        else if (operation is IExpressionStatementOperation expressionStatementOperation)
        {
            operation = expressionStatementOperation.Operation;
        }

        if (operation is ISimpleAssignmentOperation assignmentOperation &&
            assignmentOperation.Target is IMemberReferenceOperation { Member: IFieldSymbol { } candidateField })
        {
            // Handle direct parameter assignment
            if (assignmentOperation.Value is IParameterReferenceOperation parameterReference &&
                SymbolEqualityComparer.Default.Equals(parameterReference.Parameter, testContextParameter))
            {
                fieldsAssignedInConstructor.Add(candidateField);
                return;
            }

            // Handle null-coalescing operator with parameter on left side
            // e.g., _testContext = testContext ?? throw new ArgumentNullException(nameof(testContext));
            if (assignmentOperation.Value is IBinaryOperation binaryOperation &&
                binaryOperation.OperatorKind == BinaryOperatorKind.Coalescing &&
                binaryOperation.LeftOperand is IParameterReferenceOperation coalescingParamRef &&
                SymbolEqualityComparer.Default.Equals(coalescingParamRef.Parameter, testContextParameter))
            {
                fieldsAssignedInConstructor.Add(candidateField);
                return;
            }
        }
    }

    private static IParameterSymbol? TryGetTestContextParameterIfValidConstructor(ISymbol candidate, INamedTypeSymbol testContextSymbol)
        => candidate is IMethodSymbol { MethodKind: MethodKind.Constructor, Parameters: [{ } parameter] } &&
            SymbolEqualityComparer.Default.Equals(parameter.Type, testContextSymbol)
            ? parameter
            : null;

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                context.RegisterSymbolStartAction(
                    context =>
                    {
                        if (!context.Symbol.GetAttributes().Any(attr => attr.AttributeClass.Inherits(testClassAttributeSymbol)))
                        {
                            return;
                        }

                        var namedType = (INamedTypeSymbol)context.Symbol;
                        foreach (ISymbol member in namedType.GetMembers())
                        {
                            IFieldSymbol? fieldReturnedByProperty = null;
                            ConcurrentBag<IFieldSymbol>? fieldsAssignedInConstructor = null;
                            switch (member.Kind)
                            {
                                case SymbolKind.Property:
                                case SymbolKind.Field:
                                    if (member is IPropertySymbol propertySymbol)
                                    {
                                        if (!SymbolEqualityComparer.Default.Equals(propertySymbol.Type, testContextSymbol) ||
                                            !member.Name.Equals(TestContextPropertyName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            continue;
                                        }

                                        if (propertySymbol.IsStatic)
                                        {
                                            context.RegisterSymbolEndAction(
                                                context => context.ReportDiagnostic(member.CreateDiagnostic(TestContextShouldBeValidRule)));
                                            continue;
                                        }

                                        if (IsTestContextPropertyAutomaticallyAssigned(propertySymbol, testContextSymbol))
                                        {
                                            return;
                                        }

                                        fieldsAssignedInConstructor = new();

                                        context.RegisterOperationBlockAction(context =>
                                        {
                                            if (context.OwningSymbol.Equals(propertySymbol.GetMethod, SymbolEqualityComparer.Default))
                                            {
                                                fieldReturnedByProperty = TryGetReturnedField(context.OperationBlocks);
                                            }
                                            else if (TryGetTestContextParameterIfValidConstructor(context.OwningSymbol, testContextSymbol) is { } parameter)
                                            {
                                                CollectTestContextFieldsAssignedInConstructor(parameter, context.OperationBlocks, fieldsAssignedInConstructor);
                                            }
                                        });
                                    }
                                    else if (member is IFieldSymbol fieldSymbol)
                                    {
                                        // AssociatedSymbol check is to not analyze compiler-generated backing field.
                                        if (fieldSymbol.AssociatedSymbol is not null ||
                                            // Workaround https://github.com/dotnet/roslyn/issues/70208
                                            // https://github.com/dotnet/roslyn/blob/05e49aa98995349ffa26a19020333293ffe99670/src/Compilers/CSharp/Portable/Symbols/Synthesized/GeneratedNameKind.cs#L47
                                            (fieldSymbol.Name.StartsWith('<') && fieldSymbol.Name.EndsWith(">P", StringComparison.Ordinal)) ||
                                            !SymbolEqualityComparer.Default.Equals(fieldSymbol.Type, testContextSymbol))
                                        {
                                            continue;
                                        }

                                        // For fields, we check the type but not the name to allow analyzing different conventions.
                                        // The field could be named _testContext, testContext, or s_testContext. So we want to analyze all these.
                                        if (fieldSymbol.IsStatic)
                                        {
                                            context.RegisterSymbolEndAction(
                                                context => context.ReportDiagnostic(member.CreateDiagnostic(TestContextShouldBeValidRule)));
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        throw ApplicationStateGuard.Unreachable();
                                    }

                                    // Initially, we consider the field/property as not assigned in the constructor.
                                    // Then, we look for a constructor with a single TestContext parameter and look for assignment
                                    // in the constructor. We simply iterate over the operation blocks (no DFA involved for now).
                                    bool isAssigned = false;

                                    context.RegisterOperationBlockAction(
                                        context =>
                                        {
                                            if (TryGetTestContextParameterIfValidConstructor(context.OwningSymbol, testContextSymbol) is not { } parameter)
                                            {
                                                return;
                                            }

                                            if (AssignsParameterToMember(parameter, member, context.OperationBlocks))
                                            {
                                                isAssigned = true;
                                            }
                                        });

                                    context.RegisterSymbolEndAction(
                                        context =>
                                        {
                                            if (!isAssigned)
                                            {
                                                isAssigned = fieldReturnedByProperty is not null &&
                                                    fieldsAssignedInConstructor?.Contains(fieldReturnedByProperty, SymbolEqualityComparer.Default) == true;
                                            }

                                            if (!isAssigned)
                                            {
                                                context.ReportDiagnostic(member.CreateDiagnostic(TestContextShouldBeValidRule));
                                            }
                                        });

                                    break;
                            }
                        }
                    }, SymbolKind.NamedType);
            }
        });
    }

    private static bool IsTestContextPropertyAutomaticallyAssigned(IPropertySymbol property, INamedTypeSymbol testContextSymbol) =>
        // See TypeCache.ResolveTestContext

        // For the property to be auto assigned, the name must be exactly TestContext (case sensitive)
        property.Name.Equals(TestContextPropertyName, StringComparison.Ordinal) &&
            // The TestContext property must be public regardless of the presence of DiscoverInternals attribute.
            property.DeclaredAccessibility == Accessibility.Public &&
            // A setter must exist, even if private.
            property.SetMethod is not null &&
            // The TestContext property type must be TestContext
            SymbolEqualityComparer.Default.Equals(property.Type, testContextSymbol);
}
