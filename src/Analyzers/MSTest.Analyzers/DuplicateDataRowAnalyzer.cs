// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Testing.Platform;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0042: <inheritdoc cref="Resources.DuplicateDataRowTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DuplicateDataRowAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DuplicateDataRowRuleId,
        new LocalizableResourceString(nameof(Resources.DuplicateDataRowTitle), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.DuplicateDataRowMessageFormat), Resources.ResourceManager, typeof(Resources)),
        null,
        Category.Usage,
        DiagnosticSeverity.Warning,
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
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataRowAttribute, out INamedTypeSymbol? dataRowAttribute))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, dataRowAttribute),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol dataRowAttribute)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var dataRowArguments = new Dictionary<ImmutableArray<TypedConstant>, int>(TypedConstantArrayComparer.Instance);

        ImmutableArray<AttributeData> attributes = methodSymbol.GetAttributes();
        for (int i = 0; i < attributes.Length; i++)
        {
            AttributeData attribute = attributes[i];
            if (!dataRowAttribute.Equals(attribute.AttributeClass, SymbolEqualityComparer.Default))
            {
                continue;
            }

            if (dataRowArguments.TryGetValue(attribute.ConstructorArguments, out int existingIndex) &&
                attribute.ApplicationSyntaxReference is not null)
            {
                context.ReportDiagnostic(attribute.ApplicationSyntaxReference.CreateDiagnostic(Rule, context.CancellationToken, existingIndex, i));
                continue;
            }

            dataRowArguments[attribute.ConstructorArguments] = i;
        }
    }

    private sealed class TypedConstantArrayComparer : IEqualityComparer<ImmutableArray<TypedConstant>>
    {
        public static TypedConstantArrayComparer Instance { get; } = new();

        public bool Equals(ImmutableArray<TypedConstant> x, ImmutableArray<TypedConstant> y)
        {
            if (x.Length != y.Length)
            {
                return false;
            }

            for (int i = 0; i < x.Length; i++)
            {
                if (!AreTypedConstantEquals(x[i], y[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreTypedConstantEquals(TypedConstant typedConstant1, TypedConstant typedConstant2)
        {
            // If the Kind doesn't match or the Type doesn't match, they are not equal.
            if (typedConstant1.Kind != typedConstant2.Kind ||
                !SymbolEqualityComparer.Default.Equals(typedConstant1.Type, typedConstant2.Type))
            {
                return false;
            }

            // If IsNull is true and the Kind is array, Values will return default(ImmutableArray<TypedConstant>), not empty.
            // To avoid dealing with that, we do the quick IsNull checks first.
            // If both are nulls, then we are good, everything is equal.
            if (typedConstant1.IsNull && typedConstant2.IsNull)
            {
                return true;
            }

            // If only one is null, then we are not equal.
            if (typedConstant1.IsNull || typedConstant2.IsNull)
            {
                return false;
            }

            // If the kind is array (at this point we know both have the same Kind), we compare Values.
            // Accessing `Value` property for arrays will throw so we need to have explicit check to decide whether
            // we compare `Value` or `Values`.
            if (typedConstant1.Kind == TypedConstantKind.Array)
            {
                return TypedConstantArrayComparer.Instance.Equals(typedConstant1.Values, typedConstant2.Values);
            }

            // At this point, the type is matching and the kind is matching and is not array.
            return object.Equals(typedConstant1.Value, typedConstant2.Value);
        }

        public int GetHashCode(ImmutableArray<TypedConstant> obj)
        {
            var hashCode = default(RoslynHashCode);
            foreach (TypedConstant typedConstant in obj)
            {
                hashCode.Add(typedConstant.Kind);
                hashCode.Add(SymbolEqualityComparer.Default.GetHashCode(typedConstant.Type));
            }

            return hashCode.ToHashCode();
        }
    }
}
