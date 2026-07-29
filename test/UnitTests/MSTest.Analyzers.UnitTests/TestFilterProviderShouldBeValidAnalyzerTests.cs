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

    // The two attributes are distinct types, so the compiler's own duplicate-attribute check does not fire
    // even though the adapter rejects the pair with UTA079.
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
#endif
}
