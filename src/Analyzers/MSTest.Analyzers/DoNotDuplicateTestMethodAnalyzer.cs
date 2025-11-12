// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;

using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0033: <inheritdoc cref="Resources.DoNotDuplicateTestMethodTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DoNotDuplicateTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DoNotDuplicateTestMethodTitle), Resources.ResourceManager, typeof(Resources));
    // private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DoNotDuplicateTestMethodDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DoNotDuplicateTestMethodMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor DoNotDuplicateTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DoNotDuplicateTestMethodRuleId,
        Title,
        MessageFormat,
        null,
        Category.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the diagnostic descriptors supported by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DoNotDuplicateTestMethodRule);

    /// <summary>
    /// Initializes the analyzer by registering actions to analyze named type symbols for duplicate test methods.
    /// </summary>
    /// <param name="context">The analysis context to register actions with.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            if (compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol)
                && compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                compilationContext.RegisterSymbolAction(
                    context => AnalyzeNamedTypeSymbol(context, testClassAttributeSymbol, testMethodAttributeSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeNamedTypeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol testMethodAttributeSymbol)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        // Check if the type is a test class
        if (!namedType.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testClassAttributeSymbol)))
        {
            return;
        }

        // Get all test methods in the class (including inherited ones)
        var testMethods = namedType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => IsTestMethod(method, testMethodAttributeSymbol))
            .ToList();

        // Check for duplicate method names within the same class
        var duplicatesByName = testMethods
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (IGrouping<string, IMethodSymbol> duplicateGroup in duplicatesByName)
        {
            foreach (IMethodSymbol method in duplicateGroup.Skip(1)) // Report all but the first occurrence
            {
                IMethodSymbol firstMethod = duplicateGroup.First();
                Location? location = method.Locations.FirstOrDefault();

                if (location != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DoNotDuplicateTestMethodRule,
                        location,
                        method.Name,
                        namedType.Name));
                }
            }
        }

        // Check for duplicate method signatures (overloads)
        var duplicatesBySignature = testMethods
            .GroupBy(GetMethodSignature, MethodSignatureComparer.Instance)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (IGrouping<MethodSignature, IMethodSymbol> duplicateGroup in duplicatesBySignature)
        {
            // Only report if we haven't already reported by name
            // (to avoid duplicate diagnostics)
            if (!duplicatesByName.Any(ng => ng.Key == duplicateGroup.Key.Name))
            {
                foreach (IMethodSymbol method in duplicateGroup.Skip(1))
                {
                    Location? location = method.Locations.FirstOrDefault();

                    if (location != null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DoNotDuplicateTestMethodRule,
                            location,
                            method.Name,
                            namedType.Name));
                    }
                }
            }
        }
    }

    private static bool IsTestMethod(IMethodSymbol method, INamedTypeSymbol testMethodAttributeSymbol) =>
     // Check if the method has [TestMethod] or a derived attribute
     method.GetAttributes().Any(attr =>
     {
         if (attr.AttributeClass == null)
         {
             return false;
         }

         // Check if it's TestMethodAttribute or derives from it
         INamedTypeSymbol? currentType = attr.AttributeClass;
         while (currentType != null)
         {
             if (SymbolEqualityComparer.Default.Equals(currentType, testMethodAttributeSymbol))
             {
                 return true;
             }

             currentType = currentType.BaseType;
         }

         return false;
     });

    private static MethodSignature GetMethodSignature(IMethodSymbol method) =>
        new(
            method.Name,
            method.Parameters.Select(p => p.Type).ToImmutableArray(),
            method.TypeParameters.Length);

    private sealed class MethodSignature
    {
        public string Name { get; }

        public ImmutableArray<ITypeSymbol> ParameterTypes { get; }

        public int TypeParameterCount { get; }

        public MethodSignature(string name, ImmutableArray<ITypeSymbol> parameterTypes, int typeParameterCount)
        {
            Name = name;
            ParameterTypes = parameterTypes;
            TypeParameterCount = typeParameterCount;
        }
    }

    private sealed class MethodSignatureComparer : IEqualityComparer<MethodSignature>
    {
        public static readonly MethodSignatureComparer Instance = new();

        public bool Equals(MethodSignature? x, MethodSignature? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (x.Name != y.Name || x.TypeParameterCount != y.TypeParameterCount)
            {
                return false;
            }

            if (x.ParameterTypes.Length != y.ParameterTypes.Length)
            {
                return false;
            }

            for (int i = 0; i < x.ParameterTypes.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(x.ParameterTypes[i], y.ParameterTypes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(MethodSignature obj)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (obj.Name?.GetHashCode() ?? 0);
                hash = (hash * 31) + obj.TypeParameterCount.GetHashCode();
                hash = (hash * 31) + obj.ParameterTypes.Length.GetHashCode();
                return hash;
            }
        }
    }
}
