// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.IgnoreStringMethodReturnValueAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class IgnoreStringMethodReturnValueAnalyzerTests
{
    [TestMethod]
    public async Task WhenStringMethodReturnValueIsUsed_NoDiagnostic()
    {
        string code = """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    string str = "Hello World";
                    
                    // Return values are used - should not trigger diagnostic
                    bool result1 = str.Contains("Hello");
                    bool result2 = str.StartsWith("Hello");
                    bool result3 = str.EndsWith("World");
                    
                    // Used in conditions
                    if (str.Contains("Hello"))
                    {
                        Console.WriteLine("Found");
                    }
                    
                    // Used in expressions
                    bool combined = str.StartsWith("Hello") && str.EndsWith("World");
                    
                    // Used in return statements
                    bool IsValid() => str.Contains("test");
                    
                    // Used in assignments with method chaining
                    bool hasHello = str.ToLower().Contains("hello");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringMethodReturnValueIsIgnored_Diagnostic()
    {
        string code = """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    string str = "Hello World";
                    
                    // Return values are ignored - should trigger diagnostics
                    [|str.Contains("Hello")|];
                    [|str.StartsWith("Hello")|];
                    [|str.EndsWith("World")|];
                    
                    // Multiple calls on same line
                    [|str.Contains("test")|]; [|str.StartsWith("test")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonStringMethodsCalled_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;

            public class TestClass
            {
                public void TestMethod()
                {
                    var list = new List<string>();
                    
                    // These are not string methods, should not trigger
                    list.Add("test");
                    Console.WriteLine("test");
                    
                    // Custom Contains method - should not trigger
                    var custom = new CustomClass();
                    custom.Contains("test");
                }
            }

            public class CustomClass
            {
                public bool Contains(string value) => true;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringMethodsFromOtherSources_Diagnostic()
    {
        string code = """
            public class TestClass
            {
                public void TestMethod()
                {
                    string str1 = "test";
                    string str2 = GetString();
                    
                    // Return values from different sources are ignored
                    [|str1.Contains("e")|];
                    [|str2.StartsWith("t")|];
                    [|GetString().EndsWith("t")|];
                    [|"literal".Contains("l")|];
                }
                
                private string GetString() => "test";
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringMethodsWithParameters_Diagnostic()
    {
        string code = """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    string str = "Hello World";
                    
                    // Methods with different parameter overloads
                    [|str.Contains("Hello")|];
                    [|str.Contains("Hello", StringComparison.OrdinalIgnoreCase)|];
                    [|str.StartsWith("Hello")|];
                    [|str.StartsWith("Hello", StringComparison.OrdinalIgnoreCase)|];
                    [|str.EndsWith("World")|];
                    [|str.EndsWith("World", StringComparison.OrdinalIgnoreCase)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDiscardAssignmentIgnoresReturnValue_NoDiagnostic()
    {
        string code = """
            public class TestClass
            {
                public void TestMethod()
                {
                    string str = "Hello World";

                    // Discard assignment: outer operation is IAssignmentOperation, not IInvocationOperation,
                    // so the analyzer's early-return guard fires and no diagnostic is reported.
                    _ = str.Contains("Hello");
                    _ = str.StartsWith("Hello");
                    _ = str.EndsWith("World");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringMethodReturnValueIgnoredInsideLambda_Diagnostic()
    {
        string code = """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    string str = "Hello World";

                    // ExpressionStatement inside a lambda block body is still an ExpressionStatement
                    // operation, so the analyzer fires inside the lambda just as it does at method level.
                    Action action = () => { [|str.Contains("Hello")|]; };
                    Action action2 = () => { [|str.StartsWith("World")|]; };
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringMethodResultUsedAsReceiverForChainedCall_NoDiagnostic()
    {
        string code = """
            public class TestClass
            {
                public void TestMethod()
                {
                    string str = "Hello World";

                    // The ExpressionStatement's outermost operation is GetHashCode() (on System.Boolean),
                    // not Contains/StartsWith/EndsWith on System.String, so no diagnostic is reported.
                    // The string method's return value IS used — as the receiver of GetHashCode().
                    str.Contains("Hello").GetHashCode();
                    str.StartsWith("Hello").GetHashCode();
                    str.EndsWith("World").GetHashCode();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
