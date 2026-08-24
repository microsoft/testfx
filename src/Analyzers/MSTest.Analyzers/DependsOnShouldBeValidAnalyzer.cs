// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0078: <inheritdoc cref="Resources.DependsOnShouldBeValidTitle"/>.
/// </summary>
/// <remarks>
/// <para>
/// The runtime deliberately treats a dependency that matches no test as a non-fatal warning, so that
/// <c>--filter</c> and single-test runs keep working (see RFC 022). That makes build time the right place
/// to catch the references that are statically decidable - a typo, a rename, a target that is not a test -
/// which is what the <c>nameof</c>/<c>typeof</c> shape of the attribute is for.
/// </para>
/// <para>
/// Every check bails out when the answer depends on information the compilation does not have. The most
/// important case is a test method declared on a non-test base class: its dependencies are resolved against
/// each <em>derived</em> test class at run time, so a reference that names no member of the base class is
/// not necessarily broken and is left alone.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DependsOnShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Caps the dependency walk so that a pathological graph cannot make the analyzer quadratic. Real
    /// dependency graphs are tiny; a graph larger than this simply does not get its cycles reported at
    /// build time, and the runtime still reports them.
    /// </summary>
    private const int MaxVisitedNodes = 512;

    private static readonly LocalizableResourceString Title = new(nameof(Resources.DependsOnShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.DependsOnShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_MethodNotFound"/>
    public static readonly DiagnosticDescriptor MethodNotFoundRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DependsOnShouldBeValidRuleId,
        Title,
        new LocalizableResourceString(nameof(Resources.DependsOnShouldBeValidMessageFormat_MethodNotFound), Resources.ResourceManager, typeof(Resources)),
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_NotATestMethod"/>
    public static readonly DiagnosticDescriptor NotATestMethodRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_NotATestMethod), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_NotATestClass"/>
    public static readonly DiagnosticDescriptor NotATestClassRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_NotATestClass), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_OtherAssembly"/>
    public static readonly DiagnosticDescriptor OtherAssemblyRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_OtherAssembly), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_AbstractTarget"/>
    public static readonly DiagnosticDescriptor AbstractTargetRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_AbstractTarget), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_SelfReference"/>
    public static readonly DiagnosticDescriptor SelfReferenceRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_SelfReference), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_Cycle"/>
    public static readonly DiagnosticDescriptor CycleRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_Cycle), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_NoEffect"/>
    public static readonly DiagnosticDescriptor NoEffectRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_NoEffect), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_NoEffectOnClass"/>
    public static readonly DiagnosticDescriptor NoEffectOnClassRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_NoEffectOnClass), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MethodNotFoundRule,
        NotATestMethodRule,
        NotATestClassRule,
        OtherAssemblyRule,
        AbstractTargetRule,
        SelfReferenceRule,
        CycleRule,
        NoEffectRule,
        NoEffectOnClassRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDependsOnAttribute, out INamedTypeSymbol? dependsOnAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                return;
            }

            var symbols = new AnalysisSymbols(
                dependsOnAttributeSymbol,
                testMethodAttributeSymbol,
                testClassAttributeSymbol,
                DiscoversInternals(context.Compilation));
            context.RegisterSymbolAction(context => AnalyzeMethod(context, symbols), SymbolKind.Method);
            context.RegisterSymbolAction(context => AnalyzeNamedType(context, symbols), SymbolKind.NamedType);
        });
    }

    /// <summary>
    /// Whether the assembly opts internal members into discovery, which decides whether an internal
    /// <c>[TestMethod]</c> is a runnable test (<c>TestMethodValidator.IsValidTestMethod</c>).
    /// </summary>
    private static bool DiscoversInternals(Compilation compilation)
    {
        if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDiscoverInternalsAttribute, out INamedTypeSymbol? discoverInternalsAttributeSymbol))
        {
            return false;
        }

        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, discoverInternalsAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, AnalysisSymbols symbols)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        List<AttributeData> attributes = GetDependsOnAttributes(typeSymbol, symbols);

        // '[DependsOn]' is not inherited (AttributeUsage(Inherited = false)) and is only read off a class
        // discovery actually runs tests for, so an application on a type that is not a runnable test class -
        // a shared base class whose derived types are the test classes, or an abstract '[TestClass]' whose
        // tests are enumerated under each concrete derived class - never produces an edge.
        if (!IsRunnableTestClass(typeSymbol, symbols))
        {
            ReportNoEffect(context, attributes, typeSymbol.Name, NoEffectOnClassRule);
            return;
        }

        foreach (AttributeData attribute in attributes)
        {
            AnalyzeTarget(context, attribute, symbols, declaringClass: typeSymbol, declaringMethod: null);
        }

        // Every cycle walk starts here, once per test the class effectively runs - including the ones it only
        // inherits. Starting them from AnalyzeMethod instead would key the node off the class that *declares*
        // the method, which is not the class the edge resolves against, and would need a de-duplication rule
        // that no name-based check can get right in the presence of overloads.
        //
        // A class with a duplicated method signature in its hierarchy is skipped: discovery's duplicate
        // fallback then collapses *every* same-named test to the declaration closest to the class, including
        // genuine overloads, and modelling that here would mean baking a run-time quirk into the analyzer.
        // Going quiet is the safe answer, and MSTEST0036 already reports the shadowing that triggers it.
        if (HasDuplicateSignature(typeSymbol, symbols))
        {
            return;
        }

        foreach (string testMethodName in EnumerateTestMethodNames(typeSymbol, symbols))
        {
            AnalyzeCycle(context, symbols, new TestNode(typeSymbol, testMethodName));
        }
    }

    /// <summary>
    /// Whether the type's hierarchy declares the same method signature twice <em>among the methods discovery
    /// turns into tests</em>, which is what makes <c>TypeEnumerator.GetTests</c> fall back to collapsing tests
    /// by name. Methods discovery never looks at - private helpers and the like - cannot trigger it, so they
    /// must not disable cycle analysis here either.
    /// </summary>
    private static bool HasDuplicateSignature(INamedTypeSymbol type, AnalysisSymbols symbols)
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                // An override is not a duplicate: reflection surfaces only the most-derived declaration.
                if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, OverriddenMethod: null } method
                    && IsRunnableTestMethod(method, symbols)
                    && !signatures.Add(BuildSignatureKey(method)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, AnalysisSymbols symbols)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        List<AttributeData> attributes = GetDependsOnAttributes(methodSymbol, symbols);
        if (!methodSymbol.IsTestMethod(symbols.TestMethodAttribute))
        {
            ReportNoEffect(context, attributes, methodSymbol.Name, NoEffectRule);
            return;
        }

        foreach (AttributeData attribute in attributes)
        {
            AnalyzeTarget(context, attribute, symbols, methodSymbol.ContainingType, methodSymbol);
        }
    }

    /// <summary>
    /// Validates one <c>[DependsOn]</c> application against the members it names.
    /// </summary>
    private static void AnalyzeTarget(
        SymbolAnalysisContext context,
        AttributeData attribute,
        AnalysisSymbols symbols,
        INamedTypeSymbol declaringClass,
        IMethodSymbol? declaringMethod)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not { } attributeSyntax)
        {
            return;
        }

        (ITypeSymbol? explicitTarget, string? targetMethodName, bool isMalformed) = ReadTarget(attribute);
        if (isMalformed)
        {
            return;
        }

        // An implicit target ("a method of my own class") is resolved against the class the test runs under.
        // For a test method declared on a class discovery does not run directly - an unannotated base, or an
        // abstract '[TestClass]' - that class is each concrete derived test class, which is not knowable from
        // this declaration, so only explicit 'typeof' targets are validated there.
        INamedTypeSymbol targetClass;
        if (explicitTarget is null)
        {
            if (!IsRunnableTestClass(declaringClass, symbols))
            {
                return;
            }

            targetClass = declaringClass;
        }
        else if (explicitTarget is INamedTypeSymbol namedTarget)
        {
            targetClass = namedTarget;
        }
        else
        {
            // typeof(int[]), typeof(int*), ... - the attribute accepts them, but an array or pointer can
            // never carry a '[TestClass]', so the reference is decidably dead. Reported before the
            // assembly check below, which has no answer for a type with no containing assembly.
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(NotATestClassRule, DescribeType(explicitTarget)));
            return;
        }

        if (targetClass.TypeKind == TypeKind.Error)
        {
            return;
        }

        // Discovery resolves a dependency against the tests of one test source, and stores a 'typeof' target
        // as its CLR full name. A type from another assembly therefore matches nothing, however well-formed
        // the reference is - so this is reported before the "is it a test class" question, which is moot.
        if (!IsFromCurrentAssembly(context, targetClass))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(OtherAssemblyRule, targetClass.Name));
            return;
        }

        if (!IsRunnableTestClass(targetClass, symbols))
        {
            // An abstract '[TestClass]' is skipped by discovery - its tests are enumerated under each
            // concrete derived class - so the reference matches nothing even though the attribute is there.
            // That needs its own message: "add [TestClass]" is not the fix. A static class is rejected by
            // discovery too (reflection sees it as abstract), but nothing derives from it, so it gets the
            // plain message instead.
            DiagnosticDescriptor rule = targetClass is { IsAbstract: true, IsStatic: false } && targetClass.IsTestClass(symbols.TestClassAttribute)
                ? AbstractTargetRule
                : NotATestClassRule;
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(rule, targetClass.Name));
            return;
        }

        if (targetMethodName is null)
        {
            // A whole-class target on a test class always matches something (or the class has no test at
            // all, which MSTEST0016 already reports).
            return;
        }

        if (string.IsNullOrWhiteSpace(targetMethodName))
        {
            // The attribute constructor throws for these, so there is nothing useful to add.
            return;
        }

        MethodLookup lookup = LookupMethods(targetClass, targetMethodName, symbols);
        if (!lookup.Exists)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MethodNotFoundRule, targetClass.Name, targetMethodName));
            return;
        }

        if (!lookup.IsTestMethod)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(NotATestMethodRule, targetClass.Name, targetMethodName));
            return;
        }

        // Only a reference written directly on the method is a self reference. The same reference written on
        // the class means "every *other* test waits for this one", and discovery drops the generated
        // self-edge.
        if (declaringMethod is not null
            && string.Equals(targetMethodName, declaringMethod.Name, StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(targetClass, declaringClass))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(SelfReferenceRule, declaringMethod.Name));
        }
    }

    /// <summary>
    /// Whether <see cref="AnalyzeTarget"/> already reported <paramref name="node"/> as a self reference.
    /// That happens only for a declaration on the node's own class, because that is the one case where the
    /// class a reference resolves against is the same as the class declaring it.
    /// </summary>
    private static bool IsSelfReferenceReported(TestNode node, AnalysisSymbols symbols)
    {
        foreach (ISymbol member in node.TestClass.GetMembers(node.MethodName))
        {
            if (member is not IMethodSymbol method || !method.IsTestMethod(symbols.TestMethodAttribute))
            {
                continue;
            }

            foreach (AttributeData attribute in GetDependsOnAttributes(method, symbols))
            {
                (ITypeSymbol? explicitTarget, string? targetMethodName, bool isMalformed) = ReadTarget(attribute);
                if (isMalformed || !string.Equals(targetMethodName, node.MethodName, StringComparison.Ordinal))
                {
                    continue;
                }

                // An implicit target resolves to the declaring class, which is this node's class here.
                if (explicitTarget is null
                    || SymbolEqualityComparer.Default.Equals(explicitTarget, node.TestClass))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AnalyzeCycle(SymbolAnalysisContext context, AnalysisSymbols symbols, TestNode start)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<TestNode>();
        if (!TryFindCycle(context, symbols, start, start, visited, path, out AttributeData? firstEdge)
            || firstEdge?.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not { } attributeSyntax)
        {
            return;
        }

        // A one-node cycle is a test naming itself. AnalyzeTarget reports that as a self reference, but only
        // when it can see it: it compares the target against the class that *declares* the method, so an
        // inherited test pointing at its own derived class (`[DependsOn(typeof(Derived), nameof(Run))]` on a
        // base method) is not reported there. Suppress only the cycles that really were.
        if (path.Count == 1 && IsSelfReferenceReported(start, symbols))
        {
            return;
        }

        var builder = new StringBuilder();
        builder.Append(start.Describe());
        foreach (TestNode node in path)
        {
            builder.Append(" > ").Append(node.Describe());
        }

        context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(CycleRule, builder.ToString()));
    }

    /// <summary>
    /// Depth-first search for a path from <paramref name="current"/> back to <paramref name="start"/>.
    /// <paramref name="path"/> ends up holding that path (excluding the start node, including the closing
    /// return to it) and <paramref name="firstEdge"/> the attribute application that begins it, so the
    /// diagnostic can be reported where the cycle is declared rather than on the test symbol.
    /// </summary>
    private static bool TryFindCycle(
        SymbolAnalysisContext context,
        AnalysisSymbols symbols,
        TestNode start,
        TestNode current,
        HashSet<string> visited,
        List<TestNode> path,
        out AttributeData? firstEdge)
    {
        firstEdge = null;
        if (visited.Count >= MaxVisitedNodes)
        {
            return false;
        }

        foreach ((TestNode target, AttributeData source) in GetPrerequisites(context, symbols, current))
        {
            // Re-checked per target, not just on entry: a single high fan-out node would otherwise push
            // 'visited' past the budget wholesale, since the entry check only stops the *next* call.
            // Bailing before 'path.Add' keeps the path balanced for the caller's own RemoveAt.
            if (visited.Count >= MaxVisitedNodes)
            {
                return false;
            }

            path.Add(target);
            if (target.Equals(start))
            {
                firstEdge = source;
                return true;
            }

            if (visited.Add(target.Key) && TryFindCycle(context, symbols, start, target, visited, path, out _))
            {
                firstEdge = source;
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    /// <summary>
    /// Expands the <c>[DependsOn]</c> applications that apply to <paramref name="node"/> - those on its test
    /// class and those on the test methods that carry its name - into the nodes they point at, mirroring what
    /// discovery does: a class-wide target covers every test of that class except the dependent itself, and a
    /// class-level declaration never makes a test depend on itself.
    /// </summary>
    private static IEnumerable<(TestNode Target, AttributeData Source)> GetPrerequisites(SymbolAnalysisContext context, AnalysisSymbols symbols, TestNode node)
    {
        // The duplicate-signature bail-out has to be per node, not just per walk start: a walk beginning in
        // another class can still traverse *into* a class whose tests discovery collapsed by name, and
        // expanding this node's declarations would then invent edges discovery removed.
        if (HasDuplicateSignature(node.TestClass, symbols))
        {
            yield break;
        }

        foreach (AttributeData attribute in GetDependsOnAttributes(node.TestClass, symbols))
        {
            foreach ((TestNode target, AttributeData source) in ExpandTarget(context, symbols, node, attribute, isClassLevel: true))
            {
                yield return (target, source);
            }
        }

        foreach (IMethodSymbol method in EnumerateMethods(node.TestClass, node.MethodName, symbols))
        {
            // Graph nodes use the stricter "does discovery run this" predicate: reading the attributes of a
            // method discovery never turns into a test would invent edges, and an invented edge can close a
            // cycle that does not exist at run time.
            if (!IsRunnableTestMethod(method, symbols))
            {
                continue;
            }

            foreach (AttributeData attribute in GetDependsOnAttributes(method, symbols))
            {
                foreach ((TestNode target, AttributeData source) in ExpandTarget(context, symbols, node, attribute, isClassLevel: false))
                {
                    yield return (target, source);
                }
            }
        }
    }

    private static IEnumerable<(TestNode Target, AttributeData Source)> ExpandTarget(
        SymbolAnalysisContext context,
        AnalysisSymbols symbols,
        TestNode node,
        AttributeData attribute,
        bool isClassLevel)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        (ITypeSymbol? explicitTarget, string? targetMethodName, bool isMalformed) = ReadTarget(attribute);
        if (isMalformed)
        {
            yield break;
        }

        // Only a named type can contribute graph nodes; an array or pointer target is reported by
        // AnalyzeTarget and contributes no edge.
        if (explicitTarget is not null and not INamedTypeSymbol)
        {
            yield break;
        }

        INamedTypeSymbol targetClass = explicitTarget as INamedTypeSymbol ?? node.TestClass;
        if (targetClass.TypeKind == TypeKind.Error
            || !IsFromCurrentAssembly(context, targetClass)
            || !IsRunnableTestClass(targetClass, symbols))
        {
            // A target in another assembly produces no edge at run time, so the walk must not follow it -
            // otherwise a "cycle" could be reported through a link that does not exist.
            yield break;
        }

        if (targetMethodName is null)
        {
            foreach (string testMethodName in EnumerateTestMethodNames(targetClass, symbols))
            {
                var target = new TestNode(targetClass, testMethodName);

                // Discovery drops the self-edge a whole-class target generates, whether the declaration is on
                // the class or on the method.
                if (!target.Equals(node))
                {
                    yield return (target, attribute);
                }
            }

            yield break;
        }

        if (string.IsNullOrWhiteSpace(targetMethodName) || !HasRunnableTestMethod(targetClass, targetMethodName, symbols))
        {
            yield break;
        }

        var namedTarget = new TestNode(targetClass, targetMethodName);

        // A class-level '[DependsOn(nameof(Setup))]' means "every *other* test waits for Setup", so the edge
        // it would generate for Setup itself is dropped. Written on the method, the same reference is a real
        // (and reported) self reference.
        if (isClassLevel && namedTarget.Equals(node))
        {
            yield break;
        }

        yield return (namedTarget, attribute);
    }

    /// <summary>
    /// Reads the target of one <c>[DependsOn]</c> application: the three constructors are
    /// <c>(string)</c>, <c>(Type)</c> and <c>(Type, string)</c>, so the target class and method name can be
    /// told apart by argument kind.
    /// </summary>
    private static (ITypeSymbol? TargetClass, string? TargetMethodName, bool IsMalformed) ReadTarget(AttributeData attribute)
    {
        ITypeSymbol? targetClass = null;
        string? targetMethodName = null;
        foreach (TypedConstant argument in attribute.ConstructorArguments)
        {
            // A null argument means the constructor throws and no dependency is ever recorded. Reading the
            // remaining arguments would misread '[DependsOn((Type)null!, nameof(A))]' as an implicit
            // same-class target and could report a self reference against code that never builds a graph.
            if (argument.IsNull)
            {
                return (null, null, true);
            }

            switch (argument)
            {
                case { Kind: TypedConstantKind.Type, Value: ITypeSymbol type }:
                    targetClass = type;
                    break;

                case { Kind: TypedConstantKind.Primitive, Value: string name }:
                    targetMethodName = name;
                    break;
            }
        }

        return (targetClass, targetMethodName, targetClass is null && targetMethodName is null);
    }

    /// <summary>
    /// Names a type for a diagnostic message. <see cref="ISymbol.Name"/> is empty for an array or pointer,
    /// so those fall back to the display form (<c>int[]</c>).
    /// </summary>
    private static string DescribeType(ITypeSymbol type)
        => string.IsNullOrEmpty(type.Name)
            ? type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            : type.Name;

    /// <summary>
    /// Whether discovery runs tests under <paramref name="type"/>'s own name. Mirrors
    /// <c>TypeValidator.IsValidTestClass</c> on the points that decide whether a <c>[DependsOn]</c> reference
    /// can ever match: the <c>[TestClass]</c> attribute, accessibility, the fact that an abstract test class
    /// is skipped so its tests are enumerated under each concrete derived class, and the fact that a
    /// non-abstract generic test class is rejected outright.
    /// </summary>
    /// <remarks>
    /// A static class is checked separately from an abstract one. Reflection sees a static class as
    /// <c>abstract sealed</c>, so <c>IsValidTestClass</c> rejects it through its <c>IsAbstract</c> test, but
    /// Roslyn models the two independently and reports <c>IsAbstract == false</c> for it.
    /// </remarks>
    private static bool IsRunnableTestClass(INamedTypeSymbol type, AnalysisSymbols symbols)
        => !type.IsAbstract
            && !type.IsStatic
            && !IsGenericOrNestedInGeneric(type)
            && HasValidAccessibility(type, symbols)
            && type.HasCorrectTestContextSignature()
            && type.IsTestClass(symbols.TestClassAttribute);

    /// <summary>
    /// Whether any generic type is involved. Discovery enumerates the assembly's type <em>definitions</em>,
    /// and <c>TypeValidator.IsValidTestClass</c> rejects a non-abstract generic definition - so no test ever
    /// exists under a generic class's name, whether it is written open (<c>typeof(Tests&lt;&gt;)</c>) or
    /// constructed (<c>typeof(Tests&lt;int&gt;)</c>). A type nested in a generic container is itself a generic
    /// definition at run time, so it is covered too.
    /// </summary>
    private static bool IsGenericOrNestedInGeneric(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Mirrors <c>TypeValidator.TypeHasValidAccessibility</c>: the type and every type it is nested in must be
    /// public, or internal when the assembly opts in with <c>[DiscoverInternals]</c>. Anything else - a
    /// private, protected or protected-internal nested class - is skipped by discovery.
    /// </summary>
    private static bool HasValidAccessibility(INamedTypeSymbol type, AnalysisSymbols symbols)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility == Accessibility.Public)
            {
                continue;
            }

            if (!symbols.DiscoverInternals || current.DeclaredAccessibility != Accessibility.Internal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether discovery turns <paramref name="method"/> into a test. Mirrors the parts of
    /// <c>TestMethodValidator.IsValidTestMethod</c> that are decidable here: the <c>[TestMethod]</c>
    /// attribute, accessibility (public, or internal when the assembly opts in with
    /// <c>[DiscoverInternals]</c>), and the rejection of static and abstract methods.
    /// </summary>
    /// <remarks>
    /// The return-type rule is deliberately not mirrored. MSTEST0003 already reports a test method with an
    /// invalid signature, so the only thing this omission costs is a graph node for a method that is
    /// reported as broken anyway - whereas getting <c>async void</c> and <c>ValueTask&lt;T&gt;</c> subtly
    /// wrong would drop nodes for methods that really do run.
    /// </remarks>
    private static bool IsRunnableTestMethod(IMethodSymbol method, AnalysisSymbols symbols)
        => method is { IsStatic: false, IsAbstract: false }
            && (method.DeclaredAccessibility == Accessibility.Public
                || (symbols.DiscoverInternals && method.DeclaredAccessibility == Accessibility.Internal))
            && method.IsTestMethod(symbols.TestMethodAttribute);

    private static bool IsFromCurrentAssembly(SymbolAnalysisContext context, INamedTypeSymbol type)
        => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, context.Compilation.Assembly);

    private static MethodLookup LookupMethods(INamedTypeSymbol targetClass, string methodName, AnalysisSymbols symbols)
    {
        bool exists = false;
        foreach (IMethodSymbol method in EnumerateMethods(targetClass, methodName, symbols))
        {
            exists = true;

            // The name is what the run-time graph matches on, so any overload in the type's effective method
            // set being a test is enough for the reference to resolve.
            if (method.IsTestMethod(symbols.TestMethodAttribute))
            {
                return new MethodLookup(Exists: true, IsTestMethod: true);
            }
        }

        return new MethodLookup(exists, IsTestMethod: false);
    }

    /// <summary>
    /// Enumerates the methods named <paramref name="methodName"/> that <paramref name="type"/> effectively
    /// has: its own declarations plus the inherited ones, excluding base declarations that an override
    /// replaces.
    /// </summary>
    private static IEnumerable<IMethodSymbol> EnumerateMethods(INamedTypeSymbol type, string methodName, AnalysisSymbols symbols)
        => EnumerateEffectiveMethods(type, methodName, symbols);

    private static bool HasRunnableTestMethod(INamedTypeSymbol targetClass, string methodName, AnalysisSymbols symbols)
    {
        foreach (IMethodSymbol method in EnumerateMethods(targetClass, methodName, symbols))
        {
            if (IsRunnableTestMethod(method, symbols))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateTestMethodNames(INamedTypeSymbol type, AnalysisSymbols symbols)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (IMethodSymbol method in EnumerateEffectiveMethods(type, methodName: null, symbols))
        {
            if (IsRunnableTestMethod(method, symbols) && seen.Add(method.Name))
            {
                yield return method.Name;
            }
        }
    }

    /// <summary>
    /// Walks <paramref name="type"/> and its base types most-derived first, yielding the ordinary methods
    /// that make up the type's effective method set - optionally only those named
    /// <paramref name="methodName"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A base declaration that an override replaces is dropped, because reflection surfaces only the
    /// most-derived declaration and both <c>[TestMethod]</c> and <c>[DependsOn]</c> are declared
    /// <c>Inherited = false</c>. Keeping the base declaration would let its attributes travel to the
    /// override: an overridden test whose override does not re-declare <c>[TestMethod]</c> would still count
    /// as a test, and - worse - a <c>[DependsOn]</c> the author deliberately dropped by overriding would
    /// still contribute an edge, which can close a cycle that does not exist at run time.
    /// </para>
    /// <para>
    /// A base declaration that a <c>new</c> member hides with the <em>same signature</em> is dropped for the
    /// same reason: <c>TypeEnumerator.GetTests</c> detects the duplicate and keeps the declaration closest to
    /// the test class. Genuine overloads (a different signature) are kept, since those are distinct tests.
    /// That duplicate detection only looks at the methods discovery turns into tests, so a hiding declaration
    /// discovery ignores - a private helper, say - must not displace the base test it happens to shadow.
    /// </para>
    /// </remarks>
    private static IEnumerable<IMethodSymbol> EnumerateEffectiveMethods(INamedTypeSymbol type, string? methodName, AnalysisSymbols symbols)
    {
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in methodName is null ? current.GetMembers() : current.GetMembers(methodName))
            {
                if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                    || overridden.Contains(method))
                {
                    continue;
                }

                if (IsRunnableTestMethod(method, symbols) && !signatures.Add(BuildSignatureKey(method)))
                {
                    continue;
                }

                for (IMethodSymbol? baseMethod = method.OverriddenMethod; baseMethod is not null; baseMethod = baseMethod.OverriddenMethod)
                {
                    overridden.Add(baseMethod);
                }

                yield return method;
            }
        }
    }

    /// <summary>
    /// Builds a key identifying a method's CLR signature. Method type parameters are normalized to their
    /// ordinal, and the walk is structural, because two declarations that hide each other can spell their
    /// type parameters differently (<c>Run&lt;T&gt;(T)</c> and <c>Run&lt;U&gt;(U)</c> are the same signature).
    /// Rendering the parameter types as source text would miss that and keep the hidden declaration.
    /// </summary>
    /// <remarks>
    /// Erring toward collision is safe here: the walk yields most-derived first, so a key collision only ever
    /// drops a <em>base</em> declaration, which removes edges rather than inventing them.
    /// </remarks>
    private static string BuildSignatureKey(IMethodSymbol method)
    {
        var builder = new StringBuilder(method.Name);
        builder.Append('`').Append(method.Arity).Append('(');
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            IParameterSymbol parameter = method.Parameters[i];
            builder.Append(parameter.RefKind).Append(':');
            AppendTypeKey(builder, parameter.Type);
        }

        return builder.Append(')').ToString();
    }

    private static void AppendTypeKey(StringBuilder builder, ITypeSymbol type)
    {
        switch (type)
        {
            // The name a method type parameter is given is not part of the signature; its position is.
            case ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method } methodTypeParameter:
                builder.Append("!!").Append(methodTypeParameter.Ordinal);
                return;

            // 'dynamic' and 'object' are the same type to the CLR.
            case IDynamicTypeSymbol:
                builder.Append("object");
                return;

            case IArrayTypeSymbol array:
                AppendTypeKey(builder, array.ElementType);
                builder.Append('[').Append(array.Rank).Append(']');
                return;

            case IPointerTypeSymbol pointer:
                AppendTypeKey(builder, pointer.PointedAtType);
                builder.Append('*');
                return;

            // A constructed generic has to be compared argument by argument, so that 'List<T>' and 'List<U>'
            // are recognized as the same signature.
            case INamedTypeSymbol { IsGenericType: true } named:
                builder.Append(named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append('<');
                for (int i = 0; i < named.TypeArguments.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    AppendTypeKey(builder, named.TypeArguments[i]);
                }

                builder.Append('>');
                return;

            default:
                builder.Append(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                return;
        }
    }

    private static List<AttributeData> GetDependsOnAttributes(ISymbol symbol, AnalysisSymbols symbols)
    {
        List<AttributeData> attributes = [];
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, symbols.DependsOnAttribute))
            {
                attributes.Add(attribute);
            }
        }

        return attributes;
    }

    private static void ReportNoEffect(SymbolAnalysisContext context, List<AttributeData> attributes, string memberName, DiagnosticDescriptor rule)
    {
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is { } attributeSyntax)
            {
                context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(rule, memberName));
            }
        }
    }

    private readonly record struct MethodLookup(bool Exists, bool IsTestMethod);

    private sealed record AnalysisSymbols(
        INamedTypeSymbol DependsOnAttribute,
        INamedTypeSymbol TestMethodAttribute,
        INamedTypeSymbol TestClassAttribute,
        bool DiscoverInternals);

    /// <summary>
    /// One node of the dependency graph. The run-time graph keys tests by class name and method name - an
    /// overloaded or data-driven test contributes several tests under one name - so the analyzer models a
    /// node the same way rather than by method symbol.
    /// </summary>
    private readonly struct TestNode : IEquatable<TestNode>
    {
        public TestNode(INamedTypeSymbol testClass, string methodName)
        {
            TestClass = testClass;
            MethodName = methodName;
            Key = testClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + methodName;
        }

        public INamedTypeSymbol TestClass { get; }

        public string MethodName { get; }

        public string Key { get; }

        public string Describe() => TestClass.Name + "." + MethodName;

        public bool Equals(TestNode other) => string.Equals(Key, other.Key, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is TestNode other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Key);
    }
}
