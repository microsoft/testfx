// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.ReviewAlwaysTrueAssertConditionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class ReviewAlwaysTrueAssertConditionAnalyzerTests
{
    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public async Task WhenIsNotNullAssertion_ValueParameterAsReferenceObjectIsNotNullableByNullabilityAnalysis_NoDiagnostic()
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
                    Assert.IsNotNull(obj);
                }
            }

            public class ObjectClass
            {

            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocal_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    [|Assert.AreEqual(x, x)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameParameter_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod(int x)
                {
                    [|Assert.AreEqual(x, x)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameField_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private int _value;

                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(_value, _value)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameProperty_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private int Value { get; set; }

                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(Value, Value)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSamePropertyChain_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private SomeType _value = new SomeType();

                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(_value.Inner, _value.Inner)|];
                }

                private sealed class SomeType
                {
                    public int Inner { get; set; }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedDifferentInstances_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var a = new SomeType();
                    var b = new SomeType();
                    Assert.AreEqual(a.Inner, b.Inner);
                }

                private sealed class SomeType
                {
                    public int Inner { get; set; }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedDifferentLocals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    int y = 1;
                    Assert.AreEqual(x, y);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameMethodCall_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(GetValue(), GetValue());
                }

                private int GetValue() => 1;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreSameIsPassedSameLocal_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    [|Assert.AreSame(x, x)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreSameIsPassedSameLocalWithNamedArgs_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    [|Assert.AreSame(actual: x, expected: x)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreSameIsPassedSameLocalParenthesized_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    [|Assert.AreSame((x), x)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreSameIsPassedDifferentLocals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    var y = new object();
                    Assert.AreSame(x, y);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithUserDefinedConversion_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new Wrapper();
                    Assert.AreEqual((int)x, (int)x);
                }

                private sealed class Wrapper
                {
                    private static int _counter;
                    public static explicit operator int(Wrapper value) => ++_counter;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithBuiltInConversion_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    [|Assert.AreEqual((long)x, (long)x)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedIndexerAccess_NoDiagnostic()
    {
        // list[0] and list[1] are IPropertyReferenceOperation on the same Item indexer + same
        // receiver, but the index arguments differ -- and the analyzer doesn't compare those.
        // We must NOT flag this as always-equal.
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var list = new List<string> { "a", "b" };
                    Assert.AreEqual(list[0], list[1]);
                    Assert.AreEqual(list[0], list[0]);
                    Assert.AreSame(list[0], list[1]);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithOverriddenEquals_NoDiagnostic()
    {
        // The type overrides object.Equals, so Assert.AreEqual routes through user code and the
        // self-comparison is a legitimate way to exercise the equality contract (see issue #9972).
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new MyType();
                    Assert.AreEqual(x, x);
                }

                private sealed class MyType
                {
                    public override bool Equals(object obj) => obj is MyType;
                    public override int GetHashCode() => 0;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithEquatable_NoDiagnostic()
    {
        // A sealed type implementing IEquatable<self> (without overriding object.Equals) routes equality
        // through user code, so it must not be flagged. This exercises the IEquatable<T> detection branch.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new MyType();
                    Assert.AreEqual(x, x);
                }

                private sealed class MyType : IEquatable<MyType>
                {
                    public bool Equals(MyType other) => true;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithOnlyEqualityOperator_Diagnostic()
    {
        // EqualityComparer<T>.Default (used by Assert.AreEqual) never calls operator ==; it uses
        // IEquatable<T>.Equals or the virtual object.Equals. A type that only overloads == (without
        // overriding Equals or implementing IEquatable<T>) still compares by reference, so the
        // self-comparison is genuinely always true and must be flagged.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new MyType();
                    [|Assert.AreEqual(x, x)|];
                }

            #pragma warning disable CS0660, CS0661
                private sealed class MyType
                {
                    public static bool operator ==(MyType left, MyType right) => true;
                    public static bool operator !=(MyType left, MyType right) => false;
                }
            #pragma warning restore CS0660, CS0661
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithEquatableOfOtherType_Diagnostic()
    {
        // The type implements IEquatable<string>, not IEquatable<MyType>, so EqualityComparer<MyType>.Default
        // falls back to reference equality and the self-comparison is genuinely always true.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new MyType();
                    [|Assert.AreEqual(x, x)|];
                }

                private sealed class MyType : IEquatable<string>
                {
                    public bool Equals(string other) => true;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameGenericTypeParameter_NoDiagnostic()
    {
        // T can be substituted with a type whose equality is not reflexive, so we cannot prove the
        // comparison is always true.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Helper(1);
                }

                private static void Helper<T>(T value)
                {
                    Assert.AreEqual(value, value);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithoutCustomEquality_Diagnostic()
    {
        // A reference type that does not customize equality falls back to reference equality,
        // so a self-comparison is genuinely always true and should still be flagged.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new MyType();
                    [|Assert.AreEqual(x, x)|];
                }

                private sealed class MyType
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameArray_Diagnostic()
    {
        // Arrays use reference equality, so a self-comparison is genuinely always true.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new int[0];
                    [|Assert.AreEqual(x, x)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalOfPolymorphicType_NoDiagnostic()
    {
        // A non-sealed reference type can hold a derived instance whose overridden Equals is not
        // reflexive, so equality cannot be proven from the static type.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    object x = new object();
                    Assert.AreEqual(x, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalOfNonSealedType_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new MyType();
                    Assert.AreEqual(x, x);
                }

                private class MyType
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedEqualConstantsWithCustomComparer_NoDiagnostic()
    {
        // A caller-supplied comparer can return any result, so the comparison is not provably always true.
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(1, 1, new NeverEqualComparer());
                }

                private sealed class NeverEqualComparer : IEqualityComparer<int>
                {
                    public bool Equals(int x, int y) => false;
                    public int GetHashCode(int obj) => 0;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithCustomComparer_NoDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    Assert.AreEqual(x, x, new NeverEqualComparer());
                }

                private sealed class NeverEqualComparer : IEqualityComparer<int>
                {
                    public bool Equals(int x, int y) => false;
                    public int GetHashCode(int obj) => 0;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameDateTime_Diagnostic()
    {
        // DateTime is a primitive-like value type with reflexive built-in equality, so a self-comparison
        // is genuinely always true and should still be flagged.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    DateTime x = DateTime.Now;
                    [|Assert.AreEqual(x, x)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameValueWithPolymorphicComparerType_NoDiagnostic()
    {
        // The comparison uses EqualityComparer<Base>.Default (the method type argument), which invokes the
        // non-reflexive IEquatable<Base>, even though the operand's static type is the sealed Derived.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new Derived();
                    Assert.AreEqual<Base>(x, x);
                }

                private class Base : IEquatable<Base>
                {
                    public bool Equals(Base other) => false;
                }

                private sealed class Derived : Base
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameStructWithNonReflexiveField_NoDiagnostic()
    {
        // A struct using the default field-based ValueType.Equals compares its fields via their equality,
        // so a field whose Equals is not reflexive makes the self-comparison return false.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new Wrapper();
                    Assert.AreEqual(x, x);
                }

                private struct Wrapper
                {
                    public NeverEqual Field;
                }

                private sealed class NeverEqual
                {
                    public override bool Equals(object obj) => false;
                    public override int GetHashCode() => 0;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameNullable_NoDiagnostic()
    {
        // Nullable<T> delegates equality to the underlying T, which may not be reflexive, so it is treated
        // conservatively.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int? x = 1;
                    Assert.AreEqual(x, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithNullComparer_Diagnostic()
    {
        // MSTest treats a null comparer as EqualityComparer<T>.Default, so the self-comparison is still
        // provably always true and must be flagged.
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    [|Assert.AreEqual(x, x, (IEqualityComparer<int>)null)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithDefaultComparer_Diagnostic()
    {
        // default(IEqualityComparer<int>) is null, which MSTest treats as EqualityComparer<T>.Default, so the
        // self-comparison is still provably always true. Only built-in conversions are stripped when inspecting
        // the comparer argument.
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    [|Assert.AreEqual(x, x, default(IEqualityComparer<int>))|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedSameLocalWithDefaultEqualityComparer_Diagnostic()
    {
        // EqualityComparer<T>.Default passed explicitly is the default comparer, so the self-comparison is
        // still provably always true and must be flagged.
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    [|Assert.AreEqual(x, x, EqualityComparer<int>.Default)|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
