// Copyright (c) Microsoft Corporation. All rights reserved.
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
        (UseAttributeOnTestMethodAnalyzer.DescriptionRule, """Description("description")"""),
        (UseAttributeOnTestMethodAnalyzer.TestPropertyRule, """TestProperty("name", "value")"""),
        (UseAttributeOnTestMethodAnalyzer.WorkItemRule, "WorkItem(100)"),
    ];

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

    [TestMethod]
    public async Task WhenMethodWithMultiLineBody_PreservesIndentation()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:Owner("owner")|}]
                public void TestMethod()
                {
                    var result = SomeMethod(
                        1,
                        2,
                        3);
                }

                private int SomeMethod(int a, int b, int c) => a + b + c;
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Owner("owner")]
                [TestMethod]
                public void TestMethod()
                {
                    var result = SomeMethod(
                        1,
                        2,
                        3);
                }

                private int SomeMethod(int a, int b, int c) => a + b + c;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic(UseAttributeOnTestMethodAnalyzer.OwnerRule).WithLocation(0), fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodIsMarkedWithIgnoreButNotWithTestMethod_UsesConcreteConditionAttributeInDiagnosticAsync()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:Ignore|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Ignore]
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UseAttributeOnTestMethodAnalyzer.ConditionBaseRule).WithLocation(0).WithArguments("IgnoreAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodIsMarkedWithDataTestMethodAndOwnerAttribute_NoDiagnosticAsync()
    {
        // DataTestMethod inherits TestMethodAttribute — the analyzer exits early on any
        // method decorated with a TestMethod subclass, including the standard DataTestMethod.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [Owner("owner")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code, useMSTestV4: true);
    }

    [TestMethod]
    public async Task WhenMethodIsMarkedWithOSConditionAttributeButNotWithTestMethod_DiagnosticAsync()
    {
        // OSConditionAttribute inherits ConditionBaseAttribute — the analyzer fires
        // ConditionBaseRule and includes the concrete attribute class name in the message.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:OSCondition(OperatingSystems.Windows)|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [OSCondition(OperatingSystems.Windows)]
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UseAttributeOnTestMethodAnalyzer.ConditionBaseRule).WithLocation(0).WithArguments("OSConditionAttribute"),
            fixedCode);
    }

    [DataRow("AssemblyInitialize", "public static void Fixture(TestContext context)")]
    [DataRow("AssemblyCleanup", "public static void Fixture()")]
    [DataRow("ClassInitialize", "public static void Fixture(TestContext context)")]
    [DataRow("ClassCleanup", "public static void Fixture()")]
    [DataRow("TestInitialize", "public void Fixture()")]
    [DataRow("TestCleanup", "public void Fixture()")]
    [DataRow("GlobalTestInitialize", "public static void Fixture(TestContext context)")]
    [DataRow("GlobalTestCleanup", "public static void Fixture(TestContext context)")]
    [TestMethod]
    public async Task WhenFixtureMethodIsMarkedWithSchedulingAttributes_DiagnosticWithoutCodeFixAsync(string fixtureAttribute, string signature)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{{fixtureAttribute}}]
                [{|#0:ResourceLock("db")|}]
                [{|#1:DoNotParallelize|}]
                {{signature}}
                {
                }

                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.Diagnostic(UseAttributeOnTestMethodAnalyzer.ResourceLockOnFixtureRule).WithLocation(0),
                VerifyCS.Diagnostic(UseAttributeOnTestMethodAnalyzer.DoNotParallelizeOnFixtureRule).WithLocation(1),
            ],
            code);
    }

    [TestMethod]
    public async Task WhenFixtureMethodIsMarkedWithResourceLock_MessageExplainsFixtureHasNoEffectAsync()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                [{|#0:ResourceLock("db", Mode = ResourceAccessMode.Read)|}]
                public void Initialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UseAttributeOnTestMethodAnalyzer.ResourceLockOnFixtureRule).WithLocation(0).WithMessage("[ResourceLock] has no effect on fixture methods; apply it to test methods or to the test class"),
            code);
    }

    [TestMethod]
    public async Task WhenSchedulingAttributesAreOnTestMethodsTestClassOrHelperMethods_NoDiagnosticAsync()
    {
        // Non-fixture helpers are not reported: an inherited declaration on a virtual base method can flow to an
        // overriding test method, which discovery honors.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [ResourceLock("db")]
            public class MyTestClass
            {
                [TestMethod]
                [ResourceLock("db")]
                [DoNotParallelize]
                public void TestMethod()
                {
                }

                [ResourceLock("db")]
                [DoNotParallelize]
                public virtual void Helper()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
