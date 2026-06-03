// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DoNotStoreStaticTestContextAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DoNotStoreStaticTestContextAnalyzerTests
{
#if NET
    [TestMethod]
    public async Task WhenAssemblyInitializeOrClassInitialize_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass1
            {
                private static TestContext s_testContext;
                private static TestContext StaticContext { get; set; }

                [AssemblyInitialize]
                public static void AssemblyInit(TestContext tc)
                {
                    [|s_testContext = tc|];
                    [|StaticContext = tc|];
                }

                [ClassInitialize]
                public static void ClassInit(TestContext tc)
                {
                    [|s_testContext = tc|];
                    [|StaticContext = tc|];
                }
            }

            [TestClass]
            public class MyTestClass2
            {
                private static TestContext s_testContext;
                private static TestContext StaticContext { get; set; }
            
                [AssemblyInitialize]
                public static Task AssemblyInit(TestContext tc)
                {
                    [|s_testContext = tc|];
                    [|StaticContext = tc|];
                    return Task.CompletedTask;
                }
            
                [ClassInitialize]
                public static Task ClassInit(TestContext tc)
                {
                    [|s_testContext = tc|];
                    [|StaticContext = tc|];
                    return Task.CompletedTask;
                }
            }

            [TestClass]
            public class MyTestClass3
            {
                private static TestContext s_testContext;
                private static TestContext StaticContext { get; set; }
            
                [AssemblyInitialize]
                public static ValueTask AssemblyInit(TestContext tc)
                {
                    [|s_testContext = tc|];
                    [|StaticContext = tc|];
                    return ValueTask.CompletedTask;
                }
            
                [ClassInitialize]
                public static ValueTask ClassInit(TestContext tc)
                {
                    [|s_testContext = tc|];
                    [|StaticContext = tc|];
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
#endif

    [TestMethod]
    public async Task WhenOtherTestContext_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private static TestContext s_testContext;
                private static TestContext StaticContext { get; set; }

                private TestContext _testContext;
                public TestContext TestContext { get; set; }

                [AssemblyInitialize]
                public static void AssemblyInit(TestContext tc)
                {
                    tc.WriteLine("");
                }
            
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            
                [ClassInitialize]
                public static void ClassInit(TestContext tc)
                {
                    tc.WriteLine("");
                }

                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssigningToInstanceMember_NoDiagnostic()
    {
        // Assigning TestContext to an instance field or property should not trigger the diagnostic.
        // The analyzer only fires when the assignment target has no 'Instance' (i.e. static member).
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private TestContext _testContext;
                public TestContext TestContext { get; set; }

                [ClassInitialize]
                public static void ClassInit(TestContext tc)
                {
                }

                [TestInitialize]
                public void TestInit()
                {
                    _testContext = TestContext;
                    TestContext = TestContext;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssigningToLocalVariable_NoDiagnostic()
    {
        // Assigning a TestContext parameter to a local variable is fine — not a static member reference.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext tc)
                {
                    TestContext local = tc;
                    local.WriteLine("");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssigningNonTestContextParameterToStaticField_NoDiagnostic()
    {
        // Only assignments where the *value* is a TestContext parameter are flagged.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private static string s_name;

                [AssemblyInitialize]
                public static void AssemblyInit(TestContext tc)
                {
                    s_name = "value";
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

#if NET
    [TestMethod]
    public async Task WhenAssigningTestContextInHelperMethod_Diagnostic()
    {
        // The diagnostic fires on any static member assignment of a TestContext parameter,
        // regardless of whether the containing method is [AssemblyInitialize] or [ClassInitialize].
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private static TestContext s_testContext;

                [AssemblyInitialize]
                public static void AssemblyInit(TestContext tc)
                {
                    Store(tc);
                }

                private static void Store(TestContext tc)
                {
                    [|s_testContext = tc|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
#endif
}
