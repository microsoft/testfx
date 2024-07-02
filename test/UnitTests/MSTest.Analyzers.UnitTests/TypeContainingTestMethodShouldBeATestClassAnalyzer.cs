// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TypeContainingTestMethodShouldBeATestClassAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TypeContainingTestMethodShouldBeATestClassAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestClassHasTestMethod_NoDiagnostic()
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

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassWithoutTestAttribute_HaveTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class [|MyTestClass|]
            {
                [TestMethod]
                public void TestMethod1() {}
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassWithoutTestAttribute_AndWithoutTestMethods_InheritTestClassWithTestMethods_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class [|MyTestClass|] : WithTestMethods_WithoutTestClass
            {

            }

            [TestClass]
            public class WithTestMethods_WithoutTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [TestMethod]
                public void TestMethod2()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassWithoutTestAttribute_AndWithTestMethods_InheritTestClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Base
            {

            }

            public class [|MyTestClass|] : Base
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [TestMethod]
                public void TestMethod2()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenInheritedTestClassAttribute_HasInheritedTestMethodAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class DerivedTestMethod : TestMethodAttribute
            {
            }

            public class DerivedTestClass : TestClassAttribute
            {
            }

            [DerivedTestClass]
            public class MyTestClass
            {
                [DerivedTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassWithoutTestAttribute_HasInheritedTestMethodAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class DerivedTestMethod : TestMethodAttribute
            {
            }

            public class [|MyTestClass|]
            {
                [DerivedTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAbstractClass_DoesNotHaveTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class AbstractClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
