// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AnalyzerResidualHelperTests
{
    [TestMethod]
    [DataRow(1, false)]
    [DataRow(1, true)]
    [DataRow(2, false)]
    [DataRow(2, true)]
    public async Task ParenthesizedOperations_PreserveParameterIdentityThroughBuiltInConversions(int depth, bool converted)
    {
        const string source = """
            public class Sample
            {
                public void Inspect(int first, int second)
                {
                    object converted = (object)first;
                    int same = first;
                    int different = second;
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        IOperation Initializer(string name) => model.GetOperation(root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Single(variable => variable.Identifier.ValueText == name).Initializer!.Value)!;

        IOperation operand = Initializer(converted ? "converted" : "same");
        IOperation same = Initializer("same");
        IOperation different = Initializer("different");
        for (int level = 0; level < depth; level++)
        {
            operand = ProductReflection.Parenthesize(operand, model);
        }

        Type extensions = ProductReflection.Type(false, "MSTest.Analyzers.RoslynAnalyzerHelpers.IOperationExtensions");
        var referenced = (ISymbol)ProductReflection.Call(extensions, null, "GetReferencedMemberOrLocalOrParameter", [typeof(IOperation)], operand)!;
        IParameterSymbol expected = compilation.GetTypeByMetadataName("Sample")!.GetMembers("Inspect").OfType<IMethodSymbol>().Single().Parameters[0];
        Assert.IsTrue(SymbolEqualityComparer.Default.Equals(expected, referenced));
        Assert.IsTrue((bool)ProductReflection.Call(extensions, null, "IsEquivalentReferenceTo", [typeof(IOperation), typeof(IOperation)], operand, same)!);
        Assert.IsTrue((bool)ProductReflection.Call(extensions, null, "IsEquivalentReferenceTo", [typeof(IOperation), typeof(IOperation)], same, operand)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsEquivalentReferenceTo", [typeof(IOperation), typeof(IOperation)], operand, different)!);
    }

    [TestMethod]
    public async Task EquivalentReferences_DistinguishInstancesSymbolsIndexersAndSideEffects()
    {
        const string source = """
            public class Sample
            {
                public int Field;
                public static int StaticField;
                public int Property => Field;
                public int this[int index] => Field;
                public int Read() => Field;
                public static implicit operator int(Sample value) => value.Field;

                public void Inspect(Sample other, int first, int second)
                {
                    int local = 17;
                    int otherLocal = 17;
                    int localFirst = local;
                    int localAgain = local;
                    int localDifferent = otherLocal;
                    int parameterFirst = first;
                    int parameterAgain = first;
                    int parameterDifferent = second;
                    int field = Field;
                    int fieldAgain = this.Field;
                    int fieldDifferent = other.Field;
                    int staticField = StaticField;
                    int staticFieldAgain = Sample.StaticField;
                    int property = Property;
                    int propertyAgain = this.Property;
                    int propertyDifferent = other.Property;
                    int indexer = this[0];
                    int indexerAgain = this[0];
                    int call = Read();
                    int callAgain = Read();
                    int conversion = (int)this;
                    Sample self = this;
                    Sample selfAgain = this;
                    Sample different = other;
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        var operations = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(variable => variable.Initializer is not null)
            .ToDictionary(variable => variable.Identifier.ValueText, variable => model.GetOperation(variable.Initializer!.Value)!, StringComparer.Ordinal);
        Type extensions = ProductReflection.Type(false, "MSTest.Analyzers.RoslynAnalyzerHelpers.IOperationExtensions");
        (string Left, string Right, bool Expected)[] cases =
        [
            ("localFirst", "localAgain", true),
            ("localFirst", "localDifferent", false),
            ("parameterFirst", "parameterAgain", true),
            ("parameterFirst", "parameterDifferent", false),
            ("field", "fieldAgain", true),
            ("field", "fieldDifferent", false),
            ("staticField", "staticFieldAgain", true),
            ("field", "staticField", false),
            ("property", "propertyAgain", true),
            ("property", "propertyDifferent", false),
            ("property", "field", false),
            ("indexer", "indexerAgain", false),
            ("call", "callAgain", false),
            ("conversion", "conversion", false),
            ("self", "selfAgain", true),
            ("self", "different", false),
            ("local", "otherLocal", false),
        ];
        foreach ((string left, string right, bool expected) in cases)
        {
            Assert.AreEqual(expected, ProductReflection.Call(extensions, null, "IsEquivalentReferenceTo",
                [typeof(IOperation), typeof(IOperation)], operations[left], operations[right]), $"{left} versus {right}");
        }

        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsEquivalentReferenceTo",
            [typeof(IOperation), typeof(IOperation)], null, operations["field"])!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsEquivalentReferenceTo",
            [typeof(IOperation), typeof(IOperation)], operations["field"], null)!);
        Assert.IsNull(ProductReflection.Call(extensions, null, "GetReferencedMemberOrLocalOrParameter", [typeof(IOperation)], [null]));
        IConversionOperation conversion = Assert.IsInstanceOfType<IConversionOperation>(operations["conversion"]);
        Assert.IsTrue(conversion.Conversion.IsUserDefined);
        Assert.AreSame(conversion, ProductReflection.Call(extensions, null, "WalkDownBuiltInConversion", [typeof(IOperation)], conversion));
        Assert.AreSame(conversion.Operand, ProductReflection.Call(extensions, null, "WalkDownConversion", [typeof(IOperation)], conversion));
    }

    [TestMethod]
    public async Task SkipGuard_EmptyMissingAndValueReturningBodies_AreNotPureSkips()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Sample
            {
                public void Empty(bool flag) { if (flag) { } }
                public void Return(bool flag) { if (flag) return; }
                public int Value(bool flag) { if (flag) return 17; return 29; }
                public void Inconclusive(bool flag) { if (flag) Assert.Inconclusive("skip"); }
                public void Later(bool flag) { Read(); if (flag) return; }
                private void Read() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        INamedTypeSymbol assert = compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.Assert")!;
        Type helper = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.ConditionGuardHelper");
        foreach ((string name, bool expected) in new[] { ("Empty", false), ("Return", true), ("Value", false), ("Inconclusive", true), ("Later", false) })
        {
            MethodDeclarationSyntax method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(candidate => candidate.Identifier.ValueText == name);
            IConditionalOperation conditional = Assert.IsInstanceOfType<IConditionalOperation>(model.GetOperation(method.DescendantNodes().OfType<IfStatementSyntax>().Single()));
            IBlockOperation body = Assert.IsInstanceOfType<IBlockOperation>(model.GetOperation(method.Body!));
            Assert.AreEqual(expected, ProductReflection.Call(helper, null, "IsSkipGuard",
                [typeof(IConditionalOperation), typeof(IBlockOperation), typeof(INamedTypeSymbol)], conditional, body, assert), name);
        }

        Assert.IsFalse((bool)ProductReflection.Call(helper, null, "IsEarlyReturnOrAssertInconclusive",
            [typeof(IOperation), typeof(INamedTypeSymbol)], null, assert)!);
    }

    [TestMethod]
    public async Task EqualityAndFixtureHelpers_PrimitiveEqualityAndUnknownParameterModes_AreExplicit()
    {
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, "public class Sample { public void Inspect() { } }", includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        Type equality = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.AssertConditionAnalyzerHelper");
        foreach (SpecialType specialType in new[] { SpecialType.System_Boolean, SpecialType.System_Int32, SpecialType.System_String })
        {
            Assert.IsFalse(
                (bool)ProductReflection.Call(equality, null, "HasUserDefinedEquality",
                [typeof(ITypeSymbol)], compilation.GetSpecialType(specialType))!, specialType.ToString());
        }

        Type fixtures = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.FixtureUtils");
        Type mode = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.FixtureParameterMode");
        IMethodSymbol method = compilation.GetTypeByMetadataName("Sample")!.GetMembers("Inspect").OfType<IMethodSymbol>().Single();
        Exception exception = Assert.Throws<Exception>(() => ProductReflection.Call(fixtures, null, "HasCorrectParameters",
            [typeof(IMethodSymbol), mode, typeof(INamedTypeSymbol)], method, Enum.ToObject(mode, 731), null));
        Assert.AreEqual(ProductReflection.Type(false, "System.Diagnostics.UnreachableException"), exception.GetType());
        StringAssert.StartsWith(exception.Message, "This program location is thought to be unreachable. File='");
        StringAssert.Contains(exception.Message, "FixtureUtils.cs' Line=");
    }

    [TestMethod]
    public async Task InterfaceHelpers_ConstructedContractsAndMissingMembers_PreserveTheImplementingSymbol()
    {
        const string source = """
            public interface IContract<T> { void Apply(T value); }
            public sealed class Sample : IContract<int>
            {
                public void Apply(int value) { }
                public void Other(int value) { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("Sample")!;
        INamedTypeSymbol contract = compilation.GetTypeByMetadataName("IContract`1")!;
        ITypeSymbol integer = compilation.GetSpecialType(SpecialType.System_Int32);
        IMethodSymbol apply = type.GetMembers("Apply").OfType<IMethodSymbol>().Single();
        IMethodSymbol other = type.GetMembers("Other").OfType<IMethodSymbol>().Single();
        ISymbol interfaceMember = contract.Construct(integer).GetMembers("Apply").Single();
        Type extensions = ProductReflection.Type(false, "Analyzer.Utilities.Extensions.IMethodSymbolExtensions");
        Assert.IsTrue((bool)ProductReflection.Call(extensions, null, "IsImplementationOfAnyInterfaceMember", [typeof(ISymbol)], apply)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsImplementationOfAnyInterfaceMember", [typeof(ISymbol)], other)!);
        Assert.IsTrue((bool)ProductReflection.Call(extensions, null, "IsImplementationOfInterfaceMember", [typeof(ISymbol), typeof(ISymbol)], apply, interfaceMember)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsImplementationOfInterfaceMember", [typeof(ISymbol), typeof(ISymbol)], other, interfaceMember)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsImplementationOfInterfaceMember", [typeof(ISymbol), typeof(ISymbol)], apply, null)!);
        foreach ((ITypeSymbol? argument, INamedTypeSymbol? interfaceType, string name, bool expected) in new (ITypeSymbol?, INamedTypeSymbol?, string, bool)[]
        {
            (integer, contract, "Apply", true),
            (null, contract.Construct(integer), "Apply", true),
            (integer, contract, "Missing", false),
            (integer, null, "Apply", false),
            (null, null, "Apply", false),
        })
        {
            Assert.AreEqual(expected, ProductReflection.Call(extensions, null, "IsImplementationOfInterfaceMethod",
                [typeof(IMethodSymbol), typeof(ITypeSymbol), typeof(INamedTypeSymbol), typeof(string)], apply, argument, interfaceType, name));
        }
    }

    [TestMethod]
    public async Task ParallelSafetyHelpers_ResolveNestedBodiesWithoutInventingMethodOrOptOutMetadata()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Sample
            {
                public int Property => 17;
                [TestMethod]
                public void Test()
                {
                    void Local() { }
                    System.Action lambda = () => { };
                }
                [TestInitialize]
                public void Initialize() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("Sample")!;
        IMethodSymbol test = type.GetMembers("Test").OfType<IMethodSymbol>().Single();
        IMethodSymbol initialize = type.GetMembers("Initialize").OfType<IMethodSymbol>().Single();
        IMethodSymbol getter = type.GetMembers("Property").OfType<IPropertySymbol>().Single().GetMethod!;
        IMethodSymbol local = Assert.IsInstanceOfType<IMethodSymbol>(
            model.GetDeclaredSymbol(root.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Single()));
        IMethodSymbol lambda = Assert.IsInstanceOfType<IAnonymousFunctionOperation>(
            model.GetOperation(root.DescendantNodes().OfType<LambdaExpressionSyntax>().Single())).Symbol;
        INamedTypeSymbol testAttribute = compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute")!;
        INamedTypeSymbol fixtureAttribute = compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute")!;
        var fixtures = ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default, fixtureAttribute);
        Type helper = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.ParallelSafetyHelper");
        foreach ((ISymbol? symbol, IMethodSymbol? expected) in new (ISymbol?, IMethodSymbol?)[]
        {
            (local, test),
            (lambda, test),
            (initialize, initialize),
            (getter, null),
            (null, null),
        })
        {
            var actual = (IMethodSymbol?)ProductReflection.Call(helper, null, "GetEnclosingTestMethod",
                [typeof(ISymbol), typeof(ImmutableHashSet<INamedTypeSymbol>), typeof(INamedTypeSymbol)], symbol, fixtures, testAttribute);
            Assert.IsTrue(SymbolEqualityComparer.Default.Equals(expected, actual), symbol?.Name);
        }

        Assert.IsFalse((bool)ProductReflection.Call(helper, null, "IsOptedOutOfParallelization",
            [typeof(IMethodSymbol), typeof(INamedTypeSymbol), typeof(INamedTypeSymbol)], test, null, testAttribute)!);
        Assert.IsFalse((bool)ProductReflection.Call(helper, null, "IsTestMethod", [typeof(IMethodSymbol), typeof(INamedTypeSymbol)], test, null)!);
    }

    [TestMethod]
    public async Task WellKnownTypes_HiddenAndFriendDefinitions_DoNotHideOneUnambiguousVisibleType()
    {
        MetadataReference hidden = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            "namespace Duplicate { internal class Target { } }", "ResidualHidden");
        MetadataReference friend = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            """
            using System.Runtime.CompilerServices;
            [assembly: InternalsVisibleTo("ContinuationFixture")]
            namespace Duplicate { internal class Target { } }
            """, "ResidualFriend");
        MetadataReference visible = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            "namespace Duplicate { public class Target { } }", "ResidualVisible");
        (MetadataReference[] References, string? ExpectedAssembly, Accessibility ExpectedAccessibility)[] cases =
        [
            ([hidden, visible], "ResidualVisible", Accessibility.Public),
            ([hidden, friend], "ResidualFriend", Accessibility.Internal),
            ([hidden], null, Accessibility.NotApplicable),
            ([friend, visible], null, Accessibility.NotApplicable),
        ];
        Type type = ProductReflection.Type(false, "Analyzer.Utilities.WellKnownTypeProvider");
        foreach ((MetadataReference[] references, string? expectedAssembly, Accessibility expectedAccessibility) in cases)
        {
            using var workspace = new AdhocWorkspace();
            Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(
                workspace, "public class Consumer { }", includeMSTest: false, additionalReferences: references);
            Compilation compilation = (await document.Project.GetCompilationAsync())!;
            AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
            object provider = ProductReflection.Call(type, null, "GetOrCreate", [typeof(Compilation)], compilation)!;
            object? actual = ProductReflection.Call(type, provider, "GetOrCreateTypeByMetadataName", [typeof(string)], "Duplicate.Target");
            if (expectedAssembly is null)
            {
                Assert.IsNull(actual);
            }
            else
            {
                INamedTypeSymbol symbol = Assert.IsInstanceOfType<INamedTypeSymbol>(actual);
                Assert.AreEqual(expectedAssembly, symbol.ContainingAssembly.Name);
                Assert.AreEqual(expectedAccessibility, symbol.DeclaredAccessibility);
            }

            Assert.AreSame(actual, ProductReflection.Call(type, provider, "GetOrCreateTypeByMetadataName", [typeof(string)], "Duplicate.Target"));
        }
    }

    [TestMethod]
    public async Task WellKnownTypes_NonInternedSourceName_ResolvesAndCachesTheDeclaredSymbol()
    {
        string namespaceName = "Residual_" + Guid.NewGuid().ToString("N");
        string metadataName = namespaceName + ".Target";
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(
            workspace, $"namespace {namespaceName} {{ public class Target {{ }} }}", includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        INamedTypeSymbol expected = compilation.GetTypeByMetadataName(metadataName)!;
        Type type = ProductReflection.Type(false, "Analyzer.Utilities.WellKnownTypeProvider");
        object provider = ProductReflection.Call(type, null, "GetOrCreate", [typeof(Compilation)], compilation)!;
        Assert.AreSame(expected, ProductReflection.Call(type, provider, "GetOrCreateTypeByMetadataName", [typeof(string)], metadataName));
        Assert.AreSame(expected, ProductReflection.Call(type, provider, "GetOrCreateTypeByMetadataName", [typeof(string)], metadataName));
        Assert.IsNull(ProductReflection.Call(type, provider, "GetOrCreateTypeByMetadataName", [typeof(string)], metadataName + "Missing"));
    }

    [TestMethod]
    public async Task EqualityArguments_MissingNamesAndNonReflexiveStaticTypes_DoNotInventSelfEquality()
    {
        const string source = """
            public static class Sample
            {
                private static void Compare(int expected, int actual) { }
                private static void OtherNames(int left, int right) { }
                private static void ExpectedOnly(int expected) { }
                private static void Generic<T>(T expected, T actual) { }
                public static void Inspect(int first, int second)
                {
                    Compare(first, first);
                    Compare(first, second);
                    OtherNames(first, first);
                    ExpectedOnly(first);
                    Generic<int>(first, first);
                    Generic<object>(first, first);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        IInvocationOperation[] invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(invocation => Assert.IsInstanceOfType<IInvocationOperation>(model.GetOperation(invocation)))
            .ToArray();
        bool[] same = [true, false, false, false, true, true];
        bool[] reflexive = [true, false, false, false, true, false];
        SpecialType?[] comparedTypes = [SpecialType.System_Int32, SpecialType.System_Int32, null, SpecialType.System_Int32, SpecialType.System_Int32, SpecialType.System_Object];
        Assert.HasCount(same.Length, invocations);
        Type helper = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.AssertConditionAnalyzerHelper");
        for (int index = 0; index < invocations.Length; index++)
        {
            IInvocationOperation invocation = invocations[index];
            Assert.AreEqual(same[index], ProductReflection.Call(helper, null, "HasIdenticalExpectedAndActual",
                [typeof(IInvocationOperation), typeof(string)], invocation, "expected"), invocation.Syntax.ToString());
            Assert.AreEqual(reflexive[index], ProductReflection.Call(helper, null, "HasIdenticalExpectedAndActualWithBuiltInEquality",
                [typeof(IInvocationOperation), typeof(string)], invocation, "expected"), invocation.Syntax.ToString());
            var comparedType = (ITypeSymbol?)ProductReflection.Call(helper, null, "GetComparedType",
                [typeof(IInvocationOperation), typeof(string)], invocation, "expected");
            Assert.AreEqual(comparedTypes[index], comparedType?.SpecialType, invocation.Syntax.ToString());
        }
    }
}
