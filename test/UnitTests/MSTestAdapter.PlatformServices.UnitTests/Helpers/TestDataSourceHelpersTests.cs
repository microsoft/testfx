// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UnitTests.Helpers;

public class TestDataSourceHelpersTests : TestContainer
{
    public void TryHandleTupleDataSource_ExpandsExplicitTokenTuplesForObjectArgument()
    {
        using CancellationTokenSource source = new();
        ParameterInfo[] parameters = GetCancellationTokenParameters<object>();
        foreach (object? suppliedToken in new object?[] { source.Token, CancellationToken.None, null, Type.Missing })
        {
            foreach (object tuple in new object[] { (42, suppliedToken), Tuple.Create(42, suppliedToken) })
            {
                bool result = TestDataSourceHelpers.TryHandleTupleDataSource(tuple, parameters, out object?[] data);

                result.Should().BeTrue();
                data.Should().Equal(42, suppliedToken);
            }
        }
    }

    public void IsDataConsideredSingleArgumentValue_RejectsExplicitTokenTuplesForObjectArrayArgument()
    {
        using CancellationTokenSource source = new();
        ParameterInfo[] parameters = GetCancellationTokenParameters<object[]>();
        object[] values = [17, "array"];
        foreach (object? suppliedToken in new object?[] { source.Token, CancellationToken.None, null, Type.Missing })
        {
            foreach (object tuple in new object[] { (values, suppliedToken), Tuple.Create(values, suppliedToken) })
            {
                TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([tuple], parameters).Should().BeFalse();

                bool result = TestDataSourceHelpers.TryHandleTupleDataSource(tuple, parameters, out object?[] data);

                result.Should().BeTrue();
                data.Should().HaveCount(2);
                data[0].Should().BeSameAs(values);
                data[1].Should().Be(suppliedToken);
                TestDataSourceHelpers.IsDataConsideredSingleArgumentValue(data, parameters).Should().BeFalse();
            }
        }
    }

    public void TryHandleITestDataRow_ExpandsExplicitTokenTuplesForObjectAndObjectArrayArguments()
    {
        using CancellationTokenSource source = new();
        object[] values = [17, "array"];
        (ITestDataRow Row, ParameterInfo[] Parameters, object Value)[] cases =
        [
            (new TestDataRow<(int, CancellationToken)>((42, source.Token)), GetCancellationTokenParameters<object>(), 42),
            (new TestDataRow<Tuple<int, CancellationToken>>(Tuple.Create(42, source.Token)), GetCancellationTokenParameters<object>(), 42),
            (new TestDataRow<(object[], CancellationToken)>((values, source.Token)), GetCancellationTokenParameters<object[]>(), values),
            (new TestDataRow<Tuple<object[], CancellationToken>>(Tuple.Create(values, source.Token)), GetCancellationTokenParameters<object[]>(), values),
        ];
        foreach ((ITestDataRow row, ParameterInfo[] parameters, object value) in cases)
        {
            bool result = TestDataSourceHelpers.TryHandleITestDataRow([row], parameters, out object?[] data, out _, out _);

            result.Should().BeTrue();
            data.Should().Equal(value, source.Token);
        }
    }

    public void TryHandleTupleDataSource_PreservesIntentionalSingleTupleArguments()
    {
        ParameterInfo[] objectParameters = GetCancellationTokenParameters<object>();
        (object Tuple, ParameterInfo[] Parameters)[] cases =
        [
            ((42, "tuple"), objectParameters),
            (Tuple.Create(42, "tuple"), objectParameters),
            ((42, CancellationToken.None, "extra"), objectParameters),
            (Tuple.Create(42, CancellationToken.None, "extra"), objectParameters),
            ((42, CancellationToken.None), GetCancellationTokenParameters<(int, CancellationToken)>()),
            (Tuple.Create(42, CancellationToken.None), GetCancellationTokenParameters<Tuple<int, CancellationToken>>()),
            ((42, CancellationToken.None), typeof(Action<object>).GetMethod(nameof(Action.Invoke))!.GetParameters()),
        ];
        foreach ((object tuple, ParameterInfo[] parameters) in cases)
        {
            bool result = TestDataSourceHelpers.TryHandleTupleDataSource(tuple, parameters, out object?[] data);

            result.Should().BeFalse();
            data.Should().BeEmpty();
        }
    }

    public void IsDataConsideredSingleArgumentValue_PreservesTupleAsObjectArrayData()
    {
        ParameterInfo[] parameters = GetCancellationTokenParameters<object[]>();
        object[] values = [17, "array"];
        object[] tuples =
        [
            (42, "tuple"),
            Tuple.Create(42, "tuple"),
            (42, CancellationToken.None),
            Tuple.Create(42, CancellationToken.None),
            (values, "not a token"),
            Tuple.Create(values, "not a token"),
            (values, CancellationToken.None, "extra"),
            Tuple.Create(values, CancellationToken.None, "extra"),
        ];
        foreach (object tuple in tuples)
        {
            TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([tuple], parameters).Should().BeTrue();
        }
    }

