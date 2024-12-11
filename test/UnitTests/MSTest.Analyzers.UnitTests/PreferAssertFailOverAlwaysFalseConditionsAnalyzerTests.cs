// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferAssertFailOverAlwaysFalseConditionsAnalyzer,
    MSTest.Analyzers.PreferAssertFailOverAlwaysFalseConditionsFixer>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class PreferAssertFailOverAlwaysFalseConditionsAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenIsNullAssertion_ValueParameterIsNullable_NoDiagnostic()
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
                    Assert.IsNull(var);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenIsNullAssertion_ValueParameterIsNotNullable_Diagnostic()
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
                    [|Assert.IsNull(var)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int var = 3;
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenIsNullAssertion_ValueParameterAsPropertySymbolIsNotNullable_Diagnostic()
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
                    [|Assert.IsNull(var)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                private int var = 3;

                [TestMethod]
                public void Test()
                {
                    Assert.Fail();
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenIsNullAssertion_ValueParameterAsPropertySymbolIsNullable_NoDiagnostic()
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
                    Assert.IsNull(var);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenIsNullAssertion_ValueParameterAsReferenceObjectIsNotNullable_Diagnostic()
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
                    [|Assert.IsNull(obj)|];
                }
            }

            public class ObjectClass
            {

            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void Test()
                {
                    ObjectClass obj = new ObjectClass();
                    Assert.Fail();
                }
            }

            public class ObjectClass
            {

            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenIsNullAssertion_ValueParameterAsReferenceObjectIsNotNullable_WithoutEnableNullable_NoDiagnostic()
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
                    Assert.IsNull(obj);
                }
            }

            public class ObjectClass
            {

            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenIsNullAssertion_ValueParameterAsReferenceObjectIsNullable_NoDiagnostic()
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

                    Assert.IsNull(obj);
                }
            }

            public class ObjectClass
            {

            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedTrue_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedTrue_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(true, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedTrue_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(message: "message", condition: true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsTrueIsPassedFalse_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertIsTrueIsPassedFalse_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(false, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertIsTrueIsPassedFalse_WithMessageAndArgsAsParams_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(false, "message", "1")|];
                    [|Assert.IsTrue(false, "message", "1", "2")|];
                    [|Assert.IsTrue(false, "message", new object[] { "1", "2" })|];
                    [|Assert.IsTrue(message: "message", parameters: new object[] { "1", "2" }, condition: false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message", "1");
                    Assert.Fail("message", "1", "2");
                    Assert.Fail("message", new object[] { "1", "2" });
                    Assert.Fail(message: "message", parameters: new object[] { "1", "2" });
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertIsTrueIsPassedFalse_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(message: "message", condition: false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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

    public async Task WhenAssertIsFalseIsPassedTrue_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsFalse(true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertIsFalseIsPassedTrue_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsFalse(true, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertIsFalseIsPassedTrue_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsFalse(message: "message", condition: true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertIsFalseIsPassedFalse_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedFalse_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(false, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsFalseIsPassedFalse_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(message: "message", condition: false);
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

    public async Task WhenAssertIsNotNullIsPassedNull_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsNotNull(null)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertIsNotNullIsPassedNull_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsNotNull(null, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertIsNotNullIsPassedNull_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsNotNull(message: "message", value: null)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertIsNotNullIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsNotNull(GetObject());
                }

                private static object GetObject() => new object();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsNotNullIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsNotNull(GetObject(), "message");
                }
            
                private static object GetObject() => new object();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertIsNotNullIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsNotNull(message: "message", value: GetObject());
                }
            
                private static object GetObject() => new object();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedEqual_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(true, true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedEqual_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(true, true, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedEqual_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(message: "message", expected: true, actual: true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedEqual_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(expected: true, message: "message", actual: true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreEqualIsPassedNonEqual_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(true, false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertAreEqualIsPassedNonEqual_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(true, false, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertAreEqualIsPassedNonEqual_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(message: "message", expected: true, actual: false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertAreEqualIsPassedNonEqual_WithMessageSecond_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(expected: true, message: "message", actual: false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                    Assert.AreEqual(expected: GetBoolean(), message: "message", actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedEqual_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(true, true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertAreNotEqualIsPassedEqual_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(true, true, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertAreNotEqualIsPassedEqual_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(message: "message", notExpected: true, actual: true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertAreNotEqualIsPassedEqual_WithMessageSecond_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(notExpected: true, message: "message", actual: true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task WhenAssertAreNotEqualIsPassedNonEqual_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(true, false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedNonEqual_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(true, false, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedNonEqual_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(message: "message", notExpected: true, actual: false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenAssertAreNotEqualIsPassedNonEqual_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(notExpected: true, message: "message", actual: false);
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
                    Assert.AreNotEqual(message: "message", notExpected: GetBoolean(), actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
