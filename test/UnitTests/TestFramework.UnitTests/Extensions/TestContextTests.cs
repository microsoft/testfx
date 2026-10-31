// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;

using TestFramework.ForTestingMSTest;

#pragma warning disable MSTESTEXP // Tests exercise the experimental ambient context contract.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions;

public sealed class TestContextTests : TestContainer
{
    public void InvalidPropertyTypesReportTheKeyAndActualType()
    {
        string? failure = null;
        using var observer = new DiagnosticAssertionObserver(message => failure = message);
        using var context = new Context();
        context.Properties["TestName"] = 42;
        Action read = () => _ = context.TestName;
        read.Should().ThrowExactly<InvalidCastException>().WithMessage("*TestName*System.Int32*System.String*");
#if DEBUG
        failure.Should().Be("How did an invalid value get in here?");
#endif
    }

    public void MissingAndNullPropertiesHaveDefinedDefaults()
    {
        using var context = new Context();
        Action className = () => _ = context.FullyQualifiedTestClassName;
        Action testName = () => _ = context.TestName;
        className.Should().ThrowExactly<InvalidOperationException>().WithMessage("*FullyQualifiedTestClassName*");
        testName.Should().ThrowExactly<InvalidOperationException>().WithMessage("*TestName*");
        context.Properties["TestName"] = null;
        testName.Should().ThrowExactly<InvalidOperationException>().WithMessage("*TestName*");
        context.CurrentTestOutcome.Should().Be(UnitTestOutcome.Unknown);
        context.TestRunCount.Should().Be(0);
#if !WIN_UI
        context.TestTempDirectory.Should().BeNull();
        context.TestRunDirectory.Should().BeNull();
        context.DeploymentDirectory.Should().BeNull();
        context.ResultsDirectory.Should().BeNull();
        context.TestRunResultsDirectory.Should().BeNull();
        context.TestResultsDirectory.Should().BeNull();
#endif
    }

#if !WIN_UI
    public void DirectoryPropertiesUseIndependentKeys()
    {
        using var context = new Context();
        context.Properties["TestRunDirectory"] = "run";
        context.Properties["DeploymentDirectory"] = "out";
        context.Properties["ResultsDirectory"] = "in";
        context.Properties["TestRunResultsDirectory"] = "machine";
        context.Properties["TestResultsDirectory"] = "results";
        context.TestRunDirectory.Should().Be("run");
        context.DeploymentDirectory.Should().Be("out");
        context.ResultsDirectory.Should().Be("in");
        context.TestRunResultsDirectory.Should().Be("machine");
        context.TestResultsDirectory.Should().Be("results");
    }
#endif

    public void MetadataAndCancellationArePerContext()
    {
        using var context = new Context();
        context.Properties["FullyQualifiedTestClassName"] = "Example.Tests";
        context.Properties["TestName"] = "Calculate";
        context.SetMetadata([3, "row"], "Calculation", new InvalidOperationException("failure"));
        context.FullyQualifiedTestClassName.Should().Be("Example.Tests");
        context.TestName.Should().Be("Calculate");
        context.TestData.Should().Equal(3, "row");
        context.TestDisplayName.Should().Be("Calculation");
        context.TestException!.Message.Should().Be("failure");
        context.CancellationToken.IsCancellationRequested.Should().BeFalse();
        context.CancellationTokenSource.Cancel();
        context.CancellationToken.IsCancellationRequested.Should().BeTrue();

        using var other = new Context();
        other.CancellationToken.IsCancellationRequested.Should().BeFalse();
        other.TestData.Should().BeNull();
        other.TestException.Should().BeNull();
        PropertyInfo runCount = typeof(TestContext).GetProperty(nameof(TestContext.TestRunCount))!;
        runCount.SetValue(context, 2);
        context.TestRunCount.Should().Be(2);
    }

    public async Task CurrentFlowsWithoutLeakingToCaller()
    {
        PropertyInfo current = typeof(TestContext).GetProperty(nameof(TestContext.Current))!;
        TestContext? previous = TestContext.Current;
        using var parent = new Context();
        using var child = new Context();
        try
        {
            current.SetValue(null, parent);
            await Task.Run(() =>
            {
                TestContext.Current.Should().BeSameAs(parent);
                current.SetValue(null, child);
                TestContext.Current.Should().BeSameAs(child);
            });
            TestContext.Current.Should().BeSameAs(parent);
            current.SetValue(null, null);
            TestContext.Current.Should().BeNull();
        }
        finally
        {
            current.SetValue(null, previous);
        }
    }

    private sealed class Context : TestContext, IDisposable
    {
        public override IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

#if NETFRAMEWORK
        public override System.Data.DataRow? DataRow => null;

        public override System.Data.Common.DbConnection? DataConnection => null;
#endif

        public void SetMetadata(object?[] data, string displayName, Exception exception)
        {
            TestData = data;
            TestDisplayName = displayName;
            TestException = exception;
        }

        public void Dispose() => CancellationTokenSource.Dispose();

        public override void AddResultFile(string fileName) => throw new NotSupportedException();

        public override void Write(string? message) => throw new NotSupportedException();

        public override void Write(string format, params object?[] args) => throw new NotSupportedException();

        public override void WriteLine(string? message) => throw new NotSupportedException();

        public override void WriteLine(string format, params object?[] args) => throw new NotSupportedException();

        public override void DisplayMessage(MessageLevel messageLevel, string message) => throw new NotSupportedException();
    }
}
