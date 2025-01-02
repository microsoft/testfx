// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidAssertAreSameWithValueTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

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
                    [|Assert.AreSame(0, 0, "Message {0}", "Arg")|];
                    [|Assert.AreSame(message: "Message", expected: 0, actual: 0)|];

                    [|Assert.AreSame<object>(0, 0)|];
                    [|Assert.AreSame<object>(0, 0, "Message")|];
                    [|Assert.AreSame<object>(0, 0, "Message {0}", "Arg")|];
                    [|Assert.AreSame<object>(message: "Message", expected: 0, actual: 0)|];

                    // Expected is value type. This is always-failing assert.
                    [|Assert.AreSame<object>("0", 0)|];
                    [|Assert.AreSame<object>("0", 0, "Message")|];
                    [|Assert.AreSame<object>("0", 0, "Message {0}", "Arg")|];
                    [|Assert.AreSame<object>(message: "Message", expected: "0", actual: 0)|];

                    // Actual is value type. This is always-failing assert.
                    [|Assert.AreSame<object>(0, "0")|];
                    [|Assert.AreSame<object>(0, "0", "Message")|];
                    [|Assert.AreSame<object>(0, "0", "Message {0}", "Arg")|];
                    [|Assert.AreSame<object>(message: "Message", expected: 0, actual: "0")|];

                    // Both are reference types. No diagnostic.
                    Assert.AreSame("0", "0");
                    Assert.AreSame("0", "0", "Message");
                    Assert.AreSame("0", "0", "Message {0}", "Arg");
                    Assert.AreSame(message: "Message", expected: "0", actual: "0");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
