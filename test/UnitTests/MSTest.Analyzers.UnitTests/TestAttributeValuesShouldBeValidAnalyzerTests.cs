// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestAttributeValuesShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestAttributeValuesShouldBeValidAnalyzerTests
{
    [TestMethod]
    [DataRow("Timeout", 0, "timeout", DisplayName = "TimeoutZero")]
    [DataRow("Timeout", -1, "timeout", DisplayName = "TimeoutNegative")]
    [DataRow("Retry", 0, "maxRetryAttempts", DisplayName = "RetryZero")]
    [DataRow("Retry", -1, "maxRetryAttempts", DisplayName = "RetryNegative")]
    [DataRow("ExpectedDuration", 0, "milliseconds", DisplayName = "ExpectedDurationZero")]
    [DataRow("ExpectedDuration", -1, "milliseconds", DisplayName = "ExpectedDurationNegative")]
    public async Task NonPositiveTestAttributeArgument_ReportsItsAttribute(string attribute, int value, string parameter)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [{|#0:{{attribute}}({{value}})|}]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic($"'{attribute}Attribute' requires a positive '{parameter}' value"));
    }

    [TestMethod]
    [DataRow("AssemblyInitialize", "public static void Fixture(TestContext context)", DisplayName = "AssemblyInitialize")]
    [DataRow("AssemblyCleanup", "public static void Fixture()", DisplayName = "AssemblyCleanup")]
    [DataRow("ClassInitialize", "public static void Fixture(TestContext context)", DisplayName = "ClassInitialize")]
    [DataRow("ClassCleanup", "public static void Fixture()", DisplayName = "ClassCleanup")]
    [DataRow("TestInitialize", "public void Fixture()", DisplayName = "TestInitialize")]
    [DataRow("TestCleanup", "public void Fixture()", DisplayName = "TestCleanup")]
    [DataRow("GlobalTestInitialize", "public static void Fixture(TestContext context)", DisplayName = "GlobalTestInitialize")]
    [DataRow("GlobalTestCleanup", "public static void Fixture(TestContext context)", DisplayName = "GlobalTestCleanup")]
    public async Task NonPositiveFixtureTimeout_ReportsEverySupportedFixture(string fixture, string signature)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Tests
            {
                [{{fixture}}]
                [{|#0:Timeout(0)|}]
                {{signature}} { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("'TimeoutAttribute' requires a positive 'timeout' value"));
    }

    [TestMethod]
    public async Task NonPositiveRetryOnDerivedTestClass_IsReported()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class CustomTestClassAttribute : TestClassAttribute { }

            [CustomTestClass]
            [{|#0:Retry(0)|}]
            public class Tests { }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("'RetryAttribute' requires a positive 'maxRetryAttempts' value"));
    }

    [TestMethod]
    public async Task NonPositiveTimeoutOnDerivedTestMethod_IsReported()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class CustomTestMethodAttribute : TestMethodAttribute { }
            [TestClass]
            public class Tests
            {
                [CustomTestMethod]
                [{|#0:Timeout(0)|}]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("'TimeoutAttribute' requires a positive 'timeout' value"));
    }

    [TestMethod]
    [DataRow(-2, DisplayName = "NegativeTwo")]
    [DataRow(int.MinValue, DisplayName = "MinimumInteger")]
    public async Task InvalidRetryDelay_ReportsTheAttribute(int delay)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [{|#0:Retry(2, MillisecondsDelayBetweenRetries = {{delay}})|}]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("MillisecondsDelayBetweenRetries must be -1 or greater"));
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "Negative")]
    [DataRow(2, DisplayName = "AboveDefinedValues")]
    public async Task UndefinedRetryBackoff_ReportsTheAttribute(int value)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [{|#0:Retry(2, BackoffType = (DelayBackoffType)({{value}}))|}]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("The Retry.BackoffType value is not defined"));
    }

    [TestMethod]
    public async Task ValidBoundariesAndKnownBackoffValues_AreNotReported()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass, Retry(1, MillisecondsDelayBetweenRetries = -1, BackoffType = DelayBackoffType.Constant)]
            public class Tests
            {
                [TestMethod, Timeout(1), ExpectedDuration(1)]
                [Retry(1, MillisecondsDelayBetweenRetries = 0, BackoffType = DelayBackoffType.Exponential)]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task AttributesOnUnrelatedHelpers_AreNotReported()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [Retry(0)]
            public class Helper
            {
                [Timeout(0), Retry(0), ExpectedDuration(0)]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task OlderFramework_StillValidatesAvailableAttributes()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [{|#0:Timeout(0)|}]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, useMSTestV4: true, Diagnostic("'TimeoutAttribute' requires a positive 'timeout' value"));
    }

    private static DiagnosticResult Diagnostic(string message)
        => new DiagnosticResult("MSTEST0093", DiagnosticSeverity.Warning).WithLocation(0).WithMessage(message);
}
