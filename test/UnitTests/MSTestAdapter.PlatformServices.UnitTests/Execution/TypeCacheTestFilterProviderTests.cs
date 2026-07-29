// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Emit;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

#pragma warning disable MSTESTEXP // Programmatic test-filter API is experimental; exercised here under test.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheTestFilterProviderTests : TestContainer
{
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheTestFilterProviderTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    // Direct unit tests for the validation branches of InstantiateTestFilter. Driving these
    // through GetOrLoadTestFilter would require polluting AssemblyAttributes.cs with broken
    // markers that would then run for every test in this assembly. The helper is scoped,
    // internal, and self-contained, so testing it directly is both safer and clearer.
    public void InstantiateTestFilter_WhenTypeIsOpenGeneric_ThrowsUTA074()
    {
        Action act = () => TypeCache.InstantiateTestFilter(typeof(GenericFilter<>));
        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA074*");
    }

    public void InstantiateTestFilter_WhenTypeIsClosedGeneric_ThrowsUTA074()
    {
        Action act = () => TypeCache.InstantiateTestFilter(typeof(GenericFilter<int>));
        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA074*");
    }

    public void InstantiateTestFilter_WhenTypeIsAbstract_ThrowsUTA075()
    {
        Action act = () => TypeCache.InstantiateTestFilter(typeof(AbstractFilter));
        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA075*");
    }

    public void InstantiateTestFilter_WhenTypeIsInterface_ThrowsUTA075()
    {
        Action act = () => TypeCache.InstantiateTestFilter(typeof(IFilterInterface));
        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA075*");
    }

    public void InstantiateTestFilter_WhenTypeDoesNotImplementITestFilter_ThrowsUTA076()
    {
        Action act = () => TypeCache.InstantiateTestFilter(typeof(NotAFilter));
        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA076*");
    }

    public void InstantiateTestFilter_WhenConstructorThrows_ThrowsUTA077()
    {
        // Activator.CreateInstance wraps the constructor exception in TargetInvocationException;
        // the diagnostic surfaces UTA077 either way and preserves the inner exception.
        Action act = () => TypeCache.InstantiateTestFilter(typeof(ThrowingFilter));
        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA077*");
    }

    public void InstantiateTestFilter_WhenTypeIsMissingPublicParameterlessConstructor_ThrowsUTA077()
    {
        Action act = () => TypeCache.InstantiateTestFilter(typeof(FilterWithoutPublicCtor));
        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA077*");
    }

    public void InstantiateTestFilter_WhenTypeIsValid_ReturnsInstance()
    {
        ITestFilter filter = TypeCache.InstantiateTestFilter(typeof(NoOpFilter));
        filter.Should().NotBeNull().And.BeOfType<NoOpFilter>();
    }

    public void GetOrLoadTestFilter_WhenNonGenericMarkerComesFromFrameworkWithoutInternalContract_UsesConcreteAttributeLookup()
    {
        const string AssemblySource = nameof(GetOrLoadTestFilter_WhenNonGenericMarkerComesFromFrameworkWithoutInternalContract_UsesConcreteAttributeLookup);

        Assembly assembly = CreateDynamicAssemblyWithTestFilterProviderAttribute();
        var reflectionOperations = new ReflectionOperations();
        var mockReflectionOperations = new Mock<IReflectionOperations>(MockBehavior.Strict);

        _testablePlatformServiceProvider.MockFileOperations
            .Setup(fileOperations => fileOperations.LoadAssembly(AssemblySource))
            .Returns(assembly);
        mockReflectionOperations
            .Setup(operations => operations.GetCustomAttributes(assembly, typeof(TestFilterProviderAttribute)))
            .Returns(() => reflectionOperations.GetCustomAttributes(assembly, typeof(TestFilterProviderAttribute)));
        mockReflectionOperations
            .Setup(operations => operations.GetCustomAttributes(assembly, typeof(ITestFilterProviderAttribute)))
            .Throws(new TypeLoadException("Older MSTest.TestFramework does not have this contract."));
        _testablePlatformServiceProvider.SetupMockReflectionOperations(mockReflectionOperations);

        ITestFilter? filter = new TypeCache().GetOrLoadTestFilter(AssemblySource);

        filter.Should().BeOfType<NoOpFilter>();
        mockReflectionOperations.Verify(
            operations => operations.GetCustomAttributes(assembly, typeof(ITestFilterProviderAttribute)),
            Times.Never);
    }

    // The metadata probe has to recognize every supported marker shape without instantiating the attribute.
    // Missing one silently drops the user's filter, which is the exact failure mode [TestFilterProvider] is
    // designed to avoid. The probe's enumeration over GetCustomAttributesData is trivial; what these tests
    // pin down is the type-matching decision it makes per attribute.
    public void IsTestFilterProviderMarkerType_WhenTypeIsTheNonGenericMarker_ReturnsTrue()
        => TypeCache.IsTestFilterProviderMarkerType(typeof(TestFilterProviderAttribute)).Should().BeTrue();

    public void IsTestFilterProviderMarkerType_WhenTypeIsUnrelatedAttribute_ReturnsFalse()
        => TypeCache.IsTestFilterProviderMarkerType(typeof(TestClassAttribute)).Should().BeFalse();

    public void IsTestFilterProviderMarkerType_WhenTypeIsNull_ReturnsFalse()
        => TypeCache.IsTestFilterProviderMarkerType(null).Should().BeFalse();

#if NET
    // The constructed generic type's FullName embeds the type argument, so comparing it directly against the
    // marker's FullName would miss it — hence the generic-type-definition normalization in the probe.
    public void IsTestFilterProviderMarkerType_WhenTypeIsGenericMarker_ReturnsTrue()
        => TypeCache.IsTestFilterProviderMarkerType(typeof(TestFilterProviderAttribute<NoOpFilter>)).Should().BeTrue();
#endif

#if NET
    // The generic attribute is only shipped in the .NET assets of MSTest.TestFramework, because .NET
    // Framework reflection cannot materialize a generic custom attribute at all.
    public void GenericTestFilterProviderAttribute_IsDiscoverableAsMarkerAndExposesTypeArgument()
    {
        // Implementing the internal ITestFilterProviderAttribute is what lets the adapter's generic-provider
        // lookup pick the generic form up.
        ITestFilterProviderAttribute attribute = new TestFilterProviderAttribute<NoOpFilter>();

        attribute.FilterType.Should().Be(typeof(NoOpFilter));
        TypeCache.InstantiateTestFilter(attribute.FilterType).Should().BeOfType<NoOpFilter>();
    }
#endif

    private static Assembly CreateDynamicAssemblyWithTestFilterProviderAttribute()
    {
        AssemblyBuilder assemblyBuilder = DefineDynamicAssembly();
        ConstructorInfo constructor = typeof(TestFilterProviderAttribute).GetConstructor([typeof(Type)])!;
        var attributeBuilder = new CustomAttributeBuilder(constructor, [typeof(NoOpFilter)]);
        assemblyBuilder.SetCustomAttribute(attributeBuilder);

        return assemblyBuilder;
    }

#if NETFRAMEWORK
    private static AssemblyBuilder DefineDynamicAssembly()
        => AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName("TestFilterProviderVersionSkew" + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
#else
    private static AssemblyBuilder DefineDynamicAssembly()
        => AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("TestFilterProviderVersionSkew" + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
#endif

    // ----- test types -----
    public sealed class NoOpFilter : ITestFilter
    {
        public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
    }

    public sealed class GenericFilter<T> : ITestFilter
    {
        public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
    }

    public abstract class AbstractFilter : ITestFilter
    {
        public abstract TestFilterResult Filter(TestFilterContext context);
    }

    public interface IFilterInterface : ITestFilter
    {
    }

    // Intentionally does not implement ITestFilter.
    public sealed class NotAFilter
    {
    }

    public sealed class ThrowingFilter : ITestFilter
    {
        public ThrowingFilter() => throw new InvalidOperationException("filter ctor blew up");

        public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
    }

    public sealed class FilterWithoutPublicCtor : ITestFilter
    {
        private FilterWithoutPublicCtor()
        {
        }

        public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
    }
}
