﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0036: <inheritdoc cref="Resources.DoNotUseShadowingTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DoNotUseShadowingAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DoNotUseShadowingTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.DoNotUseShadowingDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.DoNotUseShadowingMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor DoNotUseShadowingRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DoNotUseShadowingRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(DoNotUseShadowingRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.TypeKind != TypeKind.Class
            || !namedTypeSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testClassAttributeSymbol)))
        {
            return;
        }

        Dictionary<string, List<ISymbol>> membersByName = GetBaseMembers(namedTypeSymbol);
        foreach (ISymbol member in namedTypeSymbol.GetMembers())
        {
            foreach (ISymbol baseMember in membersByName.GetValueOrDefault(member.Name, new List<ISymbol>()))
            {
                // Check if the member is shadowing a base class member
                if (IsMemberShadowing(member, baseMember))
                {
                    context.ReportDiagnostic(member.CreateDiagnostic(DoNotUseShadowingRule, member.Name));
                }
            }
        }
    }

    private static Dictionary<string, List<ISymbol>> GetBaseMembers(INamedTypeSymbol namedTypeSymbol)
    {
        Dictionary<string, List<ISymbol>> membersByName = new();
        INamedTypeSymbol? currentType = namedTypeSymbol.BaseType;
        while (currentType is not null)
        {
            foreach (ISymbol member in currentType.GetMembers())
            {
                if ((member is IMethodSymbol methodSymbol && methodSymbol.MethodKind != MethodKind.Ordinary)
                    || member.IsOverride)
                {
                    continue;
                }

                if (!membersByName.TryGetValue(member.Name, out List<ISymbol>? members))
                {
                    members = new List<ISymbol>();
                    membersByName[member.Name] = members;
                }

                // Add the member to the list
                members.Add(member);
            }

            currentType = currentType.BaseType;
        }

        return membersByName;
    }

    private static bool IsMemberShadowing(ISymbol member, ISymbol baseMember)
    {
        // Ensure both members are of the same kind (Method, Property, etc.)
        if (member.Kind != baseMember.Kind || member.IsOverride)
        {
            return false;
        }

        // Compare methods
        if (member is IMethodSymbol methodSymbol && baseMember is IMethodSymbol baseMethodSymbol && methodSymbol.IsGenericMethod == baseMethodSymbol.IsGenericMethod
            && !(methodSymbol.DeclaredAccessibility == Accessibility.Private && baseMethodSymbol.DeclaredAccessibility == Accessibility.Private))
        {
            return methodSymbol.Name == baseMethodSymbol.Name &&
                   methodSymbol.Parameters.Length == baseMethodSymbol.Parameters.Length &&
                   methodSymbol.Parameters.Zip(baseMethodSymbol.Parameters, (p1, p2) =>
                   SymbolEqualityComparer.Default.Equals(p1.Type, p2.Type)).All(equal => equal);
        }

        // Compare properties
        else if (member is IPropertySymbol propertySymbol && baseMember is IPropertySymbol basePropertySymbol)
        {
            return propertySymbol.Name == basePropertySymbol.Name &&
                   SymbolEqualityComparer.Default.Equals(propertySymbol.Type, basePropertySymbol.Type);
        }

        return false;
    }
}
