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
    public async Task WhenAssigningTestContextParameterToInstanceMember_NoDiagnostic()
    {
        // Assigning a TestContext *parameter* to an instance field or property should not trigger
        // the diagnostic. This exercises the analyzer's 'Instance: null' guard while the value still
        // satisfies the 'Value: IParameterReferenceOperation' check (only the target-side guard fails).
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private TestContext _testContext;
                public TestContext TestContextProperty { get; set; }

                public void Store(TestContext tc)
                {
                    _testContext = tc;
                    TestContextProperty = tc;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssigningTestContextParameterToLocalVariable_NoDiagnostic()
    {
        // Assigning a TestContext parameter to a local variable is fine — the assignment target is
        // not an IMemberReferenceOperation, so the analyzer's pattern doesn't match. An explicit
        // assignment (not a declaration initializer) is used so OperationKind.SimpleAssignment fires.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext tc)
                {
                    TestContext local;
                    local = tc;
                    local.WriteLine("");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssigningNonTestContextParameterToStaticField_NoDiagnostic()
    {
        // Covers the IParameterReferenceOperation *type-mismatch* path: the value is a parameter
        // reference, but its type is not TestContext, so the analyzer must not fire.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private static string s_name;

                [AssemblyInitialize]
                public static void AssemblyInit(TestContext tc)
                {
                    Store("value");
                }

                private static void Store(string name)
                {
                    s_name = name;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssigningNonParameterValueToStaticField_NoDiagnostic()
    {
        // Covers the 'Value is not IParameterReferenceOperation' path: a literal (or any non-parameter
        // expression) assigned to a static field must not be flagged, even when the field type is TestContext.
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
