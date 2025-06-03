// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestClassShouldBeValidAnalyzer,
    MSTest.Analyzers.TestClassShouldBeValidFixer>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.TestClassShouldBeValidAnalyzer,
    MSTest.Analyzers.TestClassShouldBeValidFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestClassShouldBeValidAnalyzerTests
{
    [TestMethod]
    public async Task WhenClassIsPublicAndTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassIsInternalAndTestClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            internal class {|#0:MyTestClass|}
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassIsInternalAndTestRecord_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            internal record {|#0:MyTestClass|}
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public record MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            fixedCode);
    }

    [DataRow("private")]
    [DataRow("internal")]
    [TestMethod]
    public async Task WhenClassIsInnerAndNotPublicTestClass_Diagnostic(string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class OuterClass
            {
                [TestClass]
                {{accessibility}} class {|#0:MyTestClass|}
                {
                }
            }
            """;

        string fixedCode =
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class OuterClass
            {
                [TestClass]
                public class MyTestClass
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassIsInternalAndNotTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassIsPublicAndTestClassAsInnerOfInternalClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal class OuterClass
            {
                [TestClass]
                public class {|#0:MyTestClass|}
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    [TestMethod]
    public async Task WhenClassIsGeneric_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDiscoverInternalsAndTypeIsInternal_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDiscoverInternalsAndTypeIsPrivate_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            public class A
            {
                [TestClass]
                private class {|#0:MyTestClass|}
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            public class A
            {
                [TestClass]
                internal class MyTestClass
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassIsStaticAndEmpty_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassIsStaticAndContainsAssemblyInitialize_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassIsStaticAndContainsAssemblyCleanup_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassIsStaticAndContainsClassInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class {|#0:MyTestClass|}
            {
                [ClassInitialize]
                public static void ClassInit(TestContext testContext)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void ClassInit(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassIsStaticAndContainsClassCleanup_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class {|#0:MyTestClass|}
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassIsStaticAndContainsTestInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class {|#0:MyTestClass|}
            {
                [TestInitialize]
                public static void TestInit()
                {
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public static void TestInit()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassIsStaticAndContainsTestCleanup_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class {|#0:MyTestClass|}
            {
                [TestCleanup]
                public static void TestCleanup()
                {
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public static void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassIsStaticAndContainsTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class {|#0:MyTestClass|}
            {
                [TestMethod]
                public static void TestMethod()
                {
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.TestClassShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            fixedCode);
    }
    [TestMethod]
    public async Task WhenClassIsPublicAndTestClass_VB_NoDiagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenClassIsPrivateAndTestClass_VB_Diagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Public Class OuterClass
                <TestClass>
                Private Class {|#0:MyTestClass|}
                End Class
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code,
            VerifyVB.Diagnostic(TestClassShouldBeValidAnalyzer.ClassNotPublicRule).WithLocation(0));
    }

    [TestMethod]
    public async Task WhenClassIsStaticAndTestClass_VB_Diagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Module {|#0:MyTestClass|}
            End Module
            """;

        await VerifyVB.VerifyAnalyzerAsync(code,
            VerifyVB.Diagnostic(TestClassShouldBeValidAnalyzer.ClassStaticRule).WithLocation(0));
    }
}
