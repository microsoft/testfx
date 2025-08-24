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
/// MSTEST0053: <inheritdoc cref="Resources.UseCancellationTokenPropertyTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseCancellationTokenPropertyAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseCancellationTokenPropertyTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseCancellationTokenPropertyDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseCancellationTokenPropertyMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor UseCancellationTokenPropertyRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseCancellationTokenPropertyRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Performance,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(UseCancellationTokenPropertyRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            // Get the required symbols
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationTokenSource, out INamedTypeSymbol? cancellationTokenSourceSymbol))
            {
                return;
            }

            context.RegisterOperationAction(
                context => AnalyzePropertyReference(context, testContextSymbol, cancellationTokenSourceSymbol),
                OperationKind.PropertyReference);
        });
    }

    private static void AnalyzePropertyReference(
        OperationAnalysisContext context,
        INamedTypeSymbol testContextSymbol,
        INamedTypeSymbol cancellationTokenSourceSymbol)
    {
        var propertyReferenceOperation = (IPropertyReferenceOperation)context.Operation;
        
        // Check if this is accessing the Token property on CancellationTokenSource
        if (propertyReferenceOperation.Property.Name != "Token" ||
            !SymbolEqualityComparer.Default.Equals(propertyReferenceOperation.Property.ContainingType, cancellationTokenSourceSymbol))
        {
            return;
        }

        // Check if the instance being accessed is the CancellationTokenSource property of TestContext
        if (propertyReferenceOperation.Instance is not IPropertyReferenceOperation parentPropertyReference ||
            parentPropertyReference.Property.Name != "CancellationTokenSource" ||
            !SymbolEqualityComparer.Default.Equals(parentPropertyReference.Property.ContainingType, testContextSymbol))
        {
            return;
        }

        // Report the diagnostic
        context.ReportDiagnostic(propertyReferenceOperation.Syntax.CreateDiagnostic(UseCancellationTokenPropertyRule));
    }
}