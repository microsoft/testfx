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

        AttributeData? localOSConditionAttribute = attributes.FirstOrDefault(
            attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, osConditionAttributeSymbol));
        AttributeData? containingClassOSConditionAttribute = context.Symbol is IMethodSymbol methodSymbol
            ? methodSymbol.ContainingType.GetAttributes().FirstOrDefault(
                attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, osConditionAttributeSymbol))
            : null;
        bool hasContainingClassCondition = containingClassOSConditionAttribute is not null;

        ImmutableArray<ImmutableArray<AttributeData>>.Builder containingTypeAttributeScopes =
            ImmutableArray.CreateBuilder<ImmutableArray<AttributeData>>();
        for (INamedTypeSymbol? containingType = context.Symbol.ContainingType;
            containingType is not null;
            containingType = containingType.ContainingType)
        {
            containingTypeAttributeScopes.Add(containingType.GetAttributes());
        }

        ImmutableArray<AttributeData> assemblyAttributes = context.Compilation.Assembly.GetAttributes();
        bool hasInheritedPlatformAttributes = containingTypeAttributeScopes.Any(scope => HasPlatformAttributes(
                scope,
                supportedOSPlatformAttributeSymbol,
                unsupportedOSPlatformAttributeSymbol))
            || HasPlatformAttributes(
                assemblyAttributes,
                supportedOSPlatformAttributeSymbol,
                unsupportedOSPlatformAttributeSymbol);
        if (platformAttributes.IsEmpty
            && (context.Symbol is IMethodSymbol || !hasInheritedPlatformAttributes))
        {
            return;
        }

        bool canFix = TryGetExpectedCondition(
            platformAttributes,
            containingTypeAttributeScopes.ToImmutable(),
            assemblyAttributes,
            supportedOSPlatformAttributeSymbol,
            unsupportedOSPlatformAttributeSymbol,
            out bool includeMode,
            out int operatingSystems,
            out string? operatingSystemsExpression);

        if (canFix && IsEquivalentOSCondition(
            localOSConditionAttribute,
            containingClassOSConditionAttribute,
            includeMode,
            operatingSystems))
        {
            return;
        }

        ImmutableDictionary<string, string?> properties = canFix && !hasContainingClassCondition
            ? ImmutableDictionary<string, string?>.Empty
                .Add(ConditionModeKey, includeMode ? "Include" : "Exclude")
                .Add(OperatingSystemsKey, operatingSystemsExpression)
            : ImmutableDictionary<string, string?>.Empty;

        if (!platformAttributes.IsEmpty
            && platformAttributes[0].ApplicationSyntaxReference is { } syntaxReference)
        {
            context.ReportDiagnostic(syntaxReference.GetSyntax(context.CancellationToken).CreateDiagnostic(Rule, properties, context.Symbol.Name));
        }
        else
        {
            context.ReportDiagnostic(context.Symbol.CreateDiagnostic(Rule, properties, context.Symbol.Name));
        }
    }

    private static bool HasPlatformAttributes(
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol supportedOSPlatformAttributeSymbol,
        INamedTypeSymbol unsupportedOSPlatformAttributeSymbol)
        => attributes.Any(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, supportedOSPlatformAttributeSymbol)
            || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, unsupportedOSPlatformAttributeSymbol));

    private static bool TryGetExpectedCondition(
        ImmutableArray<AttributeData> localPlatformAttributes,
        ImmutableArray<ImmutableArray<AttributeData>> containingTypeAttributeScopes,
        ImmutableArray<AttributeData> assemblyAttributes,
        INamedTypeSymbol supportedOSPlatformAttributeSymbol,
        INamedTypeSymbol unsupportedOSPlatformAttributeSymbol,
        out bool includeMode,
        out int operatingSystems,
        out string? operatingSystemsExpression)
    {
        const int allOperatingSystems = (1 << 4) - 1;
        if (!TryGetAllowedOperatingSystems(
                localPlatformAttributes,
                supportedOSPlatformAttributeSymbol,
                unsupportedOSPlatformAttributeSymbol,
                out int localAllowedOperatingSystems,
                out bool localAllowsUnknownOperatingSystems)
            || !TryGetAllowedOperatingSystems(
                assemblyAttributes,
                supportedOSPlatformAttributeSymbol,
                unsupportedOSPlatformAttributeSymbol,
                out int assemblyAllowedOperatingSystems,
                out bool assemblyAllowsUnknownOperatingSystems))
        {
            includeMode = false;
            operatingSystems = 0;
            operatingSystemsExpression = null;
            return false;
        }

        int allowedOperatingSystems = localAllowedOperatingSystems & assemblyAllowedOperatingSystems;
        bool allowsUnknownOperatingSystems =
            localAllowsUnknownOperatingSystems
            && assemblyAllowsUnknownOperatingSystems;
        foreach (ImmutableArray<AttributeData> containingTypeAttributes in containingTypeAttributeScopes)
        {
            if (!TryGetAllowedOperatingSystems(
                    containingTypeAttributes,
                    supportedOSPlatformAttributeSymbol,
                    unsupportedOSPlatformAttributeSymbol,
                    out int containingTypeAllowedOperatingSystems,
                    out bool containingTypeAllowsUnknownOperatingSystems))
            {
                includeMode = false;
                operatingSystems = 0;
                operatingSystemsExpression = null;
                return false;
            }

            allowedOperatingSystems &= containingTypeAllowedOperatingSystems;
            allowsUnknownOperatingSystems &= containingTypeAllowsUnknownOperatingSystems;
        }

        includeMode = !allowsUnknownOperatingSystems;
        operatingSystems = includeMode
            ? allowedOperatingSystems
            : allOperatingSystems & ~allowedOperatingSystems;
        if (operatingSystems == 0)
        {
            operatingSystemsExpression = null;
            return false;
        }

        operatingSystemsExpression = CreateOperatingSystemsExpression(operatingSystems);
        return true;
    }

    private static bool TryGetAllowedOperatingSystems(
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol supportedOSPlatformAttributeSymbol,
        INamedTypeSymbol unsupportedOSPlatformAttributeSymbol,
        out int allowedOperatingSystems,
        out bool allowsUnknownOperatingSystems)
    {
        const int allOperatingSystems = (1 << 4) - 1;
        var platformAttributes = attributes
            .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, supportedOSPlatformAttributeSymbol)
                || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, unsupportedOSPlatformAttributeSymbol))
            .ToImmutableArray();
        if (platformAttributes.IsEmpty)
        {
            allowedOperatingSystems = allOperatingSystems;
            allowsUnknownOperatingSystems = true;
            return true;
        }

        bool includeMode = SymbolEqualityComparer.Default.Equals(
            platformAttributes[0].AttributeClass,
            supportedOSPlatformAttributeSymbol);
        int operatingSystems = 0;
        foreach (AttributeData attribute in platformAttributes)
        {
            bool isSupportedAttribute = SymbolEqualityComparer.Default.Equals(
                attribute.AttributeClass,
                supportedOSPlatformAttributeSymbol);
            if (includeMode != isSupportedAttribute
                || !TryGetPlatformName(attribute, isSupportedAttribute, out string? platformName)
                || !TryMapPlatform(platformName, out int operatingSystem))
            {
                allowedOperatingSystems = 0;
                allowsUnknownOperatingSystems = false;
                return false;
            }

            operatingSystems |= operatingSystem;
        }

        allowedOperatingSystems = includeMode
            ? operatingSystems
            : allOperatingSystems & ~operatingSystems;
        allowsUnknownOperatingSystems = !includeMode;
        return true;
    }

    private static bool TryGetPlatformName(
        AttributeData attribute,
        bool isSupportedAttribute,
        [NotNullWhen(true)] out string? platformName)
    {
        platformName = attribute.ConstructorArguments switch
        {
            [{ Value: string value }] => value,
            [{ Value: string value }, _] when !isSupportedAttribute => value,
            _ => null,
        };

        return platformName is not null;
    }

    private static bool TryMapPlatform(string platformName, out int operatingSystem)
    {
        operatingSystem = platformName.ToUpperInvariant() switch
        {
            // Bit positions must match the OperatingSystems enum in Microsoft.VisualStudio.TestTools.UnitTesting.
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

    private static bool IsEquivalentOSCondition(
        AttributeData? localAttribute,
        AttributeData? containingClassAttribute,
        bool includeMode,
        int operatingSystems)
    {
        const int allOperatingSystems = (1 << 4) - 1;
        int expectedAllowedOperatingSystems = includeMode
            ? operatingSystems
            : allOperatingSystems & ~operatingSystems;
        bool expectedAllowsUnknownOperatingSystems = !includeMode;

        if (!TryGetAllowedOperatingSystems(
                localAttribute,
                out int localAllowedOperatingSystems,
                out bool localAllowsUnknownOperatingSystems)
            || !TryGetAllowedOperatingSystems(
                containingClassAttribute,
                out int containingClassAllowedOperatingSystems,
                out bool containingClassAllowsUnknownOperatingSystems))
        {
            return false;
        }

        int actualAllowedOperatingSystems = localAllowedOperatingSystems & containingClassAllowedOperatingSystems;
        bool actualAllowsUnknownOperatingSystems =
            localAllowsUnknownOperatingSystems && containingClassAllowsUnknownOperatingSystems;
        return actualAllowedOperatingSystems == expectedAllowedOperatingSystems
            && actualAllowsUnknownOperatingSystems == expectedAllowsUnknownOperatingSystems;
    }

    private static bool TryGetAllowedOperatingSystems(
        AttributeData? attribute,
        out int allowedOperatingSystems,
        out bool allowsUnknownOperatingSystems)
    {
        const int allOperatingSystems = (1 << 4) - 1;
        if (attribute is null)
        {
            allowedOperatingSystems = allOperatingSystems;
            allowsUnknownOperatingSystems = true;
            return true;
        }

        ImmutableArray<TypedConstant> arguments = attribute.ConstructorArguments;
        switch (arguments)
        {
            case [{ Value: int operatingSystems }]:
                allowedOperatingSystems = operatingSystems;
                allowsUnknownOperatingSystems = false;
                return true;

            case [{ Value: int mode }, { Value: int operatingSystems }]:
                allowedOperatingSystems = mode == 0
                    ? operatingSystems
                    : allOperatingSystems & ~operatingSystems;
                allowsUnknownOperatingSystems = mode != 0;
                return true;

            default:
                allowedOperatingSystems = 0;
                allowsUnknownOperatingSystems = false;
                return false;
        }
    }
}
