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

    [TestMethod]
    public async Task WhenNonTestContextTypeWithSamePropertyNamesAccessedInFixtureMethods_NoDiagnostic()
    {
        // A custom class that happens to have properties with the same names as restricted
        // TestContext properties should NOT trigger the diagnostic — the analyzer guards against
        // this with SymbolEqualityComparer.Default.Equals(propertyReference.Property.ContainingType, testContextSymbol).
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class FakeContext
            {
                public string TestData => "data";
                public string TestName => "name";
                public string TestDisplayName => "display";
                public string FullyQualifiedTestClassName => "class";
            }

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext tc)
                {
                    var fake = new FakeContext();
                    _ = fake.TestData;
                    _ = fake.TestName;
                    _ = fake.TestDisplayName;
                    _ = fake.FullyQualifiedTestClassName;
                }

                [ClassInitialize]
                public static void ClassInit(TestContext tc)
                {
                    var fake = new FakeContext();
                    _ = fake.TestData;
                    _ = fake.TestName;
                    _ = fake.FullyQualifiedTestClassName;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRestrictedTestContextPropertyAccessedInLambdaInsideAssemblyInitialize_Diagnostic()
    {
        // The analyzer uses context.ContainingSymbol, which for operations inside a lambda refers
        // to the enclosing named method (not the lambda's anonymous method). Therefore the
        // [AssemblyInitialize] attribute is still visible and the diagnostic fires correctly —
        // the lambda will be invoked during assembly initialization where these properties are unavailable.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext testContext)
                {
                    Action action = () =>
                    {
                        _ = [|testContext.TestName|];
                        _ = [|testContext.TestData|];
                    };
                    action();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRestrictedPropertyAccessedThroughPropertiesBagInFixtureMethods_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext testContext)
                {
                    _ = [|testContext.Properties["TestData"]|];
                    _ = [|testContext.Properties["TestDisplayName"]|];
                    _ = [|testContext.Properties["TestName"]|];
                    _ = [|testContext.Properties["FullyQualifiedTestClassName"]|];
                }

                [ClassInitialize]
                public static void ClassInit(TestContext testContext)
                {
                    _ = [|testContext.Properties["TestName"]|];
                    // FullyQualifiedTestClassName is allowed in class initialize.
                    _ = testContext.Properties["FullyQualifiedTestClassName"];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRestrictedPropertyAccessedThroughPropertiesBagInTestMethod_NoDiagnostic()
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
                    _ = TestContext.Properties["TestName"];
                    _ = TestContext.Properties["TestData"];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonConstantOrUnrelatedKeyAccessedThroughPropertiesBagInFixtureMethods_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext testContext)
                {
                    // Non-restricted key.
                    _ = testContext.Properties["MyCustomKey"];
                    // Non-constant key cannot be resolved.
                    string key = "TestName";
                    _ = testContext.Properties[key];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenManagedMethodOrManagedTypeAccessedThroughPropertiesBagInFixtureMethods_NoDiagnostic()
    {
        // "ManagedMethod" and "ManagedType" are VSTest TestCase-level keys in the string-keyed
        // Properties bag; they are not restricted TestContext properties, so no diagnostic is reported.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext testContext)
                {
                    _ = testContext.Properties["ManagedMethod"];
                    _ = testContext.Properties["ManagedType"];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
