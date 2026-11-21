// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class PlatformServiceProviderMutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly IPlatformServiceProvider _provider;

    public PlatformServiceProviderMutationTests()
    {
        PlatformServiceProvider.Instance = null;
        _provider = PlatformServiceProvider.Instance;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = _previousProvider;
        }
    }

    public void TestDeploymentIsCreatedOnceAndReusedByEveryAccess()
    {
        ITestDeployment first = _provider.TestDeployment;
        ITestDeployment second = _provider.TestDeployment;

#if WIN_UI
        first.Should().BeOfType<UnsupportedTestDeployment>();
#else
        first.Should().BeOfType<TestDeployment>();
#endif
        second.Should().BeSameAs(first);
    }

    public void SettingsProviderIsCreatedOnceAndReusedByEveryAccess()
    {
        ISettingsProvider first = _provider.SettingsProvider;
        ISettingsProvider second = _provider.SettingsProvider;

        first.Should().BeOfType<MSTestSettingsProvider>();
        second.Should().BeSameAs(first);
    }

    public void ThreadOperationsAreCreatedOnceAndReusedByEveryAccess()
    {
        IThreadOperations first = _provider.ThreadOperations;
        IThreadOperations second = _provider.ThreadOperations;

        first.Should().BeOfType<ThreadOperations>();
        second.Should().BeSameAs(first);
    }

    public void GracefulStopRequestedRoundTripsBothValues()
    {
        _provider.IsGracefulStopRequested.Should().BeFalse();

        _provider.IsGracefulStopRequested = true;
        _provider.IsGracefulStopRequested.Should().BeTrue();

        _provider.IsGracefulStopRequested = false;
        _provider.IsGracefulStopRequested.Should().BeFalse();
    }

    public void GetTestContextAppliesTheRequestedOutcome()
    {
        var testMethod = new Mock<ITestMethod>();
        testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        testMethod.Setup(tm => tm.Name).Returns("M");

        UTF.UnitTestOutcome[] outcomes = [UTF.UnitTestOutcome.Passed, UTF.UnitTestOutcome.Inconclusive, UTF.UnitTestOutcome.Timeout];
        foreach (UTF.UnitTestOutcome outcome in outcomes)
        {
            ITestContext testContext = _provider.GetTestContext(testMethod.Object, null, new Dictionary<string, object?>(), null!, outcome);

            testContext.Context.CurrentTestOutcome.Should().Be(outcome, because: $"the context is created with the {outcome} outcome");
        }
    }
}
