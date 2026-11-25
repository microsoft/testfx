// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0093: <inheritdoc cref="Resources.TestAttributeValuesShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestAttributeValuesShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestAttributeValuesShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestAttributeValuesShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly DiagnosticDescriptor PositiveRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestAttributeValuesShouldBeValidRuleId,
        Title,
        new LocalizableResourceString(nameof(Resources.TestAttributeValuesShouldBeValidMessageFormat_Positive), Resources.ResourceManager, typeof(Resources)),
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DelayRule = PositiveRule.WithMessage(
        new(nameof(Resources.TestAttributeValuesShouldBeValidMessageFormat_Delay), Resources.ResourceManager, typeof(Resources)));

    private static readonly DiagnosticDescriptor BackoffRule = PositiveRule.WithMessage(
        new(nameof(Resources.TestAttributeValuesShouldBeValidMessageFormat_Backoff), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = [PositiveRule, DelayRule, BackoffRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? timeout = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTimeoutAttribute);
            INamedTypeSymbol? retry = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingRetryAttribute);
            INamedTypeSymbol? duration = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingExpectedDurationAttribute);
            INamedTypeSymbol? testMethod = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
            INamedTypeSymbol? testClass = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute);
            if (timeout is null && retry is null && duration is null)
            {
                return;
            }

            ImmutableHashSet<INamedTypeSymbol> fixtures = ParallelSafetyHelper.GetFixtureAttributeSymbols(compilation);
            INamedTypeSymbol? assemblyInitialize = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute);
            INamedTypeSymbol? assemblyCleanup = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute);
            context.RegisterSymbolAction(
                context => AnalyzeSymbol(context, timeout, retry, duration, testMethod, testClass, fixtures, assemblyInitialize, assemblyCleanup),
                SymbolKind.Method,
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol? timeout,
        INamedTypeSymbol? retry,
        INamedTypeSymbol? duration,
        INamedTypeSymbol? testMethod,
        INamedTypeSymbol? testClass,
        ImmutableHashSet<INamedTypeSymbol> fixtures,
        INamedTypeSymbol? assemblyInitialize,
        INamedTypeSymbol? assemblyCleanup)
    {
        ImmutableArray<AttributeData> attributes = context.Symbol.GetAttributes();
        bool isTestMethod = context.Symbol is IMethodSymbol && testMethod is not null
            && attributes.Any(attribute => attribute.AttributeClass.Inherits(testMethod));
        bool isTestClass = context.Symbol is INamedTypeSymbol && testClass is not null
            && attributes.Any(attribute => attribute.AttributeClass.Inherits(testClass));
        bool isFixture = context.Symbol is IMethodSymbol && attributes.Any(attribute =>
            attribute.AttributeClass is { } type
            && (fixtures.Contains(type)
                || SymbolEqualityComparer.Default.Equals(type, assemblyInitialize)
                || SymbolEqualityComparer.Default.Equals(type, assemblyCleanup)));

        foreach (AttributeData attribute in attributes)
        {
            bool isTimeout = timeout is not null && (isTestMethod || isFixture) && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, timeout);
            bool isRetry = retry is not null && (isTestMethod || isTestClass) && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, retry);
            bool isDuration = duration is not null && isTestMethod && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, duration);
            if ((!isTimeout && !isRetry && !isDuration)
                || attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not { } syntax)
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is int value
                && value <= 0
                && attribute.AttributeConstructor?.Parameters is [{ Name: var parameterName }])
            {
                context.ReportDiagnostic(syntax.CreateDiagnostic(PositiveRule, attribute.AttributeClass!.Name, parameterName));
            }

            if (isRetry)
            {
                foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
                {
                    if (argument.Key == "MillisecondsDelayBetweenRetries" && argument.Value.Value is int delay && delay < -1)
                    {
                        context.ReportDiagnostic(syntax.CreateDiagnostic(DelayRule));
                    }
                    else if (argument.Key == "BackoffType"
                        && argument.Value.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
                        && argument.Value.Value is int backoff
                        && !enumType.GetMembers().OfType<IFieldSymbol>().Any(field => field.HasConstantValue && Equals(field.ConstantValue, backoff)))
                    {
                        context.ReportDiagnostic(syntax.CreateDiagnostic(BackoffRule));
                    }
                }
            }
        }
    }
}
