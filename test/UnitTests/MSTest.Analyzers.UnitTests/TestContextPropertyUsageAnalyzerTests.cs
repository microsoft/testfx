// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestContextPropertyUsageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestContextPropertyUsageAnalyzerTests(TestContext testContext)
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
                    _ = [|testContext.ManagedMethod|];
                    _ = [|testContext.FullyQualifiedTestClassName|];
                    _ = [|testContext.ManagedType|];
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
                    _ = [|testContext.ManagedMethod|];
                    _ = [|testContext.FullyQualifiedTestClassName|];
                    _ = [|testContext.ManagedType|];
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
                    _ = [|testContext.ManagedMethod|];
                    // These should NOT trigger diagnostics in class initialize
                    _ = testContext.FullyQualifiedTestClassName;
                    _ = testContext.ManagedType;
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
                    _ = [|testContext.ManagedMethod|];
                    // These should NOT trigger diagnostics in class cleanup
                    _ = testContext.FullyQualifiedTestClassName;
                    _ = testContext.ManagedType;
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
                    _ = TestContext.ManagedMethod;
                    _ = TestContext.FullyQualifiedTestClassName;
                    _ = TestContext.ManagedType;
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
                    _ = TestContext.ManagedMethod;
                    _ = TestContext.FullyQualifiedTestClassName;
                    _ = TestContext.ManagedType;
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
                    _ = TestContext.ManagedMethod;
                    _ = TestContext.FullyQualifiedTestClassName;
                    _ = TestContext.ManagedType;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    [Ignore("DataRow and DataConnection are not available in .NET Core, test needs to be fixed")]
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
                    _ = testContext.DataRow;
                    _ = testContext.DataConnection;
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net462.Default,
            TestState =
            {
                Sources = { code, },
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(TestContextPropertyUsageAnalyzer.Rule)
                        .WithLocation(7, 30) // Location of DataRow
                        .WithArguments("Microsoft.VisualStudio.TestTools.UnitTesting.TestContext", "DataRow"),
                    VerifyCS.Diagnostic(TestContextPropertyUsageAnalyzer.Rule)
                        .WithLocation(8, 30) // Location of DataConnection
                        .WithArguments("Microsoft.VisualStudio.TestTools.UnitTesting.TestContext", "DataConnection"),
                },
            },
        };

        await test.RunAsync(testContext.CancellationTokenSource.Token);
    }

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
                    _ = testContext.ManagedType;
                    _ = testContext.Properties;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
