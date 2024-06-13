// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssertionArgsShouldBePassedInCorrectOrderAnalyzer,
    MSTest.Analyzers.AssertionArgsShouldBePassedInCorrectOrderFixer>;

namespace MSTest.Analyzers.UnitTests;

[TestGroup]
public sealed class AssertionArgsShouldBePassedInCorrectOrderAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenUsingLiterals()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;

                    [|Assert.AreEqual(s, "")|];
                    [|Assert.AreEqual(b, true)|];
                    [|Assert.AreEqual(i, 1)|];

                    [|Assert.AreNotEqual(s, "")|];
                    [|Assert.AreNotEqual(b, true)|];
                    [|Assert.AreNotEqual(i, 1)|];

                    [|Assert.AreSame(s, "")|];
                    [|Assert.AreSame(b, true)|];
                    [|Assert.AreSame(i, 1)|];

                    [|Assert.AreNotSame(s, "")|];
                    [|Assert.AreNotSame(b, true)|];
                    [|Assert.AreNotSame(i, 1)|];

                    [|Assert.AreEqual(s, "", EqualityComparer<string>.Default)|];
                    [|Assert.AreEqual(s, "", "some message")|];
                    [|Assert.AreEqual(s, "", EqualityComparer<string>.Default, "some message")|];
                    [|Assert.AreEqual(s, "", "some message", 1, "input")|];
                    [|Assert.AreEqual(s, "", EqualityComparer<string>.Default, "some message", 1, "input")|];

                    [|Assert.AreNotEqual(s, "", EqualityComparer<string>.Default)|];
                    [|Assert.AreNotEqual(s, "", "some message")|];
                    [|Assert.AreNotEqual(s, "", EqualityComparer<string>.Default, "some message")|];
                    [|Assert.AreNotEqual(s, "", "some message", 1, "input")|];
                    [|Assert.AreNotEqual(s, "", EqualityComparer<string>.Default, "some message", 1, "input")|];

                    [|Assert.AreSame(s, "", "some message")|];
                    [|Assert.AreSame(s, "", "some message", 1, "input")|];
                }

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;
            
                    Assert.AreEqual("", s);
                    Assert.AreEqual(true, b);
                    Assert.AreEqual(1, i);

                    Assert.AreNotEqual("", s);
                    Assert.AreNotEqual(true, b);
                    Assert.AreNotEqual(1, i);
            
                    Assert.AreSame("", s);
                    Assert.AreSame(true, b);
                    Assert.AreSame(1, i);

                    Assert.AreNotSame("", s);
                    Assert.AreNotSame(true, b);
                    Assert.AreNotSame(1, i);
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;

                    Assert.AreEqual("", s);
                    Assert.AreEqual(true, b);
                    Assert.AreEqual(1, i);

                    Assert.AreNotEqual("", s);
                    Assert.AreNotEqual(true, b);
                    Assert.AreNotEqual(1, i);

                    Assert.AreSame("", s);
                    Assert.AreSame(true, b);
                    Assert.AreSame(1, i);

                    Assert.AreNotSame("", s);
                    Assert.AreNotSame(true, b);
                    Assert.AreNotSame(1, i);

                    Assert.AreEqual("", s, EqualityComparer<string>.Default);
                    Assert.AreEqual("", s, "some message");
                    Assert.AreEqual("", s, EqualityComparer<string>.Default, "some message");
                    Assert.AreEqual("", s, "some message", 1, "input");
                    Assert.AreEqual("", s, EqualityComparer<string>.Default, "some message", 1, "input");

                    Assert.AreNotEqual("", s, EqualityComparer<string>.Default);
                    Assert.AreNotEqual("", s, "some message");
                    Assert.AreNotEqual("", s, EqualityComparer<string>.Default, "some message");
                    Assert.AreNotEqual("", s, "some message", 1, "input");
                    Assert.AreNotEqual("", s, EqualityComparer<string>.Default, "some message", 1, "input");

                    Assert.AreSame("", s, "some message");
                    Assert.AreSame("", s, "some message", 1, "input");
                }

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;
            
                    Assert.AreEqual("", s);
                    Assert.AreEqual(true, b);
                    Assert.AreEqual(1, i);

                    Assert.AreNotEqual("", s);
                    Assert.AreNotEqual(true, b);
                    Assert.AreNotEqual(1, i);
            
                    Assert.AreSame("", s);
                    Assert.AreSame(true, b);
                    Assert.AreSame(1, i);

                    Assert.AreNotSame("", s);
                    Assert.AreNotSame(true, b);
                    Assert.AreNotSame(1, i);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            fixedCode);
    }

    public async Task LiteralUsingNamedArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;

                    [|Assert.AreEqual(actual: "", expected: s)|];
                    [|Assert.AreEqual(actual: true, expected: b)|];
                    [|Assert.AreEqual(actual: 1, expected: i)|];

                    [|Assert.AreNotEqual(actual: "", notExpected: s)|];
                    [|Assert.AreNotEqual(actual: true, notExpected: b)|];
                    [|Assert.AreNotEqual(actual: 1, notExpected: i)|];

                    [|Assert.AreSame(actual: "", expected: s)|];
                    [|Assert.AreSame(actual: true, expected: b)|];
                    [|Assert.AreSame(actual: 1, expected: i)|];

                    [|Assert.AreNotSame(actual: "", notExpected: s)|];
                    [|Assert.AreNotSame(actual: true, notExpected: b)|];
                    [|Assert.AreNotSame(actual: 1, notExpected: i)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;
            
                    Assert.AreEqual(actual: s, expected: "");
                    Assert.AreEqual(actual: b, expected: true);
                    Assert.AreEqual(actual: i, expected: 1);

                    Assert.AreNotEqual(actual: s, notExpected: "");
                    Assert.AreNotEqual(actual: b, notExpected: true);
                    Assert.AreNotEqual(actual: i, notExpected: 1);
            
                    Assert.AreSame(actual: s, expected: "");
                    Assert.AreSame(actual: b, expected: true);
                    Assert.AreSame(actual: i, expected: 1);

                    Assert.AreNotSame(actual: s, notExpected: "");
                    Assert.AreNotSame(actual: b, notExpected: true);
                    Assert.AreNotSame(actual: i, notExpected: 1);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task ConstantValue()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private const string constString = "";
                private const int constInt = 42;

                [TestMethod]
                public void NonCompliant()
                {
                    string s = "";
                    const string localConstString = "";
                    int i = 42;
                    const int localConstInt = 42;

                    [|Assert.AreEqual(s, constString)|];
                    [|Assert.AreEqual(s, localConstString)|];
                    [|Assert.AreEqual(i, constInt)|];
                    [|Assert.AreEqual(i, localConstInt)|];

                    [|Assert.AreNotEqual(s, constString)|];
                    [|Assert.AreNotEqual(s, localConstString)|];
                    [|Assert.AreNotEqual(i, constInt)|];
                    [|Assert.AreNotEqual(i, localConstInt)|];

                    [|Assert.AreSame(s, constString)|];
                    [|Assert.AreSame(s, localConstString)|];
                    [|Assert.AreSame(i, constInt)|];
                    [|Assert.AreSame(i, localConstInt)|];

                    [|Assert.AreNotSame(s, constString)|];
                    [|Assert.AreNotSame(s, localConstString)|];
                    [|Assert.AreNotSame(i, constInt)|];
                    [|Assert.AreNotSame(i, localConstInt)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    const string localConstString = "";
                    int i = 42;
                    const int localConstInt = 42;
            
                    Assert.AreEqual(constString, s);
                    Assert.AreEqual(localConstString, s);
                    Assert.AreEqual(constInt, i);
                    Assert.AreEqual(localConstInt, i);
            
                    Assert.AreNotEqual(constString, s);
                    Assert.AreNotEqual(localConstString, s);
                    Assert.AreNotEqual(constInt, i);
                    Assert.AreNotEqual(localConstInt, i);
            
                    Assert.AreSame(constString, s);
                    Assert.AreSame(localConstString, s);
                    Assert.AreSame(constInt, i);
                    Assert.AreSame(localConstInt, i);
            
                    Assert.AreNotSame(constString, s);
                    Assert.AreNotSame(localConstString, s);
                    Assert.AreNotSame(constInt, i);
                    Assert.AreNotSame(localConstInt, i);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task ActualAsLocalVariableOrNot()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private string fieldString = "";
                private int fieldInt = 42;

                internal string PropertyString { get; set; } = "";
                internal int PropertyInt { get; set; } = 42;

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    int i = 42;
            
                    Assert.AreEqual(fieldString, s);
                    Assert.AreEqual(PropertyString, s);
                    Assert.AreEqual(fieldInt, i);
                    Assert.AreEqual(PropertyInt, i);
                    Assert.AreEqual(s, fieldString);
                    Assert.AreEqual(s, PropertyString);
                    Assert.AreEqual(i, fieldInt);
                    Assert.AreEqual(i, PropertyInt);

                    Assert.AreNotEqual(fieldString, s);
                    Assert.AreNotEqual(PropertyString, s);
                    Assert.AreNotEqual(fieldInt, i);
                    Assert.AreNotEqual(PropertyInt, i);
                    Assert.AreNotEqual(s, fieldString);
                    Assert.AreNotEqual(s, PropertyString);
                    Assert.AreNotEqual(i, fieldInt);
                    Assert.AreNotEqual(i, PropertyInt);
            
                    Assert.AreSame(fieldString, s);
                    Assert.AreSame(PropertyString, s);
                    Assert.AreSame(fieldInt, i);
                    Assert.AreSame(PropertyInt, i);
                    Assert.AreSame(s, fieldString);
                    Assert.AreSame(s, PropertyString);
                    Assert.AreSame(i, fieldInt);
                    Assert.AreSame(i, PropertyInt);
            
                    Assert.AreNotSame(fieldString, s);
                    Assert.AreNotSame(PropertyString, s);
                    Assert.AreNotSame(fieldInt, i);
                    Assert.AreNotSame(PropertyInt, i);
                    Assert.AreNotSame(s, fieldString);
                    Assert.AreNotSame(s, PropertyString);
                    Assert.AreNotSame(i, fieldInt);
                    Assert.AreNotSame(i, PropertyInt);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task ActualOrExpectedPrefix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private string _expectedString = "";
                private string ExpectedString { get; } = "";

                [TestMethod]
                public void NonCompliant()
                {
                    string s = "";
                    string actualString = "";
                    string expectedString = "";
                    object o = 42;
                    object actualObject = 42;
                    object expectedObject = 42;
            
                    [|Assert.AreEqual(actualString, s)|];
                    [|Assert.AreEqual(s, expectedString)|];
                    [|Assert.AreEqual(actualString, expectedString)|];
                    [|Assert.AreEqual(s, _expectedString)|];
                    [|Assert.AreEqual(s, ExpectedString)|];
                    [|Assert.AreEqual(actualObject, o)|];
                    [|Assert.AreEqual(o, expectedObject)|];
                    [|Assert.AreEqual(actualObject, expectedObject)|];

                    [|Assert.AreNotEqual(actualString, s)|];
                    [|Assert.AreNotEqual(s, expectedString)|];
                    [|Assert.AreNotEqual(actualString, expectedString)|];
                    [|Assert.AreNotEqual(s, _expectedString)|];
                    [|Assert.AreNotEqual(s, ExpectedString)|];
                    [|Assert.AreNotEqual(actualObject, o)|];
                    [|Assert.AreNotEqual(o, expectedObject)|];
                    [|Assert.AreNotEqual(actualObject, expectedObject)|];

                    [|Assert.AreSame(actualString, s)|];
                    [|Assert.AreSame(s, expectedString)|];
                    [|Assert.AreSame(actualString, expectedString)|];
                    [|Assert.AreSame(s, _expectedString)|];
                    [|Assert.AreSame(s, ExpectedString)|];
                    [|Assert.AreSame(actualObject, o)|];
                    [|Assert.AreSame(o, expectedObject)|];
                    [|Assert.AreSame(actualObject, expectedObject)|];

                    [|Assert.AreNotSame(actualString, s)|];
                    [|Assert.AreNotSame(s, expectedString)|];
                    [|Assert.AreNotSame(actualString, expectedString)|];
                    [|Assert.AreNotSame(s, _expectedString)|];
                    [|Assert.AreNotSame(s, ExpectedString)|];
                    [|Assert.AreNotSame(actualObject, o)|];
                    [|Assert.AreNotSame(o, expectedObject)|];
                    [|Assert.AreNotSame(actualObject, expectedObject)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    string actualString = "";
                    string expectedString = "";
                    object o = 42;
                    object actualObject = 42;
                    object expectedObject = 42;
            
                    Assert.AreEqual(expectedString, s);
                    Assert.AreEqual(s, actualString);
                    Assert.AreEqual(expectedString, actualString);
                    Assert.AreEqual(expectedObject, o);
                    Assert.AreEqual(o, actualObject);
                    Assert.AreEqual(expectedObject, actualObject);
            
                    Assert.AreNotEqual(expectedString, s);
                    Assert.AreNotEqual(s, actualString);
                    Assert.AreNotEqual(expectedString, actualString);
                    Assert.AreNotEqual(expectedObject, o);
                    Assert.AreNotEqual(o, actualObject);
                    Assert.AreNotEqual(expectedObject, actualObject);
            
                    Assert.AreSame(expectedString, s);
                    Assert.AreSame(s, actualString);
                    Assert.AreSame(expectedString, actualString);
                    Assert.AreSame(expectedObject, o);
                    Assert.AreSame(o, actualObject);
                    Assert.AreSame(expectedObject, actualObject);
            
                    Assert.AreNotSame(expectedString, s);
                    Assert.AreNotSame(s, actualString);
                    Assert.AreNotSame(expectedString, actualString);
                    Assert.AreNotSame(expectedObject, o);
                    Assert.AreNotSame(o, actualObject);
                    Assert.AreNotSame(expectedObject, actualObject);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task MethodCalls()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    int i = 42;
            
                    Assert.AreEqual(GetString(), s);
                    Assert.AreEqual(s, GetString());
                    Assert.AreEqual(GetInt(), i);
                    Assert.AreEqual(i, GetInt());
            
                    Assert.AreNotEqual(GetString(), s);
                    Assert.AreNotEqual(s, GetString());
                    Assert.AreNotEqual(GetInt(), i);
                    Assert.AreNotEqual(i, GetInt());
            
                    Assert.AreSame(GetString(), s);
                    Assert.AreSame(s, GetString());
                    Assert.AreSame(GetInt(), i);
                    Assert.AreSame(i, GetInt());
            
                    Assert.AreNotSame(GetString(), s);
                    Assert.AreNotSame(s, GetString());
                    Assert.AreNotSame(GetInt(), i);
                    Assert.AreNotSame(i, GetInt());
                }

                private string GetString() => "";
                private int GetInt() => 42;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
