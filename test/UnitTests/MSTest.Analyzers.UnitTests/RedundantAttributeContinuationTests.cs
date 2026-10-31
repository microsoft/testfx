// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class RedundantAttributeContinuationTests
{
    [TestMethod]
    [DataRow("", "OSCondition(OperatingSystems.Linux)", "")]
    [DataRow("", "CICondition(ConditionMode.Include)", "")]
    [DataRow("OSCondition()", "OSCondition(OperatingSystems.Linux)", "CS1729")]
    [DataRow("OSCondition(OperatingSystems.Linux)", "OSCondition()", "CS1729")]
    [DataRow("CICondition()", "CICondition(ConditionMode.Include)", "CS7036")]
#if NET
    [DataRow("", "ArchitectureCondition(TestArchitectures.X64)", "")]
    [DataRow("ArchitectureCondition()", "ArchitectureCondition(TestArchitectures.X64)", "CS1729")]
#endif
    public async Task Conditions_AbsentOrMalformedClassContract_DoesNotRemoveMethodAttribute(string classAttribute, string methodAttribute, string compilerError)
        => await VerifyAsync(classAttribute, methodAttribute, expectedDiagnostic: false, compilerError);

    [TestMethod]
    [DataRow("OSCondition(ConditionMode.Include, OperatingSystems.Linux)", "OSCondition(OperatingSystems.Linux | OperatingSystems.FreeBSD)", true)]
    [DataRow("OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)", "OSCondition(OperatingSystems.Windows | OperatingSystems.OSX | OperatingSystems.FreeBSD)", false)]
    [DataRow("OSCondition(ConditionMode.Exclude, OperatingSystems.Linux | OperatingSystems.Windows)", "OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)", true)]
    [DataRow("CICondition(ConditionMode.Exclude)", "CICondition(ConditionMode.Include)", false)]
#if NET
    [DataRow("ArchitectureCondition(ConditionMode.Exclude, TestArchitectures.X64)", "ArchitectureCondition(ConditionMode.Exclude, TestArchitectures.X64)", true)]
#endif
    public async Task Conditions_MatchingAndRestrictingFlags_RespectUnknownValues(string classAttribute, string methodAttribute, bool expectedDiagnostic)
        => await VerifyAsync(classAttribute, methodAttribute, expectedDiagnostic);

    [TestMethod]
    [DataRow("TestCategory(\"Search\")", "TestCategory(\"search\")", false, "")]
    [DataRow("TestCategory(null)", "TestCategory(\"Search\")", false, "")]
    [DataRow("TestCategory(null)", "TestCategory(null)", true, "")]
    [DataRow("TestProperty(\"Feature\", \"Search\")", "TestProperty(\"Other\", \"Search\")", false, "")]
    [DataRow("TestProperty(\"Feature\", null)", "TestProperty(\"Feature\", \"Search\")", false, "")]
    [DataRow("TestProperty(\"Feature\", null)", "TestProperty(\"Feature\", null)", true, "")]
    [DataRow("TestProperty(\"Feature\")", "TestProperty(\"Feature\", \"Search\")", false, "CS7036")]
    public async Task CategoryAndPropertyConstants_CompareAllArguments(string classAttribute, string methodAttribute, bool expectedDiagnostic, string compilerError)
        => await VerifyAsync(classAttribute, methodAttribute, expectedDiagnostic, compilerError);

    [TestMethod]
    [DataRow("DependsOn(typeof(Other))", "DependsOn(typeof(Other))", true)]
    [DataRow("DependsOn(typeof(Other))", "DependsOn(typeof(Different))", false)]
    [DataRow("DependsOn(typeof(Other), \"Setup\")", "DependsOn(typeof(Other), \"Setup\")", true)]
    [DataRow("DependsOn(typeof(Other), \"Setup\")", "DependsOn(typeof(Other), \"Different\")", false)]
    [DataRow("DependsOn(typeof(Other))", "DependsOn((System.Type)null)", false)]
    [DataRow("DependsOn((System.Type)null)", "DependsOn(typeof(Other))", false)]
    [DataRow("DependsOn(\"Check\")", "DependsOn(\"Check\")", false)]
    [DataRow("DependsOn(\"Setup\", ProceedOnFailure = true)", "DependsOn(\"Setup\", ProceedOnFailure = false)", false)]
    [DataRow("DependsOn(\"Setup\", ProceedOnFailure = false)", "DependsOn(\"Setup\", ProceedOnFailure = true)", true)]
    [DataRow("DependsOn(\"Setup\", ProceedOnFailure = true)", "DependsOn(\"Setup\", ProceedOnFailure = true)", true)]
    public async Task Dependencies_TypeTargetsAndFailurePolicy_RespectIdentity(string classAttribute, string methodAttribute, bool expectedDiagnostic)
        => await VerifyAsync(classAttribute, methodAttribute, expectedDiagnostic);

    [TestMethod]
    [DataRow("ResourceLock(\"Cache\")", "ResourceLock(\"cache\")", false)]
    [DataRow("ResourceLock(\"Cache\")", "ResourceLock(null)", false)]
    [DataRow("ResourceLock(null)", "ResourceLock(\"Cache\")", false)]
    [DataRow("ResourceLock(\"Cache\")", "ResourceLock(\"Cache\", Mode = (ResourceAccessMode)7)", false)]
    [DataRow("ResourceLock(\"Cache\", Mode = ResourceAccessMode.Read)", "ResourceLock(\"Cache\", Mode = ResourceAccessMode.Read)", true)]
    [DataRow("DeploymentItem(\"Data\")", "DeploymentItem(\"Data\")", true)]
    [DataRow("DeploymentItem(\"Data\")", "DeploymentItem(\"Data\", \"\")", true)]
    [DataRow("DeploymentItem(\"Data\", \"Out\")", "DeploymentItem(\"Data\", \"Other\")", false)]
    [DataRow("DeploymentItem(\"Data\")", "DeploymentItem(null)", false)]
    [DataRow("DeploymentItem(\"Data\")", "DeploymentItem(\"\")", false)]
    [DataRow("DeploymentItem(\"Data\")", "DeploymentItem(\"Data\", null)", false)]
    [DataRow("DeploymentItem(\"Data\", null)", "DeploymentItem(\"Data\")", false)]
    public async Task ResourcesAndDeployment_InvalidOrDistinctContracts_AreNotRedundant(string classAttribute, string methodAttribute, bool expectedDiagnostic)
        => await VerifyAsync(classAttribute, methodAttribute, expectedDiagnostic);

    [TestMethod]
    [DataRow("", "Retry(2)", false, "")]
    [DataRow("Retry()", "Retry(2)", false, "CS7036")]
    [DataRow("", "Ignore(\"Method reason\")", false, "")]
    [DataRow("Ignore(null)", "Ignore()", false, "")]
    [DataRow("Ignore()", "Ignore(null)", false, "")]
    [DataRow("Ignore(null)", "Ignore(null)", true, "")]
    [DataRow("Ignore(\"Class reason\", IgnoreMessage = null)", "Ignore(null)", true, "")]
    [DataRow("Retry(2, MillisecondsDelayBetweenRetries = 5)", "Retry(2, MillisecondsDelayBetweenRetries = 6)", false, "")]
    [DataRow("Retry(2, BackoffType = DelayBackoffType.Exponential)", "Retry(2)", false, "")]
    public async Task RetryAndIgnore_AbsentClassAndNullMessage_PreserveMethodContract(string classAttribute, string methodAttribute, bool expectedDiagnostic, string compilerError)
        => await VerifyAsync(classAttribute, methodAttribute, expectedDiagnostic, compilerError);

    private static async Task VerifyAsync(string classAttribute, string methodAttribute, bool expectedDiagnostic, string compilerError = "")
    {
        string source = CreateSource(classAttribute, methodAttribute);
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        string[] compilerErrors = compilerError.Length == 0 ? [] : [compilerError];
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new RedundantTestMethodAttributeAnalyzer(), expectedCompilerErrors: compilerErrors);
        Assert.HasCount(expectedDiagnostic ? 1 : 0, diagnostics);
        if (!expectedDiagnostic)
        {
            return;
        }

        string attributeName = methodAttribute.Split('(')[0];
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0086", DiagnosticSeverity.Info, source, methodAttribute,
            $"[{attributeName}] on test method 'Check' is redundant because it does not change behavior established by its containing test class");
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostics[0], new RedundantTestMethodAttributeFixer());
        Assert.HasCount(1, actions);
        Document changed = await AnalyzerContinuationTestHelpers.ApplyAsync(document, actions[0]);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(changed, CreateSource(classAttribute, string.Empty), compilerErrors);
    }

    private static string CreateSource(string classAttribute, string methodAttribute) => $$"""
        using Microsoft.VisualStudio.TestTools.UnitTesting;
        [TestClass]
        {{(classAttribute.Length == 0 ? string.Empty : $"[{classAttribute}]")}}
        public sealed class Tests
        {
            [TestMethod]
            public void Setup() { }
            [TestMethod]
            {{(methodAttribute.Length == 0 ? string.Empty : $"[{methodAttribute}]")}}
            public void Check() { }
        }
        public class Other { }
        public class Different { }
        """;

    [TestMethod]
    public async Task NonconstantCategoryArgument_IsNotEquivalentToExplicitNull()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass, TestCategory(Tests.GetCategory())]
            public sealed class Tests
            {
                public static string GetCategory() => "Search";
                [TestMethod, TestCategory(null)]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new RedundantTestMethodAttributeAnalyzer(), expectedCompilerErrors: ["CS0182"]));
    }

    [TestMethod]
    [DataRow("Name = \"Class\"", "Name = \"Class\"")]
    [DataRow("Name = \"Class\"", "Name = \"Method\"")]
    [DataRow("Name = \"Class\"", "Value = \"Class\"")]
    public async Task ReadonlyTestPropertyNamedArguments_CompilerRejectionRetainsConstructorEquivalence(string classNamedArgument, string methodNamedArgument)
    {
        string classAttribute = $"TestProperty(\"Feature\", \"Search\", {classNamedArgument})";
        string methodAttribute = $"TestProperty(\"Feature\", \"Search\", {methodNamedArgument})";
        string source = CreateSource(classAttribute, methodAttribute);
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new RedundantTestMethodAttributeAnalyzer(), expectedCompilerErrors: ["CS0617", "CS0617"]);
        // Roslyn retains compiler-rejected get-only assignments as error constants.
        // Pin that fixture shape as well as the actual diagnostic and complete fix.
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        INamedTypeSymbol tests = compilation.GetTypeByMetadataName("Tests")!;
        ImmutableArray<KeyValuePair<string, TypedConstant>> classArguments = tests.GetAttributes()
            .Single(attribute => attribute.AttributeClass?.Name == "TestPropertyAttribute").NamedArguments;
        ImmutableArray<KeyValuePair<string, TypedConstant>> methodArguments = tests.GetMembers("Check").Single().GetAttributes()
            .Single(attribute => attribute.AttributeClass?.Name == "TestPropertyAttribute").NamedArguments;
        Assert.HasCount(1, classArguments);
        Assert.HasCount(1, methodArguments);
        Assert.AreEqual("Name", classArguments[0].Key);
        Assert.AreEqual(methodNamedArgument.Split('=')[0].Trim(), methodArguments[0].Key);
        Assert.AreEqual(TypedConstantKind.Error, classArguments[0].Value.Kind);
        Assert.AreEqual(TypedConstantKind.Error, methodArguments[0].Value.Kind);
        Assert.IsNull(classArguments[0].Value.Value);
        Assert.IsNull(methodArguments[0].Value.Value);
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0086", DiagnosticSeverity.Info, source, methodAttribute,
            "[TestProperty] on test method 'Check' is redundant because it does not change behavior established by its containing test class");
        List<CodeAction> actions = await AnalyzerContinuationTestHelpers.GetActionsAsync(document, diagnostics[0], new RedundantTestMethodAttributeFixer());
        Assert.HasCount(1, actions);
        await AnalyzerContinuationTestHelpers.AssertSourceAsync(
            await AnalyzerContinuationTestHelpers.ApplyAsync(document, actions[0]), CreateSource(classAttribute, string.Empty), "CS0617");
    }
}
