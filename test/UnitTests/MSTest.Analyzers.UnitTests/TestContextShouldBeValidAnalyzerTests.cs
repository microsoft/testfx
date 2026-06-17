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
    public async Task WhenTestContextCaseInsensitiveIsField_NoDiagnostic(string fieldName, string accessibility)
    {
        // MSTEST0005 only validates the TestContext property layout. Fields of type TestContext
        // are intentionally not flagged, because doing so produced too many false positives
        // (see https://github.com/microsoft/testfx/issues/4590). The static-field case is
        // covered by MSTEST0024 (DoNotStoreStaticTestContext).
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                {{accessibility}} TestContext {{fieldName}};
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [DataRow("_testContext")]
    [DataRow("s_testContext")]
    [DataRow("testContext")]
    [TestMethod]
    public async Task WhenStaticFieldOfTypeTestContextAssignedInClassInitialize_NoDiagnostic(string fieldName)
    {
        // Regression test for https://github.com/microsoft/testfx/issues/4590:
        // a static field of type TestContext that is assigned in a [ClassInitialize] method
        // must not trigger MSTEST0005. MSTEST0024 already covers the "do not store TestContext
        // in a static member" guidance.
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private static TestContext {{fieldName}};

                [ClassInitialize]
                public static void ClassInitialize(TestContext context) =>
                    {{fieldName}} = context;

                [TestMethod]
                public void TestMethod1() { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenStaticFieldOfTypeTestContextIsNeverAssigned_NoDiagnostic()
    {
        // Documents the deliberate behavior change tied to https://github.com/microsoft/testfx/issues/4590:
        // MSTEST0005 no longer reports on fields of type TestContext, including unassigned static fields.
        // Such a field is also not reported by MSTEST0024 (which only fires on assignment), so this is
        // an accepted trade-off in favor of removing the false positives that previously bothered users.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private static TestContext _context;

                [TestMethod]
                public void TestMethod1() { }
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

    [TestMethod]
    public async Task WhenTestContextPrimaryConstructorParameterAssignedToField_NoDiagnostic()
    {
        // Regression test for https://github.com/microsoft/testfx/issues/8984:
        // capturing a TestContext primary-constructor parameter in a readonly field
        // must not trigger MSTEST0005. Fields of type TestContext are not validated
        // by MSTEST0005 (see WhenTestContextCaseInsensitiveIsField_NoDiagnostic), so
        // this pattern is allowed regardless of whether the constructor is primary or not.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTest(TestContext testContext)
            {
                private readonly TestContext _testContext = testContext;

                [TestMethod]
                public void TestMethod1()
                {
                    _testContext.CancellationTokenSource.Cancel();
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
                {{accessibility}} TestContext [|{{propertyName}}|] { get; set; }
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
                private TestContext [|TestContext|] { get; set; }
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
                public static TestContext [|TestContext|] { get; set; }
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
                public TestContext [|TestContext|] { get; }
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
                public TestContext [|testContext|] { get; set; }
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
    public async Task WhenTestContextPropertyHasPrivateSetter_NoDiagnostic()
    {
        // A public TestContext property named exactly "TestContext" with a private setter
        // satisfies IsTestContextPropertyAutomaticallyAssigned (SetMethod is not null), so
        // the runtime can still inject the value and no diagnostic should be reported.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; private set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyHasProtectedSetter_NoDiagnostic()
    {
        // Same as the private-setter case: a protected setter still satisfies the
        // SetMethod-is-not-null check in IsTestContextPropertyAutomaticallyAssigned.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; protected set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenInvalidTestContextPropertyIsOnNonTestClass_NoDiagnostic()
    {
        // The analyzer only validates TestContext properties inside [TestClass]-attributed types.
        // A class without [TestClass] must never be flagged, even if its TestContext property
        // layout would be invalid inside a test class.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyClass
            {
                public TestContext TestContext { get; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestContextAssignedOnlyInMultiParamConstructor_Diagnostic()
    {
        // TryGetTestContextParameterIfValidConstructor requires the constructor to have
        // exactly ONE parameter of type TestContext. A constructor with extra parameters
        // is not recognised, so the assignment is invisible to the analyzer and the
        // property (which lacks an auto-setter) must be reported.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext, string name)
                {
                    TestContext = testContext;
                }

                public TestContext [|TestContext|] { get; }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext, string name)
                {
                    TestContext = testContext;
                }

                public TestContext TestContext { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
