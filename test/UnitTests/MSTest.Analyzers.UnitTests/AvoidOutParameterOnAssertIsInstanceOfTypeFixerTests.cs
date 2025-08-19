// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.Testing.EmptyDiagnosticAnalyzer,
    MSTest.Analyzers.AvoidOutParameterOnAssertIsInstanceOfTypeFixer>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AvoidOutParameterOnAssertIsInstanceOfTypeFixerTests
{
    [TestMethod]
    public async Task FixIsInstanceOfTypeWithOutVar()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                    object value = "test";
                    /*trivia1*/Assert.IsInstanceOfType<string>(value, out {|CS1615:var result|})/*trivia2*/;/*trivia3*/
                }

                [TestMethod]
                public void TestMethod2()
                {
                    object value = "test";


                    Assert.IsInstanceOfType<string>(value, out {|CS1615:var result|});

                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                    object value = "test";
                    /*trivia1*/var result = Assert.IsInstanceOfType<string>(value)/*trivia2*/;/*trivia3*/
                }

                [TestMethod]
                public void TestMethod2()
                {
                    object value = "test";


                    var result = Assert.IsInstanceOfType<string>(value);

                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task FixIsInstanceOfTypeWithExistingVariable()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                    object value = "test";
                    string result;
                    /*trivia1*/Assert.IsInstanceOfType<string>(value, out {|CS1615:result|})/*trivia2*/;/*trivia3*/
                }

                [TestMethod]
                public void TestMethod2()
                {
                    object value = "test";
                    string result;


                    Assert.IsInstanceOfType<string>(value, out {|CS1615:result|});

                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                    object value = "test";
                    string result;
                    /*trivia1*/
                    result = Assert.IsInstanceOfType<string>(value)/*trivia2*/;/*trivia3*/
                }

                [TestMethod]
                public void TestMethod2()
                {
                    object value = "test";
                    string result;


                    result = Assert.IsInstanceOfType<string>(value);

                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task FixIsInstanceOfTypeWithOutVarAndMessage()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                    object value = "test";
                    /*trivia1*/Assert.IsInstanceOfType<string>(value, out {|CS1615:var result|}, "message")/*trivia2*/;/*trivia3*/
                }

                [TestMethod]
                public void TestMethod2()
                {
                    object value = "test";


                    Assert.IsInstanceOfType<string>(value, out {|CS1615:var result|}, "message");

                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                    object value = "test";
                    /*trivia1*/var result = Assert.IsInstanceOfType<string>(value, "message")/*trivia2*/;/*trivia3*/
                }

                [TestMethod]
                public void TestMethod2()
                {
                    object value = "test";


                    var result = Assert.IsInstanceOfType<string>(value, "message");

                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task FixIsInstanceOfTypeWithExistingVariableAndMessage()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                    object value = "test";
                    string result;
                    /*trivia1*/Assert.IsInstanceOfType<string>(value, out {|CS1615:result|}, "message")/*trivia2*/;/*trivia3*/
                }

                [TestMethod]
                public void TestMethod2()
                {
                    object value = "test";
                    string result;


                    Assert.IsInstanceOfType<string>(value, out {|CS1615:result|}, "message");

                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                    object value = "test";
                    string result;
                    /*trivia1*/
                    result = Assert.IsInstanceOfType<string>(value, "message")/*trivia2*/;/*trivia3*/
                }

                [TestMethod]
                public void TestMethod2()
                {
                    object value = "test";
                    string result;


                    result = Assert.IsInstanceOfType<string>(value, "message");

                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task FixIsInstanceOfTypeWithFullyQualifiedCall_ShouldTransformToAssignment()
    {
        string code = """
            public class C
            {
                public void M()
                {
                    object value = "test";
                    Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsInstanceOfType<string>(value, out {|CS1615:var result|});
                }
            }
            """;

        string fixedCode = """
            public class C
            {
                public void M()
                {
                    object value = "test";
                    var result = Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsInstanceOfType<string>(value);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
