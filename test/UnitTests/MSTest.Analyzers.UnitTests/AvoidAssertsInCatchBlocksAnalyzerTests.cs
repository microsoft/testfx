// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
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
}
