// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestContextShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestContextShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    [Arguments("TestContext", "private")]
    [Arguments("TestContext", "public")]
    [Arguments("TestContext", "internal")]
    [Arguments("TestContext", "protected")]
    [Arguments("testcontext", "private")]
    [Arguments("testcontext", "public")]
    [Arguments("testcontext", "internal")]
    [Arguments("testcontext", "protected")]
    [Arguments("TESTCONTEXT", "private")]
    [Arguments("TESTCONTEXT", "public")]
    [Arguments("TESTCONTEXT", "internal")]
    [Arguments("TESTCONTEXT", "protected")]
    [Arguments("TeStCoNtExT", "private")]
    [Arguments("TeStCoNtExT", "public")]
    [Arguments("TeStCoNtExT", "internal")]
    [Arguments("TeStCoNtExT", "protected")]
    public async Task WhenTestContextCaseInsensitiveIsField_Diagnostic(string fieldName, string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                {{accessibility}} TestContext {|#0:{{fieldName}}|};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.NotFieldRule).WithLocation(0));
    }

    [Arguments("TestContext", "private")]
    [Arguments("TestContext", "internal")]
    [Arguments("testcontext", "private")]
    [Arguments("testcontext", "internal")]
    [Arguments("TESTCONTEXT", "private")]
    [Arguments("TESTCONTEXT", "internal")]
    [Arguments("TeStCoNtExT", "private")]
    [Arguments("TeStCoNtExT", "internal")]
    public async Task WhenTestContextPropertyIsPrivateOrInternal_Diagnostic(string propertyName, string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                {{accessibility}} TestContext {|#0:{{propertyName}}|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0));
    }

    public async Task WhenTestContextPropertyIsValid_NoDiagnostic()
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDiscoverInternalsTestContextPropertyIsPrivate_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                private TestContext {|#0:TestContext|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.PublicOrInternalRule)
                .WithLocation(0));
    }

    [Arguments("public")]
    [Arguments("internal")]
    public async Task WhenDiscoverInternalsTestContextPropertyIsPublicOrInternal_NoDiagnostic(string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                {{accessibility}} TestContext TestContext { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestContextPropertyIsStatic_Diagnostic()
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public static TestContext {|#0:TestContext|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.NotStaticRule)
                .WithLocation(0));
    }

    public async Task WhenTestContextPropertyIsReadonly_Diagnostic()
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext {|#0:TestContext|} { get; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.NotReadonlyRule)
                .WithLocation(0));
    }

    [Arguments("TestContext", "private")]
    [Arguments("TestContext", "public")]
    [Arguments("TestContext", "internal")]
    [Arguments("TestContext", "protected")]
    [Arguments("testcontext", "private")]
    [Arguments("testcontext", "public")]
    [Arguments("testcontext", "internal")]
    [Arguments("testcontext", "protected")]
    [Arguments("TESTCONTEXT", "private")]
    [Arguments("TESTCONTEXT", "public")]
    [Arguments("TESTCONTEXT", "internal")]
    [Arguments("TESTCONTEXT", "protected")]
    [Arguments("TeStCoNtExT", "private")]
    [Arguments("TeStCoNtExT", "public")]
    [Arguments("TeStCoNtExT", "internal")]
    [Arguments("TeStCoNtExT", "protected")]
    public async Task WhenTestContextIsFieldNotOnTestClass_NoDiagnostic(string fieldName, string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                {{accessibility}} TestContext {{fieldName}};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
