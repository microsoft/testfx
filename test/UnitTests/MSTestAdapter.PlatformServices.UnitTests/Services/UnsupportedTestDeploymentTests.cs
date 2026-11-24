// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class UnsupportedTestDeploymentTests : TestContainer
{
    private readonly MethodInfo _method = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.TestMethod))!;
    private readonly TestablePlatformServiceProvider _platformServiceProvider;

    public UnsupportedTestDeploymentTests()
    {
        _platformServiceProvider = new TestablePlatformServiceProvider();
        _platformServiceProvider.SetupMockReflectionOperations();
        _platformServiceProvider.MockReflectionOperations.Setup(value => value.GetCustomAttributes(It.IsAny<MemberInfo>())).Returns([]);
        PlatformServiceProvider.Instance = _platformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void GetUnsupportedDeploymentMessageReturnsNullWhenNoDeploymentItemIsDeclared()
        => new UnsupportedTestDeployment().GetUnsupportedDeploymentMessage(_method, typeof(DummyTestClass)).Should().BeNull();

    public void GetUnsupportedDeploymentMessageRejectsDeploymentItemOnMethod()
    {
        _platformServiceProvider.MockReflectionOperations.Setup(value => value.GetCustomAttributes(_method)).Returns([new DeploymentItemAttribute("data.txt")]);

        string? message = new UnsupportedTestDeployment().GetUnsupportedDeploymentMessage(_method, typeof(DummyTestClass));

        message.Should().Be(Resource.DeploymentItemNotSupportedForUi);
        _platformServiceProvider.MockReflectionOperations.Verify(value => value.GetCustomAttributes(typeof(DummyTestClass)), Times.Never);
    }

    public void GetUnsupportedDeploymentMessageRejectsDeploymentItemOnClass()
    {
        _platformServiceProvider.MockReflectionOperations.Setup(value => value.GetCustomAttributes(typeof(DummyTestClass))).Returns([new DeploymentItemAttribute("data.txt")]);

        string? message = new UnsupportedTestDeployment().GetUnsupportedDeploymentMessage(_method, typeof(DummyTestClass));

        message.Should().Be(Resource.DeploymentItemNotSupportedForUi);
        _platformServiceProvider.MockReflectionOperations.Verify(value => value.GetCustomAttributes(_method), Times.Once);
    }

    public void LifecycleMembersDeployNothing()
    {
        var deployment = new UnsupportedTestDeployment();
        var warnings = new List<string>();

        deployment.Deploy([new UnitTestElement(new TestMethod("M", "C", "A.dll", displayName: null))], new DeploymentContext(null, null), new Mock<IAdapterMessageLogger>().Object).Should().BeFalse();
        deployment.GetDeploymentItems(_method, typeof(DummyTestClass), warnings).Should().BeNull();
        deployment.GetDeploymentDirectory().Should().BeNull();
        deployment.Cleanup();

        warnings.Should().BeEmpty();
        _platformServiceProvider.MockReflectionOperations.Verify(value => value.GetCustomAttributes(It.IsAny<MemberInfo>()), Times.Never);
    }

    public void TestDeploymentNeverRejectsDeploymentItems()
    {
        _platformServiceProvider.MockReflectionOperations.Setup(value => value.GetCustomAttributes(It.IsAny<MemberInfo>())).Returns([new DeploymentItemAttribute("data.txt")]);

        new TestDeployment().GetUnsupportedDeploymentMessage(_method, typeof(DummyTestClass)).Should().BeNull();
    }

    public void PlatformServiceProviderSelectsDeploymentFromAppModelCapabilities()
    {
        PlatformServiceProvider.Instance = null;

        ITestDeployment deployment = PlatformServiceProvider.Instance.TestDeployment;

#if WIN_UI
        deployment.Should().BeOfType<UnsupportedTestDeployment>();
        AppModelCapabilities.SupportsDeployment.Should().BeFalse();
#else
        deployment.Should().BeOfType<TestDeployment>();
        AppModelCapabilities.SupportsDeployment.Should().BeTrue();
#endif
    }

    public class DummyTestClass
    {
        public void TestMethod()
        {
        }
    }
}
