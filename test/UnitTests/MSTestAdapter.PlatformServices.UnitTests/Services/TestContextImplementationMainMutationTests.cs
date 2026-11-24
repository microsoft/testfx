// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestContextImplementationMainMutationTests : TestContainer
{
    private readonly Mock<ITestMethod> _testMethod = new();

#if DEBUG && !NETFRAMEWORK
    public void ConstructorAssertsThatPropertiesAreProvided()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        Action create = () => _ = new TestContextImplementation(null, null, null!, null, null);

        create.Should().Throw<ArgumentNullException>();
        assertions.Should().Equal("properties is not null");
    }

    public void ConstructorDoesNotAssertWhenPropertiesAreProvided()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        using TestContextImplementation context = CreateContext(null);

        assertions.Should().BeEmpty();
    }
#endif

    public void TestRunCancellationCancelsTheTokenOfTheContext()
    {
        var runCancellation = new TestRunCancellationToken();
        using TestContextImplementation context = CreateContext(runCancellation);
        context.CancellationTokenSource.IsCancellationRequested.Should().BeFalse();

        runCancellation.Cancel();

        context.CancellationTokenSource.IsCancellationRequested.Should().BeTrue();
    }

    public void DisposeUnregistersTheTestRunCancellationCallback()
    {
        var runCancellation = new TestRunCancellationToken();
        TestContextImplementation context = CreateContext(runCancellation);

        context.Dispose();
        runCancellation.Cancel();

        runCancellation.Canceled.Should().BeTrue();
        context.CancellationTokenSource.IsCancellationRequested.Should().BeFalse();
    }

    private TestContextImplementation CreateContext(TestRunCancellationToken? runCancellation)
        => new(_testMethod.Object, null, new Dictionary<string, object?>(), null, runCancellation);
}
