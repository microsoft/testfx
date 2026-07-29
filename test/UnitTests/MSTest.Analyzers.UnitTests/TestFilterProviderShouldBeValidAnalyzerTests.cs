// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    // The generic constraints cannot rule this one out: a closed generic satisfies 'class, ITestFilter, new()'
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
