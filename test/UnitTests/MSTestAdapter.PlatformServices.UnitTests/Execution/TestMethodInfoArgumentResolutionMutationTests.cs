// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodInfoArgumentResolutionMutationTests : TestContainer
{
    public void ResolveArgumentsWhenEveryParameterIsSuppliedAndSomeAreOptionalMaterializesAFreshArray()
    {
        TestMethodInfo testMethodInfo = CreateTestMethodInfo(nameof(ArgumentFixture.OptionalStrings));
        object?[] arguments = ["one", "two", "three"];

        object?[] resolved = testMethodInfo.ResolveArguments(arguments);

        resolved.Should().Equal("one", "two", "three");
        resolved.Should().NotBeSameAs(arguments, because: "a full argument list for a method with optional parameters still goes through resolution");
    }

    public void ResolveArgumentsWhenArgumentCountIsOutsideTheAcceptedRangeReturnsTheCallerArray()
    {
        TestMethodInfo testMethodInfo = CreateTestMethodInfo(nameof(ArgumentFixture.OptionalStrings));
        object?[] tooFew = [];
        object?[] tooMany = ["one", "two", "three", "four"];

        testMethodInfo.ResolveArguments(tooFew).Should().BeSameAs(tooFew, because: "fewer arguments than required parameters are left untouched");
        testMethodInfo.ResolveArguments(tooMany).Should().BeSameAs(tooMany, because: "extra arguments without a params parameter are left untouched");
    }

    public void ResolveArgumentsWhenOptionalParametersAreOmittedFillsTheirDefaults()
    {
        TestMethodInfo testMethodInfo = CreateTestMethodInfo(nameof(ArgumentFixture.OptionalStrings));

        object?[] expectedForOne = ["one", null, null];
        object?[] expectedForTwo = ["one", "two", null];

        testMethodInfo.ResolveArguments(["one"]).Should().Equal(expectedForOne);
        testMethodInfo.ResolveArguments(["one", "two"]).Should().Equal(expectedForTwo);
    }

    public void ResolveArgumentsWhenTrailingOptionalCancellationTokenIsSuppliedResolvesIntoAFreshArray()
    {
        TestMethodInfo testMethodInfo = CreateTestMethodInfo(nameof(ArgumentFixture.OptionalDataThenOptionalToken));
        using var cancellationTokenSource = new CancellationTokenSource();
        object?[] arguments = [1, "supplied", cancellationTokenSource.Token];

        object?[] resolved = testMethodInfo.ResolveArguments(arguments);

        resolved.Should().Equal(1, "supplied", cancellationTokenSource.Token);
        resolved.Should().NotBeSameAs(arguments, because: "a trailing optional token does not make the supplied data row a pass-through");
    }

    public void ResolveArgumentsWhenTrailingOptionalCancellationTokenIsOmittedFillsOnlyDataDefaults()
    {
        TestMethodInfo testMethodInfo = CreateTestMethodInfo(nameof(ArgumentFixture.OptionalDataThenOptionalToken));

        object?[] resolved = testMethodInfo.ResolveArguments([1]);

        resolved.Should().Equal(1, "default");
    }

    public void ResolveArgumentsWhenRequiredCancellationTokenFollowsAnOptionalParameterDoesNotRequireTheToken()
    {
        TestMethodInfo testMethodInfo = CreateTestMethodInfo(nameof(ArgumentFixture.OptionalDataThenRequiredToken));

        testMethodInfo.ResolveArguments([1]).Should().Equal(1, 7);
    }

    public void ResolveArgumentsWhenDataOmitsOnlyTheRequiredCancellationTokenResolvesIntoAFreshArray()
    {
        TestMethodInfo testMethodInfo = CreateTestMethodInfo(nameof(ArgumentFixture.OptionalDataThenRequiredToken));
        object?[] dataOnly = [1, 5];

        object?[] resolved = testMethodInfo.ResolveArguments(dataOnly);

        resolved.Should().Equal(1, 5);
        resolved.Should().NotBeSameAs(dataOnly, because: "the token is excluded from the required count, so the data row is resolved");
    }

    public void ResolveArgumentsWhenDataAndRequiredCancellationTokenAreSuppliedResolvesIntoAFreshArray()
    {
        TestMethodInfo testMethodInfo = CreateTestMethodInfo(nameof(ArgumentFixture.OptionalDataThenRequiredToken));
        using var cancellationTokenSource = new CancellationTokenSource();
        object?[] arguments = [1, 5, cancellationTokenSource.Token];

        object?[] resolved = testMethodInfo.ResolveArguments(arguments);

        resolved.Should().Equal(1, 5, cancellationTokenSource.Token);
        resolved.Should().NotBeSameAs(arguments, because: "a full argument list including the token is not a pass-through when optional data is declared");
    }

    public void ParameterMetadataDetectsTheParamsParameterFromItsOwnDeclarationOnly()
    {
        ParameterInfo[] parameters = [new ParameterDouble(isParams: false), new ParameterDouble(isParams: true)];

        Type metadataType = typeof(TestMethodInfo).GetNestedType("ParameterMetadata", BindingFlags.NonPublic)!;
        object metadata = metadataType.GetMethod("Compute", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [parameters])!;

        metadataType.GetProperty("ParamsParameterIndex")!.GetValue(metadata).Should().Be(1, because: "the params attribute is looked up without inheritance");
        metadataType.GetProperty("RequiredParameterCount")!.GetValue(metadata).Should().Be(1, because: "the params parameter is not counted as required");
    }

    private static TestMethodInfo CreateTestMethodInfo(string methodName)
    {
        MethodInfo method = typeof(ArgumentFixture).GetMethod(methodName)!;
        var assemblyInfo = new TestAssemblyInfo(typeof(ArgumentFixture).Assembly);
        var classInfo = new TestClassInfo(typeof(ArgumentFixture), typeof(ArgumentFixture).GetConstructor([])!, true, new TestClassAttribute(), assemblyInfo);
        return new TestMethodInfo(method, classInfo);
    }

    // Real reflected parameters ignore the inherit flag, so a double is needed to observe how the attribute is queried.
    private sealed class ParameterDouble(bool isParams) : ParameterInfo
    {
        public override bool IsDefined(Type attributeType, bool inherit)
            => isParams && attributeType == typeof(ParamArrayAttribute) && !inherit;
    }

    private sealed class ArgumentFixture
    {
        public void OptionalStrings(string first, string? second = null, string? third = null)
        {
        }

        public void OptionalDataThenOptionalToken(int first, string second = "default", CancellationToken token = default)
        {
        }

        // The C# compiler rejects a required parameter after a defaulted one, but the [Optional] attribute form is accepted.
        public void OptionalDataThenRequiredToken(int first, [Optional, DefaultParameterValue(7)] int second, CancellationToken token)
        {
        }
    }
}
