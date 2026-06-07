// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using TestFramework.ForTestingMSTest;

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
