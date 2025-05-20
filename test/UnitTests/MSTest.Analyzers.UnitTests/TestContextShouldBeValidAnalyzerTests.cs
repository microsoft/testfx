// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestContextShouldBeValidAnalyzer,
    MSTest.Analyzers.TestContextShouldBeValidFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestContextShouldBeValidAnalyzerTests
{
    [DataRow("TestContext", "private")]
    [DataRow("TestContext", "public")]
    [DataRow("TestContext", "internal")]
    [DataRow("TestContext", "protected")]
    [DataRow("testcontext", "private")]
    [DataRow("testcontext", "public")]
    [DataRow("testcontext", "internal")]
    [DataRow("testcontext", "protected")]
    [DataRow("TESTCONTEXT", "private")]
    [DataRow("TESTCONTEXT", "public")]
    [DataRow("TESTCONTEXT", "internal")]
    [DataRow("TESTCONTEXT", "protected")]
    [DataRow("TeStCoNtExT", "private")]
    [DataRow("TeStCoNtExT", "public")]
    [DataRow("TeStCoNtExT", "internal")]
    [DataRow("TeStCoNtExT", "protected")]
    [TestMethod]
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
        string fixedCode =
            """
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

    [DataRow("TestContext", "private")]
    [DataRow("TestContext", "public")]
    [DataRow("TestContext", "internal")]
    [DataRow("TestContext", "protected")]
    [DataRow("testcontext", "private")]
    [DataRow("testcontext", "public")]
    [DataRow("testcontext", "internal")]
    [DataRow("testcontext", "protected")]
    [DataRow("TESTCONTEXT", "private")]
    [DataRow("TESTCONTEXT", "public")]
    [DataRow("TESTCONTEXT", "internal")]
    [DataRow("TESTCONTEXT", "protected")]
    [DataRow("TeStCoNtExT", "private")]
    [DataRow("TeStCoNtExT", "public")]
    [DataRow("TeStCoNtExT", "internal")]
    [DataRow("TeStCoNtExT", "protected")]
    [TestMethod]
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

    [TestMethod]
    public async Task WhenTestContextIsInPrimaryConstructor_NoDiagnostic()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public sealed class Test1(TestContext testContext)
            {
                [TestMethod]
                public void TestMethod1()
                {
                    testContext.CancellationTokenSource.Cancel();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [DataRow("TestContext", "private")]
    [DataRow("TestContext", "internal")]
    [DataRow("testcontext", "private")]
    [DataRow("testcontext", "internal")]
    [DataRow("TESTCONTEXT", "private")]
    [DataRow("TESTCONTEXT", "internal")]
    [DataRow("TeStCoNtExT", "private")]
    [DataRow("TeStCoNtExT", "internal")]
    [TestMethod]
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
        string fixedCode =
            """
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

    [DataRow("TestContext", "private")]
    [DataRow("TestContext", "internal")]
    [DataRow("testcontext", "private")]
    [DataRow("testcontext", "internal")]
    [DataRow("TESTCONTEXT", "private")]
    [DataRow("TESTCONTEXT", "internal")]
    [DataRow("TeStCoNtExT", "private")]
    [DataRow("TeStCoNtExT", "internal")]
    [TestMethod]
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

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public async Task WhenTestContextPropertyIsStatic_Diagnostic()
    {
        string code =
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public static TestContext {|#0:TestContext|} { get; set; }
            }
            """;
        string fixedCode =
            """
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

    [TestMethod]
    public async Task WhenTestContextPropertyIsReadonly_Diagnostic()
    {
        string code =
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext {|#0:TestContext|} { get; }
            }
            """;
        string fixedCode =
            """
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

    [TestMethod]
    public async Task WhenTestContextPropertyIsNotCasedCorrectly_Diagnostic()
    {
        string code =
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext {|#0:testContext|} { get; set; }
            }
            """;
        string fixedCode =
            """
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

    [TestMethod]
    public async Task WhenTestContextPropertyIsReadonly_AssignedInConstructor_NoDiagnostic()
    {
        string code =
            """
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

    [TestMethod]
    public async Task WhenTestContextPropertyIsReadonly_AssignedInConstructorViaField_NoDiagnostic()
    {
        string code =
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private readonly TestContext _testContext;
                public MyTestClass(TestContext testContext)
                {
                    _testContext = testContext;
                }
                public TestContext TestContext => _testContext;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [DataRow("TestContext", "private")]
    [DataRow("TestContext", "public")]
    [DataRow("TestContext", "internal")]
    [DataRow("TestContext", "protected")]
    [DataRow("testcontext", "private")]
    [DataRow("testcontext", "public")]
    [DataRow("testcontext", "internal")]
    [DataRow("testcontext", "protected")]
    [DataRow("TESTCONTEXT", "private")]
    [DataRow("TESTCONTEXT", "public")]
    [DataRow("TESTCONTEXT", "internal")]
    [DataRow("TESTCONTEXT", "protected")]
    [DataRow("TeStCoNtExT", "private")]
    [DataRow("TeStCoNtExT", "public")]
    [DataRow("TeStCoNtExT", "internal")]
    [DataRow("TeStCoNtExT", "protected")]
    [TestMethod]
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

    [TestMethod]
    public async Task WhenTestContextAssignedInConstructorWithNullCheck_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private readonly TestContext _testContext;
                
                public MyTestClass(TestContext testContext)
                {
                    _testContext = testContext ?? throw new ArgumentNullException(nameof(testContext));
                }
                
                public TestContext TestContext => _testContext;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyAssignedInConstructorWithNullCheck_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext)
                {
                    TestContext = testContext ?? throw new ArgumentNullException(nameof(testContext));
                }
                
                public TestContext TestContext { get; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestContextAssignedInConstructorWithNullCheckToDefault_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private readonly TestContext _testContext;
                
                public MyTestClass(TestContext testContext)
                {
                    _testContext = testContext ?? new TestContext();
                }
                
                public TestContext TestContext => _testContext;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
