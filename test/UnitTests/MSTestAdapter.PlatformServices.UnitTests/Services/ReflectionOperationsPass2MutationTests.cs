// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

[DoNotParallelize]
public sealed class ReflectionOperationsPass2MutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();
    private readonly ReflectionOperations _reflectionOperations = new();

    public ReflectionOperationsPass2MutationTests() => PlatformServiceProvider.Instance = _provider;

    protected override void Dispose(bool disposing)
    {
        try
        {
            PlatformServiceProvider.Instance = _previousProvider;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public void GetCustomAttributesCached_ReturnsNoAttributesWithoutWarningWhenTheProviderReturnsNull()
    {
        var reflection = new Mock<IReflectionOperations>();
        reflection.Setup(setup => setup.GetCustomAttributes(It.IsAny<MemberInfo>())).Returns((object[])null!);
        _provider.SetupMockReflectionOperations(reflection);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        List<(string Format, object?[] Arguments)> warnings = [];
        _provider.MockTraceLogger.Setup(logger => logger.Warning(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => warnings.Add((format, arguments)));

        Attribute[] result = _reflectionOperations.GetCustomAttributesCached(typeof(ReflectionOperationsPass2MutationTests));

        result.Should().BeEmpty();
        warnings.Should().BeEmpty(because: "a provider that reports no attributes at all has not failed to get them");
    }

    public void GetCustomAttributesCached_CachesTheAttributeArrayOfTheProviderWithoutCopyingIt()
    {
        Attribute[] attributes = [new DummyAttribute()];
        var reflection = new Mock<IReflectionOperations>();
        reflection.Setup(setup => setup.GetCustomAttributes(It.IsAny<MemberInfo>())).Returns(attributes);
        _provider.SetupMockReflectionOperations(reflection);

        Attribute[] result = _reflectionOperations.GetCustomAttributesCached(typeof(ReflectionOperationsPass2MutationTests));

        result.Should().BeSameAs(attributes, because: "attributes that already are an Attribute[] need no conversion");
    }

    private sealed class DummyAttribute : Attribute;
}
