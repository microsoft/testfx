// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

public sealed partial class DependsOnShouldBeValidAnalyzer
{
    /// <summary>
    /// Caps the dependency walk so that a pathological graph cannot make the analyzer quadratic. Real
    /// dependency graphs are tiny; a graph larger than this simply does not get its cycles reported at
    /// build time, and the runtime still reports them.
    /// </summary>
    private const int MaxVisitedNodes = 512;

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
