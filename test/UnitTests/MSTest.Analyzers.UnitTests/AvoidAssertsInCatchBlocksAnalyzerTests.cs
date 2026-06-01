// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidAssertsInCatchBlocksAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.AvoidAssertsInCatchBlocksAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AvoidAssertsInCatchBlocksAnalyzerTests
{
    [TestMethod]
    public async Task AssertOutsideCatchBlock_NoDiagnostic()
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

    [TestMethod]
    public async Task AssertInTryBlock_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        Assert.IsTrue(true);
                    }
                    catch
                    {
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task AssertInCatchBlock_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        // code that may throw
                    }
                    catch
                    {
                        [|Assert.Fail("Exception was thrown")|];
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task AssertInCatchBlockWithExceptionVariable_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        throw new InvalidOperationException("test");
                    }
                    catch (InvalidOperationException ex)
                    {
                        [|Assert.IsNotNull(ex)|];
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task StringAssertInCatchBlock_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        throw new InvalidOperationException("test message");
                    }
                    catch (InvalidOperationException ex)
                    {
                        [|StringAssert.Contains(ex.Message, "test")|];
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task CollectionAssertInCatchBlock_Diagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        throw new InvalidOperationException("test");
                    }
                    catch
                    {
                        var list = new List<int> { 1, 2, 3 };
                        [|CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, list)|];
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task MultipleAssertsInCatchBlock_MultipleDiagnostics()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        throw new InvalidOperationException("test");
                    }
                    catch (InvalidOperationException ex)
                    {
                        [|Assert.IsNotNull(ex)|];
                        [|Assert.AreEqual("test", ex.Message)|];
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task AssertInFinallyBlock_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        // code
                    }
                    finally
                    {
                        Assert.IsTrue(true);
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task AssertInNestedCatchBlock_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        try
                        {
                            throw new InvalidOperationException();
                        }
                        catch
                        {
                            [|Assert.Fail("Inner exception")|];
                        }
                    }
                    catch
                    {
                        [|Assert.Fail("Outer exception")|];
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task AssertInCatchBlockWithSpecificExceptionType_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        throw new ArgumentNullException("param");
                    }
                    catch (ArgumentNullException ex)
                    {
                        [|Assert.IsNotNull(ex)|];
                    }
                    catch (Exception ex)
                    {
                        [|Assert.Fail("Wrong exception type")|];
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task AssertInconclusiveInFilteredCatch_NoDiagnostic()
    {
        // Exemption: Assert.Inconclusive inside a filtered catch (catch ... when ...) is the only
        // way to demote a caught failure to an Inconclusive outcome and must remain allowed.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        throw new AssertFailedException("no exception was thrown.");
                    }
                    catch (AssertFailedException ex) when (ex.Message.Contains("no exception was thrown."))
                    {
                        Assert.Inconclusive("Environment is not properly set up");
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task AssertInconclusiveInUnfilteredCatch_Diagnostic()
    {
        // Unfiltered catch still falls through silently if no exception is thrown, so keep flagging it.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        // code
                    }
                    catch (Exception)
                    {
                        [|Assert.Inconclusive("Environmental issue")|];
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task AssertFailInFilteredCatch_Diagnostic()
    {
        // Assert.Fail has no unique value in a catch — even with a filter — because MSTest already
        // converts any unhandled exception in a test method to a Failed outcome.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        // code
                    }
                    catch (Exception ex) when (ex.Message.Contains("foo"))
                    {
                        [|Assert.Fail("Got foo, but unexpected")|];
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task ThrowNewAssertFailedExceptionInCatchBlock_Diagnostic()
    {
        // Desugared form of Assert.Fail — same fall-through risk, same diagnostic.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        // code
                    }
                    catch (InvalidOperationException ex)
                    {
                        [|throw new AssertFailedException("wrapped", ex);|]
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task ThrowNewAssertInconclusiveExceptionInUnfilteredCatch_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        // code
                    }
                    catch (Exception)
                    {
                        [|throw new AssertInconclusiveException("inconclusive");|]
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task ThrowNewAssertInconclusiveExceptionInFilteredCatch_NoDiagnostic()
    {
        // Same exemption as Assert.Inconclusive in a filtered catch: outcome demotion is legitimate.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        throw new AssertFailedException("no exception was thrown.");
                    }
                    catch (AssertFailedException ex) when (ex.Message.Contains("no exception was thrown."))
                    {
                        throw new AssertInconclusiveException("Environment is not properly set up");
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task RethrowInCatchBlock_NoDiagnostic()
    {
        // Bare 'throw;' rethrow propagates the caught exception unchanged — no new MSTest outcome
        // is being introduced at the catch site, so the rule should not fire.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        throw new AssertFailedException("boom");
                    }
                    catch (AssertFailedException)
                    {
                        throw;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task ThrowCaughtAssertFailedExceptionVariableInCatchBlock_NoDiagnostic()
    {
        // 'throw ex;' propagates an already-thrown exception; not flagged because no new exception
        // is being constructed in the catch.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        throw new AssertFailedException("boom");
                    }
                    catch (AssertFailedException ex)
                    {
                        throw ex;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task ThrowNewAssertFailedExceptionOutsideCatchBlock_NoDiagnostic()
    {
        // The rule is specific to catch blocks. Throwing AssertFailedException directly from a test
        // body is allowed (it is equivalent to Assert.Fail and outside the rule's scope).
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    throw new AssertFailedException("direct fail");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task ThrowNewAssertFailedExceptionDerivedTypeInCatchBlock_Diagnostic()
    {
        // Derived MSTest assertion exception types are still flagged — same fall-through risk.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyAssertFailedException : AssertFailedException
            {
                public MyAssertFailedException(string message) : base(message) { }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    try
                    {
                        // code
                    }
                    catch (Exception)
                    {
                        [|throw new MyAssertFailedException("derived");|]
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task VB_AssertOutsideCatchBlock_NoDiagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    Assert.IsTrue(True)
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task VB_AssertInCatchBlock_Diagnostic()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    Try
                        ' code that may throw
                    Catch
                        [|Assert.Fail("Exception was thrown")|]
                    End Try
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task VB_AssertInCatchBlockWithExceptionVariable_Diagnostic()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    Try
                        Throw New InvalidOperationException("test")
                    Catch ex As InvalidOperationException
                        [|Assert.IsNotNull(ex)|]
                    End Try
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task VB_AssertInconclusiveInFilteredCatch_NoDiagnostic()
    {
        // Exemption: Assert.Inconclusive inside a filtered catch (Catch ... When ...) is the only
        // way to demote a caught failure to an Inconclusive outcome and must remain allowed.
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    Try
                        Throw New AssertFailedException("no exception was thrown.")
                    Catch ex As AssertFailedException When ex.Message.Contains("no exception was thrown.")
                        Assert.Inconclusive("Environment is not properly set up")
                    End Try
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task VB_StringAssertInCatchBlock_Diagnostic()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    Try
                        Throw New InvalidOperationException("test message")
                    Catch ex As InvalidOperationException
                        [|StringAssert.Contains(ex.Message, "test")|]
                    End Try
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }
}
