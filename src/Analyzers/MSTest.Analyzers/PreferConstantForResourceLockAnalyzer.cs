// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0073: <inheritdoc cref="Resources.PreferConstantForResourceLockTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PreferConstantForResourceLockAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.PreferConstantForResourceLockTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PreferConstantForResourceLockMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.PreferConstantForResourceLockDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PreferConstantForResourceLockRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingResourceLockAttribute, out INamedTypeSymbol? resourceLockAttributeSymbol))
            {
                return;
            }

            context.RegisterSymbolAction(
                context => AnalyzeSymbol(context, resourceLockAttributeSymbol),
                SymbolKind.Method,
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol resourceLockAttributeSymbol)
    {
        foreach (AttributeData attribute in context.Symbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, resourceLockAttributeSymbol))
            {
                continue;
            }

            AnalyzeAttribute(context, attribute);
        }
    }

    private static void AnalyzeAttribute(SymbolAnalysisContext context, AttributeData attribute)
    {
        // The only 'string'-typed argument of '[ResourceLock]' is the 'resource' key (the 'Mode'
        // named argument is an enum). So the presence of a string-literal token anywhere in the
        // attribute application means the resource key was written as a bare literal (or a literal
        // concatenation) rather than as a reference to a shared constant.
        if (attribute.ConstructorArguments.Length == 0
            || attribute.ConstructorArguments[0].Value is not string resourceKey)
        {
            return;
        }

        if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not { } attributeSyntax)
        {
            return;
        }

        // A string-literal token always contains a double-quote character, while an identifier or
        // member-access (i.e. a 'const' reference) never does. This distinction is language-neutral
        // and avoids creating a semantic model (RS1030) or depending on C#/VB-specific syntax nodes.
        foreach (SyntaxToken token in attributeSyntax.DescendantTokens())
        {
            if (token.Text.IndexOf('"') >= 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, token.GetLocation(), resourceKey));
                return;
            }
        }
    }
}
