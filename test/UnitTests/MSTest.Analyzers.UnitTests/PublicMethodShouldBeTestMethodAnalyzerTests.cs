// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PublicMethodShouldBeTestMethodAnalyzer,
    MSTest.Analyzers.PublicMethodShouldBeTestMethodFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PublicMethodShouldBeTestMethodAnalyzerTests
{
    [TestMethod]
    public async Task WhenMethodIsPrivate_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicButWithInvalidTestMethodSignature_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public static void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndMarkedAsTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodIsPrivateButNotInsideTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                private void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndMarkedAsDerivedTestMethodAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class DerivedTestMethod : TestMethodAttribute
            {
            }

            [TestClass]
            public class MyTestClass
            {
                [DerivedTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndNotMarkedAsTestMethodWithInheritanceFromBaseTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Base
            {
            }

            public class MyTestClass : Base
            {
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodIsPrivateAndNotMarkedAsTestMethodWithInheritedTestClassAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class DerivedTestClass : TestClassAttribute
            {
            }

            [DerivedTestClass]
            public class MyTestClass
            {
                private void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndNotMarkedAsTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void [|MyTestMethod|]()
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
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndNotMarkedAsTestMethodWithInheritedTestClassAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class DerivedTestClass : TestClassAttribute
            {
            }

            [DerivedTestClass]
            public class MyTestClass
            {
                public void [|MyTestMethod|]()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class DerivedTestClass : TestClassAttribute
            {
            }

            [DerivedTestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndMarkedAsTestInitialize_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndMarkedAsTestCleanup_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestCleanup()
                {
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndImplementsDispose_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;

            [TestClass]
            public class MyTestClass : IDisposable
            {
                public void Dispose()
                {
                }
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndImplementsUserDefinedInterface_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;

            public interface IMyInterface
            {
                void MyMethod();
            }

            [TestClass]
            public class MyTestClass : IMyInterface
            {
                public void MyMethod()
                {
                }
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndImplementsExplicitlyUserDefinedInterface_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;

            public interface IMyInterface
            {
                void MyMethod();
            }

            [TestClass]
            public class MyTestClass : IMyInterface
            {
                void IMyInterface.MyMethod()
                {
                }
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicAndImplementsDisposeAsVirtual_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;

            [TestClass]
            public class MyTestClass : IDisposable
            {
                public virtual void Dispose()
                {
                }
            }

            [TestClass]
            public class SubTestClass : MyTestClass
            {
                public override void Dispose()
                {
                }
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodIsPublicWithMultiLineBody_PreservesIndentation()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void [|MyTestMethod|]()
                {
                    var result = SomeMethod(
                        1,
                        2,
                        3);
                }

                private int SomeMethod(int a, int b, int c) => a + b + c;
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var result = SomeMethod(
                        1,
                        2,
                        3);
                }

                private int SomeMethod(int a, int b, int c) => a + b + c;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
