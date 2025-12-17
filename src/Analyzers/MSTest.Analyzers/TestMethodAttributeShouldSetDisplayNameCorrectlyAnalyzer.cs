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
/// MSTEST0056: <inheritdoc cref="Resources.TestMethodAttributeShouldSetDisplayNameCorrectlyTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestMethodAttributeShouldSetDisplayNameCorrectlyAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor TestMethodAttributeShouldSetDisplayNameCorrectlyRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestMethodAttributeShouldSetDisplayNameCorrectlyRuleId,
        title: new LocalizableResourceString(nameof(Resources.TestMethodAttributeShouldSetDisplayNameCorrectlyTitle), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.TestMethodAttributeShouldSetDisplayNameCorrectlyMessageFormat), Resources.ResourceManager, typeof(Resources)),
        description: null,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(TestMethodAttributeShouldSetDisplayNameCorrectlyRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesCallerFilePathAttribute, out INamedTypeSymbol? callerFilePath))
            {
                return;
            }

            context.RegisterOperationAction(context => AnalyzeObjectCreation(context, testMethodAttributeSymbol, callerFilePath), OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol callerFilePathAttributeSymbol)
    {
        var operation = (IObjectCreationOperation)context.Operation;
        if (!operation.Type.DerivesFrom(testMethodAttributeSymbol))
        {
            return;
        }

        foreach (IArgumentOperation arg in operation.Arguments)
        {
            // If user passes an explicit value to optional string parameter that has CallerFilePathAttribute, we issue a diagnostic.
            // User intent is likely to pass "display name", not the file path.
            if (arg is
                {
                    ArgumentKind: ArgumentKind.Explicit,
                    Parameter: { IsOptional: true, Type.SpecialType: SpecialType.System_String }
                }

                && arg.Parameter.GetAttributes().Any(attr => callerFilePathAttributeSymbol.Equals(attr.AttributeClass, SymbolEqualityComparer.Default))
                // Skip reporting if the value is accessing DeclaringFilePath or DeclaringLineNumber property
                // from another TestMethodAttribute instance, as this is a legitimate use case.
                && !IsAccessingDeclaringProperty(arg.Value, testMethodAttributeSymbol))
            {
                context.ReportDiagnostic(arg.CreateDiagnostic(TestMethodAttributeShouldSetDisplayNameCorrectlyRule));
            }
        }
    }

    private static bool IsAccessingDeclaringProperty(IOperation? operation, INamedTypeSymbol testMethodAttributeSymbol)
    {
        if (operation is not IPropertyReferenceOperation propertyReference)
        {
            return false;
        }

        // Check if the property is DeclaringFilePath or DeclaringLineNumber
        if (propertyReference.Property.Name is not ("DeclaringFilePath" or "DeclaringLineNumber"))
        {
            return false;
        }

        // Check if the property belongs to a type that inherits from TestMethodAttribute
        return propertyReference.Property.ContainingType?.DerivesFrom(testMethodAttributeSymbol) == true;
    }
}
