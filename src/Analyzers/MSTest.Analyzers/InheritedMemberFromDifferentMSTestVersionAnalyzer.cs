// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0082: <inheritdoc cref="Resources.InheritedMemberFromDifferentMSTestVersionTitle"/>.
/// </summary>
/// <remarks>
/// MSTest matches lifecycle and test attributes (<c>[TestInitialize]</c>, <c>[TestMethod]</c>, …) by exact CLR type
/// identity. The framework assembly was renamed from <c>Microsoft.VisualStudio.TestPlatform.TestFramework</c> (v3) to
/// <c>MSTest.TestFramework</c> (v4), so a base class compiled against a different MSTest major carries attributes
/// whose type identity does not match the ones the current adapter looks for. When such a base is inherited by a test
/// class compiled against another version, the inherited fixtures do not run and the inherited test methods are not
/// discovered — with no build error and no discovery error. This analyzer surfaces that silent mismatch.
/// <para>
/// Limitation: class-fixture inheritance (<c>[ClassInitialize]</c>/<c>[ClassCleanup]</c>) is only detected when the
/// legacy attribute constructor resolves. When the base library's framework assembly is entirely absent from the
/// compilation (a real PE reference whose dependency is not provided), Roslyn cannot bind the attribute constructor
/// and exposes no constructor arguments, so <c>InheritanceBehavior</c> cannot be read and the inherited class fixture
/// is conservatively not reported — treating an undecodable argument as <c>BeforeEachDerivedClass</c> would falsely
/// flag the default <c>None</c>. Instance fixtures and test methods, which carry no constructor arguments, are
/// detected regardless of whether the legacy framework is resolvable.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed partial class InheritedMemberFromDifferentMSTestVersionAnalyzer : DiagnosticAnalyzer
{
    private const string LegacyFrameworkAssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework";
    private const string LegacyFrameworkExtensionsAssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions";
    private const string CurrentFrameworkAssemblyName = "MSTest.TestFramework";
    private const string CurrentFrameworkExtensionsAssemblyName = "MSTest.TestFramework.Extensions";
    private const string MSTestNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting";
    private const string TestClassAttributeName = "TestClassAttribute";
    private const string InheritanceBehaviorTypeName = "InheritanceBehavior";
    private const int BeforeEachDerivedClass = 1;

    private static readonly LocalizableResourceString Title = new(nameof(Resources.InheritedMemberFromDifferentMSTestVersionTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.InheritedMemberFromDifferentMSTestVersionMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.InheritedMemberFromDifferentMSTestVersionDescription), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.InheritedMemberFromDifferentMSTestVersionTitle" />
    public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.InheritedMemberFromDifferentMSTestVersionRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            INamedTypeSymbol? taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
            INamedTypeSymbol? valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
            bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();

            var globallyVisibleAssemblies = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
            bool hasGloballyVisibleFramework = false;
            foreach (MetadataReference reference in context.Compilation.References)
            {
                if (context.Compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly
                    || (!reference.Properties.Aliases.IsDefaultOrEmpty
                        && !reference.Properties.Aliases.Contains("global", StringComparer.Ordinal)))
                {
                    continue;
                }

                globallyVisibleAssemblies.Add(assembly);
                hasGloballyVisibleFramework |= IsKnownFrameworkAssembly(assembly)
                    && assembly.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute) is not null;
            }

            context.RegisterSymbolAction(
                context => AnalyzeSymbol(context, taskSymbol, valueTaskSymbol, canDiscoverInternals, globallyVisibleAssemblies, hasGloballyVisibleFramework),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol,
        bool canDiscoverInternals,
        HashSet<IAssemblySymbol> globallyVisibleAssemblies,
        bool hasGloballyVisibleFramework)
    {
        var classSymbol = (INamedTypeSymbol)context.Symbol;

        // A test class the adapter cannot discover never runs, so no inherited member can run for it: abstract and
        // generic types are rejected — including a concrete class nested in a generic container, which cannot be
        // constructed without the container's type arguments — and the type must be visible enough to be discovered
        // (public, or internal when the assembly opts into DiscoverInternals). Concrete, accessible, non-generic derived
        // classes are analyzed separately and still get the warning.
        // These type-only checks are cheap and reject the majority of named-type symbols in a typical compilation, so
        // they run first to avoid the more expensive GetFrameworkAssembly lookup for symbols filtered out anyway.
        if (classSymbol.IsAbstract
            || classSymbol.IsGenericType
            || IsNestedInGenericType(classSymbol)
            || !IsDiscoverableTestClassVisibility(classSymbol, canDiscoverInternals))
        {
            return;
        }

        IAssemblySymbol? referenceAssembly = GetFrameworkAssembly(classSymbol, globallyVisibleAssemblies, hasGloballyVisibleFramework);
        if (referenceAssembly is null || !classSymbol.HasCorrectTestContextSignature())
        {
            return;
        }

        for (INamedTypeSymbol? baseType = classSymbol.BaseType;
            baseType is not null && baseType.SpecialType != SpecialType.System_Object;
            baseType = baseType.BaseType)
        {
            // Members declared in the same assembly as the test class bind to the current framework, so they can
            // never be the mismatched version. Only inherited members from another assembly can be affected.
            if (SymbolEqualityComparer.Default.Equals(baseType.ContainingAssembly, classSymbol.ContainingAssembly))
            {
                continue;
            }

            foreach (ISymbol member in baseType.GetMembers())
            {
                if (member is not IMethodSymbol method)
                {
                    continue;
                }

                foreach (AttributeData attribute in method.GetAttributes())
                {
                    // Resolve the canonical MSTest attribute in the applied attribute's base chain, so inherited
                    // custom lifecycle/test attribute subclasses compiled against the old framework are matched too.
                    INamedTypeSymbol? canonicalAttribute = GetCanonicalMSTestAttribute(attribute.AttributeClass);
                    if (canonicalAttribute is null)
                    {
                        continue;
                    }

                    MSTestMemberKind kind = GetMemberKind(canonicalAttribute.Name);
                    if (kind == MSTestMemberKind.None)
                    {
                        continue;
                    }

                    IAssemblySymbol? attributeAssembly = canonicalAttribute.ContainingAssembly;
                    if (attributeAssembly is null
                        || string.Equals(attributeAssembly.Name, referenceAssembly.Name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Only warn about members that would actually run or be discovered if the base were recompiled
                    // against the current version. Otherwise the remediation ("recompile the base") cannot help and
                    // the diagnostic is a false positive.
                    if (!WouldRunOrBeDiscoveredIfSameVersion(method, kind, attribute, classSymbol, baseType, referenceAssembly, canDiscoverInternals, taskSymbol, valueTaskSymbol))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(classSymbol.CreateDiagnostic(
                        Rule,
                        method.Name,
                        baseType.Name,
                        GetDisplayName(attribute.AttributeClass!),
                        attributeAssembly.Name));
                }
            }
        }
    }

}
