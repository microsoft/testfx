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

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_SelfReference"/>
    public static readonly DiagnosticDescriptor SelfReferenceRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_SelfReference), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_Cycle"/>
    public static readonly DiagnosticDescriptor CycleRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_Cycle), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_NoEffect"/>
    public static readonly DiagnosticDescriptor NoEffectRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_NoEffect), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MethodNotFoundRule,
        NotATestMethodRule,
        NotATestClassRule,
        OtherAssemblyRule,
        SelfReferenceRule,
        CycleRule,
        NoEffectRule);

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

            var symbols = new AnalysisSymbols(dependsOnAttributeSymbol, testMethodAttributeSymbol, testClassAttributeSymbol);
            context.RegisterSymbolAction(context => AnalyzeMethod(context, symbols), SymbolKind.Method);
            context.RegisterSymbolAction(context => AnalyzeNamedType(context, symbols), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, AnalysisSymbols symbols)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        List<AttributeData> attributes = GetDependsOnAttributes(typeSymbol, symbols);
        if (attributes.Count == 0)
        {
            return;
        }

        // '[DependsOn]' is not inherited (AttributeUsage(Inherited = false)) and is only read off the test
        // class itself, so an application on a type that is not a test class - including a shared base class
        // whose derived types are the test classes - never produces an edge.
        if (!typeSymbol.IsTestClass(symbols.TestClassAttribute))
        {
            ReportNoEffect(context, attributes, typeSymbol.Name);
            return;
        }

        foreach (AttributeData attribute in attributes)
        {
            AnalyzeTarget(context, attribute, symbols, declaringClass: typeSymbol, declaringMethod: null);
        }
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, AnalysisSymbols symbols)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        List<AttributeData> attributes = GetDependsOnAttributes(methodSymbol, symbols);

        bool isTestMethod = methodSymbol.IsTestMethod(symbols.TestMethodAttribute);
        if (!isTestMethod)
        {
            ReportNoEffect(context, attributes, methodSymbol.Name);
            return;
        }

        INamedTypeSymbol declaringClass = methodSymbol.ContainingType;
        List<AttributeData> classAttributes = GetDependsOnAttributes(declaringClass, symbols);
        if (attributes.Count == 0 && classAttributes.Count == 0)
        {
            return;
        }

        bool selfReferenceReported = false;
        foreach (AttributeData attribute in attributes)
        {
            selfReferenceReported |= AnalyzeTarget(context, attribute, symbols, declaringClass, methodSymbol);
        }

        // A self reference is the one-node cycle; reporting it again as a cycle would be noise. Cycles are
        // only modelled for tests of a test class, because for any other declaring type the class the edge
        // resolves against is not known at compile time.
        if (selfReferenceReported || !declaringClass.IsTestClass(symbols.TestClassAttribute))
        {
            return;
        }

        AnalyzeCycle(context, symbols, new TestNode(declaringClass, methodSymbol.Name));
    }

    /// <summary>
    /// Validates one <c>[DependsOn]</c> application against the members it names.
    /// </summary>
    /// <returns><see langword="true"/> when the application was reported as a self reference.</returns>
    private static bool AnalyzeTarget(
        SymbolAnalysisContext context,
        AttributeData attribute,
        AnalysisSymbols symbols,
        INamedTypeSymbol declaringClass,
        IMethodSymbol? declaringMethod)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not { } attributeSyntax)
        {
            return false;
        }

        (INamedTypeSymbol? explicitTargetClass, string? targetMethodName, bool isMalformed) = ReadTarget(attribute);
        if (isMalformed)
        {
            return false;
        }

        // An implicit target ("a method of my own class") is resolved against the class the test runs under.
        // For a test method declared on a non-test base class that class is each derived test class, which is
        // not knowable from this declaration, so only explicit 'typeof' targets are validated there.
        INamedTypeSymbol? targetClass = explicitTargetClass;
        if (targetClass is null)
        {
            if (!declaringClass.IsTestClass(symbols.TestClassAttribute))
            {
                return false;
            }

            targetClass = declaringClass;
        }

        if (targetClass.TypeKind == TypeKind.Error)
        {
            return false;
        }

        // Discovery resolves a dependency against the tests of one test source, and stores a 'typeof' target
        // as its CLR full name. A type from another assembly therefore matches nothing, however well-formed
        // the reference is - so this is reported before the "is it a test class" question, which is moot.
        if (!IsFromCurrentAssembly(context, targetClass))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(OtherAssemblyRule, targetClass.Name));
            return false;
        }

        if (!targetClass.IsTestClass(symbols.TestClassAttribute))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(NotATestClassRule, targetClass.Name));
            return false;
        }

        if (targetMethodName is null)
        {
            // A whole-class target on a test class always matches something (or the class has no test at
            // all, which MSTEST0016 already reports).
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetMethodName))
        {
            // The attribute constructor throws for these, so there is nothing useful to add.
            return false;
        }

        MethodLookup lookup = LookupMethods(targetClass, targetMethodName, symbols);
        if (!lookup.Exists)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MethodNotFoundRule, targetClass.Name, targetMethodName));
            return false;
        }

        if (!lookup.IsTestMethod)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(NotATestMethodRule, targetClass.Name, targetMethodName));
            return false;
        }

        // Only a reference written directly on the method is a self reference. The same reference written on
        // the class means "every *other* test waits for this one", and discovery drops the generated
        // self-edge.
        if (declaringMethod is not null
            && string.Equals(targetMethodName, declaringMethod.Name, StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(targetClass, declaringClass))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(SelfReferenceRule, declaringMethod.Name));
            return true;
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
        foreach (AttributeData attribute in GetDependsOnAttributes(node.TestClass, symbols))
        {
            foreach ((TestNode target, AttributeData source) in ExpandTarget(context, symbols, node, attribute, isClassLevel: true))
            {
                yield return (target, source);
            }
        }

        foreach (IMethodSymbol method in EnumerateMethods(node.TestClass, node.MethodName))
        {
            if (!method.IsTestMethod(symbols.TestMethodAttribute))
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

        (INamedTypeSymbol? explicitTargetClass, string? targetMethodName, bool isMalformed) = ReadTarget(attribute);
        if (isMalformed)
        {
            yield break;
        }

        INamedTypeSymbol targetClass = explicitTargetClass ?? node.TestClass;
        if (targetClass.TypeKind == TypeKind.Error
            || !IsFromCurrentAssembly(context, targetClass)
            || !targetClass.IsTestClass(symbols.TestClassAttribute))
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

        if (string.IsNullOrWhiteSpace(targetMethodName) || !LookupMethods(targetClass, targetMethodName, symbols).IsTestMethod)
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
    private static (INamedTypeSymbol? TargetClass, string? TargetMethodName, bool IsMalformed) ReadTarget(AttributeData attribute)
    {
        INamedTypeSymbol? targetClass = null;
        string? targetMethodName = null;
        foreach (TypedConstant argument in attribute.ConstructorArguments)
        {
            switch (argument)
            {
                case { Kind: TypedConstantKind.Type, Value: INamedTypeSymbol type }:
                    targetClass = type;
                    break;

                case { Kind: TypedConstantKind.Type }:
                    // typeof(int[]), typeof(T), ... - not something that can carry tests, and not something
                    // this analyzer models.
                    return (null, null, true);

                case { Kind: TypedConstantKind.Primitive, Value: string name }:
                    targetMethodName = name;
                    break;
            }
        }

        return (targetClass, targetMethodName, targetClass is null && targetMethodName is null);
    }

    private static bool IsFromCurrentAssembly(SymbolAnalysisContext context, INamedTypeSymbol type)
        => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, context.Compilation.Assembly);

    private static MethodLookup LookupMethods(INamedTypeSymbol targetClass, string methodName, AnalysisSymbols symbols)
    {
        bool exists = false;
        foreach (IMethodSymbol method in EnumerateMethods(targetClass, methodName))
        {
            exists = true;

            // The name is what the run-time graph matches on, so any overload (or any declaration of that
            // name in the hierarchy) being a test is enough for the reference to resolve.
            if (method.IsTestMethod(symbols.TestMethodAttribute))
            {
                return new MethodLookup(Exists: true, IsTestMethod: true);
            }
        }

        return new MethodLookup(exists, IsTestMethod: false);
    }

    /// <summary>
    /// Enumerates the methods named <paramref name="methodName"/> declared by <paramref name="type"/> or any
    /// of its base types: a test method declared on a base class runs as a test of the derived test class, so
    /// the hierarchy is part of the lookup.
    /// </summary>
    private static IEnumerable<IMethodSymbol> EnumerateMethods(INamedTypeSymbol type, string methodName)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(methodName))
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } method)
                {
                    yield return method;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateTestMethodNames(INamedTypeSymbol type, AnalysisSymbols symbols)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                    && method.IsTestMethod(symbols.TestMethodAttribute)
                    && seen.Add(method.Name))
                {
                    yield return method.Name;
                }
            }
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

    private static void ReportNoEffect(SymbolAnalysisContext context, List<AttributeData> attributes, string memberName)
    {
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is { } attributeSyntax)
            {
                context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(NoEffectRule, memberName));
            }
        }
    }

    private readonly record struct MethodLookup(bool Exists, bool IsTestMethod);

    private sealed record AnalysisSymbols(
        INamedTypeSymbol DependsOnAttribute,
        INamedTypeSymbol TestMethodAttribute,
        INamedTypeSymbol TestClassAttribute);

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
