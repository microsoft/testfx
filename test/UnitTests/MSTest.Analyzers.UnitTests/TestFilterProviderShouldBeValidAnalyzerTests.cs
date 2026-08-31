// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestFilterProviderShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestFilterProviderShouldBeValidAnalyzerTests
{
    // The programmatic test-filter API is [Experimental], which the compiler reports as an error unless
    // suppressed. The repo .editorconfig does that for repo sources, but the analyzer test harness compiles
    // these snippets in-memory where no .editorconfig applies, so every snippet suppresses it explicitly.
    private const string Header = """
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        #pragma warning disable MSTESTEXP

        """;

    // Every _NoDiagnostic test in this class -- including the generic-provider ones under #if NET further
    // down -- is deliberately negative: they pin that valid shapes, unrelated attributes and
    // compiler-rejected applications are left alone. Individually a "silent analyzer" mutation would
    // survive them, which is exactly what the paired _Diagnostic tests are for: every branch asserted as
    // silent here has a companion asserting the exact rule, location and arguments when that branch should
    // fire. For instance WhenFilterTypeIsStruct_NoDiagnostic pins the struct exemption on the constructor
    // check while WhenFilterTypeHasNoPublicParameterlessConstructor_Diagnostic pins the firing side of it,
    // WhenFilterTypeDoesNotImplementITestFilter_Diagnostic does the same for the interface check the struct
    // also passes through, and WhenGenericProviderReferencesGenericFilter_Diagnostic covers the
    // generic-provider branch. Kept as one class-level note rather than repeated per test, so it also
    // covers negative tests added later.
    [TestMethod]
    public async Task WhenFilterTypeIsValid_NoDiagnostic()
    {
        string code = Header + """
            [assembly: TestFilterProvider(typeof(MyFilter))]

            public sealed class MyFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    // Activator.CreateInstance(Type) can instantiate a non-public type as long as its parameterless
    // constructor is public, so the type's effective accessibility must not cause a diagnostic.
    [TestMethod]
    public async Task WhenFilterTypeIsInternalWithPublicParameterlessConstructor_NoDiagnostic()
    {
        string code = Header + """
            [assembly: TestFilterProvider(typeof(MyFilter))]

            internal sealed class MyFilter : ITestFilter
            {
                public MyFilter() { }

                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNoProviderIsRegistered_NoDiagnostic()
    {
        string code = Header + """
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    // A struct is instantiable through Activator.CreateInstance and always has a public parameterless
    // constructor, so it must not be flagged even though the documented shape is a class.
    [TestMethod]
    public async Task WhenFilterTypeIsStruct_NoDiagnostic()
    {
        string code = Header + """
            [assembly: TestFilterProvider(typeof(MyFilter))]

            public struct MyFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    // A ref struct can implement an interface (C# 13), so it passes the ITestFilter check and would be
    // waved through by the struct exemption on the constructor check. It is byref-like though, so it
    // cannot be boxed and Activator.CreateInstance always fails: registration dies with UTA077 at run
    // time. It belongs with the other non-instantiable types.
    //
    // The test harness compiles against net8.0 reference assemblies, where implementing an interface on a
    // ref struct is itself a compiler error, so CS8343 is expected alongside MSTEST0081. Roslyn still
    // models the interface as implemented, which is why the analyzer sees — and correctly rejects — the
    // type. If the harness ever moves to net9.0+ references this test will fail on the now-absent CS8343;
    // dropping that expectation is then the right fix, and MSTEST0081 alone becomes the whole assertion.
    [TestMethod]
    public async Task WhenFilterTypeIsRefStruct_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(MyFilter))|}]

            public ref struct MyFilter : {|#1:ITestFilter|}
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NotInstantiableRule)
                .WithLocation(0)
                .WithArguments("MyFilter"),
            DiagnosticResult.CompilerError("CS8343").WithLocation(1).WithArguments("MyFilter"));
    }

    [TestMethod]
    public async Task WhenFilterTypeDoesNotImplementITestFilter_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(NotAFilter))|}]

            public sealed class NotAFilter
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NotATestFilterRule)
                .WithLocation(0)
                .WithArguments("NotAFilter"));
    }

    [TestMethod]
    public async Task WhenFilterTypeIsNotANamedType_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(int[]))|}]
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NotATestFilterRule)
                .WithLocation(0)
                .WithArguments("int[]"));
    }

    [TestMethod]
    public async Task WhenFilterTypeIsAbstract_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(AbstractFilter))|}]

            public abstract class AbstractFilter : ITestFilter
            {
                public abstract TestFilterResult Filter(TestFilterContext context);
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NotInstantiableRule)
                .WithLocation(0)
                .WithArguments("AbstractFilter"));
    }

    [TestMethod]
    public async Task WhenFilterTypeIsInterface_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(IMyFilter))|}]

            public interface IMyFilter : ITestFilter
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NotInstantiableRule)
                .WithLocation(0)
                .WithArguments("IMyFilter"));
    }

    [TestMethod]
    public async Task WhenFilterTypeIsStaticClass_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(StaticFilter))|}]

            public static class StaticFilter
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NotInstantiableRule)
                .WithLocation(0)
                .WithArguments("StaticFilter"));
    }

    [TestMethod]
    public async Task WhenFilterTypeIsClosedGeneric_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(GenericFilter<int>))|}]

            public sealed class GenericFilter<T> : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.GenericRule)
                .WithLocation(0)
                .WithArguments("GenericFilter<int>"));
    }

    [TestMethod]
    public async Task WhenFilterTypeIsOpenGeneric_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(GenericFilter<>))|}]

            public sealed class GenericFilter<T> : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.GenericRule)
                .WithLocation(0)
                .WithArguments("GenericFilter<>"));
    }

    [TestMethod]
    public async Task WhenFilterTypeHasNoPublicParameterlessConstructor_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(MyFilter))|}]

            public sealed class MyFilter : ITestFilter
            {
                private MyFilter() { }

                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NoParameterlessConstructorRule)
                .WithLocation(0)
                .WithArguments("MyFilter"));
    }

    [TestMethod]
    public async Task WhenFilterTypeIsInternalWithInternalParameterlessConstructor_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(MyFilter))|}]

            internal sealed class MyFilter : ITestFilter
            {
                internal MyFilter() { }

                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NoParameterlessConstructorRule)
                .WithLocation(0)
                .WithArguments("MyFilter"));
    }

    [TestMethod]
    public async Task WhenFilterTypeOnlyHasParameterizedConstructor_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(MyFilter))|}]

            public sealed class MyFilter : ITestFilter
            {
                public MyFilter(int unused) { }

                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NoParameterlessConstructorRule)
                .WithLocation(0)
                .WithArguments("MyFilter"));
    }

    // TestFilterProviderAttribute and TestFilterProviderAttribute<TFilter> are both sealed and the contract
    // they share is internal to MSTest, so a user-defined attribute can never register a filter. This pins
    // that down: an unrelated assembly attribute must not be mistaken for a provider.
    [TestMethod]
    public async Task WhenUnrelatedAssemblyAttributeIsApplied_NoDiagnostic()
    {
        string code = Header + """
            [assembly: Nightly]

            public sealed class NightlyAttribute : System.Attribute
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    // typeof(...) cannot produce null, but a plain null literal binds to the Type parameter and compiles.
    // At run time the attribute constructor throws and discovery reports UTA073.
    [TestMethod]
    public async Task WhenFilterTypeIsNull_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(null)|}]
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NullRule).WithLocation(0));
    }

    // A malformed application is already a compiler error, so MSTEST0081 must stay quiet rather than stack
    // a misleading "null filter type" diagnostic on top of it.
    [TestMethod]
    public async Task WhenAttributeHasNoArgument_NoAnalyzerDiagnostic()
    {
        string code = Header + """
            [assembly: {|CS7036:TestFilterProvider()|}]
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAttributeArgumentIsNotAType_NoAnalyzerDiagnostic()
    {
        string code = Header + """
            [assembly: TestFilterProvider({|CS1503:1|})]
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    // Reflection treats a type nested in a generic container as generic, so the adapter rejects it with
    // UTA074. Roslyn agrees -- IsGenericType is documented as "this type or some containing type has type
    // parameters" -- so no ContainingType walk is needed here; these two tests pin that equivalence, which
    // is not obvious from the API name and has been queried in review.
    [TestMethod]
    public async Task WhenFilterTypeIsNestedInGenericType_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(Outer<int>.NestedFilter))|}]

            public class Outer<T>
            {
                public sealed class NestedFilter : ITestFilter
                {
                    public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.GenericRule)
                .WithLocation(0)
                .WithArguments("Outer<int>.NestedFilter"));
    }

    [TestMethod]
    public async Task WhenMultipleNonGenericProvidersAreRegistered_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(MyFilter))|}]
            [assembly: {|#1:TestFilterProvider(typeof(MyOtherFilter))|}]

            public sealed class MyFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }

            public sealed class MyOtherFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.MultipleRule).WithLocation(0),
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.MultipleRule).WithLocation(1),
            DiagnosticResult.CompilerError("CS0579").WithSpan(5, 12, 5, 30).WithArguments("TestFilterProvider"));
    }

#if NET
    // TestFilterProviderAttribute<TFilter> only exists in the .NET assets of MSTest.TestFramework, so these
    // cases cannot be compiled when this test project runs on .NET Framework.
    [TestMethod]
    public async Task WhenGenericProviderIsUsed_NoDiagnostic()
    {
        string code = Header + """
            [assembly: TestFilterProvider<MyFilter>]

            public sealed class MyFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    // A struct filter works through the non-generic attribute, so the generic form must accept it too —
    // otherwise a valid registration could not be migrated to the type-safe shape. This is why
    // TestFilterProviderAttribute<TFilter> deliberately has no 'class' constraint.
    [TestMethod]
    public async Task WhenGenericProviderReferencesStruct_NoDiagnostic()
    {
        string code = Header + """
            [assembly: TestFilterProvider<MyFilter>]

            public struct MyFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    // The 'ITestFilter, new()' constraints already make these compiler errors, so MSTEST0081 must not
    // stack a second diagnostic on top of CS0311 / CS0310.
    [TestMethod]
    public async Task WhenGenericProviderViolatesInterfaceConstraint_NoAnalyzerDiagnostic()
    {
        string code = Header + """
            [assembly: TestFilterProvider<{|CS0311:NotAFilter|}>]

            public sealed class NotAFilter
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenGenericProviderViolatesConstructorConstraint_NoAnalyzerDiagnostic()
    {
        string code = Header + """
            [assembly: TestFilterProvider<{|CS0310:MyFilter|}>]

            public sealed class MyFilter : ITestFilter
            {
                private MyFilter() { }

                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    // The generic form goes through the same check: 'Outer<int>.NestedFilter' satisfies
    // 'ITestFilter, new()', so it compiles and only this check stops it reaching UTA074 at run time.
    [TestMethod]
    public async Task WhenGenericProviderReferencesTypeNestedInGenericType_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider<Outer<int>.NestedFilter>|}]

            public class Outer<T>
            {
                public sealed class NestedFilter : ITestFilter
                {
                    public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.GenericRule)
                .WithLocation(0)
                .WithArguments("Outer<int>.NestedFilter"));
    }

    // The generic constraints cannot rule this one out: a closed generic satisfies 'ITestFilter, new()'
    // but is still rejected by the adapter at run time.
    [TestMethod]
    public async Task WhenGenericProviderReferencesGenericFilter_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider<GenericFilter<int>>|}]

            public sealed class GenericFilter<T> : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.GenericRule)
                .WithLocation(0)
                .WithArguments("GenericFilter<int>"));
    }

    [TestMethod]
    public async Task WhenMultipleGenericProvidersAreRegistered_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider<MyFilter>|}]
            [assembly: {|#1:TestFilterProvider<MyOtherFilter>|}]

            public sealed class MyFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }

            public sealed class MyOtherFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.MultipleRule).WithLocation(0),
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.MultipleRule).WithLocation(1),
            DiagnosticResult.CompilerError("CS0579").WithSpan(5, 12, 5, 45).WithArguments("TestFilterProvider<>"));
    }

    // The two attributes are distinct types, so the compiler's own duplicate-attribute check does not fire
    // even though the adapter rejects the pair with UTA079. This mixed pair is in fact the *only* shape that
    // can reach MultipleRule from well-formed C#: both attributes are AllowMultiple = false, and Roslyn keys
    // its duplicate check off the original definition, so a same-shape pair - two 'typeof' registrations, or
    // two 'TestFilterProvider<T>' registrations, even with different T - is rejected as CS0579 and never
    // reaches the adapter. Those two shapes are deliberately untested: MSTEST0081 does still fire on them
    // (Roslyn keeps both attributes in the symbol model despite the duplicate error), but it reports the same
    // defect CS0579 already reports, through the same analyzer branch this test covers - so such a test would
    // add no coverage while pinning the analyzer's behavior on input that cannot compile.
    [TestMethod]
    public async Task WhenGenericAndNonGenericProvidersAreCombined_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(MyFilter))|}]
            [assembly: {|#1:TestFilterProvider<MyOtherFilter>|}]

            public sealed class MyFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }

            public sealed class MyOtherFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.MultipleRule).WithLocation(0),
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.MultipleRule).WithLocation(1));
    }

    // MultipleRule and the per-provider rules are reported by two separate loops over the same set
    // (AnalyzeCompilation), and the second one is not gated on the first. Every other multiple-provider test
    // registers only valid filters, so nothing pins that the per-provider pass still runs once MultipleRule has
    // fired: short-circuiting after the MultipleRule loop would leave them all green while silently hiding every
    // other MSTEST0081 sub-rule from any assembly with more than one provider. Hence one registration here is
    // also invalid on its own, so the assembly must report both MultipleRule twice and NotATestFilterRule once.
    [TestMethod]
    public async Task WhenMultipleProvidersAreRegisteredAndOneIsInvalid_Diagnostic()
    {
        string code = Header + """
            [assembly: {|#0:TestFilterProvider(typeof(NotAFilter))|}]
            [assembly: {|#1:TestFilterProvider<MyFilter>|}]

            public sealed class NotAFilter
            {
            }

            public sealed class MyFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.MultipleRule).WithLocation(0),
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.MultipleRule).WithLocation(1),
            VerifyCS.Diagnostic(TestFilterProviderShouldBeValidAnalyzer.NotATestFilterRule)
                .WithLocation(0)
                .WithArguments("NotAFilter"));
    }
#endif
}
