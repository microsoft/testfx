// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferAssertPassOverAlwaysTrueConditionsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class PreferAssertPassOverAlwaysTrueConditionsAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenIsNotNullAssertion_ValueParameterIsNotNullable_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int? var = null;
                    Assert.IsNotNull(var);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenIsNotNullAssertion_ValueParameterIsNotNullable_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int var = 3;
                    [|Assert.IsNotNull(var)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenIsNotNullAssertion_ValueParameterAsPropertySymbolIsNotNullable_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                private int var = 3;

                [TestMethod]
                public void Test()
                {
                    [|Assert.IsNotNull(var)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenIsNotNullAssertion_ValueParameterAsPropertySymbolIsNullable_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                private int? var = 3;

                [TestMethod]
                public void Test()
                {
                    Assert.IsNotNull(var);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenIsNotNullAssertion_ValueParameterAsReferenceObjectIsNotNullable_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void Test()
                {
                    ObjectClass obj = new ObjectClass();
                    [|Assert.IsNotNull(obj)|];
                }
            }

            public class ObjectClass
            {

            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenIsNotNullAssertion_ValueParameterAsReferenceObjectIsNotNullable_WithoutEnableNullable_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void Test()
                {
                    ObjectClass obj = new ObjectClass();
                    Assert.IsNotNull(obj);
                }
            }

            public class ObjectClass
            {

            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenIsNotNullAssertion_ValueParameterAsReferenceObjectIsNotNullable_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void Test()
                {
                    ObjectClass? obj = null;

                    Assert.IsNotNull(obj);
                }
            }

            public class ObjectClass
            {

            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssertIsFalseIsPassedTrue_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedTrue_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(true, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedTrue_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(message: "message", condition: true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedFalse_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsFalse(false)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedFalse_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsFalse(false, "message")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedFalse_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsFalse(message: "message", condition: false)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(GetBoolean(), "message");
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(message: "message", condition: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedTrue_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(true)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedTrue_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(true, "message")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedTrue_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(message: "message", condition: true)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedFalse_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedFalse_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(false, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedFalse_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(message: "message", condition: false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(GetBoolean(), "message");
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(message: "message", condition: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsNullIsPassedNull_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsNull(null)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsNullIsPassedNull_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsNull(null, "message")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsNullIsPassedNull_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsNull(message: "message", value: null)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsNullIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsNull(GetObject());
                }

                private static object GetObject() => new object();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsNullIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsNull(GetObject(), "message");
                }
            
                private static object GetObject() => new object();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsNullIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsNull(message: "message", value: GetObject());
                }
            
                private static object GetObject() => new object();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedEqual_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(true, true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedEqual_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(true, true, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedEqual_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(message: "message", notExpected: true, actual: true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedEqual_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(notExpected: true, message: "message", actual: true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedNonEqual_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(true, false)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedNonEqual_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(true, false, "message")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedNonEqual_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(message: "message", notExpected: true, actual: false)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedNonEqual_WithMessageSecond_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(notExpected: true, message: "message", actual: false)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(GetBoolean(), GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(GetBoolean(), GetBoolean(), "message");
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(message: "message", notExpected: GetBoolean(), actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedUnknown_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(notExpected: GetBoolean(), message: "message", actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedEqual_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(true, true)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedEqual_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(true, true, "message")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedEqual_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(message: "message", expected: true, actual: true)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedEqual_WithMessageSecond_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(expected: true, message: "message", actual: true)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedNonEqual_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(true, false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedNonEqual_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(true, false, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedNonEqual_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(message: "message", expected: true, actual: false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedNonEqual_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(expected: true, message: "message", actual: false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(GetBoolean(), GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(GetBoolean(), GetBoolean(), "message");
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(message: "message", expected: GetBoolean(), actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedUnknown_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(message: "message", expected: GetBoolean(), actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
