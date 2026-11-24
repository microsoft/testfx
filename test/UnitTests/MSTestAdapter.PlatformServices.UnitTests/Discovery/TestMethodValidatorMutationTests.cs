// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public sealed class TestMethodValidatorMutationTests : TestContainer
{
    private readonly Mock<ReflectHelper> _mockReflectHelper = new(MockBehavior.Strict);
    private readonly List<string> _warnings = [];

    public TestMethodValidatorMutationTests()
    {
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestMethodAttribute>(It.IsAny<MemberInfo>()))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetFirstAttributeOrDefault<AsyncStateMachineAttribute>(It.IsAny<MemberInfo>()))
            .Returns((AsyncStateMachineAttribute?)null);
    }

    public void IsValidTestMethodReportsTheGenericSignatureMessageForAnInaccessibleMethodReturningAGenericValueTask()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(
            nameof(SampleTestClass.InternalGenericValueTaskMethod),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        bool isValid = CreateValidator().IsValidTestMethod(method, typeof(SampleTestClass), _warnings);

        isValid.Should().BeFalse();
        _warnings.Should().Equal(Format(Resource.UTA_ErrorIncorrectTestMethodSignature, typeof(SampleTestClass), method));
    }

    public void IsValidTestMethodReportsTheGenericSignatureMessageForAnAbstractMethodReturningAGenericValueTask()
    {
        MethodInfo method = typeof(AbstractSampleTestClass).GetMethod(nameof(AbstractSampleTestClass.AbstractGenericValueTaskMethod))!;

        bool isValid = CreateValidator().IsValidTestMethod(method, typeof(AbstractSampleTestClass), _warnings);

        isValid.Should().BeFalse();
        _warnings.Should().Equal(Format(Resource.UTA_ErrorIncorrectTestMethodSignature, typeof(AbstractSampleTestClass), method));
    }

    public void IsValidTestMethodReportsTheTargetedMessageWhenOnlyTheGenericValueTaskReturnTypeIsInvalid()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.PublicGenericValueTaskMethod))!;

        bool isValid = CreateValidator().IsValidTestMethod(method, typeof(SampleTestClass), _warnings);

        isValid.Should().BeFalse();
        _warnings.Should().Equal(Format(Resource.UTA_ErrorGenericValueTaskReturnType, typeof(SampleTestClass), method));
    }

    public void IsValidTestMethodReportsTheRegularSignatureMessageForAnInvalidMethodOfATypeThatIsNotAnFSharpModule()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.StaticMethod))!;

        bool isValid = CreateValidator().IsValidTestMethod(method, typeof(SampleTestClass), _warnings);

        isValid.Should().BeFalse();
        _warnings.Should().Equal(Format(Resource.UTA_ErrorIncorrectTestMethodSignature, typeof(SampleTestClass), method));
    }

    public void IsValidTestMethodAcceptsMethodsWithOutOrRefParametersAndWarnsAboutThemOnce()
    {
        string[] methodNames =
        [
            nameof(SampleTestClass.OutParameter),
            nameof(SampleTestClass.RefParameter),
            nameof(SampleTestClass.ValueThenOutParameter),
            nameof(SampleTestClass.RefThenValueParameter),
        ];

        foreach (string methodName in methodNames)
        {
            MethodInfo method = typeof(SampleTestClass).GetMethod(methodName)!;
            var warnings = new List<string>();

            bool isValid = CreateValidator().IsValidTestMethod(method, typeof(SampleTestClass), warnings);

            isValid.Should().BeTrue(because: methodName);
            warnings.Should().Equal(
                [Format(Resource.UTA_OutRefParametersNotSupported, typeof(SampleTestClass), method)],
                because: methodName);
        }
    }

    public void IsValidTestMethodDoesNotWarnForMethodsWithoutOutOrRefParameters()
    {
        string[] methodNames =
        [
            nameof(SampleTestClass.NoParameters),
            nameof(SampleTestClass.ValueParameters),
        ];

        foreach (string methodName in methodNames)
        {
            MethodInfo method = typeof(SampleTestClass).GetMethod(methodName)!;
            var warnings = new List<string>();

            bool isValid = CreateValidator().IsValidTestMethod(method, typeof(SampleTestClass), warnings);

            isValid.Should().BeTrue(because: methodName);
            warnings.Should().BeEmpty(because: methodName);
        }
    }

    private static string Format(string format, Type type, MethodInfo method)
        => string.Format(CultureInfo.CurrentCulture, format, type.FullName, method.Name);

    private TestMethodValidator CreateValidator()
        => new(_mockReflectHelper.Object, discoverInternals: false);

    private sealed class SampleTestClass
    {
        public static void StaticMethod()
        {
        }

        public void NoParameters()
        {
        }

        public void ValueParameters(int first, string second)
        {
            _ = first;
            _ = second;
        }

        public void OutParameter(out int value)
            => value = 0;

        public void RefParameter(ref int value)
            => value++;

        public void ValueThenOutParameter(int input, out int output)
            => output = input;

        public void RefThenValueParameter(ref int input, string text)
        {
            input++;
            _ = text;
        }

        public ValueTask<int> PublicGenericValueTaskMethod() => default;

        internal ValueTask<int> InternalGenericValueTaskMethod() => default;
    }

    private abstract class AbstractSampleTestClass
    {
        public abstract ValueTask<int> AbstractGenericValueTaskMethod();
    }
}
