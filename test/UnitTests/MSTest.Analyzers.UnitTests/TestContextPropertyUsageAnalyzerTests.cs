// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestContextPropertyUsageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestContextPropertyUsageAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestContextPropertyAccessedInAssemblyInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext testContext)
                {
                    _ = [|testContext.TestData|];
                    _ = [|testContext.TestDisplayName|];
                    _ = [|testContext.TestName|];
                    _ = [|testContext.FullyQualifiedTestClassName|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyAccessedInAssemblyCleanup_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                    TestContext testContext = GetTestContext();
                    _ = [|testContext.TestData|];
                    _ = [|testContext.TestDisplayName|];
                    _ = [|testContext.TestName|];
                    _ = [|testContext.FullyQualifiedTestClassName|];
                }

                private static TestContext GetTestContext() => null;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyAccessedInClassInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void ClassInit(TestContext testContext)
                {
                    _ = [|testContext.TestData|];
                    _ = [|testContext.TestDisplayName|];
                    _ = [|testContext.TestName|];
                    // These should NOT trigger diagnostics in class initialize
                    _ = testContext.FullyQualifiedTestClassName;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyAccessedInClassCleanup_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                    TestContext testContext = GetTestContext();
                    _ = [|testContext.TestData|];
                    _ = [|testContext.TestDisplayName|];
                    _ = [|testContext.TestName|];
                    // These should NOT trigger diagnostics in class cleanup
                    _ = testContext.FullyQualifiedTestClassName;
                }

                private static TestContext GetTestContext() => null;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyAccessedInTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void TestMethod()
                {
                    // All of these should be allowed in test methods
                    _ = TestContext.TestData;
                    _ = TestContext.TestDisplayName;
                    _ = TestContext.TestName;
                    _ = TestContext.FullyQualifiedTestClassName;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyAccessedInTestInitialize_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestInitialize]
                public void TestInit()
                {
                    // All of these should be allowed in test initialize
                    _ = TestContext.TestData;
                    _ = TestContext.TestDisplayName;
                    _ = TestContext.TestName;
                    _ = TestContext.FullyQualifiedTestClassName;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyAccessedInTestCleanup_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestCleanup]
                public void TestCleanup()
                {
                    // All of these should be allowed in test cleanup
                    _ = TestContext.TestData;
                    _ = TestContext.TestDisplayName;
                    _ = TestContext.TestName;
                    _ = TestContext.FullyQualifiedTestClassName;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

#if NETFRAMEWORK
    [TestMethod]
    public async Task WhenDataRowAndDataConnectionAccessedInNetFramework_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext testContext)
                {
                    _ = [|testContext.DataRow|];
                    _ = [|testContext.DataConnection|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
#endif

    [TestMethod]
    public async Task WhenAllowedPropertiesAccessedInFixtureMethods_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext testContext)
                {
                    // These properties should be allowed in fixture methods
                    _ = testContext.Properties;
                    testContext.WriteLine("test");
                    testContext.Write("test");
                }

                [ClassInitialize]
                public static void ClassInit(TestContext testContext)
                {
                    // These properties should be allowed in class initialize
                    _ = testContext.FullyQualifiedTestClassName;
                    _ = testContext.Properties;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
