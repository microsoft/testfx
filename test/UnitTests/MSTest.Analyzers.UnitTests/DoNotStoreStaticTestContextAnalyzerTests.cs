// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DoNotStoreStaticTestContextAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class DoNotStoreStaticTestContextAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
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
}
