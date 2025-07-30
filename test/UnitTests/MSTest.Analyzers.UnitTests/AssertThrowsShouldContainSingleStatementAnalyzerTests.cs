// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssertThrowsShouldContainSingleStatementAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.AssertThrowsShouldContainSingleStatementAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AssertThrowsShouldContainSingleStatementAnalyzerTests
{
    [TestMethod]
    public async Task WhenAssertThrowsContainsMultipleStatements_CSharp_Diagnostic()
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
    public async Task WhenAssertThrowsContainsMultipleStatements_VB_Diagnostic()
    {
        string code = """
            Imports System
            Imports System.Threading.Tasks
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    ' Multiple statements in Assert.Throws - should be flagged
                    [|Assert.Throws(Of Exception)(Sub()
                        Console.WriteLine("First")
                        Console.WriteLine("Second")
                    End Sub)|]

                    ' Multiple statements in Assert.ThrowsExactly - should be flagged
                    [|Assert.ThrowsExactly(Of Exception)(Sub()
                        Console.WriteLine("First")
                        Console.WriteLine("Second")
                    End Sub)|]
                End Sub

                <TestMethod>
                Public Async Function MyAsyncTestMethod() As Task
                    ' Multiple statements in Assert.ThrowsAsync - should be flagged
                    [|Assert.ThrowsAsync(Of Exception)(Function()
                        Console.WriteLine("First")
                        Return Task.CompletedTask
                    End Function)|]

                    ' Multiple statements in Assert.ThrowsExactlyAsync - should be flagged
                    [|Assert.ThrowsExactlyAsync(Of Exception)(Function()
                        Console.WriteLine("First")
                        Return Task.CompletedTask
                    End Function)|]
                End Function
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsContainsMultipleStatementsWithVariableDeclarations_CSharp_Diagnostic()
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
    public async Task WhenAssertThrowsContainsMultipleStatementsWithVariableDeclarations_VB_Diagnostic()
    {
        string code = """
            Imports System
            Imports System.Threading.Tasks
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    ' Multiple statements including variable declarations - should be flagged
                    [|Assert.Throws(Of Exception)(Sub()
                        Dim value As Integer = 42
                        Console.WriteLine(value)
                        DoSomething()
                    End Sub)|]

                    ' Multiple statements with ThrowsExactly - should be flagged
                    [|Assert.ThrowsExactly(Of Exception)(Sub()
                        Dim value As Integer = 42
                        Console.WriteLine(value)
                        DoSomething()
                    End Sub)|]
                End Sub

                <TestMethod>
                Public Async Function MyAsyncTestMethod() As Task
                    ' Multiple statements with ThrowsAsync - should be flagged
                    [|Assert.ThrowsAsync(Of Exception)(Function()
                        Dim value As Integer = 42
                        Console.WriteLine(value)
                        Return Task.CompletedTask
                    End Function)|]

                    ' Multiple statements with ThrowsExactlyAsync - should be flagged
                    [|Assert.ThrowsExactlyAsync(Of Exception)(Function()
                        Dim value As Integer = 42
                        Console.WriteLine(value)
                        Return Task.CompletedTask
                    End Function)|]
                End Function

                Private Shared Sub DoSomething()
                    Throw New Exception()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsContainsSingleStatement_CSharp_NoDiagnostic()
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
    public async Task WhenAssertThrowsContainsSingleStatement_VB_NoDiagnostic()
    {
        string code = """
            Imports System
            Imports System.Threading.Tasks
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    ' Single statement - should NOT be flagged
                    Assert.Throws(Of Exception)(Sub() Console.WriteLine("Only one"))

                    ' Single expression - should NOT be flagged
                    Assert.Throws(Of Exception)(Sub() DoSomething())

                    ' Single statement in block - should NOT be flagged
                    Assert.Throws(Of Exception)(Sub()
                        DoSomething()
                    End Sub)

                    ' Single statement with ThrowsExactly - should NOT be flagged
                    Assert.ThrowsExactly(Of Exception)(Sub() DoSomething())
                End Sub

                <TestMethod>
                Public Async Function MyAsyncTestMethod() As Task
                    ' Single async statement - should NOT be flagged
                    Await Assert.ThrowsAsync(Of Exception)(Function() Task.CompletedTask)

                    ' Single async statement in block - should NOT be flagged
                    Await Assert.ThrowsAsync(Of Exception)(Function()
                        Return Task.CompletedTask
                    End Function)

                    ' Single async statement with ThrowsExactlyAsync - should NOT be flagged
                    Await Assert.ThrowsExactlyAsync(Of Exception)(Function() Task.CompletedTask)
                End Function

                Private Shared Sub DoSomething()
                    Throw New Exception()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingOtherAssertMethods_CSharp_NoDiagnostic()
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
    public async Task WhenUsingOtherAssertMethods_VB_NoDiagnostic()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    ' Other Assert methods should not be flagged even with multiple statements
                    Assert.IsTrue(true)
                    Assert.AreEqual(1, 1)
                    Assert.IsNotNull("test")

                    ' Non-Assert.Throws methods should not be analyzed
                    Dim action As new Action(Sub()
                        Console.WriteLine("First")
                        Console.WriteLine("Second")
                    End Sub)
                    action()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsHasMessageParameter_CSharp_StillAnalyzes()
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
    public async Task WhenAssertThrowsHasMessageParameter_VB_StillAnalyzes()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    ' Multiple statements with message parameter - should be flagged
                    [|Assert.Throws(Of Exception)(Sub()
                        Console.WriteLine("First")
                        Console.WriteLine("Second")
                    End Sub, "Custom message")|]

                    ' Single statement with message parameter - should NOT be flagged
                    Assert.Throws(Of Exception)(Sub() DoSomething(), "Custom message")
                End Sub

                Private Shared Sub DoSomething()
                    Throw New Exception()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsWithExpressionBody_CSharp_NoDiagnostic()
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
    public async Task WhenAssertThrowsWithExpressionBody_VB_NoDiagnostic()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    ' Expression-bodied lambda - should NOT be flagged
                    Assert.Throws(Of Exception)(Sub() DoSomething())
                    Assert.ThrowsExactly(Of Exception)(Sub() DoSomething())
                    
                    ' Expression-bodied with method chain - should NOT be flagged
                    Assert.Throws(Of Exception)(Function() "test".ToUpper().ToLower())
                End Sub

                Private Shared Sub DoSomething()
                    Throw New Exception()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertThrowsWithEmptyStatements_CSharp_NoDiagnostic()
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
    public async Task WhenAssertThrowsWithMultipleNonEmptyStatements_CSharp_Diagnostic()
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
    public async Task WhenAssertThrowsContainsMultipleStatementsOnSameLine_CSharp_Diagnostic()
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
