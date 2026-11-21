// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

[DoNotParallelize]
public sealed class ReflectionOperationsMutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();
    private readonly ReflectionOperations _reflectionOperations = new();

    public ReflectionOperationsMutationTests() => PlatformServiceProvider.Instance = _provider;

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

#if DEBUG
    public void GetCustomAttributesShouldReportMembersThatDoNotReturnAnAttributeArray()
    {
        var attribute = new DummyAttribute();
        object[] attributes = [attribute];
        var member = new Mock<MemberInfo>();
        member.Setup(setup => setup.GetCustomAttributes(typeof(Attribute), true)).Returns(attributes);
        var assertions = new List<string?>();

        object[]? result;
        using (new DiagnosticAssertionObserver(assertions.Add))
        {
            result = _reflectionOperations.GetCustomAttributes(member.Object);
        }

        result.Should().Equal(attribute);
        assertions.Should().Equal("Expected Attribute[], found 'System.Object[]'.");
    }
#endif

    public void GetCustomAttributesOfAnAssemblyShouldIncludeInheritedAttributes()
    {
        var assembly = new RecordingAssembly();

        _reflectionOperations.GetCustomAttributes(assembly, typeof(Attribute));

        assembly.RequestedInherit.Should().BeTrue();
    }

    public void GetRuntimePropertyShouldOnlyLookAtNonPublicPropertiesWhenRequested()
    {
        _reflectionOperations.GetRuntimeProperty(typeof(PropertyHolder), nameof(PropertyHolder.Visible), includeNonPublic: false).Should().NotBeNull();
        _reflectionOperations.GetRuntimeProperty(typeof(PropertyHolder), nameof(PropertyHolder.Visible), includeNonPublic: true).Should().NotBeNull();
        _reflectionOperations.GetRuntimeProperty(typeof(PropertyHolder), nameof(PropertyHolder.Hidden), includeNonPublic: false).Should().BeNull();
        _reflectionOperations.GetRuntimeProperty(typeof(PropertyHolder), nameof(PropertyHolder.Hidden), includeNonPublic: true).Should().NotBeNull();
    }

    public void TryGetTestMethodDescriptorsShouldNeverReportDescriptors()
    {
        bool result = _reflectionOperations.TryGetTestMethodDescriptors(typeof(ReflectionOperationsMutationTests), out MethodInfo[]? methods, out bool areAllTestMethodsSupported);

        result.Should().BeFalse();
        methods.Should().BeNull();
        areAllTestMethodsSupported.Should().BeFalse();
    }

    public void GetCustomAttributesCachedShouldRejectProvidersThatAreNeitherMembersNorAssemblies()
    {
        Action action = () => _reflectionOperations.GetCustomAttributesCached(new UnsupportedAttributeProvider());

        ArgumentException exception = action.Should().ThrowExactly<ArgumentException>().Which;
        exception.ParamName.Should().Be("attributeProvider");
        exception.Message.Should().StartWith($"Unsupported attribute provider type: {typeof(UnsupportedAttributeProvider)}. Only MemberInfo and Assembly are supported.");
    }

    public void GetCustomAttributesNotCachedShouldRejectProvidersThatAreNeitherMembersNorAssemblies()
    {
        Action action = () => ReflectionOperations.NotCachedReflectionAccessor.GetCustomAttributesNotCached(new UnsupportedAttributeProvider());

        ArgumentException exception = action.Should().ThrowExactly<ArgumentException>().Which;
        exception.ParamName.Should().Be("attributeProvider");
        exception.Message.Should().StartWith($"Unsupported attribute provider type: {typeof(UnsupportedAttributeProvider)}. Only MemberInfo and Assembly are supported.");
    }

    public void GetCustomAttributesCachedShouldConvertAttributesReturnedAsAnObjectArray()
    {
        var attribute = new DummyAttribute();
        object[] attributes = [attribute];
        var reflection = new Mock<IReflectionOperations>();
        reflection.Setup(setup => setup.GetCustomAttributes(It.IsAny<MemberInfo>())).Returns(attributes);
        _provider.SetupMockReflectionOperations(reflection);

        Attribute[] result = _reflectionOperations.GetCustomAttributesCached(typeof(ReflectionOperationsMutationTests));

        result.Should().Equal(attribute);
    }

    public void IsMethodDeclaredInSameAssemblyAsTypeShouldCompareTheDeclaringAssemblyWithTheTypeAssembly()
    {
        MethodInfo method = typeof(ReflectionOperationsMutationTests).GetMethod(nameof(IsMethodDeclaredInSameAssemblyAsTypeShouldCompareTheDeclaringAssemblyWithTheTypeAssembly))!;

        _reflectionOperations.IsMethodDeclaredInSameAssemblyAsType(method, typeof(ReflectionOperationsMutationTests)).Should().BeTrue();
        _reflectionOperations.IsMethodDeclaredInSameAssemblyAsType(method, typeof(string)).Should().BeFalse();
    }

    public void IsMethodDeclaredInSameAssemblyAsTypeShouldReturnFalseWhenTheMethodHasNoDeclaringType()
    {
        var method = new Mock<MethodInfo>();

        _reflectionOperations.IsMethodDeclaredInSameAssemblyAsType(method.Object, typeof(ReflectionOperationsMutationTests)).Should().BeFalse();
    }

    private sealed class DummyAttribute : Attribute
    {
    }

    private sealed class PropertyHolder
    {
        public int Visible { get; set; }

        internal int Hidden { get; set; }
    }

    private sealed class RecordingAssembly : Assembly
    {
        public bool? RequestedInherit { get; private set; }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            RequestedInherit = inherit;
            return [];
        }
    }

    private sealed class UnsupportedAttributeProvider : ICustomAttributeProvider
    {
        public object[] GetCustomAttributes(bool inherit) => [];

        public object[] GetCustomAttributes(Type attributeType, bool inherit) => [];

        public bool IsDefined(Type attributeType, bool inherit) => false;
    }
}
