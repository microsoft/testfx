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
/// MSTEST0089: <inheritdoc cref="Resources.ConflictingDataRowDisplayNamesTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class ConflictingDataRowDisplayNamesAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.ConflictingDataRowDisplayNamesRuleId,
        new LocalizableResourceString(nameof(Resources.ConflictingDataRowDisplayNamesTitle), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.ConflictingDataRowDisplayNamesMessageFormat), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.ConflictingDataRowDisplayNamesDescription), Resources.ResourceManager, typeof(Resources)),
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            INamedTypeSymbol? dataRowAttribute = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataRowAttribute);
            INamedTypeSymbol? testDataRow = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestDataRow1);
            if (dataRowAttribute is not null || testDataRow is not null)
            {
                context.RegisterOperationAction(context => AnalyzeCreation(context, dataRowAttribute, testDataRow), OperationKind.ObjectCreation);
            }
        });
    }

    private static void AnalyzeCreation(OperationAnalysisContext context, INamedTypeSymbol? dataRowAttribute, INamedTypeSymbol? testDataRow)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        bool isDataRowAttribute = dataRowAttribute is not null && dataRowAttribute.Equals(creation.Type, SymbolEqualityComparer.Default);
        bool isTestDataRow = testDataRow is not null && testDataRow.Equals(creation.Type?.OriginalDefinition, SymbolEqualityComparer.Default);
        if ((!isDataRowAttribute && !isTestDataRow) || creation.Initializer is null)
        {
            return;
        }

        ISimpleAssignmentOperation? displayName = null;
        ISimpleAssignmentOperation? argumentsDisplayName = null;
        foreach (IOperation initializer in creation.Initializer.Initializers)
        {
            if (initializer is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property } assignment)
            {
                switch (property.Property.Name)
                {
                    case "DisplayName":
                        displayName = assignment;
                        break;
                    case "ArgumentsDisplayName":
                        argumentsDisplayName = assignment;
                        break;
                }
            }
        }

        if (displayName?.Value.ConstantValue is not { HasValue: true, Value: string fullName }
            || argumentsDisplayName?.Value.ConstantValue is not { HasValue: true, Value: string argumentName }
            || string.IsNullOrWhiteSpace(argumentName)
            || (isDataRowAttribute && string.IsNullOrWhiteSpace(fullName)))
        {
            return;
        }

        context.ReportDiagnostic(creation.CreateDiagnostic(Rule));
    }
}
