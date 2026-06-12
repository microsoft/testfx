// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0070: <inheritdoc cref="Resources.MemberConditionShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class MemberConditionShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.MemberConditionShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.MemberConditionShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.MemberConditionShouldBeValidMessageFormat_MemberNotFound"/>
    public static readonly DiagnosticDescriptor MemberNotFoundRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.MemberConditionShouldBeValidRuleId,
        Title,
        new LocalizableResourceString(nameof(Resources.MemberConditionShouldBeValidMessageFormat_MemberNotFound), Resources.ResourceManager, typeof(Resources)),
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.MemberConditionShouldBeValidMessageFormat_MemberNotPublic"/>
    public static readonly DiagnosticDescriptor MemberNotPublicRule = MemberNotFoundRule
        .WithMessage(new(nameof(Resources.MemberConditionShouldBeValidMessageFormat_MemberNotPublic), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.MemberConditionShouldBeValidMessageFormat_MemberNotStatic"/>
    public static readonly DiagnosticDescriptor MemberNotStaticRule = MemberNotFoundRule
        .WithMessage(new(nameof(Resources.MemberConditionShouldBeValidMessageFormat_MemberNotStatic), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.MemberConditionShouldBeValidMessageFormat_MemberWrongKind"/>
    public static readonly DiagnosticDescriptor MemberWrongKindRule = MemberNotFoundRule
        .WithMessage(new(nameof(Resources.MemberConditionShouldBeValidMessageFormat_MemberWrongKind), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.MemberConditionShouldBeValidMessageFormat_MemberWrongReturnType"/>
    public static readonly DiagnosticDescriptor MemberWrongReturnTypeRule = MemberNotFoundRule
        .WithMessage(new(nameof(Resources.MemberConditionShouldBeValidMessageFormat_MemberWrongReturnType), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.MemberConditionShouldBeValidMessageFormat_MethodHasParameters"/>
    public static readonly DiagnosticDescriptor MethodHasParametersRule = MemberNotFoundRule
        .WithMessage(new(nameof(Resources.MemberConditionShouldBeValidMessageFormat_MethodHasParameters), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.MemberConditionShouldBeValidMessageFormat_PropertyNotReadable"/>
    public static readonly DiagnosticDescriptor PropertyNotReadableRule = MemberNotFoundRule
        .WithMessage(new(nameof(Resources.MemberConditionShouldBeValidMessageFormat_PropertyNotReadable), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MemberNotFoundRule,
        MemberNotPublicRule,
        MemberNotStaticRule,
        MemberWrongKindRule,
        MemberWrongReturnTypeRule,
        MethodHasParametersRule,
        PropertyNotReadableRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingMemberConditionAttribute, out INamedTypeSymbol? conditionAttributeSymbol))
            {
                return;
            }

            context.RegisterSymbolAction(
                ctx => AnalyzeSymbol(ctx, conditionAttributeSymbol),
                SymbolKind.Method,
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol conditionAttributeSymbol)
    {
        foreach (AttributeData attribute in context.Symbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, conditionAttributeSymbol))
            {
                continue;
            }

            AnalyzeAttribute(context, attribute);
        }
    }

    private static void AnalyzeAttribute(SymbolAnalysisContext context, AttributeData attribute)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not { } attributeSyntax)
        {
            return;
        }

        // Walk the constructor arguments. Across the 4 ctor overloads
        // ( (Type, string), (Type, string, params string[]),
        //   (ConditionMode, Type, string), (ConditionMode, Type, string, params string[]) )
        // we can identify the condition type, the first member name, and the optional params array
        // by inspecting argument kinds and types.
        INamedTypeSymbol? conditionType = null;
        var memberNames = new List<string>();
        foreach (TypedConstant argument in attribute.ConstructorArguments)
        {
            if (argument.IsNull)
            {
                continue;
            }

            if (argument.Kind == TypedConstantKind.Type && argument.Value is INamedTypeSymbol typeValue)
            {
                conditionType = typeValue;
            }
            else if (argument.Kind == TypedConstantKind.Primitive && argument.Value is string singleName)
            {
                memberNames.Add(singleName);
            }
            else if (argument.Kind == TypedConstantKind.Array)
            {
                foreach (TypedConstant element in argument.Values.Where(static e => !e.IsNull && e.Value is string))
                {
                    memberNames.Add((string)element.Value!);
                }
            }
        }

        if (conditionType is null || memberNames.Count == 0)
        {
            return;
        }

        string typeName = conditionType.Name;
        foreach (string memberName in memberNames)
        {
            ValidateMember(context, attributeSyntax, conditionType, typeName, memberName);
        }
    }

    private static void ValidateMember(SymbolAnalysisContext context, SyntaxNode attributeSyntax, INamedTypeSymbol conditionType, string typeName, string memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            // The runtime constructor already throws ArgumentException for null/empty/whitespace
            // names. Nothing useful to validate here.
            return;
        }

        ImmutableArray<ISymbol> candidates = LookupMember(conditionType, memberName);
        if (candidates.IsEmpty)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberNotFoundRule, typeName, memberName));
            return;
        }

        // Match the runtime resolution order: GetProperty / GetField / GetMethod with
        // BindingFlags.Public | Static | FlattenHierarchy, where GetMethod looks for the
        // *parameterless* overload only. If the runtime would bind to a candidate that
        // satisfies those filters, prefer it so we don't report a false positive against an
        // instance member or a parameterized overload that shadows the real binding target.
        ISymbol? selected =
            // Property: public + static + non-indexer (runtime rejects indexers).
            candidates.OfType<IPropertySymbol>()
                .FirstOrDefault(static p => p.DeclaredAccessibility == Accessibility.Public && p.IsStatic && !p.IsIndexer)
            // Field: public + static.
            ?? (ISymbol?)candidates.OfType<IFieldSymbol>()
                .FirstOrDefault(static f => f.DeclaredAccessibility == Accessibility.Public && f.IsStatic)
            // Method: public + static + ordinary + parameterless.
            ?? candidates.OfType<IMethodSymbol>()
                .FirstOrDefault(static m =>
                    m.DeclaredAccessibility == Accessibility.Public
                    && m.IsStatic
                    && m.MethodKind == MethodKind.Ordinary
                    && m.Parameters.Length == 0)
            // Fallback when no runtime-binding candidate exists: pick the first member by
            // kind so the more specific diagnostic (not-public, not-static, etc.) is reported.
            ?? candidates.FirstOrDefault(static s => s.Kind == SymbolKind.Property)
            ?? candidates.FirstOrDefault(static s => s.Kind == SymbolKind.Field)
            ?? candidates.FirstOrDefault(static s => s.Kind == SymbolKind.Method);

        if (selected is null)
        {
            // A nested type, event, or other unsupported member kind is shadowing the name.
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberWrongKindRule, typeName, memberName));
            return;
        }

        switch (selected)
        {
            case IPropertySymbol property:
                ValidateProperty(context, attributeSyntax, typeName, memberName, property);
                break;

            case IFieldSymbol field:
                ValidateField(context, attributeSyntax, typeName, memberName, field);
                break;

            case IMethodSymbol method:
                ValidateMethod(context, attributeSyntax, typeName, memberName, method);
                break;

            default:
                context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberWrongKindRule, typeName, memberName));
                break;
        }
    }

    private static ImmutableArray<ISymbol> LookupMember(INamedTypeSymbol type, string memberName)
    {
        // Walk the type hierarchy so inherited public static members are also recognized,
        // matching what reflection with `BindingFlags.Public | Static | FlattenHierarchy` would find.
        ImmutableArray<ISymbol>.Builder builder = ImmutableArray.CreateBuilder<ISymbol>();
        INamedTypeSymbol? current = type;
        while (current is not null)
        {
            foreach (ISymbol member in current.GetMembers(memberName))
            {
                builder.Add(member);
            }

            current = current.BaseType;
        }

        return builder.ToImmutable();
    }

    private static void ValidateProperty(SymbolAnalysisContext context, SyntaxNode attributeSyntax, string typeName, string memberName, IPropertySymbol property)
    {
        if (property.IsIndexer)
        {
            // Indexer properties (e.g. public static bool this[int i] in C# 13+) are rejected by
            // the runtime because the attribute requires a *parameterless* readable property.
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberWrongKindRule, typeName, memberName));
            return;
        }

        if (property.DeclaredAccessibility != Accessibility.Public)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberNotPublicRule, typeName, memberName));
            return;
        }

        if (!property.IsStatic)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberNotStaticRule, typeName, memberName));
            return;
        }

        if (property.GetMethod is null || property.GetMethod.DeclaredAccessibility != Accessibility.Public)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(PropertyNotReadableRule, typeName, memberName));
            return;
        }

        if (property.Type.SpecialType != SpecialType.System_Boolean)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberWrongReturnTypeRule, typeName, memberName));
        }
    }

    private static void ValidateField(SymbolAnalysisContext context, SyntaxNode attributeSyntax, string typeName, string memberName, IFieldSymbol field)
    {
        if (field.DeclaredAccessibility != Accessibility.Public)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberNotPublicRule, typeName, memberName));
            return;
        }

        if (!field.IsStatic)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberNotStaticRule, typeName, memberName));
            return;
        }

        if (field.Type.SpecialType != SpecialType.System_Boolean)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberWrongReturnTypeRule, typeName, memberName));
        }
    }

    private static void ValidateMethod(SymbolAnalysisContext context, SyntaxNode attributeSyntax, string typeName, string memberName, IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Ordinary)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberWrongKindRule, typeName, memberName));
            return;
        }

        if (method.DeclaredAccessibility != Accessibility.Public)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberNotPublicRule, typeName, memberName));
            return;
        }

        if (!method.IsStatic)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberNotStaticRule, typeName, memberName));
            return;
        }

        if (method.Parameters.Length > 0)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MethodHasParametersRule, typeName, memberName));
            return;
        }

        if (method.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberWrongReturnTypeRule, typeName, memberName));
        }
    }
}
