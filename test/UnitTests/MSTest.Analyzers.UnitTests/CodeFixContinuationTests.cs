// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class CodeFixContinuationTests
{
    [TestMethod]
    [DataRow("((!flag))", "IsFalse", "flag")]
    [DataRow("((!(!flag)))", "IsTrue", "flag")]
    [DataRow("((!(flag && other)))", "IsFalse", "flag && other")]
    public async Task NegatedBoolean_NestedParentheses_PreserveParityAndMessage(string condition, string expectedMethod, string expectedCondition)
    {
        string source = BooleanSource($"Assert.IsTrue(message: \"context\", condition: {condition})");
        string expected = BooleanSource($"Assert.{expectedMethod}(message: \"context\", condition: {expectedCondition})");
        await VerifyFixAsync(source, expected, new DoNotNegateBooleanAssertionAnalyzer(), new DoNotNegateBooleanAssertionFixer(),
            "MSTEST0023", DiagnosticSeverity.Info,
            $"Assert.IsTrue(message: \"context\", condition: {condition})",
            "Do not negate boolean assertions, instead use the opposite assertion");
    }

    [TestMethod]
    public async Task DuplicateDataRow_SharedAttributeList_PreservesDistinctRows()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Tests
            {
                [TestMethod, DataRow(17), DataRow(29), DataRow(17)]
                public void Check(int number) { }
            }
            """;
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Tests
            {
                [TestMethod, DataRow(17), DataRow(29)]
                public void Check(int number) { }
            }
            """;
        await VerifyFixAsync(source, expected, new DuplicateDataRowAnalyzer(), new DuplicateDataRowFixer(),
            "MSTEST0042", DiagnosticSeverity.Warning, "DataRow(17)",
            "Do not duplicate 'DataRow' attributes. This is usually a copy/paste error. The attribute indices are '1' and '3'.");
    }

    [TestMethod]
    [DataRow("((Action)(() => GetTask().GetAwaiter().GetResult()))")]
    [DataRow("((() => GetTask().GetAwaiter().GetResult()))")]
    [DataRow("delegate { GetTask().GetAwaiter().GetResult(); }")]
    [DataRow("new Action(() => GetTask().GetAwaiter().GetResult())")]
    [DataRow("(Action)new(() => GetTask().GetAwaiter().GetResult())")]
    public async Task AsyncAssertion_ActionSyntaxVariants_PreservePayload(string action)
    {
        string source = $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public void Check()
                {
                    Assert.ThrowsExactly<Exception>({{action}}, "context");
                }
                private Task GetTask() => Task.CompletedTask;
            }
            """;
        const string expected = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public async Task Check()
                {
                    await Assert.ThrowsExactlyAsync<Exception>(() => GetTask(), "context");
                }
                private Task GetTask() => Task.CompletedTask;
            }
            """;
        await VerifyFixAsync(source, expected, new PreferAsyncAssertionAnalyzer(), new PreferAsyncAssertionFixer(),
            "MSTEST0064", DiagnosticSeverity.Info, $"Assert.ThrowsExactly<Exception>({action}, \"context\")",
            "Use 'Assert.ThrowsExactlyAsync' instead of blocking an async call in 'Assert.ThrowsExactly'");
    }

    [TestMethod]
    public async Task AsyncAssertion_NongenericCompatibleApi_AppendsAsyncSuffix()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestMethodAttribute : Attribute { }
                public static class Assert
                {
                    public static void Throws(Action action) { }
                    public static Task ThrowsAsync(Func<Task> action) => Task.CompletedTask;
                }
            }
            public class Tests
            {
                [TestMethod]
                public void Check()
                {
                    Assert.Throws(() => Task.CompletedTask.GetAwaiter().GetResult());
                }
            }
            """;
        const string expected = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestMethodAttribute : Attribute { }
                public static class Assert
                {
                    public static void Throws(Action action) { }
                    public static Task ThrowsAsync(Func<Task> action) => Task.CompletedTask;
                }
            }
            public class Tests
            {
                [TestMethod]
                public async Task Check()
                {
                    await Assert.ThrowsAsync(() => Task.CompletedTask);
                }
            }
            """;
        await VerifyFixAsync(source, expected, new PreferAsyncAssertionAnalyzer(), new PreferAsyncAssertionFixer(),
            "MSTEST0064", DiagnosticSeverity.Info, "Assert.Throws(() => Task.CompletedTask.GetAwaiter().GetResult())",
            "Use 'Assert.ThrowsAsync' instead of blocking an async call in 'Assert.Throws'", includeMSTest: false);
    }

    [TestMethod]
    [DataRow("Task", "Task.CompletedTask")]
    [DataRow("ValueTask", "default(ValueTask)")]
    public async Task AsyncAssertion_EarlyReturns_PreserveControlFlowAndNestedScopes(string returnType, string returnExpression)
    {
        string source = $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public {{returnType}} Check(bool early)
                {
                    Func<bool, Task> simple = value => Task.CompletedTask;
                    Func<Task> nested = () => { return Task.CompletedTask; };
                    Func<Task> anonymous = delegate { return Task.CompletedTask; };
                    Task Local() { return Task.CompletedTask; }
                    if (early)
                        return {{returnExpression}};
                    Assert.ThrowsExactly<Exception>(() => Task.CompletedTask.GetAwaiter().GetResult());
                    return {{returnExpression}};
                }
            }
            """;
        string expected = $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public async {{returnType}} Check(bool early)
                {
                    Func<bool, Task> simple = value => Task.CompletedTask;
                    Func<Task> nested = () => { return Task.CompletedTask; };
                    Func<Task> anonymous = delegate { return Task.CompletedTask; };
                    Task Local() { return Task.CompletedTask; }
                    if (early)
                    {
                        await {{returnExpression}};
                        return;
                    }
                    await Assert.ThrowsExactlyAsync<Exception>(() => Task.CompletedTask);
                    await {{returnExpression}};
                }
            }
            """;
        await VerifyFixAsync(source, expected, new PreferAsyncAssertionAnalyzer(), new PreferAsyncAssertionFixer(),
            "MSTEST0064", DiagnosticSeverity.Info, "Assert.ThrowsExactly<Exception>(() => Task.CompletedTask.GetAwaiter().GetResult())",
            "Use 'Assert.ThrowsExactlyAsync' instead of blocking an async call in 'Assert.ThrowsExactly'");
    }

    [TestMethod]
    [DataRow("", "[CallerFilePath] string callerFilePath = \"\", [CallerLineNumber] int callerLineNumber = -1", "callerFilePath, callerLineNumber")]
    [DataRow("[CallerLineNumber] int callerLineNumber = -1", "[CallerFilePath] string callerFilePath = \"\", [CallerLineNumber] int callerLineNumber = -1", "callerFilePath")]
    public async Task CallerInfo_PartialParametersWithoutInitializer_AddsOnlyMissingContract(string parameters, string expectedParameters, string baseArguments)
    {
        string source = CallerInfoSource(parameters, string.Empty);
        string expected = CallerInfoSource(expectedParameters, $" : base({baseArguments})");
        await VerifyFixAsync(source, expected, new TestMethodAttributeShouldPropagateSourceInformationAnalyzer(), new TestMethodAttributeShouldPropagateSourceInformationFixer(),
            "MSTEST0057", DiagnosticSeverity.Warning, "CustomTestMethodAttribute",
            "TestMethodAttribute derived class 'CustomTestMethodAttribute' should add CallerFilePath and CallerLineNumber parameters to its constructor");
    }

    [TestMethod]
    public async Task CallerInfo_InitializerWithoutCanonicalIdentifier_PreservesArguments()
    {
        string source = CallerInfoSource("[CallerLineNumber] int line = -1", " : base(callerLineNumber: line)");
        string expected = CallerInfoSource("[CallerFilePath] string callerFilePath = \"\", [CallerLineNumber] int line = -1", " : base(callerLineNumber: line)");
        await VerifyFixAsync(source, expected, new TestMethodAttributeShouldPropagateSourceInformationAnalyzer(), new TestMethodAttributeShouldPropagateSourceInformationFixer(),
            "MSTEST0057", DiagnosticSeverity.Warning, "CustomTestMethodAttribute",
            "TestMethodAttribute derived class 'CustomTestMethodAttribute' should add CallerFilePath and CallerLineNumber parameters to its constructor");
    }

    [TestMethod]
    public async Task Timeout_IncompleteAttribute_ReportsDiagnosticWithoutUnsafeFix()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [Timeout]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new UseCooperativeCancellationForTimeoutAnalyzer(), expectedCompilerErrors: ["CS7036"]);
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0045", DiagnosticSeverity.Info, source, "Timeout",
            "Use 'CooperativeCancellation = true' with '[Timeout]' to enable cooperative cancellation behavior");
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostics[0], new UseCooperativeCancellationForTimeoutFixer()));
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(document, source, "CS7036");
    }

    [TestMethod]
    [DataRow("Timeout(37)")]
    [DataRow("Timeout(37, CooperativeCancellation = false)")]
    public async Task Timeout_ValidAttribute_EnablesCooperativeCancellationAndPreservesDuration(string attribute)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [{{attribute}}]
                public void Check() { }
            }
            """;
        const string expected = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [Timeout(37, CooperativeCancellation = true)]
                public void Check() { }
            }
            """;
        await VerifyFixAsync(source, expected, new UseCooperativeCancellationForTimeoutAnalyzer(), new UseCooperativeCancellationForTimeoutFixer(),
            "MSTEST0045", DiagnosticSeverity.Info, attribute,
            "Use 'CooperativeCancellation = true' with '[Timeout]' to enable cooperative cancellation behavior");
        using var workspace = new AdhocWorkspace();
        Document valid = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, expected);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(valid, new UseCooperativeCancellationForTimeoutAnalyzer()));
    }

    [TestMethod]
    [DataRow("ConditionMode.Exclude, OperatingSystems.Linux", "OperatingSystem.IsWindows()", "ConditionMode.Exclude, OperatingSystems.Linux | OperatingSystems.Windows")]
    [DataRow("ConditionMode.Include, OperatingSystems.OSX", "!OperatingSystem.IsLinux()", "OperatingSystems.OSX | OperatingSystems.Linux")]
    [DataRow("OperatingSystems.Linux", "!OperatingSystem.IsFreeBSD()", "OperatingSystems.Linux | OperatingSystems.FreeBSD")]
    public async Task OSCondition_ExistingModes_CombineExactFlags(string existingArguments, string check, string expectedArguments)
    {
        string source = OSSource($"OSCondition({existingArguments})", $"if ({check}) {{ return; }}");
        string expected = OSSource($"OSCondition({expectedArguments})", string.Empty);
        await VerifyFixAsync(source, expected, new UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer(), new UseOSConditionAttributeInsteadOfRuntimeCheckFixer(),
            "MSTEST0061", DiagnosticSeverity.Info, $"if ({check}) {{ return; }}",
            "Use '[OSCondition]' attribute instead of 'RuntimeInformation.IsOSPlatform' calls with early return or 'Assert.Inconclusive'");
    }

    [TestMethod]
    [DataRow("OSCondition")]
    [DataRow("OSCondition()")]
    public async Task OSCondition_IncompleteExistingAttribute_RepairsCondition(string existingAttribute)
    {
        string source = OSSource(existingAttribute, "if (!OperatingSystem.IsLinux()) { return; }");
        string expected = OSSource("OSCondition(OperatingSystems.Linux)", string.Empty);
        await VerifyFixAsync(source, expected, new UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer(), new UseOSConditionAttributeInsteadOfRuntimeCheckFixer(),
            "MSTEST0061", DiagnosticSeverity.Info, "if (!OperatingSystem.IsLinux()) { return; }",
            "Use '[OSCondition]' attribute instead of 'RuntimeInformation.IsOSPlatform' calls with early return or 'Assert.Inconclusive'",
            originalCompilerErrors: ["CS1729"]);
    }

    [TestMethod]
    [DataRow("Contains", "17", false, "int", "System.Collections.Generic.KeyValuePair<string, object>")]
    [DataRow("Remove", "17", false, "int", "string")]
    [DataRow("Contains", "\"Key\"", true, "string", "System.Collections.Generic.KeyValuePair<string, object>")]
    public async Task ContainsKey_CompilerDiagnostic_RejectsNonStringAndUnrelatedCalls(string method, string argument, bool expectedFix, string argumentType, string expectedType)
    {
        string source = $$"""
            using System.Collections.Generic;
            public class Tests
            {
                public void Check(IDictionary<string, object> properties)
                {
                    properties.{{method}}({{argument}});
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation, "CS1503");
        Diagnostic diagnostic = compilation.GetDiagnostics().Single(diagnostic => diagnostic.Id == "CS1503");
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostic, "CS1503", DiagnosticSeverity.Error, source, argument,
            $"Argument 1: cannot convert from '{argumentType}' to '{expectedType}'");
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostic, new TestContextPropertiesContainsKeyFixer());
        Assert.HasCount(expectedFix ? 1 : 0, actions);
        if (expectedFix)
        {
            Document changed = await AnalyzerContinuationTestHelpers.ApplyAsync(document, actions[0]);
            await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, source.Replace("properties.Contains(", "properties.ContainsKey("));
        }
    }

    [TestMethod]
    [DataRow("Assert.IsTrue(item == null, \"context\")", "Assert.IsNull(item, \"context\")", "CodeFixModeSimple", "IsNull", "IsTrue")]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\"), \"context\")", "Assert.Contains(\"needle\", values, \"context\")", "CodeFixModeAddArgument", "Contains", "IsTrue")]
    [DataRow("Assert.IsTrue(values.Contains(\"needle\", StringComparer.OrdinalIgnoreCase), \"context\")", "Assert.Contains(\"needle\", values, StringComparer.OrdinalIgnoreCase, \"context\")", "CodeFixModeAddTwoArguments", "Contains", "IsTrue")]
    [DataRow("Assert.AreEqual(false, flag, \"context\")", "Assert.IsFalse(flag, \"context\")", "CodeFixModeRemoveArgument", "IsFalse", "AreEqual")]
    [DataRow("Assert.AreEqual(0, values.Count, \"context\")", "Assert.IsEmpty(values, \"context\")", "CodeFixModeRemoveArgumentAndReplaceArgument", "IsEmpty", "AreEqual")]
    [DataRow("Assert.AreEqual(1, values.Count(value => value == \"needle\"), \"context\")", "Assert.ContainsSingle(value => value == \"needle\", values, \"context\")", "CodeFixModeRemoveArgumentReplaceArgumentAndAddArgument", "ContainsSingle", "AreEqual")]
    public async Task ProperAssert_AllRewriteModes_PreserveOperandOrderAndMessage(string invocation, string expectedInvocation, string expectedMode, string properMethod, string originalMethod)
    {
        string source = ProperAssertSource(invocation);
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseProperAssertMethodsAnalyzer());
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0037", DiagnosticSeverity.Info, source, invocation,
            $"Use 'Assert.{properMethod}' instead of 'Assert.{originalMethod}'");
        Assert.AreEqual(expectedMode, diagnostics[0].Properties["CodeFixModeKey"]);
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostics[0], new UseProperAssertMethodsFixer());
        Assert.HasCount(1, actions);
        Document changed = await AnalyzerContinuationTestHelpers.ApplyAsync(document, actions[0]);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, ProperAssertSource(expectedInvocation));
    }

    internal static string ProperAssertSource(string invocation) => $$"""
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Microsoft.VisualStudio.TestTools.UnitTesting;
        public class Tests
        {
            public void Check(object item, bool flag, List<string> values)
            {
                {{invocation}};
            }
        }
        """;

    internal static string BooleanSource(string invocation) => $$"""
        using Microsoft.VisualStudio.TestTools.UnitTesting;
        public class Tests
        {
            public void Check(bool flag, bool other)
            {
                {{invocation}};
            }
        }
        """;

    internal static string OSSource(string attribute, string statement) => $$"""
        using System;
        using Microsoft.VisualStudio.TestTools.UnitTesting;
        public class Tests
        {
            [TestMethod]
            [{{attribute}}]
            public void Check()
            {
                {{statement}}
                Assert.AreEqual(17, GetValue());
            }
            private int GetValue() => 17;
        }
        """;

    private static string CallerInfoSource(string parameters, string initializer) => $$"""
        using System.Runtime.CompilerServices;
        using Microsoft.VisualStudio.TestTools.UnitTesting;
        public class CustomTestMethodAttribute : TestMethodAttribute
        {
            public CustomTestMethodAttribute({{parameters}}){{initializer}} { }
        }
        """;

    private static async Task VerifyFixAsync(
        string source,
        string expected,
        DiagnosticAnalyzer analyzer,
        CodeFixProvider fixer,
        string id,
        DiagnosticSeverity severity,
        string span,
        string message,
        bool includeMSTest = true,
        string[]? originalCompilerErrors = null,
        string[]? fixedCompilerErrors = null)
    {
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, analyzer, expectedCompilerErrors: originalCompilerErrors ?? []);
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], id, severity, source, span, message);
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostics[0], fixer);
        Assert.HasCount(1, actions);
        Assert.IsFalse(string.IsNullOrWhiteSpace(actions[0].Title));
        Document changed = await AnalyzerContinuationTestHelpers.ApplyAsync(document, actions[0]);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, expected, fixedCompilerErrors ?? []);
    }
}
