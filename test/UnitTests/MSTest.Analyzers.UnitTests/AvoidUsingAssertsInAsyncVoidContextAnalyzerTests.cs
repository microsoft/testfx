// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidUsingAssertsInAsyncVoidContextAnalyzer,
    MSTest.Analyzers.AvoidUsingAssertsInAsyncVoidContextFixer>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AvoidUsingAssertsInAsyncVoidContextAnalyzerTests
{
    [TestMethod]
    public async Task UseAssertMethodInAsyncTaskMethod_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    Assert.Fail("");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncTaskDelegate_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                internal delegate Task MyDelegate();

                [TestMethod]
                public void TestMethod()
                {
                    MyDelegate d = async () =>
                    {
                        await Task.Delay(1);
                        Assert.Fail("");
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await Task.Delay(1);
                    [|Assert.Fail("")|];
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    await Task.Delay(1);
                    Assert.Fail("");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_WithoutTaskUsing_AddsTaskUsingInCorrectPosition()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                    [|Assert.Fail("")|];
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
                public async Task TestMethod()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                    Assert.Fail("");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInNonAsyncLambdaInsideAsyncVoidMethod_Diagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await Task.Delay(1);
                    Action action = () =>
                    {
                        [|Assert.Fail("")|];
                    };
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
                public async Task TestMethod()
                {
                    await Task.Delay(1);
                    Action action = () =>
                    {
                        Assert.Fail("");
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidDelegate_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                internal delegate void MyDelegate();

                [TestMethod]
                public void TestMethod()
                {
                    MyDelegate d = async () =>
                    {
                        await Task.Delay(1);
                        [|Assert.Fail("")|];
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidLocalFunction_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    async void d()
                    {
                        await Task.Delay(1);
                        [|Assert.Fail("")|];
                    };
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    async Task d()
                    {
                        await Task.Delay(1);
                        Assert.Fail("");
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseStringAssertMethodInAsyncVoidMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await Task.Delay(1);
                    [|StringAssert.Contains("abc", "a")|];
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    await Task.Delay(1);
                    StringAssert.Contains("abc", "a");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseCollectionAssertMethodInAsyncVoidMethod_Diagnostic()
    {
        string code = """
            using System.Collections;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await Task.Delay(1);
                    [|CollectionAssert.AreEqual(new[] { 1 }, new[] { 1 })|];
                }
            }
            """;

        string fixedCode = """
            using System.Collections;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    await Task.Delay(1);
                    CollectionAssert.AreEqual(new[] { 1 }, new[] { 1 });
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseStringAssertMethodInAsyncVoidLocalFunction_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    async void d()
                    {
                        await Task.Delay(1);
                        [|StringAssert.Contains("abc", "a")|];
                    };
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    async Task d()
                    {
                        await Task.Delay(1);
                        StringAssert.Contains("abc", "a");
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseCollectionAssertMethodInAsyncVoidDelegate_Diagnostic()
    {
        string code = """
            using System.Collections;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                internal delegate void MyDelegate();

                [TestMethod]
                public void TestMethod()
                {
                    MyDelegate d = async () =>
                    {
                        await Task.Delay(1);
                        [|CollectionAssert.AreEqual(new[] { 1 }, new[] { 1 })|];
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidLocalFunction_MissingTasksUsing_AddsUsing()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    async void d()
                    {
                        [|Assert.Fail("")|];
                    };
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    async Task d()
                    {
                        Assert.Fail("");
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInVirtualAsyncVoidMethod_NoCodeFix()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public virtual async void SetUp()
                {
                    await Task.Delay(1);
                    [|Assert.Fail("")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInExplicitInterfaceImplAsyncVoidMethod_NoCodeFix()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public interface ITestSetup
            {
                void SetUp();
            }

            [TestClass]
            public class MyTestClass : ITestSetup
            {
                async void ITestSetup.SetUp()
                {
                    await Task.Delay(1);
                    [|Assert.Fail("")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMultipleTimesInAsyncVoidMethod_BatchFix()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await Task.Delay(1);
                    [|Assert.IsTrue(true)|];
                    [|Assert.Fail("")|];
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    await Task.Delay(1);
                    Assert.IsTrue(true);
                    Assert.Fail("");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_WithNamespaceScopedUsing_NoExtraUsing()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                using System.Threading.Tasks;

                [TestClass]
                public class MyTestClass
                {
                    [TestMethod]
                    public async void TestMethod()
                    {
                        await Task.Delay(1);
                        [|Assert.Fail("")|];
                    }
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                using System.Threading.Tasks;

                [TestClass]
                public class MyTestClass
                {
                    [TestMethod]
                    public async Task TestMethod()
                    {
                        await Task.Delay(1);
                        Assert.Fail("");
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInOverrideAsyncVoidMethod_NoCodeFix()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestBase
            {
                public virtual async void SetUp()
                {
                    await Task.Delay(1);
                }
            }

            [TestClass]
            public class MyTestClass : MyTestBase
            {
                public override async void SetUp()
                {
                    await Task.Delay(1);
                    [|Assert.Fail("")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_MissingTasksUsing_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    [|Assert.Fail("")|];
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    Assert.Fail("");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_UsingsOrderedAlphabetically()
    {
        string code = """
            using System.Collections;
            using System.Xml;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                    [|Assert.Fail("")|];
                    _ = nameof(XmlReader);
                }
            }
            """;

        string fixedCode = """
            using System.Collections;
            using System.Threading.Tasks;
            using System.Xml;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                    Assert.Fail("");
                    _ = nameof(XmlReader);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_MultipleNamespaces_AddsUsingInCorrectScope()
    {
        // Namespace A has the using; namespace B contains the async void method to fix.
        // The fixer must ensure 'Task' resolves at the method's location (namespace B),
        // not just rely on the using existing somewhere in the file.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace A
            {
                using System.Threading.Tasks;

                public class Helper
                {
                    public Task DoAsync() => Task.CompletedTask;
                }
            }

            namespace B
            {
                [TestClass]
                public class MyTestClass
                {
                    [TestMethod]
                    public async void TestMethod()
                    {
                        await System.Threading.Tasks.Task.Delay(1);
                        [|Assert.Fail("")|];
                    }
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace A
            {
                using System.Threading.Tasks;

                public class Helper
                {
                    public Task DoAsync() => Task.CompletedTask;
                }
            }

            namespace B
            {
                using System.Threading.Tasks;

                [TestClass]
                public class MyTestClass
                {
                    [TestMethod]
                    public async Task TestMethod()
                    {
                        await System.Threading.Tasks.Task.Delay(1);
                        Assert.Fail("");
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_FileScopedNamespace_AddsUsingAtFileScope()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                    [|Assert.Fail("")|];
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                    Assert.Fail("");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_BatchFixAcrossNamespaces_AddsUsingInEachNamespace()
    {
        // FixAll/BatchFixer scenario: two async void methods in two separate namespaces, both needing
        // the 'using System.Threading.Tasks;' to be added inside their own namespace.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace A
            {
                [TestClass]
                public class TestClassA
                {
                    [TestMethod]
                    public async void TestMethodA()
                    {
                        await System.Threading.Tasks.Task.Delay(1);
                        [|Assert.Fail("")|];
                    }
                }
            }

            namespace B
            {
                [TestClass]
                public class TestClassB
                {
                    [TestMethod]
                    public async void TestMethodB()
                    {
                        await System.Threading.Tasks.Task.Delay(1);
                        [|Assert.Fail("")|];
                    }
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace A
            {
                using System.Threading.Tasks;

                [TestClass]
                public class TestClassA
                {
                    [TestMethod]
                    public async Task TestMethodA()
                    {
                        await System.Threading.Tasks.Task.Delay(1);
                        Assert.Fail("");
                    }
                }
            }

            namespace B
            {
                using System.Threading.Tasks;

                [TestClass]
                public class TestClassB
                {
                    [TestMethod]
                    public async Task TestMethodB()
                    {
                        await System.Threading.Tasks.Task.Delay(1);
                        Assert.Fail("");
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_GlobalUsingPresent_InsertsAfterGlobalUsings()
    {
        // C# 10+ same-file 'global using' directives must precede non-global usings.
        // Verify the new 'using System.Threading.Tasks;' is inserted after the global block,
        // not before it (which would be a compile error).
        string code = """
            global using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                    var _ = nameof(Console);
                    [|Assert.Fail("")|];
                }
            }
            """;

        string fixedCode = """
            global using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                    var _ = nameof(Console);
                    Assert.Fail("");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
