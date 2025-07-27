// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeVerifier<
    MSTest.Analyzers.AssertThrowsShouldContainSingleStatementAnalyzer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AssertThrowsShouldContainSingleStatementAnalyzerTests
{
    [TestMethod]
    public async Task WhenAssertThrowsContainsMultipleStatements_Diagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    // Multiple statements in Assert.Throws - should be flagged
                    [|Assert.Throws<Exception>(() =>
                    {
                        Console.WriteLine("First");
                        Console.WriteLine("Second");
                    })|];

                    // Multiple statements in Assert.ThrowsExactly - should be flagged
                    [|Assert.ThrowsExactly<Exception>(() =>
                    {
                        Console.WriteLine("First");
                        Console.WriteLine("Second");
                    })|];
                }

                [TestMethod]
                public async Task MyAsyncTestMethod()
                {
                    // Multiple statements in Assert.ThrowsAsync - should be flagged
                    [|Assert.ThrowsAsync<Exception>(() =>
                    {
                        Console.WriteLine("First");
                        return Task.CompletedTask;
                    })|];

                    // Multiple statements in Assert.ThrowsExactlyAsync - should be flagged
                    [|Assert.ThrowsExactlyAsync<Exception>(() =>
                    {
                        Console.WriteLine("First");
                        return Task.CompletedTask;
                    })|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsContainsMultipleStatementsWithVariableDeclarations_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    // Multiple statements including variable declarations - should be flagged
                    [|Assert.Throws<Exception>(() =>
                    {
                        var value = 42;
                        Console.WriteLine(value);
                        DoSomething();
                    })|];
                }

                private static void DoSomething() => throw new Exception();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsContainsSingleStatement_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    // Single statement - should NOT be flagged
                    Assert.Throws<Exception>(() => Console.WriteLine("Only one"));

                    // Single expression - should NOT be flagged
                    Assert.Throws<Exception>(() => DoSomething());

                    // Single statement in block - should NOT be flagged
                    Assert.Throws<Exception>(() =>
                    {
                        DoSomething();
                    });

                    // Single statement with ThrowsExactly - should NOT be flagged
                    Assert.ThrowsExactly<Exception>(() => DoSomething());
                }

                [TestMethod]
                public async Task MyAsyncTestMethod()
                {
                    // Single async statement - should NOT be flagged
                    await Assert.ThrowsAsync<Exception>(() => Task.CompletedTask);

                    // Single async statement in block - should NOT be flagged
                    await Assert.ThrowsAsync<Exception>(() =>
                    {
                        return Task.CompletedTask;
                    });

                    // Single async statement with ThrowsExactlyAsync - should NOT be flagged
                    await Assert.ThrowsExactlyAsync<Exception>(() => Task.CompletedTask);
                }

                private static void DoSomething() => throw new Exception();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsContainsSingleExpressionStatement_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    // Single expression statement - should NOT be flagged
                    Assert.Throws<Exception>(() => { DoSomething(); });

                    // Single variable declaration and usage - should be flagged as it's multiple statements
                    [|Assert.Throws<Exception>(() =>
                    {
                        var value = 42;
                        DoSomething(value);
                    })|];
                }

                private static void DoSomething() => throw new Exception();
                private static void DoSomething(int value) => throw new Exception();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingOtherAssertMethods_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    // Other Assert methods should not be flagged even with multiple statements
                    Assert.IsTrue(true);
                    Assert.AreEqual(1, 1);
                    Assert.IsNotNull("test");

                    // Non-Assert.Throws methods should not be analyzed
                    var action = new Action(() =>
                    {
                        Console.WriteLine("First");
                        Console.WriteLine("Second");
                    });
                    action();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsHasMessageParameter_StillAnalyzes()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    // Multiple statements with message parameter - should be flagged
                    [|Assert.Throws<Exception>(() =>
                    {
                        Console.WriteLine("First");
                        Console.WriteLine("Second");
                    }, "Custom message")|];

                    // Single statement with message parameter - should NOT be flagged
                    Assert.Throws<Exception>(() => DoSomething(), "Custom message");
                }

                private static void DoSomething() => throw new Exception();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}