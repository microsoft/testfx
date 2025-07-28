// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssertThrowsShouldContainSingleStatementAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

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
            using System.Threading.Tasks;
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

                    // Multiple statements with ThrowsExactly - should be flagged
                    [|Assert.ThrowsExactly<Exception>(() =>
                    {
                        var value = 42;
                        Console.WriteLine(value);
                        DoSomething();
                    })|];
                }

                [TestMethod]
                public async Task MyAsyncTestMethod()
                {
                    // Multiple statements with ThrowsAsync - should be flagged
                    [|Assert.ThrowsAsync<Exception>(() =>
                    {
                        var value = 42;
                        Console.WriteLine(value);
                        return Task.CompletedTask;
                    })|];

                    // Multiple statements with ThrowsExactlyAsync - should be flagged
                    [|Assert.ThrowsExactlyAsync<Exception>(() =>
                    {
                        var value = 42;
                        Console.WriteLine(value);
                        return Task.CompletedTask;
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

    [TestMethod]
    public async Task WhenAssertThrowsWithExpressionBody_NoDiagnostic()
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
                    // Expression-bodied lambda - should NOT be flagged
                    Assert.Throws<Exception>(() => DoSomething());
                    Assert.ThrowsExactly<Exception>(() => DoSomething());
                    
                    // Expression-bodied with method chain - should NOT be flagged
                    Assert.Throws<Exception>(() => "test".ToUpper().ToLower());
                }

                private static void DoSomething() => throw new Exception();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsWithEmptyStatements_NoDiagnostic()
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
                    // Single statement with empty statements - should NOT be flagged
                    Assert.Throws<Exception>(() =>
                    {
                        DoSomething();
                        ; // empty statement
                    });
                }

                private static void DoSomething() => throw new Exception();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsWithMultipleNonEmptyStatements_Diagnostic()
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
                    // Multiple non-empty statements - should be flagged
                    [|Assert.Throws<Exception>(() =>
                    {
                        DoSomething();
                        DoSomething();
                        ; // empty statement
                    })|];
                }

                private static void DoSomething() => throw new Exception();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsContainsMultipleStatementsOnSameLine_Diagnostic()
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
                    // Multiple statements on same line - should be flagged (shows we count statements, not lines)
                    [|Assert.Throws<Exception>(() => { DoSomething(); DoSomething(); })|];

                    // Multiple statements on same line in block - should be flagged
                    [|Assert.Throws<Exception>(() =>
                    {
                        DoSomething(); DoSomething();
                    })|];
                }

                private static void DoSomething() => throw new Exception();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