    public void IsDataConsideredSingleArgumentValue_PreservesExplicitAndImplicitObjectArrayShapes()
    {
        ParameterInfo[] parameters = GetCancellationTokenParameters<object[]>();
        object[] values = [17, "array"];
        foreach (object? suppliedToken in new object?[] { new CancellationToken(canceled: true), CancellationToken.None, null, Type.Missing })
        {
            TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([values, suppliedToken], parameters).Should().BeFalse();
            TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([null, suppliedToken], parameters).Should().BeFalse();
            TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([17, suppliedToken], parameters).Should().BeTrue();
        }

        TestDataSourceHelpers.IsDataConsideredSingleArgumentValue(values, parameters).Should().BeTrue();
        TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([values], parameters).Should().BeTrue();
        TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([null], parameters).Should().BeFalse();
    }

    public void TryHandleITestDataRow_ShouldExtractArgumentsDisplayNameAndPreserveOtherMetadata()
    {
        var testData = new TestDataRow<(string, int)>((string.Empty, 42))
        {
            ArgumentsDisplayName = "empty input",
            DisplayName = "full name",
            IgnoreMessage = "ignored row",
            TestCategories = ["row category"],
        };

        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            [testData], [], out object?[] data, out string? ignoreMessage, out string? displayName,
            out IList<string>? categories, out string? argumentsDisplayName);

        result.Should().BeTrue();
        data.Should().Equal(string.Empty, 42);
        ignoreMessage.Should().Be("ignored row");
        displayName.Should().Be("full name");
        categories.Should().Equal("row category");
        argumentsDisplayName.Should().Be("empty input");
    }

    public void TryHandleITestDataRow_ArgumentsDisplayNameDefaultsToNull()
    {
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            [new TestDataRow<string>(string.Empty)], [], out object?[] data, out _, out _, out _, out string? argumentsDisplayName);

        result.Should().BeTrue();
        data.Should().Equal(string.Empty);
        argumentsDisplayName.Should().BeNull();
    }

    public void TryHandleITestDataRow_WithoutWrapperLeavesDataUnchangedAndArgumentsDisplayNameNull()
    {
        object?[] originalData = [string.Empty, 42];
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            originalData, [], out object?[] data, out _, out _, out _, out string? argumentsDisplayName);

        result.Should().BeFalse();
        data.Should().BeSameAs(originalData);
        argumentsDisplayName.Should().BeNull();
    }

    public void TryHandleITestDataRow_WithTestDataRow_ShouldExtractTestCategories()
    {
        // Arrange
        var testData = new TestDataRow<string>("test_value")
        {
            TestCategories = ["Category1", "Category2"],
            IgnoreMessage = "ignore_message",
            DisplayName = "display_name",
        };
        object?[] dataArray = [testData];
        ParameterInfo[] parameters = []; // No method parameters for this test

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray,
            parameters,
            out object?[] extractedData,
            out string? ignoreMessage,
            out string? displayName,
            out IList<string>? testCategories);

        // Assert
        result.Should().BeTrue();
        extractedData.Should().NotBeNull();
        extractedData.Length.Should().Be(1);
        ((string?)extractedData[0]).Should().Be("test_value");
        ignoreMessage.Should().Be("ignore_message");
        displayName.Should().Be("display_name");
        testCategories.Should().NotBeNull();
        testCategories.Count.Should().Be(2);
        testCategories.Should().Contain("Category1");
        testCategories.Should().Contain("Category2");
    }

    public void TryHandleITestDataRow_WithTestDataRowNullCategories_ShouldReturnNullCategories()
    {
        // Arrange
        var testData = new TestDataRow<string>("test_value");
        object?[] dataArray = [testData];
        ParameterInfo[] parameters = [];

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray,
            parameters,
            out _,
            out _,
            out _,
            out IList<string>? testCategories);

        // Assert
        result.Should().BeTrue();
        testCategories.Should().BeNull();
    }

    public void TryHandleITestDataRow_WithNonTestDataRow_ShouldReturnFalseAndNullCategories()
    {
        // Arrange
        object?[] dataArray = ["regular_string"];
        ParameterInfo[] parameters = [];

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray,
            parameters,
            out object?[] extractedData,
            out string? ignoreMessage,
            out string? displayName,
            out IList<string>? testCategories);

        // Assert
        result.Should().BeFalse();
        extractedData.Should().BeEquivalentTo(dataArray);
        ignoreMessage.Should().BeNull();
        displayName.Should().BeNull();
        testCategories.Should().BeNull();
    }

    public void TryHandleITestDataRow_BackwardCompatibilityOverload_ShouldWork()
    {
        // Arrange
        var testData = new TestDataRow<string>("test_value")
        {
            TestCategories = ["Category1", "Category2"],
            IgnoreMessage = "ignore_message",
            DisplayName = "display_name",
        };
        object?[] dataArray = [testData];
        ParameterInfo[] parameters = [];

        // Act
        bool result = TestDataSourceHelpers.TryHandleITestDataRow(
            dataArray,
            parameters,
            out object?[] extractedData,
            out string? ignoreMessage,
            out string? displayName);

        // Assert - should work without TestCategories parameter
        result.Should().BeTrue();
        extractedData.Should().NotBeNull();
        extractedData.Length.Should().Be(1);
        ((string?)extractedData[0]).Should().Be("test_value");
        ignoreMessage.Should().Be("ignore_message");
        displayName.Should().Be("display_name");
    }

    private static ParameterInfo[] GetCancellationTokenParameters<T>()
        => typeof(Action<T, CancellationToken>).GetMethod(nameof(Action.Invoke))!.GetParameters();
}
