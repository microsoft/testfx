// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0084: <inheritdoc cref="Resources.OSPlatformAttributesShouldBeConsistentTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class OSPlatformAttributesShouldBeConsistentAnalyzer : DiagnosticAnalyzer
{
    internal const string ConditionModeKey = nameof(ConditionModeKey);
    internal const string OperatingSystemsKey = nameof(OperatingSystemsKey);

    private static readonly LocalizableResourceString Title = new(nameof(Resources.OSPlatformAttributesShouldBeConsistentTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.OSPlatformAttributesShouldBeConsistentMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.OSPlatformAttributesShouldBeConsistentDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.OSPlatformAttributesShouldBeConsistentRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOSConditionAttribute, out INamedTypeSymbol? osConditionAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeVersioningSupportedOSPlatformAttribute, out INamedTypeSymbol? supportedOSPlatformAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeVersioningUnsupportedOSPlatformAttribute, out INamedTypeSymbol? unsupportedOSPlatformAttributeSymbol))
            {
                return;
            }

            context.RegisterSymbolAction(
                ctx => AnalyzeSymbol(ctx, testMethodAttributeSymbol, osConditionAttributeSymbol, supportedOSPlatformAttributeSymbol, unsupportedOSPlatformAttributeSymbol),
                SymbolKind.Method);
            context.RegisterSymbolAction(
                ctx => AnalyzeSymbol(ctx, testClassAttributeSymbol, osConditionAttributeSymbol, supportedOSPlatformAttributeSymbol, unsupportedOSPlatformAttributeSymbol),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol testAttributeSymbol,
        INamedTypeSymbol osConditionAttributeSymbol,
        INamedTypeSymbol supportedOSPlatformAttributeSymbol,
        INamedTypeSymbol unsupportedOSPlatformAttributeSymbol)
    {
        ImmutableArray<AttributeData> attributes = context.Symbol.GetAttributes();
        if (!attributes.Any(attribute => attribute.AttributeClass.Inherits(testAttributeSymbol)))
        {
            return;
        }

        var platformAttributes = attributes
            .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, supportedOSPlatformAttributeSymbol)
                || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, unsupportedOSPlatformAttributeSymbol))
            .ToImmutableArray();
        if (platformAttributes.IsEmpty)
        {
            return;
        }

        AttributeData? osConditionAttribute = attributes.FirstOrDefault(
            attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, osConditionAttributeSymbol));

        bool canFix = TryGetExpectedCondition(
            platformAttributes,
            supportedOSPlatformAttributeSymbol,
            out bool includeMode,
            out int operatingSystems,
            out string? operatingSystemsExpression);

        if (canFix && IsEquivalentOSCondition(osConditionAttribute, includeMode, operatingSystems))
        {
            return;
        }

        ImmutableDictionary<string, string?> properties = canFix
            ? ImmutableDictionary<string, string?>.Empty
                .Add(ConditionModeKey, includeMode ? "Include" : "Exclude")
                .Add(OperatingSystemsKey, operatingSystemsExpression)
            : ImmutableDictionary<string, string?>.Empty;

        AttributeData diagnosticAttribute = platformAttributes[0];
        if (diagnosticAttribute.ApplicationSyntaxReference is { } syntaxReference)
        {
            context.ReportDiagnostic(syntaxReference.GetSyntax(context.CancellationToken).CreateDiagnostic(Rule, properties, context.Symbol.Name));
        }
        else
        {
            context.ReportDiagnostic(context.Symbol.CreateDiagnostic(Rule, properties, context.Symbol.Name));
        }
    }

    private static bool TryGetExpectedCondition(
        ImmutableArray<AttributeData> platformAttributes,
        INamedTypeSymbol supportedOSPlatformAttributeSymbol,
        out bool includeMode,
        out int operatingSystems,
        out string? operatingSystemsExpression)
    {
        includeMode = SymbolEqualityComparer.Default.Equals(platformAttributes[0].AttributeClass, supportedOSPlatformAttributeSymbol);
        operatingSystems = 0;

        foreach (AttributeData attribute in platformAttributes)
        {
            if (includeMode != SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, supportedOSPlatformAttributeSymbol)
                || attribute.ConstructorArguments is not [{ Value: string platformName }]
                || !TryMapPlatform(platformName, out int operatingSystem))
            {
                operatingSystemsExpression = null;
                return false;
            }

            operatingSystems |= operatingSystem;
        }

        operatingSystemsExpression = CreateOperatingSystemsExpression(operatingSystems);
        return true;
    }

    private static bool TryMapPlatform(string platformName, out int operatingSystem)
    {
        operatingSystem = platformName.ToUpperInvariant() switch
        {
            "LINUX" => 1 << 0,
            "OSX" or "MACOS" => 1 << 1,
            "WINDOWS" => 1 << 2,
            "FREEBSD" => 1 << 3,
            _ => 0,
        };

        return operatingSystem != 0;
    }

    private static string CreateOperatingSystemsExpression(int operatingSystems)
    {
        var names = new List<string>();
        AddNameIfSet(names, operatingSystems, 1 << 0, "Linux");
        AddNameIfSet(names, operatingSystems, 1 << 1, "OSX");
        AddNameIfSet(names, operatingSystems, 1 << 2, "Windows");
        AddNameIfSet(names, operatingSystems, 1 << 3, "FreeBSD");
        return string.Join("|", names);
    }

    private static void AddNameIfSet(List<string> names, int operatingSystems, int value, string name)
    {
        if ((operatingSystems & value) != 0)
        {
            names.Add(name);
        }
    }

    private static bool IsEquivalentOSCondition(AttributeData? attribute, bool includeMode, int operatingSystems)
    {
        if (attribute is null)
        {
            return false;
        }

        ImmutableArray<TypedConstant> arguments = attribute.ConstructorArguments;
        return arguments switch
        {
            [{ Value: int actualOperatingSystems }]
                => includeMode && actualOperatingSystems == operatingSystems,
            [{ Value: int actualMode }, { Value: int actualOperatingSystems }]
                => (actualMode == 0) == includeMode && actualOperatingSystems == operatingSystems,
            _ => false,
        };
    }
}
