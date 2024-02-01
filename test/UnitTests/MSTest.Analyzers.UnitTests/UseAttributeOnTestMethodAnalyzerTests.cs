// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseAttributeOnTestMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class UseAttributeOnTestMethodAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    private static readonly List<(DiagnosticDescriptor Rule, string AttributeUsageExample)> RuleUsageExamples = new()
    {
        (UseAttributeOnTestMethodAnalyzer.OwnerRule, """Owner("owner")"""),
        (UseAttributeOnTestMethodAnalyzer.PriorityRule, "Priority(1)"),
        (UseAttributeOnTestMethodAnalyzer.TestPropertyRule, """TestProperty("name", "value")"""),
        (UseAttributeOnTestMethodAnalyzer.WorkItemRule, "WorkItem(100)"),
        (UseAttributeOnTestMethodAnalyzer.DescriptionRule, """Description("description")"""),
        (UseAttributeOnTestMethodAnalyzer.ExpectedExceptionRule, "ExpectedException(null)"),
        (UseAttributeOnTestMethodAnalyzer.CssIterationRule, "CssIteration(null)"),
        (UseAttributeOnTestMethodAnalyzer.CssProjectStructureRule, "CssProjectStructure(null)"),
    };

    internal static IEnumerable<(DiagnosticDescriptor Rule, string AttributeUsageExample)> GetAttributeUsageExampleAndRuleTuples()
        => RuleUsageExamples.Select(tuple => (tuple.Rule, tuple.AttributeUsageExample));

    internal static IEnumerable<string> GetAttributeUsageExamples()
        => RuleUsageExamples.Select(tuple => tuple.AttributeUsageExample);

    // This generates all possible combinations of any two tuples (Rule, AttributeUsageExample) with the exception of the
    // combaination where the two tuples are equal. The result is flattened in a new tuple created from the elements of the
    // previous two tuples.
    internal static IEnumerable<(DiagnosticDescriptor Rule1, string AttributeUsageExample1, DiagnosticDescriptor Rule2, string AttributeUsageExample2)> GetAttributeUsageExampleAndRuleTuplesForTwoAttributes()
        => RuleUsageExamples
            .SelectMany(tuple => RuleUsageExamples, (tuple1, tuple2) => (tuple1, tuple2))
            .Where(tuples => !tuples.tuple1.AttributeUsageExample.Equals(tuples.tuple2.AttributeUsageExample, StringComparison.Ordinal))
            .Select(tuples => (tuples.tuple1.Rule, tuples.tuple1.AttributeUsageExample, tuples.tuple2.Rule, tuples.tuple2.AttributeUsageExample));

    [ArgumentsProvider(nameof(GetAttributeUsageExamples))]
    public async Task WhenMethodIsMarkedWithTestMethodAndTestAttributes_NoDiagnosticAsync(string attributeUsageExample)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{{attributeUsageExample}}]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [ArgumentsProvider(nameof(GetAttributeUsageExampleAndRuleTuples))]
    public async Task WhenMethodIsMarkedWithTestAttributeButNotWithTestMethod_DiagnosticAsync(DiagnosticDescriptor rule, string attributeUsageExample)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:{{attributeUsageExample}}|}]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic(rule).WithLocation(0));
    }

    [ArgumentsProvider(nameof(GetAttributeUsageExampleAndRuleTuplesForTwoAttributes))]
    public async Task WhenMethodIsMarkedWithMultipleTestAttributesButNotWithTestMethod_DiagnosticOnEachAttributeAsync(
        DiagnosticDescriptor rule1,
        string attributeUsageExample1,
        DiagnosticDescriptor rule2,
        string attributeUsageExample2)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:{{attributeUsageExample1}}|}]
                [{|#1:{{attributeUsageExample2}}|}]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic(rule1).WithLocation(0), VerifyCS.Diagnostic(rule2).WithLocation(1));
    }

    [ArgumentsProvider(nameof(GetAttributeUsageExamples))]
    public async Task WhenMethodIsMarkedWithTestAttributeAndCustomTestMethod_NoDiagnosticAsync(string attributeUsageExample)
    {
        var code = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{{attributeUsageExample}}]
                [MyCustomTestMethod]
                public void TestMethod()
                {
                }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public class MyCustomTestMethodAttribute : TestMethodAttribute
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
