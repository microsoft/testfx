// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.Testing.EmptyDiagnosticAnalyzer,
    MSTest.Analyzers.RemoveClassCleanupBehaviorArgumentFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class RemoveClassCleanupBehaviorArgumentFixerTests
{
    [TestMethod]
    public async Task WhenClassCleanupBehaviorArgument_Simple()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace TestProject;

            [TestClass]
            public class UnitTest1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [ClassCleanup({|CS0103:ClassCleanupBehavior|}.EndOfClass)]
                public void ClassClean()
                {
                }
            }

            [TestClass]
            public class UnitTest2
            {
                [TestMethod]
                public void TestMethod2()
                {
                }

                [{|CS1729:ClassCleanup(InheritanceBehavior.None, {|CS0103:ClassCleanupBehavior|}.EndOfClass)|}]
                public void ClassClean()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace TestProject;

            [TestClass]
            public class UnitTest1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [ClassCleanup]
                public void ClassClean()
                {
                }
            }

            [TestClass]
            public class UnitTest2
            {
                [TestMethod]
                public void TestMethod2()
                {
                }

                [ClassCleanup(InheritanceBehavior.None)]
                public void ClassClean()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenClassCleanupBehaviorIsFirstArgument_RemovesBehaviorKeepsInheritance()
    {
        // Exercises the branch where ClassCleanupBehavior appears before InheritanceBehavior,
        // ensuring the first argument is removed and the remaining one is preserved.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace TestProject;

            [TestClass]
            public class UnitTest1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [{|CS1729:ClassCleanup({|CS0103:ClassCleanupBehavior|}.EndOfClass, InheritanceBehavior.None)|}]
                public void ClassClean()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace TestProject;

            [TestClass]
            public class UnitTest1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [ClassCleanup(InheritanceBehavior.None)]
                public void ClassClean()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenClassCleanupBehaviorReferencedOutsideAttribute_NoFix()
    {
        // Exercises the guard: the fixer only applies when ClassCleanupBehavior appears
        // inside an AttributeArgument. A reference in a regular method body should not be fixed.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace TestProject;

            [TestClass]
            public class UnitTest1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    _ = {|CS0103:ClassCleanupBehavior|}.EndOfClass;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassCleanupBehaviorWithMultiLineBody_PreservesIndentation()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace TestProject;

            [TestClass]
            public class UnitTest1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [ClassCleanup({|CS0103:ClassCleanupBehavior|}.EndOfClass)]
                public void ClassClean()
                {
                    Cleanup(
                        1,
                        2,
                        3);
                }

                private void Cleanup(int a, int b, int c) { }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace TestProject;

            [TestClass]
            public class UnitTest1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [ClassCleanup]
                public void ClassClean()
                {
                    Cleanup(
                        1,
                        2,
                        3);
                }

                private void Cleanup(int a, int b, int c) { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
