// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Helpers;

/// <summary>
/// Shared logic for the parallel-safety analyzers (MSTEST0074-MSTEST0077). These rules only describe a
/// live defect when in-assembly parallelization is actually enabled, and they only apply to code that
/// runs as part of an MSTest test.
/// </summary>
internal static class ParallelSafetyHelper
{
    /// <summary>
    /// The <c>.editorconfig</c> / global analyzer option that forces the parallel-safety rules to fire
    /// even when <c>[assembly: Parallelize]</c> is not syntactically present. Set it to <c>always</c> for
    /// suites that enable parallelization through a runsettings file or the <c>MSTestParallelizeScope</c>
    /// MSBuild property, neither of which the analyzer can observe.
    /// </summary>
    internal const string ConfigOptionKey = "mstest_parallel_safety_mode";

    /// <summary>
    /// Diagnostic property key carrying the <c>WellKnownResources</c> member name that the
    /// <c>AddResourceLockFixer</c> code fix should reference. Absent when no code fix should be offered.
    /// </summary>
    internal const string ResourceMemberPropertyKey = "ResourceMember";

    /// <summary>
    /// Diagnostic property key carrying where the <c>AddResourceLockFixer</c> code fix should place the
    /// <c>[ResourceLock]</c> attribute: <see cref="FixScopeMethod"/> for a test method, or
    /// <see cref="FixScopeClass"/> for a per-test/per-class fixture (whose lock only takes effect at class scope).
    /// Absent when no code fix should be offered.
    /// </summary>
    internal const string FixScopePropertyKey = "FixScope";

    /// <summary>The <see cref="FixScopePropertyKey"/> value meaning "add the lock to the enclosing test method".</summary>
    internal const string FixScopeMethod = "method";

    /// <summary>The <see cref="FixScopePropertyKey"/> value meaning "add the lock to the enclosing test class".</summary>
    internal const string FixScopeClass = "class";

    private const string AlwaysValue = "always";

