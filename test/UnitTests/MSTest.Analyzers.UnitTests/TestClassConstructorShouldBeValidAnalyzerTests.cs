// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestClassConstructorShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestClassConstructorShouldBeValidAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestClassHasPublicParameterlessConstructor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicConstructorWithTestContext_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasNoExplicitConstructor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPrivateParameterlessConstructor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                private MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasProtectedConstructor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                protected MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasInternalConstructor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                internal MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicConstructorWithMultipleParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                public MyTestClass(int x, string y)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicConstructorWithWrongParameterType_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                public MyTestClass(string testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasBothValidAndInvalidConstructors_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }

                private MyTestClass(int x)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicParameterlessAndPublicTestContextConstructors_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }

                public MyTestClass(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasInvalidConstructor_NoDiagnostic()
    {
        string code = """
            public class MyClass
            {
                private MyClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPrivateAndInternalConstructors_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                private MyTestClass()
                {
                }

                internal MyTestClass(int x)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassInheritsFromBase_AndHasNoExplicitConstructor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseClass
            {
                public BaseClass()
                {
                }
            }

            [TestClass]
            public class MyTestClass : BaseClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicConstructorWithTestContextAndOthers_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                public MyTestClass(TestContext testContext, int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }
}
