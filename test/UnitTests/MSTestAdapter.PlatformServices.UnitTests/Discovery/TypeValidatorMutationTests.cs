// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public sealed class TypeValidatorMutationTests : TestContainer
{
    private readonly Mock<ReflectHelper> _mockReflectHelper = new();
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider = new();
    private readonly List<string> _warnings = [];

    public TypeValidatorMutationTests()
    {
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void IsValidTestClassAcceptsAnFSharpModuleWhenAtLeastOneOfItsMethodsIsATestMethod()
    {
        Type moduleType = FSharpModuleTestFixture.ModuleType;
        MethodInfo testMethod = moduleType.GetMethod("StaticMethod")!;
        MethodInfo[] methods = [moduleType.GetMethod("InstanceMethod")!, testMethod, moduleType.GetMethod("InstanceGenericValueTaskMethod")!];
        _testablePlatformServiceProvider.MockReflectionOperations!.Setup(ro => ro.GetDeclaredMethods(moduleType)).Returns(methods);
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestMethodAttribute>(testMethod)).Returns(true);

        bool isValid = new TypeValidator(_mockReflectHelper.Object).IsValidTestClass(moduleType, _warnings);

        isValid.Should().BeTrue();
        _warnings.Should().BeEmpty();
    }

    public void IsValidTestClassRejectsAnFSharpModuleWithoutAnyTestMethod()
    {
        Type moduleType = FSharpModuleTestFixture.ModuleType;
        MethodInfo[] methods = [moduleType.GetMethod("InstanceMethod")!, moduleType.GetMethod("StaticMethod")!];
        _testablePlatformServiceProvider.MockReflectionOperations!.Setup(ro => ro.GetDeclaredMethods(moduleType)).Returns(methods);

        bool isValid = new TypeValidator(_mockReflectHelper.Object).IsValidTestClass(moduleType, _warnings);

        isValid.Should().BeFalse();
        _warnings.Should().BeEmpty();
    }

    public void IsValidTestClassReportsTheInvalidTestContextSignatureAndRejectsTheClass()
    {
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(It.IsAny<MemberInfo>())).Returns(true);
        Type[] invalidTypes = [typeof(StaticTestContextClass), typeof(SetterOnlyTestContextClass)];

        foreach (Type type in invalidTypes)
        {
            SetDeclaredProperties(type);
            var warnings = new List<string>();

            bool isValid = new TypeValidator(_mockReflectHelper.Object).IsValidTestClass(type, warnings);

            isValid.Should().BeFalse(because: type.Name);
            warnings.Should().Equal(
                [string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInValidTestContextSignature, type.FullName)],
                because: type.Name);
        }
    }

    public void HasCorrectTestContextSignatureRejectsATestContextPropertyThatCannotBeRead()
    {
        Type[] invalidTypes = [typeof(SetterOnlyTestContextClass), typeof(StaticTestContextClass)];

        foreach (Type type in invalidTypes)
        {
            SetDeclaredProperties(type);

            TypeValidator.HasCorrectTestContextSignature(type).Should().BeFalse(because: type.Name);
        }
    }

#if DEBUG && !NETFRAMEWORK
    public void HasCorrectTestContextSignatureAssertsThatTheTypeIsNotNull()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        bool hasCorrectSignature = TypeValidator.HasCorrectTestContextSignature(null!);

        hasCorrectSignature.Should().BeTrue();
        assertions.Should().Equal("HasCorrectTestContextSignature type is null");
    }
#endif

    private void SetDeclaredProperties(Type type)
        => _testablePlatformServiceProvider.MockReflectionOperations!
            .Setup(ro => ro.GetDeclaredProperties(type))
            .Returns(type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));

    public class StaticTestContextClass
    {
        public static TestContext TestContext { get; set; } = null!;
    }

    public class SetterOnlyTestContextClass
    {
#pragma warning disable CA1044 // Properties should not be write only
        public TestContext TestContext
        {
            set => _ = value;
        }
#pragma warning restore CA1044 // Properties should not be write only
    }
}
