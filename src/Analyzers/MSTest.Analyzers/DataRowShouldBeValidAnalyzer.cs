﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;
using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DataRowShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DataRowShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.DataRowShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.DataRowShouldBeValidMessageFormat_OnTestMethod), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor DataRowOnTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DataRowShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor AtLeastOneArgumentRule = DataRowOnTestMethodRule
        .WithMessage(new(nameof(Resources.DataRowShouldBeValidMessageFormat_AtLeastOneArgument), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor ArgumentNumberRule = DataRowOnTestMethodRule
        .WithMessage(new(nameof(Resources.DataRowShouldBeValidMessageFormat_ArgumentNumber), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor ArgumentTypeRule = DataRowOnTestMethodRule
        .WithMessage(new(nameof(Resources.DataRowShouldBeValidMessageFormat_ArgumentType), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
       = ImmutableArray.Create(DataRowOnTestMethodRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            // No need to register any action if we don't find the TestMethodAttribute symbol since
            // the current analyzer checks if the DataRow attribute is applied on test methods. No
            // test methods, nothing to check.
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute,
                out var testMethodAttributeSymbol))
            {
                return;
            }

            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataRowAttribute,
                out var dataRowAttributeSymbol))
            {
                return;
            }

            context.RegisterSymbolAction(
                context => AnalyzeSymbol(context, testMethodAttributeSymbol, dataRowAttributeSymbol),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol dataRowAttributeSymbol)
    {
        IMethodSymbol methodSymbol = (IMethodSymbol)context.Symbol;

        bool isTestMethod = false;
        List<AttributeData> dataRowAttributes = new();
        foreach (var methodAttribute in methodSymbol.GetAttributes())
        {
            // Current method should be a test method or should inherit from the TestMethod attribute.
            // If it is, the current analyzer will trigger no diagnostic so it exits.
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                isTestMethod = true;
            }

            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, dataRowAttributeSymbol))
            {
                dataRowAttributes.Add(methodAttribute);
            }
        }

        // Check if attribute is set on a test method.
        if (!isTestMethod)
        {
            if (dataRowAttributes.Count > 0)
            {
                context.ReportDiagnostic(methodSymbol.CreateDiagnostic(DataRowOnTestMethodRule));
            }

            return;
        }

        // Check each data row attribute.
        foreach (var attribute in dataRowAttributes)
        {
            AnalyzeAttribute(context, attribute, methodSymbol);
        }
    }

    private static void AnalyzeAttribute(SymbolAnalysisContext context, AttributeData attribute, IMethodSymbol methodSymbol)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is not { } syntax)
        {
            return;
        }

        if (attribute.ConstructorArguments.Length == 0)
        {
            context.ReportDiagnostic(syntax.CreateDiagnostic(AtLeastOneArgumentRule));
            return;
        }

        // DataRow constructors have either zero or one argument(s). If we get here, we are
        // on the one argument case. Check if we match either of the array argument constructors
        // and expand the array argument if we do.
        ImmutableArray<TypedConstant> constructorArguments = attribute.ConstructorArguments;
        if (constructorArguments[0].IsNull)
        {
            return;
        }

        if (constructorArguments[0].Kind is TypedConstantKind.Array)
        {
            constructorArguments = constructorArguments[0].Values;
        }

        // 1. Diagnostic on mismatch of constructor argument count and method argument count if method
        //    doesn't accept params.
        // 2. Diagnostic on lower constructor argument count than method argument count if method
        //    accepts params. Discard params argument itself because it can contain 0 params.
        int lastMethodArgumentIndex = methodSymbol.Parameters.Length - 1;
        bool methodHasParams = methodSymbol.Parameters[lastMethodArgumentIndex].Type.Kind == SymbolKind.ArrayType;
        if ((!methodHasParams && constructorArguments.Length != methodSymbol.Parameters.Length)
            || (methodHasParams && constructorArguments.Length < methodSymbol.Parameters.Length - 1))
        {
            context.ReportDiagnostic(syntax.CreateDiagnostic(ArgumentNumberRule));
            return;
        }

        int i = 0;
        ITypeSymbol? paramsElementType = methodHasParams
            ? ((IArrayTypeSymbol)methodSymbol.Parameters[lastMethodArgumentIndex].Type).ElementType
            : null;
        foreach (TypedConstant constructorArgument in constructorArguments)
        {
            ITypeSymbol argumentType = constructorArgument.Type!;
            ITypeSymbol paramType = (i == methodSymbol.Parameters.Length - 1 && methodHasParams)
                ? paramsElementType!
                : methodSymbol.Parameters[i++].Type;
            if (!argumentType.IsAssignableTo(paramType, context.Compilation))
            {
                context.ReportDiagnostic(syntax.CreateDiagnostic(ArgumentTypeRule));
            }
        }
    }
}