    /// <summary>
    /// Determines whether the parallel-safety rules should produce diagnostics for this compilation. An
    /// analyzer cannot read runsettings and does not currently observe the <c>MSTestParallelizeScope</c>
    /// MSBuild property, so we fire when either the <c>mstest_parallel_safety_mode = always</c> opt-in is
    /// set, or <c>[assembly: Parallelize]</c> is present in source - but never when the assembly opts out of
    /// parallelization entirely with <c>[assembly: DoNotParallelize]</c>.
    /// </summary>
    internal static bool IsParallelizationInEffect(
        Compilation compilation,
        AnalyzerOptions options,
        INamedTypeSymbol? parallelizeAttributeSymbol,
        INamedTypeSymbol? doNotParallelizeAttributeSymbol)
    {
        // An assembly-level [assembly: DoNotParallelize] disables in-assembly parallelization entirely (the adapter
        // sets CanParallelizeAssembly = false), so no parallel-safety rule can describe a live defect - not even when
        // the mstest_parallel_safety_mode = always opt-in is set. Assembly opt-out therefore wins over every signal.
        bool assemblyOptsOut = doNotParallelizeAttributeSymbol is not null
            && AssemblyHasAttribute(compilation, doNotParallelizeAttributeSymbol);

        bool parallelizationRequested = IsAlwaysModeConfigured(options, compilation)
            || (parallelizeAttributeSymbol is not null && AssemblyHasAttribute(compilation, parallelizeAttributeSymbol));

        return !assemblyOptsOut && parallelizationRequested;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the <c>mstest_parallel_safety_mode</c> opt-in is set to <c>always</c>.
    /// The value is read from the global analyzer config first (a <c>.globalconfig</c> file or an MSBuild
    /// <c>build_property</c>), then from any syntax tree's <c>.editorconfig</c> options: a value set in an ordinary
    /// <c>[*.cs]</c> section is exposed per-tree rather than globally, and the opt-in is compilation-wide, so any
    /// single tree carrying it forces the rules on for the whole assembly.
    /// </summary>
    private static bool IsAlwaysModeConfigured(AnalyzerOptions options, Compilation compilation)
    {
        AnalyzerConfigOptionsProvider provider = options.AnalyzerConfigOptionsProvider;
        if (provider.GlobalOptions.TryGetValue(ConfigOptionKey, out string? globalMode)
            && string.Equals(globalMode, AlwaysValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            if (provider.GetOptions(tree).TryGetValue(ConfigOptionKey, out string? treeMode)
                && string.Equals(treeMode, AlwaysValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AssemblyHasAttribute(Compilation compilation, INamedTypeSymbol attributeSymbol)
        // Match derived attributes too: [assembly: Parallelize]/[DoNotParallelize] are non-sealed and the adapter
        // reads them reflectively (GetCustomAttributes(...).OfType<T>() / IsAttributeDefined<T>), which honors
        // subclasses. An exact-symbol comparison would miss a user-defined attribute that derives from them.
        => compilation.Assembly.GetAttributes().Any(attribute => attribute.AttributeClass.Inherits(attributeSymbol));

    /// <summary>
    /// Collects the attribute symbols that mark a method as MSTest "test code" that can run <em>concurrently with
    /// another test</em>: test methods themselves, the per-test and per-class fixtures that run inside a test's
    /// scheduling chunk, and the global per-test fixtures.
    /// <para>
    /// <c>[AssemblyInitialize]</c> and <c>[AssemblyCleanup]</c> are deliberately excluded. Assembly initialize is
    /// serialized behind <c>TestAssemblyInfo</c>'s <c>SemaphoreSlim(1, 1)</c> and every worker awaits it before
    /// running its test, and assembly cleanup runs only after the last runnable test in the whole assembly. A
    /// process-global mutation in either therefore cannot race a concurrent test, so reporting one would be a
    /// false positive - and these rules spend their credibility budget on false positives, not on silence.
    /// </para>
    /// </summary>
    internal static ImmutableHashSet<INamedTypeSymbol> GetFixtureAttributeSymbols(Compilation compilation)
    {
        ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestCleanupAttribute);
        return builder.ToImmutable();

        void AddIfPresent(string metadataName)
        {
            if (compilation.GetOrCreateTypeByMetadataName(metadataName) is { } symbol)
            {
                builder.Add(symbol);
            }
        }
    }

    /// <summary>
    /// Collects the attribute symbols for the <em>class-scoped</em> fixtures - <c>[TestInitialize]</c>,
    /// <c>[TestCleanup]</c>, <c>[ClassInitialize]</c>, <c>[ClassCleanup]</c>. A <c>[ResourceLock]</c> declared on the
    /// test class covers all of these (every test method in the class inherits the class lock and its fixtures run
    /// within those tests), whereas a lock declared on the fixture method itself is ignored by discovery, which reads
    /// resource locks only from the test class and the test method.
    /// </summary>
    internal static ImmutableHashSet<INamedTypeSymbol> GetClassScopedFixtureAttributeSymbols(Compilation compilation)
    {
        ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute);
        return builder.ToImmutable();

        void AddIfPresent(string metadataName)
        {
            if (compilation.GetOrCreateTypeByMetadataName(metadataName) is { } symbol)
            {
                builder.Add(symbol);
            }
        }
    }

    /// <summary>
    /// Decides where the <c>AddResourceLockFixer</c> code fix should place the <c>[ResourceLock]</c> attribute for a
    /// mutation inside <paramref name="testMethod"/>: <see cref="FixScopeMethod"/> when it is a test method, or
    /// <see cref="FixScopeClass"/> when it is a class-scoped fixture (whose method-level lock would be ignored by
    /// discovery). Returns <see langword="null"/> for the global per-test fixtures - there is no effective lock
    /// target for those, so no fix should be offered rather than a fix that silently does nothing at runtime.
    /// </summary>
    internal static string? GetResourceLockFixScope(
        IMethodSymbol testMethod,
        INamedTypeSymbol? testMethodAttributeSymbol,
        ImmutableHashSet<INamedTypeSymbol> classScopedFixtureAttributeSymbols)
    {
        foreach (AttributeData attribute in testMethod.GetAttributes())
        {
            INamedTypeSymbol? attributeClass = attribute.AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            if (testMethodAttributeSymbol is not null && attributeClass.Inherits(testMethodAttributeSymbol))
            {
                return FixScopeMethod;
            }

            if (classScopedFixtureAttributeSymbols.Contains(attributeClass))
            {
                return FixScopeClass;
            }
        }

        return null;
    }

    /// <summary>
    /// Walks up through local functions / lambdas to the enclosing user-declared method and returns it when
    /// that method is an MSTest test method (including inheritors of <c>[TestMethod]</c>) or one of the
    /// fixture methods in <paramref name="fixtureAttributeSymbols"/>. Returns <see langword="null"/> when the
    /// operation is not inside test code, so callers stay silent on production code and on non-MSTest
    /// frameworks (for example the internal <c>TestContainer</c> framework, whose methods carry no MSTest
    /// attributes).
    /// </summary>
    internal static IMethodSymbol? GetEnclosingTestMethod(
        ISymbol? containingSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols,
        INamedTypeSymbol? testMethodAttributeSymbol)
    {
        ISymbol? current = containingSymbol;
        while (current is IMethodSymbol method)
        {
            foreach (AttributeData attribute in method.GetAttributes())
            {
                INamedTypeSymbol? attributeClass = attribute.AttributeClass;
                if (attributeClass is null)
                {
                    continue;
                }

                if (fixtureAttributeSymbols.Contains(attributeClass))
                {
                    return method;
                }

                if (testMethodAttributeSymbol is not null && attributeClass.Inherits(testMethodAttributeSymbol))
                {
                    return method;
                }
            }

            // Only keep walking when the current symbol is synthesized from a local function or lambda body;
            // otherwise a nested non-test method should not inherit its container's test-ness.
            if (method.MethodKind is MethodKind.LocalFunction or MethodKind.AnonymousFunction)
            {
                current = method.ContainingSymbol;
                continue;
            }

            return null;
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the test <paramref name="method"/> or any of its (possibly base)
    /// containing types opts out of parallelization with <c>[DoNotParallelize]</c>. Such tests run in the
    /// sequential phase, so their process-global mutations cannot race a sibling and must not be flagged.
    /// A method-level <c>[DoNotParallelize]</c> is honored by discovery only on an actual test method - on a
    /// fixture method it has no runtime effect - so <paramref name="testMethodAttributeSymbol"/> is used to
    /// restrict the method-level check to real test methods; fixtures rely on a class/assembly-level opt-out.
    /// </summary>
    internal static bool IsOptedOutOfParallelization(
        IMethodSymbol method,
        INamedTypeSymbol? doNotParallelizeAttributeSymbol,
        INamedTypeSymbol? testMethodAttributeSymbol)
    {
        if (doNotParallelizeAttributeSymbol is null)
        {
            return false;
        }

        // TypeEnumerator builds each test method's DoNotParallelize flag from the method's own attribute, but a
        // fixture method ([TestInitialize]/[TestCleanup]/[ClassInitialize]/[ClassCleanup]/...) never goes through
        // that path, so a [DoNotParallelize] sitting on a fixture is ignored at runtime and must not silence a live
        // mutation there. Only class/assembly-level opt-out (the base-type walk below) protects a fixture.
        if (IsTestMethod(method, testMethodAttributeSymbol) && MethodHasInheritedAttribute(method, doNotParallelizeAttributeSymbol))
        {
            return true;
        }

        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.BaseType)
        {
            if (HasAttribute(type, doNotParallelizeAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the test <paramref name="method"/> or any of its (possibly base)
    /// containing types already declares a <c>[ResourceLock]</c> whose resource key equals
    /// <paramref name="resourceKey"/> (ordinal, case-sensitive - matching the runtime's conflict rule). Such
    /// a mutation is coordinated and must not be flagged by the "undeclared mutation" rules.
    /// A method-level <c>[ResourceLock]</c> is read by discovery only from an actual test method - on a fixture
    /// method it has no runtime effect - so <paramref name="testMethodAttributeSymbol"/> restricts the
    /// method-level check to real test methods; a fixture is covered only by a class-level lock.
    /// </summary>
    internal static bool HasResourceLockFor(
        IMethodSymbol method,
        INamedTypeSymbol? resourceLockAttributeSymbol,
        INamedTypeSymbol? testMethodAttributeSymbol,
        string resourceKey)
    {
        if (resourceLockAttributeSymbol is null)
        {
            return false;
        }

        // TypeEnumerator merges the class locks with the locks read from the test method itself
        // (MergeResourceLocks(GetClassResourceLocks(), ReadResourceLocks(method))), and only ever does so while
        // building a UnitTestElement for a discovered test method. A [ResourceLock] on a fixture method is
        // therefore never read at runtime and must not suppress a live mutation there.
        if (IsTestMethod(method, testMethodAttributeSymbol)
            && MethodDeclaresInheritedResourceLock(method, resourceLockAttributeSymbol, resourceKey))
        {
            return true;
        }

        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.BaseType)
        {
            if (DeclaresResourceLock(type, resourceLockAttributeSymbol, resourceKey))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the test <paramref name="method"/> or any of its (possibly base)
    /// containing types declares any <c>[ResourceLock]</c>. Used by rules whose resource has no well-known key
    /// (for example culture): the presence of any explicit lock is treated as the author having coordinated
    /// access, so the rule stays silent rather than guessing which custom key maps to the resource.
    /// As with <see cref="HasResourceLockFor"/>, a method-level lock counts only on an actual test method.
    /// </summary>
    internal static bool HasAnyResourceLock(
        IMethodSymbol method,
        INamedTypeSymbol? resourceLockAttributeSymbol,
        INamedTypeSymbol? testMethodAttributeSymbol)
    {
        if (resourceLockAttributeSymbol is null)
        {
            return false;
        }

        if (IsTestMethod(method, testMethodAttributeSymbol)
            && MethodHasInheritedAttribute(method, resourceLockAttributeSymbol))
        {
            return true;
        }

        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.BaseType)
        {
            if (HasAttribute(type, resourceLockAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="method"/> carries <c>[TestMethod]</c> (or a derived
    /// attribute), i.e. it is a method discovery treats as a real test rather than a fixture.
    /// <para>
    /// This deliberately does <em>not</em> walk the <c>OverriddenMethod</c> chain, unlike
    /// <see cref="MethodHasInheritedAttribute"/>. <c>TestMethodAttribute</c> is declared
    /// <c>[AttributeUsage(..., Inherited = false)]</c>, so the runtime's
    /// <c>GetCustomAttributes(..., inherit: true)</c> does not surface a base method's <c>[TestMethod]</c> on an
    /// override. Walking here would classify a plain override of a base test method as a test method when the
    /// runtime does not - a false positive, which is the direction these rules must never take.
    /// </para>
    /// </summary>
    private static bool IsTestMethod(IMethodSymbol method, INamedTypeSymbol? testMethodAttributeSymbol)
        => testMethodAttributeSymbol is not null && HasAttribute(method, testMethodAttributeSymbol);

    /// <summary>
    /// Method-level counterpart of <see cref="HasAttribute"/> for attributes declared <c>Inherited = true</c>
    /// (<c>[DoNotParallelize]</c> and <c>[ResourceLock]</c>). Runtime discovery reads member attributes through
    /// <c>ReflectionOperations.GetCustomAttributes(memberInfo)</c>, which passes <c>inherit: true</c>, so an
    /// override inherits such an attribute from the method it overrides. Inspecting only the Roslyn symbol's own
    /// attributes would miss that and report a mutation the runtime has already opted out of or locked.
    /// </summary>
    private static bool MethodHasInheritedAttribute(IMethodSymbol method, INamedTypeSymbol attributeSymbol)
    {
        for (IMethodSymbol? current = method; current is not null; current = current.OverriddenMethod)
        {
            if (HasAttribute(current, attributeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <see cref="DeclaresResourceLock"/> over a method and the chain of methods it overrides.
    /// <c>ResourceLockAttribute</c> is declared <c>Inherited = true</c>, so a lock on an overridden method still
    /// applies to the override at runtime.
    /// </summary>
    private static bool MethodDeclaresInheritedResourceLock(IMethodSymbol method, INamedTypeSymbol resourceLockAttributeSymbol, string resourceKey)
    {
        for (IMethodSymbol? current = method; current is not null; current = current.OverriddenMethod)
        {
            if (DeclaresResourceLock(current, resourceLockAttributeSymbol, resourceKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DeclaresResourceLock(ISymbol symbol, INamedTypeSymbol resourceLockAttributeSymbol, string resourceKey)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, resourceLockAttributeSymbol))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is string declaredKey
                && string.Equals(declaredKey, resourceKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
        // Inheritance-aware to mirror the adapter's reflective attribute lookups, which honor subclasses of the
        // MSTest attributes ([DoNotParallelize], [ResourceLock]). For a sealed attribute this is exactly an
        // identity comparison, so it is never less precise than the previous exact-symbol check.
        => symbol.GetAttributes().Any(attribute => attribute.AttributeClass.Inherits(attributeSymbol));
}
