﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseAttributeOnTestMethodAnalyzer,
    MSTest.Analyzers.UseAttributeOnTestMethodFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseAttributeOnTestMethodAnalyzerTests
{
    private static readonly List<(DiagnosticDescriptor Rule, string AttributeUsageExample)> RuleUsageExamples =
    [
        (UseAttributeOnTestMethodAnalyzer.OwnerRule, """Owner("owner")"""),
        (UseAttributeOnTestMethodAnalyzer.PriorityRule, "Priority(1)"),
        (UseAttributeOnTestMethodAnalyzer.TestPropertyRule, """TestProperty("name", "value")"""),
        (UseAttributeOnTestMethodAnalyzer.WorkItemRule, "WorkItem(100)"),
        (UseAttributeOnTestMethodAnalyzer.DescriptionRule, """Description("description")"""),
        (UseAttributeOnTestMethodAnalyzer.ExpectedExceptionRule, "ExpectedException(null)"),
        (UseAttributeOnTestMethodAnalyzer.ExpectedExceptionRule, "MyExpectedException"),
        (UseAttributeOnTestMethodAnalyzer.CssIterationRule, "CssIteration(null)"),
        (UseAttributeOnTestMethodAnalyzer.CssProjectStructureRule, "CssProjectStructure(null)")
    ];

    private const string MyExpectedExceptionAttributeDeclaration = """
        public class MyExpectedExceptionAttribute : ExpectedExceptionBaseAttribute
        {
            protected override void Verify(System.Exception exception) { }
        }
        """;

    internal static IEnumerable<(DiagnosticDescriptor Rule, string AttributeUsageExample)> GetAttributeUsageExampleAndRuleTuples()
        => RuleUsageExamples.Select(tuple => (tuple.Rule, tuple.AttributeUsageExample));

    internal static IEnumerable<object[]> GetAttributeUsageExamples()
        => RuleUsageExamples.Select(tuple => new object[] { tuple.AttributeUsageExample });

    // This generates all possible combinations of any two tuples (Rule, AttributeUsageExample) with the exception of the
    // combination where the two tuples are equal. The result is flattened in a new tuple created from the elements of the
    // previous two tuples.
    internal static IEnumerable<(DiagnosticDescriptor Rule1, string AttributeUsageExample1, DiagnosticDescriptor Rule2, string AttributeUsageExample2)> GetAttributeUsageExampleAndRuleTuplesForTwoAttributes()
        => RuleUsageExamples
            .SelectMany(tuple => RuleUsageExamples, (tuple1, tuple2) => (tuple1, tuple2))
            .Where(tuples => !tuples.tuple1.AttributeUsageExample.Equals(tuples.tuple2.AttributeUsageExample, StringComparison.Ordinal))
            .Select(tuples => (tuples.tuple1.Rule, tuples.tuple1.AttributeUsageExample, tuples.tuple2.Rule, tuples.tuple2.AttributeUsageExample));

    [DynamicData(nameof(GetAttributeUsageExamples), DynamicDataSourceType.Method)]
    [TestMethod]
    public async Task WhenMethodIsMarkedWithTestMethodAndTestAttributes_NoDiagnosticAsync(string attributeUsageExample)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            {{MyExpectedExceptionAttributeDeclaration}}

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

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [DynamicData(nameof(GetAttributeUsageExampleAndRuleTuples), DynamicDataSourceType.Method)]
    [TestMethod]
    public async Task WhenMethodIsMarkedWithTestAttributeButNotWithTestMethod_DiagnosticAsync(DiagnosticDescriptor rule, string attributeUsageExample)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            {{MyExpectedExceptionAttributeDeclaration}}

            [TestClass]
            public class MyTestClass
            {
                [{|#0:{{attributeUsageExample}}|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            {{MyExpectedExceptionAttributeDeclaration}}

            [TestClass]
            public class MyTestClass
            {
                [{{attributeUsageExample}}]
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic(rule).WithLocation(0), fixedCode);
    }

    [DynamicData(nameof(GetAttributeUsageExampleAndRuleTuplesForTwoAttributes), DynamicDataSourceType.Method)]
    [TestMethod]
    public async Task WhenMethodIsMarkedWithMultipleTestAttributesButNotWithTestMethod_DiagnosticOnEachAttributeAsync(
        DiagnosticDescriptor rule1,
        string attributeUsageExample1,
        DiagnosticDescriptor rule2,
        string attributeUsageExample2)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            {{MyExpectedExceptionAttributeDeclaration}}

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

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            {{MyExpectedExceptionAttributeDeclaration}}

            [TestClass]
            public class MyTestClass
            {
                [{{attributeUsageExample1}}]
                [{{attributeUsageExample2}}]
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, [VerifyCS.Diagnostic(rule1).WithLocation(0), VerifyCS.Diagnostic(rule2).WithLocation(1)], fixedCode);
    }

    [DynamicData(nameof(GetAttributeUsageExamples), DynamicDataSourceType.Method)]
    [TestMethod]
    public async Task WhenMethodIsMarkedWithTestAttributeAndCustomTestMethod_NoDiagnosticAsync(string attributeUsageExample)
    {
        string code = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            {{MyExpectedExceptionAttributeDeclaration}}

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

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
