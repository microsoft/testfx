// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TypeContainingTestMethodShouldBeATestClassAnalyzer,
    MSTest.Analyzers.AddTestClassFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TypeContainingTestMethodShouldBeATestClassAnalyzerTests
{
    [TestMethod]
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

    [TestMethod]
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

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1() {}
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
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

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass : WithTestMethods_WithoutTestClass
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
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

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Base
            {

            }

            [TestClass]
            public class MyTestClass : Base
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
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

    [TestMethod]
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

        string fixedCode = """
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAbstractClassWithoutTestAttribute_HaveTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class AbstractClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenClassHasTestInitializeAndThenTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class [|TestClass|]
            {
                [TestInitialize]
                public void Initialize()
                {

                }

                [TestMethod]
                public void TestMethod1()
                {

                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestInitialize]
                public void Initialize()
                {

                }

                [TestMethod]
                public void TestMethod1()
                {

                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenStructWithTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public struct [|TestStruct|]
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestStruct
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenStructWithoutTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public struct TestStruct
            {
                public void RegularMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRecordStructWithTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public record struct [|TestRecordStruct|]
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public record class TestRecordStruct
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenRecordClassWithTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public record class [|TestRecordClass|]
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public record class TestRecordClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenRecordWithTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public record [|TestRecord|]
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public record TestRecord
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenReadonlyRecordStructWithTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public readonly record struct [|TestRecordStruct|]
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public readonly record class TestRecordStruct
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
