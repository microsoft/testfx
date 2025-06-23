// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseNewerAssertThrowsAnalyzer,
    MSTest.Analyzers.UseNewerAssertThrowsFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseNewerAssertThrowsAnalyzerTests
{
    [TestMethod]
    public async Task WhenAssertThrowsException_Diagnostic()
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
                    // action only overload
                    [|Assert.ThrowsException<Exception>(() => Console.WriteLine())|];
                    [|Assert.ThrowsException<Exception>(action: () => Console.WriteLine())|];
                    [|Assert.ThrowsExceptionAsync<Exception>(() => Task.CompletedTask)|];
                    [|Assert.ThrowsExceptionAsync<Exception>(action: () => Task.CompletedTask)|];

                    // action and message overload
                    [|Assert.ThrowsException<Exception>(() => Console.WriteLine(), "Message")|];
                    [|Assert.ThrowsException<Exception>(action: () => Console.WriteLine(), message: "Message")|];
                    [|Assert.ThrowsException<Exception>(message: "Message", action: () => Console.WriteLine())|];
                    [|Assert.ThrowsException<Exception>(action: () => Console.WriteLine(), "Message")|];
                    [|Assert.ThrowsException<Exception>(() => Console.WriteLine(), message: "Message")|];
                    [|Assert.ThrowsExceptionAsync<Exception>(() => Task.CompletedTask, "Message")|];
                    [|Assert.ThrowsExceptionAsync<Exception>(action: () => Task.CompletedTask, message: "Message")|];
                    [|Assert.ThrowsExceptionAsync<Exception>(message: "Message", action: () => Task.CompletedTask)|];
                    [|Assert.ThrowsExceptionAsync<Exception>(action: () => Task.CompletedTask, "Message")|];
                    [|Assert.ThrowsExceptionAsync<Exception>(() => Task.CompletedTask, message: "Message")|];
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    // action only overload
                    Assert.ThrowsExactly<Exception>(() => Console.WriteLine());
                    Assert.ThrowsExactly<Exception>(action: () => Console.WriteLine());
                    Assert.ThrowsExactlyAsync<Exception>(() => Task.CompletedTask);
                    Assert.ThrowsExactlyAsync<Exception>(action: () => Task.CompletedTask);
            
                    // action and message overload
                    Assert.ThrowsExactly<Exception>(() => Console.WriteLine(), "Message");
                    Assert.ThrowsExactly<Exception>(action: () => Console.WriteLine(), message: "Message");
                    Assert.ThrowsExactly<Exception>(message: "Message", action: () => Console.WriteLine());
                    Assert.ThrowsExactly<Exception>(action: () => Console.WriteLine(), "Message");
                    Assert.ThrowsExactly<Exception>(() => Console.WriteLine(), message: "Message");
                    Assert.ThrowsExactlyAsync<Exception>(() => Task.CompletedTask, "Message");
                    Assert.ThrowsExactlyAsync<Exception>(action: () => Task.CompletedTask, message: "Message");
                    Assert.ThrowsExactlyAsync<Exception>(message: "Message", action: () => Task.CompletedTask);
                    Assert.ThrowsExactlyAsync<Exception>(action: () => Task.CompletedTask, "Message");
                    Assert.ThrowsExactlyAsync<Exception>(() => Task.CompletedTask, message: "Message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertThrowsExceptionFuncOverloadExpressionBody_Diagnostic()
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
                    [|Assert.ThrowsException<Exception>(() => 5)|];
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.ThrowsExactly<Exception>(() => 5);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertThrowsExceptionFuncOverloadComplexBody_Diagnostic()
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
                    [|Assert.ThrowsException<Exception>(() =>
                    {
                        Console.WriteLine();
                        Func<object> x = () =>
                        {
                            int LocalFunction()
                            {
                                // This shouldn't be touched.
                                return 0;
                            }

                            // This shouldn't be touched.
                            return LocalFunction();
                        };

                        if (true)
                        {
                            return 1;
                        }
                        else if (true)
                            return 2;

                        return 3;
                    })|];
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.ThrowsExactly<Exception>(() =>
                    {
                        Console.WriteLine();
                        Func<object> x = () =>
                        {
                            int LocalFunction()
                            {
                                // This shouldn't be touched.
                                return 0;
                            }
            
                            // This shouldn't be touched.
                            return LocalFunction();
                        };
            
                        if (true)
                        {
                            return 1;
                        }
                        else if (true)
                            return 2;

                        return 3;
                    });
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertThrowsExceptionFuncOverloadVariable_Diagnostic()
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
                    Func<object> action = () => _ = 5;
                    [|Assert.ThrowsException<Exception>(action)|];
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Func<object> action = () => _ = 5;
                    Assert.ThrowsExactly<Exception>(action);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertThrowsExceptionFuncOverloadBinaryExpression_Diagnostic()
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
                    Func<object> action = () => _ = 5;
                    [|Assert.ThrowsException<Exception>(action + action)|];
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Func<object> action = () => _ = 5;
                    Assert.ThrowsExactly<Exception>(action + action);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task VariousTestCasesForDiscard()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public sealed class Test1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    int[] numbers = [1];
                    int x = 0;
                    string s = "";

                    [|Assert.ThrowsException<ArgumentException>(() => VoidMethod(1))|];
                    [|Assert.ThrowsException<ArgumentException>(() => NonVoidMethod(1))|];
                    [|Assert.ThrowsException<ArgumentException>(() => _ = NonVoidMethod(1))|];
                    [|Assert.ThrowsException<ArgumentException>(() => new Test1())|];
                    [|Assert.ThrowsException<ArgumentException>(() => _ = new Test1())|];
                    [|Assert.ThrowsException<ArgumentException>(() => numbers[0] = 4)|];
                    [|Assert.ThrowsException<ArgumentException>(() => x++)|];
                    [|Assert.ThrowsException<ArgumentException>(() => x--)|];
                    [|Assert.ThrowsException<ArgumentException>(() => ++x)|];
                    [|Assert.ThrowsException<ArgumentException>(() => --x)|];
                    [|Assert.ThrowsException<ArgumentException>(() => s!)|];
                    [|Assert.ThrowsException<ArgumentException>(() => !true)|];
                }

                private void VoidMethod(object o) => _ = o;

                private int NonVoidMethod(int i) => i;
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [TestClass]
            public sealed class Test1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    int[] numbers = [1];
                    int x = 0;
                    string s = "";

                    Assert.ThrowsExactly<ArgumentException>(() => VoidMethod(1));
                    Assert.ThrowsExactly<ArgumentException>(() => NonVoidMethod(1));
                    Assert.ThrowsExactly<ArgumentException>(() => _ = NonVoidMethod(1));
                    Assert.ThrowsExactly<ArgumentException>(() => new Test1());
                    Assert.ThrowsExactly<ArgumentException>(() => _ = new Test1());
                    Assert.ThrowsExactly<ArgumentException>(() => numbers[0] = 4);
                    Assert.ThrowsExactly<ArgumentException>(() => x++);
                    Assert.ThrowsExactly<ArgumentException>(() => x--);
                    Assert.ThrowsExactly<ArgumentException>(() => ++x);
                    Assert.ThrowsExactly<ArgumentException>(() => --x);
                    Assert.ThrowsExactly<ArgumentException>(() => s!);
                    Assert.ThrowsExactly<ArgumentException>(() => !true);
                }
            
                private void VoidMethod(object o) => _ = o;
            
                private int NonVoidMethod(int i) => i;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
