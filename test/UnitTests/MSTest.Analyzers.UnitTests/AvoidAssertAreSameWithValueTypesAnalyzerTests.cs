// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidAssertAreSameWithValueTypesAnalyzer,
    MSTest.Analyzers.AvoidAssertAreSameWithValueTypesFixer>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AvoidAssertAreSameWithValueTypesAnalyzerTests
{
    [TestMethod]
    public async Task UseAssertAreSameWithValueTypes_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // Both are value types
                    [|Assert.AreSame(0, 0)|];
                    [|Assert.AreSame(0, 0, "Message")|];
                    [|Assert.AreSame(message: "Message", expected: 0, actual: 0)|];

                    [|Assert.AreSame<object>(0, 0)|];
                    [|Assert.AreSame<object>(0, 0, "Message")|];
                    [|Assert.AreSame<object>(message: "Message", expected: 0, actual: 0)|];

                    // Expected is value type. This is always-failing assert.
                    [|Assert.AreSame<object>("0", 0)|];
                    [|Assert.AreSame<object>("0", 0, "Message")|];
                    [|Assert.AreSame<object>(message: "Message", expected: "0", actual: 0)|];

                    // Actual is value type. This is always-failing assert.
                    [|Assert.AreSame<object>(0, "0")|];
                    [|Assert.AreSame<object>(0, "0", "Message")|];
                    [|Assert.AreSame<object>(message: "Message", expected: 0, actual: "0")|];

                    // Both are reference types. No diagnostic.
                    Assert.AreSame("0", "0");
                    Assert.AreSame("0", "0", "Message");
                    Assert.AreSame(message: "Message", expected: "0", actual: "0");
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // Both are value types
                    Assert.AreEqual(0, 0);
                    Assert.AreEqual(0, 0, "Message");
                    Assert.AreEqual(message: "Message", expected: 0, actual: 0);

                    Assert.AreEqual<object>(0, 0);
                    Assert.AreEqual<object>(0, 0, "Message");
                    Assert.AreEqual<object>(message: "Message", expected: 0, actual: 0);

                    // Expected is value type. This is always-failing assert.
                    Assert.AreEqual<object>("0", 0);
                    Assert.AreEqual<object>("0", 0, "Message");
                    Assert.AreEqual<object>(message: "Message", expected: "0", actual: 0);

                    // Actual is value type. This is always-failing assert.
                    Assert.AreEqual<object>(0, "0");
                    Assert.AreEqual<object>(0, "0", "Message");
                    Assert.AreEqual<object>(message: "Message", expected: 0, actual: "0");

                    // Both are reference types. No diagnostic.
                    Assert.AreSame("0", "0");
                    Assert.AreSame("0", "0", "Message");
                    Assert.AreSame(message: "Message", expected: "0", actual: "0");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertAreNotSameWithValueTypes_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // Both are value types
                    [|Assert.AreNotSame(0, 1)|];
                    [|Assert.AreNotSame(0, 1, "Message")|];
                    [|Assert.AreNotSame(message: "Message", notExpected: 0, actual: 1)|];

                    [|Assert.AreNotSame<object>(0, 1)|];
                    [|Assert.AreNotSame<object>(0, 1, "Message")|];
                    [|Assert.AreNotSame<object>(message: "Message", notExpected: 0, actual: 1)|];

                    // Expected is value type. This is always-failing assert.
                    [|Assert.AreNotSame<object>("0", 1)|];
                    [|Assert.AreNotSame<object>("0", 1, "Message")|];
                    [|Assert.AreNotSame<object>(message: "Message", notExpected: "0", actual: 1)|];

                    // Actual is value type. This is always-failing assert.
                    [|Assert.AreNotSame<object>(0, "1")|];
                    [|Assert.AreNotSame<object>(0, "1", "Message")|];
                    [|Assert.AreNotSame<object>(message: "Message", notExpected: 0, actual: "1")|];

                    // Both are reference types. No diagnostic.
                    Assert.AreNotSame("0", "1");
                    Assert.AreNotSame("0", "1", "Message");
                    Assert.AreNotSame(message: "Message", notExpected: "0", actual: "1");
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // Both are value types
                    Assert.AreNotEqual(0, 1);
                    Assert.AreNotEqual(0, 1, "Message");
                    Assert.AreNotEqual(message: "Message", notExpected: 0, actual: 1);

                    Assert.AreNotEqual<object>(0, 1);
                    Assert.AreNotEqual<object>(0, 1, "Message");
                    Assert.AreNotEqual<object>(message: "Message", notExpected: 0, actual: 1);

                    // Expected is value type. This is always-failing assert.
                    Assert.AreNotEqual<object>("0", 1);
                    Assert.AreNotEqual<object>("0", 1, "Message");
                    Assert.AreNotEqual<object>(message: "Message", notExpected: "0", actual: 1);

                    // Actual is value type. This is always-failing assert.
                    Assert.AreNotEqual<object>(0, "1");
                    Assert.AreNotEqual<object>(0, "1", "Message");
                    Assert.AreNotEqual<object>(message: "Message", notExpected: 0, actual: "1");

                    // Both are reference types. No diagnostic.
                    Assert.AreNotSame("0", "1");
                    Assert.AreNotSame("0", "1", "Message");
                    Assert.AreNotSame(message: "Message", notExpected: "0", actual: "1");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task AnalyzerMessageShouldBeCorrect()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    {|#0:Assert.AreSame(0, 0)|};
                    {|#1:Assert.AreNotSame(0, 1)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(0, 0);
                    Assert.AreNotEqual(0, 1);
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("AreEqual", "AreSame"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("AreNotEqual", "AreNotSame"),
            },
        }.RunAsync();
    }
}
