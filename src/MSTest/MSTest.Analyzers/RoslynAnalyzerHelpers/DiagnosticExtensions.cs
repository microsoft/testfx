// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzer.Utilities.Extensions;

internal static class DiagnosticExtensions
{
    public static Diagnostic CreateDiagnostic(
        this SyntaxNode node,
        DiagnosticDescriptor rule,
        params object[] args)
        => node.CreateDiagnostic(rule, properties: null, args);

    public static Diagnostic CreateDiagnostic(
        this SyntaxNode node,
        DiagnosticDescriptor rule,
        ImmutableDictionary<string, string?>? properties,
        params object[] args)
        => node.CreateDiagnostic(rule, additionalLocations: [], properties, args);

    public static Diagnostic CreateDiagnostic(
        this SyntaxNode node,
        DiagnosticDescriptor rule,
        ImmutableArray<Location> additionalLocations,
        ImmutableDictionary<string, string?>? properties,
        params object[] args)
        => node
            .GetLocation()
            .CreateDiagnostic(
                rule: rule,
                additionalLocations: additionalLocations,
                properties: properties,
                args: args);

    public static Diagnostic CreateDiagnostic(
        this IOperation operation,
        DiagnosticDescriptor rule,
        params object[] args)
        => operation.CreateDiagnostic(rule, properties: null, args);

    public static Diagnostic CreateDiagnostic(
        this IOperation operation,
        DiagnosticDescriptor rule,
        ImmutableDictionary<string, string?>? properties,
        params object[] args) => operation.Syntax.CreateDiagnostic(rule, properties, args);

    public static Diagnostic CreateDiagnostic(
        this IOperation operation,
        DiagnosticDescriptor rule,
        ImmutableArray<Location> additionalLocations,
        ImmutableDictionary<string, string?>? properties,
        params object[] args) => operation.Syntax.CreateDiagnostic(rule, additionalLocations, properties, args);

    public static Diagnostic CreateDiagnostic(
        this SyntaxToken token,
        DiagnosticDescriptor rule,
        params object[] args) => token.GetLocation().CreateDiagnostic(rule, args);

    public static Diagnostic CreateDiagnostic(
        this ISymbol symbol,
        DiagnosticDescriptor rule,
        params object[] args) => symbol.Locations.CreateDiagnostic(rule, args);

    public static Diagnostic CreateDiagnostic(
        this ISymbol symbol,
        DiagnosticDescriptor rule,
        ImmutableDictionary<string, string?>? properties,
        params object[] args) => symbol.Locations.CreateDiagnostic(rule, properties, args);

    public static Diagnostic CreateDiagnostic(
        this Location location,
        DiagnosticDescriptor rule,
        params object[] args)
        => location
            .CreateDiagnostic(
                rule: rule,
                properties: ImmutableDictionary<string, string?>.Empty,
                args: args);

    public static Diagnostic CreateDiagnostic(
        this Location location,
        DiagnosticDescriptor rule,
        ImmutableDictionary<string, string?>? properties,
        params object[] args)
        => location.CreateDiagnostic(rule, [], properties, args);

    public static Diagnostic CreateDiagnostic(
        this Location location,
        DiagnosticDescriptor rule,
        ImmutableArray<Location> additionalLocations,
        ImmutableDictionary<string, string?>? properties,
        params object[] args)
    {
        if (!location.IsInSource)
        {
            location = Location.None;
        }

        return Diagnostic.Create(
            descriptor: rule,
            location: location,
            additionalLocations: additionalLocations,
            properties: properties,
            messageArgs: args);
    }

    public static Diagnostic CreateDiagnostic(
        this IEnumerable<Location> locations,
        DiagnosticDescriptor rule,
        params object[] args) => locations.CreateDiagnostic(rule, null, args);

    public static Diagnostic CreateDiagnostic(
        this IEnumerable<Location> locations,
        DiagnosticDescriptor rule,
        ImmutableDictionary<string, string?>? properties,
        params object[] args)
    {
        IEnumerable<Location> inSource = locations.Where(l => l.IsInSource);
        return !inSource.Any()
            ? Diagnostic.Create(rule, null, args)
            : Diagnostic.Create(rule,
                 location: inSource.First(),
                 additionalLocations: inSource.Skip(1),
                 properties: properties,
                 messageArgs: args);
    }

    public static Diagnostic CreateDiagnostic(
        this SyntaxReference syntaxReference,
        DiagnosticDescriptor rule,
        CancellationToken cancellationToken,
        params object[] args)
        => syntaxReference.GetSyntax(cancellationToken).CreateDiagnostic(rule, args);

    public static void ReportNoLocationDiagnostic(
        this CompilationAnalysisContext context,
        DiagnosticDescriptor rule,
        params object[] args)
        => context.Compilation.ReportNoLocationDiagnostic(rule, context.ReportDiagnostic, properties: null, args);

    public static void ReportNoLocationDiagnostic(
        this Compilation compilation,
        DiagnosticDescriptor rule,
        Action<Diagnostic> addDiagnostic,
        ImmutableDictionary<string, string?>? properties,
        params object[] args)
    {
        DiagnosticSeverity? effectiveSeverity = GetEffectiveSeverity();
        if (!effectiveSeverity.HasValue)
        {
            // Disabled rule
            return;
        }

        if (effectiveSeverity.Value != rule.DefaultSeverity)
        {
#pragma warning disable RS0030 // The symbol 'DiagnosticDescriptor.DiagnosticDescriptor.#ctor' is banned in this project: Use 'DiagnosticDescriptorHelper.Create' instead
            rule = new DiagnosticDescriptor(rule.Id, rule.Title, rule.MessageFormat, rule.Category,
                effectiveSeverity.Value, rule.IsEnabledByDefault, rule.Description, rule.HelpLinkUri, [.. rule.CustomTags]);
#pragma warning restore RS0030
        }

        var diagnostic = Diagnostic.Create(rule, Location.None, properties, args);
        addDiagnostic(diagnostic);
        return;

        DiagnosticSeverity? GetEffectiveSeverity()
        {
            SyntaxTreeOptionsProvider? syntaxTreeOptionsProvider = compilation.Options.SyntaxTreeOptionsProvider;
            if (syntaxTreeOptionsProvider is null)
            {
                return rule.DefaultSeverity;
            }

            ReportDiagnostic? overriddenSeverity = null;
            foreach (SyntaxTree tree in compilation.SyntaxTrees)
            {
                if (!syntaxTreeOptionsProvider.TryGetDiagnosticValue(tree, rule.Id, CancellationToken.None, out ReportDiagnostic configuredValue))
                {
                    continue;
                }

                if (configuredValue == ReportDiagnostic.Suppress)
                {
                    // Any suppression entry always wins.
                    return null;
                }

                if (overriddenSeverity is null)
                {
                    overriddenSeverity = configuredValue;
                }
                else if (overriddenSeverity.Value.IsLessSevereThan(configuredValue))
                {
                    // Choose the most severe value for conflicts.
                    overriddenSeverity = configuredValue;
                }
            }

            return overriddenSeverity.HasValue ? overriddenSeverity.Value.ToDiagnosticSeverity() : rule.DefaultSeverity;
        }
    }
}
