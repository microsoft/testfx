// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

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

    public void ProviderServiceInstanceShouldReturnAnObjectOfItselfByDefault()
    {
        Verify(PlatformServiceProvider.Instance.GetType() == typeof(PlatformServiceProvider));
    }

    public void ProviderServiceInstanceShouldReturnTheInstanceSet()
    {
        // If this test fails most other tests would too since this
        // defines our mocking for the Service provider.
        PlatformServiceProvider.Instance = new TestablePlatformServiceProvider();
        Verify(PlatformServiceProvider.Instance.GetType() == typeof(TestablePlatformServiceProvider));
    }

    public void TestSourceShouldReturnANonNullInstance()
    {
        Verify(PlatformServiceProvider.Instance is not null);
    }

    public void TestSourceShouldReturnAValidTestSource()
    {
        Verify(PlatformServiceProvider.Instance.TestSource.GetType() == typeof(TestSource));
    }

    public void TestSourceShouldBeCached()
    {
        var testSourceInstance = PlatformServiceProvider.Instance.TestSource;

        Verify(testSourceInstance is not null);
        Verify(testSourceInstance == PlatformServiceProvider.Instance.TestSource);
    }

    public void ReflectionOperationsShouldReturnAValidInstance()
    {
        Verify(PlatformServiceProvider.Instance.ReflectionOperations.GetType() == typeof(ReflectionOperations));
    }

    public void ReflectionOperationsShouldBeCached()
    {
        var reflectionOperationsInstance = PlatformServiceProvider.Instance.ReflectionOperations;

        Verify(reflectionOperationsInstance is not null);
        Verify(reflectionOperationsInstance == PlatformServiceProvider.Instance.ReflectionOperations);
    }

    public void GetTestContextShouldReturnAValidTestContext()
    {
        // Arrange.
        var testMethod = new Mock<Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod>();
        var writer = new ThreadSafeStringWriter(null, "test");
        var properties = new Dictionary<string, object> { { "prop", "value" } };
        testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        testMethod.Setup(tm => tm.Name).Returns("M");

        // Act.
        var testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod.Object, writer, properties);

        // Assert.
        Verify(testContext.Context.FullyQualifiedTestClassName == "A.C.M");
        Verify(testContext.Context.TestName == "M");
        Verify(testContext.Context.Properties.Contains(properties.ToArray()[0].Key));
        Verify(((IDictionary<string, object>)testContext.Context.Properties).Contains(properties.ToArray()[0]));
    }
}
