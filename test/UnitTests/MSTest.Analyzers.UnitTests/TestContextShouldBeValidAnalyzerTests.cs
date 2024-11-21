// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestContextShouldBeValidAnalyzer,
    MSTest.Analyzers.TestContextShouldBeValidFixer>;

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
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                {{accessibility}} TestContext {|#0:{{fieldName}}|};
            }
            """;
        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.TestContextShouldBeValidRule).WithLocation(0),
            fixedCode);
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
    public async Task WhenTestContextCaseInsensitiveIsField_AssignedInConstructor_NoDiagnostic(string fieldName, string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext)
                {
                    this.{{fieldName}} = testContext;
                }

                {{accessibility}} TestContext {{fieldName}};
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
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
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                {{accessibility}} TestContext {|#0:{{propertyName}}|} { get; set; }
            }
            """;
        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.TestContextShouldBeValidRule)
                .WithLocation(0),
            fixedCode);
    }

    [Arguments("TestContext", "private")]
    [Arguments("TestContext", "internal")]
    [Arguments("testcontext", "private")]
    [Arguments("testcontext", "internal")]
    [Arguments("TESTCONTEXT", "private")]
    [Arguments("TESTCONTEXT", "internal")]
    [Arguments("TeStCoNtExT", "private")]
    [Arguments("TeStCoNtExT", "internal")]
    public async Task WhenTestContextPropertyIsPrivateOrInternal_AssignedInConstructor_NoDiagnostic(string propertyName, string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext)
                {
                    this.{{propertyName}} = testContext;
                }

                {{accessibility}} TestContext {|#0:{{propertyName}}|} { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [Arguments(true)]
    [Arguments(false)]
    public async Task WhenTestContextPropertyIsValid_NoDiagnostic(bool discoverInternals)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            {{(discoverInternals ? "[assembly: DiscoverInternals]" : string.Empty)}}

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenDiscoverInternalsTestContextPropertyIsPrivate_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                private TestContext {|#0:TestContext|} { get; set; }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.TestContextShouldBeValidRule)
                .WithLocation(0),
            fixedCode);
    }

    public async Task WhenDiscoverInternalsTestContextPropertyIsPrivate_AssignedInConstructor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext)
                {
                    TestContext = testContext;
                }

                private TestContext TestContext { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenDiscoverInternalsTestContextPropertyIsInternal_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                internal TestContext [|TestContext|] { get; set; }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                public TestContext [|TestContext|] { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenDiscoverInternalsTestContextPropertyIsInternal_AssignedInConstructor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext)
                {
                    TestContext = testContext;
                }

                internal TestContext TestContext { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenTestContextPropertyIsStatic_Diagnostic()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public static TestContext {|#0:TestContext|} { get; set; }
            }
            """;
        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.TestContextShouldBeValidRule)
                .WithLocation(0),
            fixedCode);
    }

    public async Task WhenTestContextPropertyIsReadonly_Diagnostic()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext {|#0:TestContext|} { get; }
            }
            """;
        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestContextShouldBeValidAnalyzer.TestContextShouldBeValidRule)
                .WithLocation(0),
            fixedCode);
    }

    public async Task WhenTestContextPropertyIsReadonly_AssignedInConstructor_NoDiagnostic()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext)
                {
                    TestContext = testContext;
                }

                public TestContext TestContext { get; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
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
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                {{accessibility}} TestContext {{fieldName}};
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
