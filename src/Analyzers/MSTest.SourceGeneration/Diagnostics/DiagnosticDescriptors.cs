// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Diagnostics;

/// <summary>
/// Catalogue of <see cref="DiagnosticDescriptor"/> values surfaced by the AOT reflection
/// source generator when it encounters a <c>[TestClass]</c> shape it cannot materialize.
/// Each id is registered in <c>AnalyzerReleases.Unshipped.md</c>.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "MSTest.AotReflection";

    public static readonly DiagnosticDescriptor StaticTestClass = new(
        id: "AOTSG0001",
        title: "Test class is static",
        messageFormat: "[TestClass] type '{0}' is static and cannot be instantiated by the generated registry",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Static test classes cannot be instantiated by the generated registry. Remove the 'static' modifier or use a non-static container.");

    public static readonly DiagnosticDescriptor GenericTestClass = new(
        id: "AOTSG0002",
        title: "Test class is generic",
        messageFormat: "[TestClass] type '{0}' has unbound type parameters (either directly or via a generic outer type) and cannot be materialized as a closed type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Open generic test classes (or test classes nested inside a generic outer type) have no closed type at generation time. Use a concrete subclass that closes every type parameter.");

    public static readonly DiagnosticDescriptor InaccessibleTestClass = new(
        id: "AOTSG0003",
        title: "Test class is not accessible from generated code",
        messageFormat: "[TestClass] type '{0}' is not reachable from generated code in the same assembly (file-local, or nested in a private/private-protected outer type)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Generated registry code lives in the same assembly but in a different file/type and therefore cannot reference file-local types or types nested in a private (or private-protected) outer type. Make the test class — and every enclosing type — at least internal.");

    public static readonly DiagnosticDescriptor GenericTestMethod = new(
        id: "AOTSG0004",
        title: "Test method is generic",
        messageFormat: "Method '{0}.{1}' has type parameters which are not knowable at compile time; the source-generated invoker will skip it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Generic test methods would need a concrete type-argument list at the invocation site. Replace the method with one or more non-generic specializations.");

    public static readonly DiagnosticDescriptor ByRefParameter = new(
        id: "AOTSG0005",
        title: "Parameter uses a by-ref kind",
        messageFormat: "Parameter '{2}' of '{0}.{1}' is declared with 'ref', 'in', or 'out' and cannot be passed through the 'object?[]' invoker; the member will be skipped",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "By-ref parameters cannot flow through the 'Func<object?, object?[]?, Task>' invoker shape. Drop the ref/in/out modifier or move the dependency out of the test signature.");

    private static readonly Dictionary<string, DiagnosticDescriptor> ById = new(StringComparer.Ordinal)
    {
        [StaticTestClass.Id] = StaticTestClass,
        [GenericTestClass.Id] = GenericTestClass,
        [InaccessibleTestClass.Id] = InaccessibleTestClass,
        [GenericTestMethod.Id] = GenericTestMethod,
        [ByRefParameter.Id] = ByRefParameter,
    };

    public static DiagnosticDescriptor GetById(string id)
        => ById.TryGetValue(id, out DiagnosticDescriptor? descriptor)
            ? descriptor
            : throw new InvalidOperationException($"Unknown diagnostic id '{id}'.");
}
