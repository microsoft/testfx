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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(TestContextShouldBeValidRule);

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

        return operation is ISimpleAssignmentOperation assignmentOperation &&
            assignmentOperation.Target is IMemberReferenceOperation targetMemberReference &&
            SymbolEqualityComparer.Default.Equals(targetMemberReference.Member, member) &&
            assignmentOperation.Value is IParameterReferenceOperation parameterReference &&
            SymbolEqualityComparer.Default.Equals(parameterReference.Parameter, parameter);
    }

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
                                    }
                                    else if (member is IFieldSymbol fieldSymbol)
                                    {
                                        // AssociatedSymbol check is to not analyze compiler-generated backing field.
                                        if (fieldSymbol.AssociatedSymbol is not null ||
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
                                            if (context.OwningSymbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor ||
                                                constructor.Parameters.Length != 1 ||
                                                !SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, testContextSymbol))
                                            {
                                                return;
                                            }

                                            if (AssignsParameterToMember(constructor.Parameters[0], member, context.OperationBlocks))
                                            {
                                                isAssigned = true;
                                            }
                                        });

                                    context.RegisterSymbolEndAction(
                                        context =>
                                        {
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
