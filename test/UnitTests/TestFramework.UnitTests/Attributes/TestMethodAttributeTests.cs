// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public sealed class TestMethodAttributeTests : TestContainer
{
    public async Task ExecuteAsync_ConfiguredMetadataAndResultPayloadSurviveSuccessAndFailure()
    {
        object association = new();
        var result = new TestResult
        {
            Outcome = UnitTestOutcome.Passed,
            DisplayName = "transported result",
            LogOutput = "stdout contract",
            LogError = "stderr contract",
            DebugTrace = "debug contract",
            TestContextMessages = "context contract",
            ExecutionId = new Guid("11111111-1111-1111-1111-111111111111"),
            ParentExecId = new Guid("22222222-2222-2222-2222-222222222222"),
            Duration = TimeSpan.FromMilliseconds(17),
            ResultFiles = ["result.json"],
            AssociatedUnitTestElement = association,
        };
        var attribute = new TestMethodAttribute("source.cs", 37)
        {
            DisplayName = "configured method",
            UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold,
        };
        var method = new Mock<ITestMethod>();
        Exception? fault = null;
        method.Setup(test => test.InvokeAsync(null)).Returns(
            () => fault is null ? Task.FromResult(result) : Task.FromException<TestResult>(fault));

        TestResult[] actual = await attribute.ExecuteAsync(method.Object);
        actual.Should().ContainSingle().Which.Should().BeSameAs(result);
        actual[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        actual[0].TestFailureException.Should().BeNull();
        actual[0].LogOutput.Should().Be("stdout contract");
        actual[0].LogError.Should().Be("stderr contract");
        actual[0].DebugTrace.Should().Be("debug contract");
        actual[0].TestContextMessages.Should().Be("context contract");
        actual[0].ExecutionId.Should().Be(new Guid("11111111-1111-1111-1111-111111111111"));
        actual[0].ParentExecId.Should().Be(new Guid("22222222-2222-2222-2222-222222222222"));
        actual[0].Duration.Should().Be(TimeSpan.FromMilliseconds(17));
        actual[0].ResultFiles.Should().Equal("result.json");
        actual[0].AssociatedUnitTestElement.Should().BeSameAs(association);
        attribute.DisplayName.Should().Be("configured method");
        attribute.UnfoldingStrategy.Should().Be(TestDataSourceUnfoldingStrategy.Fold);
        attribute.DeclaringFilePath.Should().Be("source.cs");
        attribute.DeclaringLineNumber.Should().Be(37);

        var original = new InvalidOperationException("invocation contract");
        fault = original;
        Func<Task> rejected = () => attribute.ExecuteAsync(method.Object);
        (await rejected.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(original);
        method.Verify(test => test.InvokeAsync(null), Times.Exactly(2));
        method.VerifyNoOtherCalls();

        var ignored = TestResult.CreateIgnoredResult("policy contract");
        ignored.Outcome.Should().Be(UnitTestOutcome.Ignored);
        ignored.IgnoreReason.Should().Be("policy contract");
    }

    public async Task DataTestMethod_PropagatesSourceAndRetainsBaseExecutionContract()
    {
        var attribute = new DataTestMethodAttribute("data-source.cs", 47);
        var result = new TestResult { Outcome = UnitTestOutcome.Passed, DisplayName = "data result" };
        var method = new Mock<ITestMethod>();
        Exception? fault = null;
        method.Setup(test => test.InvokeAsync(null)).Returns(
            () => fault is null ? Task.FromResult(result) : Task.FromException<TestResult>(fault));

        TestResult[] actual = await attribute.ExecuteAsync(method.Object);
        actual.Should().ContainSingle().Which.Should().BeSameAs(result);
        attribute.DeclaringFilePath.Should().Be("data-source.cs");
        attribute.DeclaringLineNumber.Should().Be(47);
        var original = new InvalidOperationException("data invocation contract");
        fault = original;
        Func<Task> rejected = () => attribute.ExecuteAsync(method.Object);
        (await rejected.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(original);
        method.Verify(test => test.InvokeAsync(null), Times.Exactly(2));
        method.VerifyNoOtherCalls();
    }

    public async Task TestClassDefault_ReturnsOriginalAttributeWithoutLosingExecutionMetadata()
    {
        var original = new TestMethodAttribute("class-source.cs", 53)
        {
            DisplayName = "class method",
            UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold,
        };
        var testClass = new TestClassAttribute();
        TestMethodAttribute selected = testClass.GetTestMethodAttribute(original)!;
        selected.Should().BeSameAs(original);
        var result = new TestResult { Outcome = UnitTestOutcome.Passed };
        var method = new Mock<ITestMethod>();
        method.Setup(test => test.InvokeAsync(null)).ReturnsAsync(result);

        TestResult[] actual = await selected.ExecuteAsync(method.Object);

        actual.Should().ContainSingle().Which.Should().BeSameAs(result);
        selected.DisplayName.Should().Be("class method");
        selected.UnfoldingStrategy.Should().Be(TestDataSourceUnfoldingStrategy.Fold);
        selected.DeclaringFilePath.Should().Be("class-source.cs");
        selected.DeclaringLineNumber.Should().Be(53);
        testClass.GetTestMethodAttribute(null!).Should().BeNull();
        method.Verify(test => test.InvokeAsync(null), Times.Once);
        method.VerifyNoOtherCalls();
    }
}
