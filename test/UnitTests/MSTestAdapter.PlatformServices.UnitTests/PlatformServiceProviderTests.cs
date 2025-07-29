// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using FluentAssertions;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class PlatformServiceProviderTests : TestContainer
{
    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void ProviderServiceInstanceShouldReturnAnObjectOfItselfByDefault() => Verify(PlatformServiceProvider.Instance.GetType() == typeof(PlatformServiceProvider));

    public void ProviderServiceInstanceShouldReturnTheInstanceSet()
    {
        // If this test fails most other tests would too since this
        // defines our mocking for the Service provider.
        PlatformServiceProvider.Instance = new TestablePlatformServiceProvider();
        Verify(PlatformServiceProvider.Instance.GetType() == typeof(TestablePlatformServiceProvider));
    }

    public void TestSourceShouldReturnANonNullInstance() => PlatformServiceProvider.Instance.Should().NotBeNull();

    public void TestSourceShouldReturnAValidTestSource() => Verify(PlatformServiceProvider.Instance.TestSource.GetType() == typeof(TestSource));

    public void TestSourceShouldBeCached()
    {
        PlatformServices.Interface.ITestSource testSourceInstance = PlatformServiceProvider.Instance.TestSource;

        testSourceInstance.Should().NotBeNull();
        testSourceInstance.Should().Be(PlatformServiceProvider.Instance.TestSource);
    }

    public void ReflectionOperationsShouldReturnAValidInstance() => Verify(PlatformServiceProvider.Instance.ReflectionOperations.GetType() == typeof(ReflectionOperations2));

    public void ReflectionOperationsShouldBeCached()
    {
        PlatformServices.Interface.IReflectionOperations reflectionOperationsInstance = PlatformServiceProvider.Instance.ReflectionOperations;

        reflectionOperationsInstance.Should().NotBeNull();
        reflectionOperationsInstance.Should().Be(PlatformServiceProvider.Instance.ReflectionOperations);
    }

    public void GetTestContextShouldReturnAValidTestContext()
    {
        // Arrange.
        var testMethod = new Mock<Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod>();
        var properties = new Dictionary<string, object?> { { "prop", "value" } };
        testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        testMethod.Setup(tm => tm.Name).Returns("M");

        // Act.
        PlatformServices.Interface.ITestContext testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod.Object, properties, null!, default);

        // Assert.
        testContext.Context.FullyQualifiedTestClassName.Should().Be("A.C.M");
        testContext.Context.TestName.Should().Be("M");
        Verify(testContext.Context.Properties.Contains(properties.ToArray()[0].Key));
        Verify(((IDictionary<string, object>)testContext.Context.Properties).Contains(properties.ToArray()[0]!));
    }
}
