// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region ReportAssertFailed tests
    // See https://github.com/dotnet/sdk/issues/25373
    public void ReportAssertFailedDoesNotThrowIfMessageContainsInvalidStringFormatComposite()
    {
        Action action = () => Assert.ReportAssertFailed("name", "{");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*name failed. {*");
    }
    #endregion

    public void Throws_WhenExceptionIsDerivedFromExpectedType_ShouldNotThrow()
        => Assert.Throws<ArgumentException>(() => throw new ArgumentNullException());

    public void Throws_WhenExceptionIsNotExpectedType_ShouldThrow()
    {
        static void Action() => Assert.Throws<ArgumentException>(() => throw new Exception());
        Action action = Action;
        action.Should().Throw<AssertFailedException>();
    }

    public void Throws_WithInterpolation()
    {
        static string GetString() => throw new Exception();

#pragma warning disable IDE0200 // Remove unnecessary lambda expression - intentionally testing overload resolution for this case.
        Exception ex = Assert.Throws<Exception>(() => GetString(), $"Hello {GetString()}");
#pragma warning restore IDE0200 // Remove unnecessary lambda expression
        Exception ex2 = Assert.Throws<Exception>(GetString, $"Hello {GetString()}");
        ex.Should().NotBeNull();
        ex2.Should().NotBeNull();
    }

    public void ThrowsExactly_WithInterpolation()
    {
        static string GetString() => throw new Exception();

#pragma warning disable IDE0200 // Remove unnecessary lambda expression - intentionally testing overload resolution for this case.
        Exception ex = Assert.ThrowsExactly<Exception>(() => GetString(), $"Hello {GetString()}");
#pragma warning restore IDE0200 // Remove unnecessary lambda expression
        Exception ex2 = Assert.ThrowsExactly<Exception>(GetString, $"Hello {GetString()}");
        ex.Should().NotBeNull();
        ex2.Should().NotBeNull();
    }

    public void ThrowsExactly_WhenExceptionIsDerivedFromExpectedType_ShouldThrow()
    {
        static void Action() => Assert.ThrowsExactly<ArgumentException>(() => throw new ArgumentNullException());
        Action action = Action;
        action.Should().Throw<AssertFailedException>();
    }

    public void ThrowsExactly_WhenExceptionExpectedType_ShouldNotThrow()
        => Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentNullException());

    public async Task ThrowsAsync_WhenExceptionIsDerivedFromExpectedType_ShouldNotThrow()
        => await Assert.ThrowsAsync<ArgumentException>(() => throw new ArgumentNullException());

    public void ThrowsAsync_WhenExceptionIsNotExpectedType_ShouldThrow()
    {
        Task t = Assert.ThrowsAsync<ArgumentException>(() => throw new Exception());
        Action action = t.Wait;
        action.Should().Throw<AggregateException>()
            .WithInnerException<AssertFailedException>()
            .WithMessage(
                $"Assertion failed. Expected exception of type ArgumentException (or derived) but caught Exception." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"expected type:    System.ArgumentException (or derived){Environment.NewLine}" +
                $"actual type:      System.Exception{Environment.NewLine}" +
                $"actual exception: System.Exception: Exception of type 'System.Exception' was thrown." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.ThrowsAsync<ArgumentException>(() => throw new Exception())");
    }

    public void ThrowsExactlyAsync_WhenExceptionIsDerivedFromExpectedType_ShouldThrow()
    {
        Task t = Assert.ThrowsExactlyAsync<ArgumentException>(() => throw new ArgumentNullException());
        Action action = t.Wait;
        action.Should().Throw<AggregateException>()
            .WithInnerException<AssertFailedException>()
            .WithMessage(
                $"Assertion failed. Expected exception of exact type ArgumentException but caught ArgumentNullException." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"expected type:    System.ArgumentException{Environment.NewLine}" +
                $"actual type:      System.ArgumentNullException{Environment.NewLine}" +
                $"actual exception: System.ArgumentNullException: Value cannot be null." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.ThrowsExactlyAsync<ArgumentException>(() => throw new ArgumentNullException())");
    }

    public void Throws_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        Assert.Throws<ArgumentNullException>(() => throw new ArgumentNullException(), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        wasBuilderCalled.Should().BeFalse();
    }

    public void Throws_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Action action = () => Assert.Throws<ArgumentNullException>(() => { }, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                $"Assertion failed. Expected exception of type ArgumentNullException (or derived) but no exception was thrown." +
                $"{Environment.NewLine}" +
                $"message constructed via builder." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.Throws<ArgumentNullException>(() => { })");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeNull();
    }

    public void Throws_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Action action = () => Assert.Throws<ArgumentNullException>(() => throw new ArgumentOutOfRangeException("MyParamNameHere"), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                $"Assertion failed. Expected exception of type ArgumentNullException (or derived) but caught ArgumentOutOfRangeException." +
                $"{Environment.NewLine}" +
                $"message constructed via builder." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"expected type:    System.ArgumentNullException (or derived){Environment.NewLine}" +
                $"actual type:      System.ArgumentOutOfRangeException{Environment.NewLine}" +
                $"actual exception: System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values. (Parameter 'MyParamNameHere')" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.Throws<ArgumentNullException>(() => throw new ArgumentOutOfRangeException(\"MyParamNameHere\"))");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeOfType<ArgumentOutOfRangeException>();
        ((ArgumentOutOfRangeException)exceptionPassedToBuilder!).ParamName.Should().Be("MyParamNameHere");
    }

    public void ThrowsExactly_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentNullException(), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        wasBuilderCalled.Should().BeFalse();
    }

    public void ThrowsExactly_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Action action = () => Assert.ThrowsExactly<ArgumentNullException>(() => { }, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                $"Assertion failed. Expected exception of exact type ArgumentNullException but no exception was thrown." +
                $"{Environment.NewLine}" +
                $"message constructed via builder." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.ThrowsExactly<ArgumentNullException>(() => { })");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeNull();
    }

    public void ThrowsExactly_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Action action = () => Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentOutOfRangeException("MyParamNameHere"), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                $"Assertion failed. Expected exception of exact type ArgumentNullException but caught ArgumentOutOfRangeException." +
                $"{Environment.NewLine}" +
                $"message constructed via builder." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"expected type:    System.ArgumentNullException{Environment.NewLine}" +
                $"actual type:      System.ArgumentOutOfRangeException{Environment.NewLine}" +
                $"actual exception: System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values. (Parameter 'MyParamNameHere')" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentOutOfRangeException(\"MyParamNameHere\"))");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeOfType<ArgumentOutOfRangeException>();
        ((ArgumentOutOfRangeException)exceptionPassedToBuilder!).ParamName.Should().Be("MyParamNameHere");
    }

    public async Task ThrowsAsync_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromException(new ArgumentNullException()), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        wasBuilderCalled.Should().BeFalse();
    }

    public async Task ThrowsAsync_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Func<Task> action = async () => await Assert.ThrowsAsync<ArgumentNullException>(() => Task.CompletedTask, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        (await action.Should().ThrowAsync<AssertFailedException>())
            .WithMessage(
                $"Assertion failed. Expected exception of type ArgumentNullException (or derived) but no exception was thrown." +
                $"{Environment.NewLine}" +
                $"message constructed via builder." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.ThrowsAsync<ArgumentNullException>(() => Task.CompletedTask)");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeNull();
    }

    public async Task ThrowsAsync_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Func<Task> action = async () => await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromException(new ArgumentOutOfRangeException("MyParamNameHere")), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        (await action.Should().ThrowAsync<AssertFailedException>())
            .WithMessage(
                $"Assertion failed. Expected exception of type ArgumentNullException (or derived) but caught ArgumentOutOfRangeException." +
                $"{Environment.NewLine}" +
                $"message constructed via builder." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"expected type:    System.ArgumentNullException (or derived){Environment.NewLine}" +
                $"actual type:      System.ArgumentOutOfRangeException{Environment.NewLine}" +
                $"actual exception: System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values. (Parameter 'MyParamNameHere')" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromException(new ArgumentOutOfRangeException(\"MyParamNameHere\")))");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeOfType<ArgumentOutOfRangeException>();
        ((ArgumentOutOfRangeException)exceptionPassedToBuilder!).ParamName.Should().Be("MyParamNameHere");
    }

    public async Task ThrowsExactlyAsync_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.FromException(new ArgumentNullException()), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        wasBuilderCalled.Should().BeFalse();
    }

    public async Task ThrowsExactlyAsync_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Func<Task> action = async () => await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.CompletedTask, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        (await action.Should().ThrowAsync<AssertFailedException>())
            .WithMessage(
                $"Assertion failed. Expected exception of exact type ArgumentNullException but no exception was thrown." +
                $"{Environment.NewLine}" +
                $"message constructed via builder." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.CompletedTask)");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeNull();
    }

    public async Task ThrowsExactlyAsync_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Func<Task> action = async () => await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.FromException(new ArgumentOutOfRangeException("MyParamNameHere")), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        (await action.Should().ThrowAsync<AssertFailedException>())
            .WithMessage(
                $"Assertion failed. Expected exception of exact type ArgumentNullException but caught ArgumentOutOfRangeException." +
                $"{Environment.NewLine}" +
                $"message constructed via builder." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"expected type:    System.ArgumentNullException{Environment.NewLine}" +
                $"actual type:      System.ArgumentOutOfRangeException{Environment.NewLine}" +
                $"actual exception: System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values. (Parameter 'MyParamNameHere')" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.FromException(new ArgumentOutOfRangeException(\"MyParamNameHere\")))");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeOfType<ArgumentOutOfRangeException>();
        ((ArgumentOutOfRangeException)exceptionPassedToBuilder!).ParamName.Should().Be("MyParamNameHere");
    }
}
